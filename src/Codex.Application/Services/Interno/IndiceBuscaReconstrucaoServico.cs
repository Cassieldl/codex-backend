using System.Diagnostics;
using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Enums;

namespace Codex.Application.Services.Interno;

/// <summary>
/// I22 — reconstrução total do índice de busca (modelo-dados.md §5.6: "comando de reconstrução
/// total para detectar/corrigir drift", já que <c>tipoitem</c>/<c>itemid</c> é referência
/// polimórfica sem FK de banco). Reprocessa toda entidade ativa das 6 tabelas de origem via
/// <see cref="IIndiceBuscaWriter.AtualizarPesosAsync"/> — mesma fórmula de peso do caminho de
/// escrita normal (<see cref="PesosBusca"/>), nunca uma segunda implementação divergente — e
/// depois recalcula a visibilidade a partir da fonte (<see cref="IIndiceBuscaWriter.RecalcularVisibilidadeAsync"/>),
/// porque uma linha recém-criada por drift (nunca indexada antes) nasce com <c>publicado=false</c>
/// por padrão e precisaria da cascata para refletir o estado real do agregado.
/// </summary>
public sealed class IndiceBuscaReconstrucaoServico : IIndiceBuscaReconstrucaoServico
{
    private readonly IProvedorRepositorioInterno _provedorRepositorio;
    private readonly IVersaoProvedorRepositorioInterno _versaoRepositorio;
    private readonly IAvisoRepositorioInterno _avisoRepositorio;
    private readonly IErroRepositorioInterno _erroRepositorio;
    private readonly ICampoLayoutRepositorioInterno _campoLayoutRepositorio;
    private readonly IArtigoRepositorioInterno _artigoRepositorio;
    private readonly IIndiceBuscaWriter _indiceBuscaWriter;

    public IndiceBuscaReconstrucaoServico(
        IProvedorRepositorioInterno provedorRepositorio,
        IVersaoProvedorRepositorioInterno versaoRepositorio,
        IAvisoRepositorioInterno avisoRepositorio,
        IErroRepositorioInterno erroRepositorio,
        ICampoLayoutRepositorioInterno campoLayoutRepositorio,
        IArtigoRepositorioInterno artigoRepositorio,
        IIndiceBuscaWriter indiceBuscaWriter)
    {
        _provedorRepositorio = provedorRepositorio;
        _versaoRepositorio = versaoRepositorio;
        _avisoRepositorio = avisoRepositorio;
        _erroRepositorio = erroRepositorio;
        _campoLayoutRepositorio = campoLayoutRepositorio;
        _artigoRepositorio = artigoRepositorio;
        _indiceBuscaWriter = indiceBuscaWriter;
    }

    public async Task<ReconstrucaoIndiceBuscaResultadoDto> ReconstruirAsync(CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();
        var total = 0;

        foreach (var provedor in await _provedorRepositorio.ListarTodosAtivosComAliasesAsync(ct))
        {
            var (a, b, c, d) = PesosBusca.DoProvedor(provedor);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.provedor, provedor.Id, a, b, c, d, ct);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.Provedor, provedor.Id, ct);
            total++;
        }

        foreach (var versao in await _versaoRepositorio.ListarTodosAtivosAsync(ct))
        {
            var (a, b, c, d) = PesosBusca.DoVersaoProvedor(versao);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.versaoprovedor, versao.Id, a, b, c, d, ct);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.VersaoProvedor, versao.Id, ct);
            total++;
        }

        foreach (var aviso in await _avisoRepositorio.ListarTodosAtivosAsync(ct))
        {
            var (a, b, c, d) = PesosBusca.DoAviso(aviso);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.aviso, aviso.Id, a, b, c, d, ct);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.Aviso, aviso.Id, ct);
            total++;
        }

        foreach (var erro in await _erroRepositorio.ListarTodosAtivosComVariantesAsync(ct))
        {
            var (a, b, c, d) = PesosBusca.DoErro(erro);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.erro, erro.Id, a, b, c, d, ct);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.Erro, erro.Id, ct);
            total++;
        }

        foreach (var campoLayout in await _campoLayoutRepositorio.ListarTodosAtivosAsync(ct))
        {
            var (a, b, c, d) = PesosBusca.DoCampoLayout(campoLayout);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.campolayout, campoLayout.Id, a, b, c, d, ct);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.CampoLayout, campoLayout.Id, ct);
            total++;
        }

        // Sexto tipo (003, dívida do PRD §2.4): artigo era o único indexável de fora da varredura,
        // então drift em linha de artigo NUNCA convergia — e a suíte, que cobria os outros cinco,
        // ficava verde do mesmo jeito.
        foreach (var artigo in await _artigoRepositorio.ListarTodosAtivosAsync(ct))
        {
            var (a, b, c, d) = PesosBusca.DoArtigo(artigo);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.artigo, artigo.Id, a, b, c, d, ct);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.Artigo, artigo.Id, ct);
            total++;
        }

        cronometro.Stop();

        return new ReconstrucaoIndiceBuscaResultadoDto
        {
            ItensReindexados = total,
            DuracaoMs = (int)cronometro.ElapsedMilliseconds,
        };
    }
}
