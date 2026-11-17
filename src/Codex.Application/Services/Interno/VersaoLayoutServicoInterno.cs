using System.Text.Json;
using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services.Interno;

/// <summary>
/// Área interna de <see cref="VersaoLayout"/> (I15a-I15d). Sem chamada a
/// <see cref="IIndiceBuscaWriter.AtualizarPesosAsync"/> em criação/edição — <c>VersaoLayout</c>
/// não tem linha própria em <c>codex.itensbusca</c> (só os <c>CampoLayout</c> descendentes têm).
/// A publicação (I15d) ainda cascateia para esses descendentes via
/// <see cref="TipoItemBusca.VersaoLayout"/>.
/// </summary>
public sealed class VersaoLayoutServicoInterno : IVersaoLayoutServicoInterno
{
    private const string MensagemConflito = "A versão do layout foi alterada por outra requisição. Recarregue e tente novamente.";

    private readonly IVersaoLayoutRepositorioInterno _repositorio;
    private readonly IIndiceBuscaWriter _indiceBuscaWriter;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;

    public VersaoLayoutServicoInterno(
        IVersaoLayoutRepositorioInterno repositorio,
        IIndiceBuscaWriter indiceBuscaWriter,
        IUnidadeDeTrabalho unidadeDeTrabalho)
    {
        _repositorio = repositorio;
        _indiceBuscaWriter = indiceBuscaWriter;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public Task<PaginaResultado<VersaoLayoutAdminDto>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var (paginaNormalizada, tamanhoNormalizado) = Paginacao.Normalizar(pagina, tamanhoPagina, padrao: 25, maximo: 200);

        return _repositorio.ListarAsync(paginaNormalizada, tamanhoNormalizado, ct);
    }

    public async Task<VersaoLayoutAdminDto> CriarAsync(CriarVersaoLayoutDto dto, CancellationToken ct)
    {
        var proximaOrdem = await _repositorio.ObterProximaOrdemAsync(ct);
        var versaoLayout = VersaoLayout.Criar(dto.Rotulo, proximaOrdem, dto.Descricao);
        versaoLayout.AtualizarCalendario(dto.NormaOrigem, dto.HomologacaoDesde, dto.LiberadaNoInvoicyEm, dto.ObrigatoriaEm);
        if (dto.Mudancas is not null)
            versaoLayout.AtualizarMudancas(MapeamentoMudancaLayout.ParaDominio(dto.Mudancas));

        await _repositorio.AdicionarAsync(versaoLayout, ct);
        await _repositorio.SalvarAlteracoesAsync(ct);

        return Mapear(versaoLayout);
    }

    public async Task<VersaoLayoutAdminDto> AtualizarAsync(int id, AtualizarVersaoLayoutDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var versaoLayout = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        if (dto.Rotulo is not null)
            versaoLayout.AtualizarRotulo(dto.Rotulo);

        if (dto.Descricao is not null)
            versaoLayout.AtualizarDescricao(dto.Descricao);

        // Campo ausente mantém o valor atual; presente, mesmo null, substitui.
        versaoLayout.AtualizarCalendario(
            dto.NormaOrigemInformada ? dto.NormaOrigem : versaoLayout.NormaOrigem,
            dto.HomologacaoDesdeInformada ? dto.HomologacaoDesde : versaoLayout.HomologacaoDesde,
            dto.LiberadaNoInvoicyEmInformada ? dto.LiberadaNoInvoicyEm : versaoLayout.LiberadaNoInvoicyEm,
            dto.ObrigatoriaEmInformada ? dto.ObrigatoriaEm : versaoLayout.ObrigatoriaEm);

        if (dto.Mudancas is not null)
            versaoLayout.AtualizarMudancas(MapeamentoMudancaLayout.ParaDominio(dto.Mudancas));

        if (dto.Ordem.HasValue)
            versaoLayout.ReordenarPara(dto.Ordem.Value);

        if (dto.Estado.HasValue)
            AplicarTransicaoDeEstado(versaoLayout, dto.Estado.Value);

        await _repositorio.SalvarAlteracoesAsync(ct);

        return Mapear(versaoLayout);
    }

    public async Task<VersaoLayoutAdminDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct)
    {
        var versaoLayout = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        if (publicado) versaoLayout.Publicar(); else versaoLayout.Despublicar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.VersaoLayout, versaoLayout.Id, ctInterno);
        }, ct);

        return Mapear(versaoLayout);
    }

    public async Task<VersaoLayoutAdminDto> AtualizarEstruturaAsync(int id, AtualizarEstruturaVersaoLayoutDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var versaoLayout = await _repositorio.ObterPorIdAsync(id, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoLayout", id);

        if (versaoLayout.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException(MensagemConflito);

        var conteudo = JsonSerializer.Serialize(dto, JsonSerializerOptions.Web);
        if (!await _repositorio.SubstituirEstruturaAsync(id, conteudo, versaoEsperada, ct))
            throw new ConflitoConcorrenciaException(MensagemConflito);

        var atualizada = await _repositorio.ObterPorIdAsync(id, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoLayout", id);

        return Mapear(atualizada);
    }

    /// <summary>
    /// <see cref="VersaoLayout"/> só expõe transições sequenciais dedicadas
    /// (<c>Prevista→Vigente→Anterior</c>) — não há caminho de volta. Pedir o mesmo estado atual é
    /// idempotente (no-op); pedir qualquer outra combinação inválida propaga
    /// <see cref="TransicaoDeEstadoInvalidaException"/> (422) do próprio domínio.
    /// </summary>
    private static void AplicarTransicaoDeEstado(VersaoLayout versaoLayout, EstadoVersaoLayout estadoAlvo)
    {
        if (versaoLayout.Estado == estadoAlvo)
            return;

        switch (estadoAlvo)
        {
            case EstadoVersaoLayout.Vigente:
                versaoLayout.MarcarVigente();
                break;
            case EstadoVersaoLayout.Anterior:
                versaoLayout.MarcarAnterior();
                break;
            default:
                throw new TransicaoDeEstadoInvalidaException(
                    $"Não é possível transicionar de '{versaoLayout.Estado}' para '{estadoAlvo}'.");
        }
    }

    private async Task<VersaoLayout> CarregarRastreadaOuFalharAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var versaoLayout = await _repositorio.ObterPorIdAsync(id, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoLayout", id);

        if (versaoLayout.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException(MensagemConflito);

        return versaoLayout;
    }

    private static VersaoLayoutAdminDto Mapear(VersaoLayout versaoLayout) => new()
    {
        Id = versaoLayout.Id,
        Rotulo = versaoLayout.Rotulo,
        Slug = versaoLayout.Slug,
        Estado = versaoLayout.Estado.ToString(),
        Ordem = versaoLayout.Ordem,
        Publicado = versaoLayout.Publicado,
        Descricao = versaoLayout.Descricao,
        NormaOrigem = versaoLayout.NormaOrigem,
        HomologacaoDesde = versaoLayout.HomologacaoDesde,
        LiberadaNoInvoicyEm = versaoLayout.LiberadaNoInvoicyEm,
        ObrigatoriaEm = versaoLayout.ObrigatoriaEm,
        Mudancas = MapeamentoMudancaLayout.ParaResposta(versaoLayout.Mudancas),
        VersaoConcorrencia = versaoLayout.Versao,
    };
}
