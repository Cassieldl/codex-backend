using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Application.Mappings;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services.Interno;

/// <summary>Área interna de <see cref="Provedor"/> (I1-I5, I3b, analise-backend.md §4.2/§3.4).</summary>
public sealed class ProvedorServicoInterno : IProvedorServicoInterno
{
    private readonly IProvedorRepositorioInterno _repositorio;
    private readonly IAvisoRepositorioInterno _avisoRepositorio;
    private readonly IIndiceBuscaWriter _indiceBuscaWriter;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;

    public ProvedorServicoInterno(
        IProvedorRepositorioInterno repositorio,
        IAvisoRepositorioInterno avisoRepositorio,
        IIndiceBuscaWriter indiceBuscaWriter,
        IUnidadeDeTrabalho unidadeDeTrabalho)
    {
        _repositorio = repositorio;
        _avisoRepositorio = avisoRepositorio;
        _indiceBuscaWriter = indiceBuscaWriter;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public Task<PaginaResultado<ProvedorAdminListaItemDto>> ListarAsync(FiltroProvedorInterno filtro, CancellationToken ct) =>
        _repositorio.ListarAsync(NormalizarPaginacao(filtro), ct);

    public async Task<ProvedorAdminDetalheDto> ObterDetalheAsync(int id, CancellationToken ct)
    {
        var provedor = await _repositorio.ObterPorIdAsync(id, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("Provedor", id);

        return await MapearAsync(provedor, ct);
    }

    public async Task<ProvedorAdminDetalheDto> CriarAsync(CriarProvedorDto dto, CancellationToken ct)
    {
        var provedor = Provedor.Criar(dto.Nome, dto.IdentificadorInvoicy, dto.Categoria);
        provedor.SubstituirAliases(dto.Aliases);

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.AdicionarAsync(provedor, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoProvedor(provedor);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.provedor, provedor.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return await MapearAsync(provedor, ct);
    }

    public async Task<ProvedorAdminDetalheDto> AtualizarAsync(int id, AtualizarProvedorDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var provedor = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        provedor.Renomear(dto.Nome);
        provedor.SubstituirAliases(dto.Aliases);

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoProvedor(provedor);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.provedor, provedor.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return await MapearAsync(provedor, ct);
    }

    public async Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var provedor = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);
        provedor.Desativar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.DesativarAsync(TipoItemIndexado.provedor, provedor.Id, ctInterno);
        }, ct);
    }

    /// <summary>Template de publicação de §3.4 — mesma transação para a mudança de estado e a cascata de <c>codex.itensbusca</c>.</summary>
    public async Task<ProvedorAdminDetalheDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct)
    {
        var provedor = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        if (publicado) provedor.Publicar(); else provedor.Despublicar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.Provedor, provedor.Id, ctInterno);
        }, ct);

        return await MapearAsync(provedor, ct);
    }

    private async Task<Provedor> CarregarRastreadaOuFalharAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var provedor = await _repositorio.ObterPorIdAsync(id, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("Provedor", id);

        if (provedor.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException("O provedor foi alterado por outra requisição. Recarregue e tente novamente.");

        return provedor;
    }

    private static FiltroProvedorInterno NormalizarPaginacao(FiltroProvedorInterno filtro)
    {
        const int tamanhoPaginaPadrao = 25;
        const int tamanhoPaginaMaximo = 200; // cap — rules/security.md AP-SECURITY-005.

        var (paginaNormalizada, tamanhoNormalizado) = Paginacao.Normalizar(
            filtro.Pagina, filtro.TamanhoPagina, tamanhoPaginaPadrao, tamanhoPaginaMaximo);

        return filtro with
        {
            Pagina = paginaNormalizada,
            TamanhoPagina = tamanhoNormalizado,
        };
    }

    /// <summary>I2: inclui TODOS os avisos de provedor (publicados e rascunho, diferente de P3/U8).</summary>
    private async Task<ProvedorAdminDetalheDto> MapearAsync(Provedor provedor, CancellationToken ct)
    {
        var avisos = provedor.Id == 0
            ? []
            : await _avisoRepositorio.ListarPorProvedorAsync(provedor.Id, ct);

        return new ProvedorAdminDetalheDto
        {
            Id = provedor.Id,
            Slug = provedor.Slug,
            Nome = provedor.Nome,
            Categoria = provedor.Categoria.ToString(),
            IdentificadorInvoicy = provedor.IdentificadorInvoicy,
            Publicado = provedor.Publicado,
            Aliases = provedor.Aliases.Select(a => a.Alias).ToArray(),
            Avisos = avisos.Select(AvisoAdminMapper.Mapear).ToArray(),
            VersaoConcorrencia = provedor.Versao,
        };
    }
}
