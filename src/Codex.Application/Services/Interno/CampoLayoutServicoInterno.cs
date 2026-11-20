using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services.Interno;

/// <summary>Área interna de <see cref="CampoLayout"/> (I16-I17c, risco #2 do DBA).</summary>
public sealed class CampoLayoutServicoInterno : ICampoLayoutServicoInterno
{
    private readonly ICampoLayoutRepositorioInterno _repositorio;
    private readonly IVersaoLayoutRepositorioInterno _versaoLayoutRepositorio;
    private readonly IIndiceBuscaWriter _indiceBuscaWriter;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;

    public CampoLayoutServicoInterno(
        ICampoLayoutRepositorioInterno repositorio,
        IVersaoLayoutRepositorioInterno versaoLayoutRepositorio,
        IIndiceBuscaWriter indiceBuscaWriter,
        IUnidadeDeTrabalho unidadeDeTrabalho)
    {
        _repositorio = repositorio;
        _versaoLayoutRepositorio = versaoLayoutRepositorio;
        _indiceBuscaWriter = indiceBuscaWriter;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public Task<IReadOnlyCollection<CampoLayoutResponseDto>> ListarPorVersaoLayoutAsync(int versaoLayoutId, CancellationToken ct) =>
        _repositorio.ListarPorVersaoLayoutAsync(versaoLayoutId, ct);

    public async Task<CampoLayoutResponseDto> CriarAsync(int versaoLayoutId, CriarCampoLayoutDto dto, CancellationToken ct)
    {
        _ = await _versaoLayoutRepositorio.ObterPorIdAsync(versaoLayoutId, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoLayout", versaoLayoutId);

        var campoLayout = CampoLayout.Criar(versaoLayoutId, dto.Nome, dto.CaminhoXml, dto.Significado, dto.Ordem, dto.Grupo, dto.ReformaTributaria);

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.AdicionarAsync(campoLayout, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoCampoLayout(campoLayout);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.campolayout, campoLayout.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return Mapear(campoLayout);
    }

    public async Task<CampoLayoutResponseDto> AtualizarAsync(int id, AtualizarCampoLayoutDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var campoLayout = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, comOrigemCarregada: dto.Estado == EstadoCampoLayout.Descontinuado, ct);

        if (dto.Nome is not null)
            campoLayout.AtualizarNome(dto.Nome);

        if (dto.Significado is not null)
            campoLayout.AtualizarSignificado(dto.Significado);

        if (dto.Grupo is not null)
            campoLayout.AtualizarGrupo(dto.Grupo);

        if (dto.Ordem.HasValue)
            campoLayout.AtualizarOrdem(dto.Ordem.Value);

        if (dto.ReformaTributaria.HasValue)
            campoLayout.AtualizarReformaTributaria(dto.ReformaTributaria.Value);

        if (dto.Estado == EstadoCampoLayout.Descontinuado && dto.VersaoLayoutDescontinuadoId.HasValue)
        {
            var versaoDescontinuacao = await _versaoLayoutRepositorio.ObterPorIdAsync(dto.VersaoLayoutDescontinuadoId.Value, rastreada: false, ct)
                ?? throw new EntidadeNaoEncontradaException("VersaoLayout", dto.VersaoLayoutDescontinuadoId.Value);

            campoLayout.Descontinuar(versaoDescontinuacao); // lança RegraDeNegocioException (422) se a ordem não for posterior — risco #2.
        }

        if (dto.Estado == EstadoCampoLayout.Ativo && campoLayout.Estado == EstadoCampoLayout.Descontinuado)
            campoLayout.DesfazerDescontinuacao();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoCampoLayout(campoLayout);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.campolayout, campoLayout.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return Mapear(campoLayout);
    }

    public async Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var campoLayout = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, comOrigemCarregada: false, ct);
        campoLayout.Desativar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.DesativarAsync(TipoItemIndexado.campolayout, campoLayout.Id, ctInterno);
        }, ct);
    }

    public async Task<CampoLayoutResponseDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct)
    {
        var campoLayout = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, comOrigemCarregada: false, ct);

        if (publicado) campoLayout.Publicar(); else campoLayout.Despublicar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.CampoLayout, campoLayout.Id, ctInterno);
        }, ct);

        return Mapear(campoLayout);
    }

    private async Task<CampoLayout> CarregarRastreadaOuFalharAsync(int id, uint versaoEsperada, bool comOrigemCarregada, CancellationToken ct)
    {
        var campoLayout = await _repositorio.ObterPorIdAsync(id, rastreada: true, comOrigemCarregada, ct)
            ?? throw new EntidadeNaoEncontradaException("CampoLayout", id);

        if (campoLayout.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException("O campo canônico foi alterado por outra requisição. Recarregue e tente novamente.");

        return campoLayout;
    }

    private static CampoLayoutResponseDto Mapear(CampoLayout campoLayout) => new()
    {
        Id = campoLayout.Id,
        VersaoLayoutId = campoLayout.VersaoLayoutId,
        VersaoLayoutDescontinuadoId = campoLayout.VersaoLayoutDescontinuadoId,
        Nome = campoLayout.Nome,
        CaminhoXml = campoLayout.CaminhoXml,
        Significado = campoLayout.Significado,
        Grupo = campoLayout.Grupo,
        Ordem = campoLayout.Ordem,
        ReformaTributaria = campoLayout.ReformaTributaria,
        Slug = campoLayout.Slug,
        Estado = campoLayout.Estado.ToString(),
        Publicado = campoLayout.Publicado,
        VersaoConcorrencia = campoLayout.Versao,
    };
}
