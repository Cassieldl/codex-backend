using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Application.Mappings;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services.Interno;

/// <summary>Área interna de <see cref="VersaoProvedor"/> (I6-I10, analise-backend.md §4.2/§3.4).</summary>
public sealed class VersaoProvedorServicoInterno : IVersaoProvedorServicoInterno
{
    private readonly IVersaoProvedorRepositorioInterno _repositorio;
    private readonly IProvedorRepositorioInterno _provedorRepositorio;
    private readonly IAvisoRepositorioInterno _avisoRepositorio;
    private readonly IVersaoLayoutRepositorioInterno _versaoLayoutRepositorio;
    private readonly ICampoVersaoRepositorioInterno _campoVersaoRepositorio;
    private readonly IIndiceBuscaWriter _indiceBuscaWriter;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;

    public VersaoProvedorServicoInterno(
        IVersaoProvedorRepositorioInterno repositorio,
        IProvedorRepositorioInterno provedorRepositorio,
        IAvisoRepositorioInterno avisoRepositorio,
        IVersaoLayoutRepositorioInterno versaoLayoutRepositorio,
        ICampoVersaoRepositorioInterno campoVersaoRepositorio,
        IIndiceBuscaWriter indiceBuscaWriter,
        IUnidadeDeTrabalho unidadeDeTrabalho)
    {
        _repositorio = repositorio;
        _provedorRepositorio = provedorRepositorio;
        _avisoRepositorio = avisoRepositorio;
        _versaoLayoutRepositorio = versaoLayoutRepositorio;
        _campoVersaoRepositorio = campoVersaoRepositorio;
        _indiceBuscaWriter = indiceBuscaWriter;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public Task<IReadOnlyCollection<VersaoAdminListaItemDto>> ListarPorProvedorAsync(int provedorId, CancellationToken ct) =>
        _repositorio.ListarPorProvedorAsync(provedorId, ct);

    public async Task<VersaoAdminDetalheDto> ObterDetalheAsync(int id, CancellationToken ct)
    {
        var versao = await _repositorio.ObterPorIdAsync(id, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoProvedor", id);

        return await MapearAsync(versao, ct);
    }

    public async Task<VersaoAdminDetalheDto> CriarAsync(int provedorId, CriarVersaoDto dto, CancellationToken ct)
    {
        _ = await _provedorRepositorio.ObterPorIdAsync(provedorId, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("Provedor", provedorId);

        var versao = VersaoProvedor.Criar(provedorId, dto.VersaoLayoutId, dto.Rotulo, dto.Introducao);

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.AdicionarAsync(versao, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoVersaoProvedor(versao);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.versaoprovedor, versao.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return await MapearAsync(versao, ct);
    }

    public async Task<VersaoAdminDetalheDto> AtualizarAsync(int id, AtualizarVersaoDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var versao = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        versao.AtualizarRotulo(dto.Rotulo);
        versao.AtualizarIntroducao(dto.Introducao);

        var trocaLayout = dto.VersaoLayoutId is { } versaoLayoutIdNovo && versaoLayoutIdNovo != versao.VersaoLayoutId;

        // Só a existência/atividade do DESTINO é lida fora da transação — não depende de nenhuma
        // outra escrita concorrente na própria versão (S-01: o que precisa do lock é a contagem).
        if (trocaLayout)
            await ValidarLayoutDestinoAsync(dto.VersaoLayoutId!.Value, ct);

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            if (trocaLayout)
            {
                // S-01 (demanda 008): lock da linha ANTES do COUNT — serializa com um POST de
                // campo concorrente na mesma versão. Sem o lock, o POST valida contra o layout
                // ATUAL (lido antes deste PUT commitar) e o COUNT deste PUT roda antes do INSERT
                // daquele POST commitar: os dois passam, e a versão fica com layout trocado E
                // campo cujo canônico pertence ao layout velho.
                await _repositorio.BloquearParaAlteracaoAsync(versao.Id, ctInterno);
                await GarantirVersaoSemCamposAsync(versao.Id, ctInterno);
                versao.TrocarVersaoLayout(dto.VersaoLayoutId!.Value);
            }

            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            var (pesoA2, pesoB2, pesoC2, pesoD2) = PesosBusca.DoVersaoProvedor(versao);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.versaoprovedor, versao.Id, pesoA2, pesoB2, pesoC2, pesoD2, ctInterno);
        }, ct);

        return await MapearAsync(versao, ct);
    }

    /// <summary>Só existência/atividade do layout destino — leitura pura, independente de concorrência na própria versão.</summary>
    private async Task ValidarLayoutDestinoAsync(int versaoLayoutIdNovo, CancellationToken ct)
    {
        var layoutDestino = await _versaoLayoutRepositorio.ObterPorIdAsync(versaoLayoutIdNovo, rastreada: false, ct);
        if (layoutDestino is null)
            throw new RegraDeNegocioException(
                CodigosErro.VersaoLayoutInvalida,
                $"A versão de layout '{versaoLayoutIdNovo}' não existe ou está desativada.");
    }

    /// <summary>
    /// A versão não pode ter nenhum <see cref="CampoVersao"/> cadastrado para trocar de layout —
    /// cada um deles existe porque o campo canônico tinha origem EXATAMENTE no layout atual. Chamar
    /// SOMENTE depois de <see cref="IVersaoProvedorRepositorioInterno.BloquearParaAlteracaoAsync"/>
    /// (S-01) — contagem sem lock é TOCTOU contra a criação de campo concorrente.
    /// </summary>
    private async Task GarantirVersaoSemCamposAsync(int versaoProvedorId, CancellationToken ct)
    {
        var totalCampos = await _campoVersaoRepositorio.ContarPorVersaoAsync(versaoProvedorId, ct);
        if (totalCampos > 0)
            throw new RegraDeNegocioException(
                CodigosErro.VersaoComCampos,
                "Não é possível trocar o layout de origem: esta versão já tem campos cadastrados, "
                + "e cada um deles tem origem no layout atual. Remova os campos antes de trocar o layout.");
    }

    public async Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var versao = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);
        versao.Desativar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.DesativarAsync(TipoItemIndexado.versaoprovedor, versao.Id, ctInterno);
        }, ct);
    }

    public async Task<VersaoAdminDetalheDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct)
    {
        var versao = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        if (publicado) versao.Publicar(); else versao.Despublicar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.VersaoProvedor, versao.Id, ctInterno);
        }, ct);

        return await MapearAsync(versao, ct);
    }

    public async Task<VersaoAdminDetalheDto> RegistrarVerificacaoAsync(int id, AtualizarVerificacaoDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var versao = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);
        versao.RegistrarVerificacao(dto.VerificadoEm);
        await _repositorio.SalvarAlteracoesAsync(ct);

        return await MapearAsync(versao, ct);
    }

    private async Task<VersaoProvedor> CarregarRastreadaOuFalharAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var versao = await _repositorio.ObterPorIdAsync(id, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoProvedor", id);

        if (versao.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException("A versão do provedor foi alterada por outra requisição. Recarregue e tente novamente.");

        return versao;
    }

    /// <summary>
    /// Os avisos entram em toda resposta de detalhe, não só na leitura: a tela de versão invalida
    /// o detalhe depois de publicar, verificar e atualizar, e receber o agregado sem eles voltaria
    /// a quebrar a tela no primeiro refetch.
    /// </summary>
    private async Task<VersaoAdminDetalheDto> MapearAsync(VersaoProvedor versao, CancellationToken ct)
    {
        var avisos = await _avisoRepositorio.ListarPorVersaoAsync(versao.Id, ct);

        return new VersaoAdminDetalheDto
        {
            Id = versao.Id,
            ProvedorId = versao.ProvedorId,
            Slug = versao.Slug,
            Rotulo = versao.Rotulo,
            Introducao = versao.Introducao,
            VersaoLayoutId = versao.VersaoLayoutId,
            Publicado = versao.Publicado,
            VerificadoEm = versao.VerificadoEm,
            Avisos = avisos.Select(AvisoAdminMapper.Mapear).ToArray(),
            VersaoConcorrencia = versao.Versao,
        };
    }
}
