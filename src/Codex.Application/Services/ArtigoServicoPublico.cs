using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services;

/// <summary>Leitura pública de artigo (P7/P8, analise-backend.md §4.2/§4.3) — mesmo padrão de <c>ErroServicoPublico</c>.</summary>
public sealed class ArtigoServicoPublico : IArtigoServicoPublico
{
    private const int TamanhoPaginaPadrao = 25;
    private const int TamanhoPaginaMaximo = 100;

    private readonly IArtigoRepositorioPublico _repositorio;

    public ArtigoServicoPublico(IArtigoRepositorioPublico repositorio)
    {
        _repositorio = repositorio;
    }

    public Task<PaginaResultado<ArtigoListaItemDto>> ListarAsync(FiltroArtigoPublico filtro, CancellationToken ct)
    {
        var (paginaNormalizada, tamanhoNormalizado) = Paginacao.Normalizar(
            filtro.Pagina, filtro.TamanhoPagina, TamanhoPaginaPadrao, TamanhoPaginaMaximo);

        var filtroNormalizado = filtro with
        {
            Pagina = paginaNormalizada,
            TamanhoPagina = tamanhoNormalizado,
        };

        return _repositorio.ListarAsync(filtroNormalizado, ct);
    }

    public async Task<ArtigoDetalheDto> ObterDetalheAsync(string artigoSlug, CancellationToken ct)
    {
        var artigo = await _repositorio.ObterPorSlugAsync(artigoSlug, ct)
            ?? throw new EntidadeNaoEncontradaException("Artigo", artigoSlug);

        if (!artigo.Publicado)
            throw new ConteudoDespublicadoException(nameof(TipoItemIndexado.artigo), artigoSlug);

        var camposLayoutRelacionados = await _repositorio.ListarCamposLayoutRelacionadosPublicadosAsync(artigo.Id, ct);
        var errosRelacionados = await _repositorio.ListarErrosRelacionadosPublicadosAsync(artigo.Id, ct);

        return new ArtigoDetalheDto
        {
            Slug = artigo.Slug,
            Tipo = artigo.Tipo.ToString(),
            Titulo = artigo.Titulo,
            Corpo = artigo.Corpo,
            VerificadoEm = artigo.VerificadoEm.HasValue ? DateOnly.FromDateTime(artigo.VerificadoEm.Value.UtcDateTime) : null,
            UrlCanonica = SlugsReservadosArtigo.RotaCanonica(artigo.Slug),
            Reservado = SlugsReservadosArtigo.EhReservado(artigo.Slug),
            CamposLayoutRelacionados = camposLayoutRelacionados,
            ErrosRelacionados = errosRelacionados,
        };
    }

    public Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct) =>
        _repositorio.ObterRedirecionamentoAsync(slugAntigo, ct);
}
