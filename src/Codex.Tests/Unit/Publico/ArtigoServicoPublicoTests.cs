using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Application.Services;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Codex.Tests.Unit.Publico;

/// <summary>Prova M7/§4.1 (404 vs 410) e §4.3 (só campos/erros publicados) para P7/P8, com repositório mockado.</summary>
public sealed class ArtigoServicoPublicoTests
{
    private readonly Mock<IArtigoRepositorioPublico> _repositorio = new();
    private readonly ArtigoServicoPublico _sut;

    public ArtigoServicoPublicoTests()
    {
        _sut = new ArtigoServicoPublico(_repositorio.Object);
    }

    [Fact]
    public async Task ObterDetalheAsync_QuandoArtigoNaoExiste_LancaEntidadeNaoEncontrada()
    {
        _repositorio.Setup(r => r.ObterPorSlugAsync("inexistente", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Artigo?)null);

        var acao = () => _sut.ObterDetalheAsync("inexistente", CancellationToken.None);

        await acao.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterDetalheAsync_QuandoArtigoDespublicado_LancaConteudoDespublicadoComTipoArtigo()
    {
        var artigo = Artigo.Criar(TipoArtigo.Conceito, "Artigo despublicado", "corpo");
        // Nunca chama Publicar() — nasce despublicado.

        _repositorio.Setup(r => r.ObterPorSlugAsync(artigo.Slug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(artigo);

        var acao = () => _sut.ObterDetalheAsync(artigo.Slug, CancellationToken.None);

        var excecao = await acao.Should().ThrowAsync<ConteudoDespublicadoException>();
        excecao.Which.TipoItem.Should().Be(nameof(TipoItemIndexado.artigo));
    }

    [Fact]
    public async Task ObterDetalheAsync_ArtigoPublicado_MapeiaCamposEUrlCanonica()
    {
        var artigo = Artigo.Criar(TipoArtigo.Procedimento, "Artigo publicado", "corpo do artigo");
        artigo.Publicar();

        _repositorio.Setup(r => r.ObterPorSlugAsync(artigo.Slug, It.IsAny<CancellationToken>())).ReturnsAsync(artigo);
        _repositorio.Setup(r => r.ListarCamposLayoutRelacionadosPublicadosAsync(artigo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repositorio.Setup(r => r.ListarErrosRelacionadosPublicadosAsync(artigo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var resultado = await _sut.ObterDetalheAsync(artigo.Slug, CancellationToken.None);

        resultado.Slug.Should().Be(artigo.Slug);
        resultado.Reservado.Should().BeFalse();
        resultado.UrlCanonica.Should().Be($"/artigos/{artigo.Slug}");
        resultado.CamposLayoutRelacionados.Should().BeEmpty();
        resultado.ErrosRelacionados.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarAsync_TamanhoPaginaAcimaDoCap_EnviaCemAoRepositorio()
    {
        FiltroArtigoPublico? filtroRecebido = null;
        _repositorio
            .Setup(r => r.ListarAsync(It.IsAny<FiltroArtigoPublico>(), It.IsAny<CancellationToken>()))
            .Callback<FiltroArtigoPublico, CancellationToken>((f, _) => filtroRecebido = f)
            .ReturnsAsync(new PaginaResultado<ArtigoListaItemDto>([], 0, 1, 25));

        await _sut.ListarAsync(new FiltroArtigoPublico(null, null, 1, 500), CancellationToken.None);

        filtroRecebido!.TamanhoPagina.Should().Be(100, because: "AP-SECURITY-005 — pageSize sempre com cap");
    }

    [Fact]
    public async Task ListarAsync_ComTipoSemNenhumArtigo_NuncaLanca_RetornaVazio()
    {
        _repositorio
            .Setup(r => r.ListarAsync(It.IsAny<FiltroArtigoPublico>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginaResultado<ArtigoListaItemDto>([], 0, 1, 25));

        var resultado = await _sut.ListarAsync(new FiltroArtigoPublico([TipoArtigo.Mudanca], null, 1, 25), CancellationToken.None);

        resultado.TotalCount.Should().Be(0, because: "U-3 — tipo sem artigo é estado de produto válido, nunca erro");
    }
}
