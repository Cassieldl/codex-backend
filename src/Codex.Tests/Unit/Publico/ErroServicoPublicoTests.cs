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

/// <summary>
/// Prova U9 (erro sem versão aplicável é resultado válido, nunca exceção) e a regra unificada de
/// M7/§4.1 (404 vs 410) para P6, com repositório mockado.
/// </summary>
public sealed class ErroServicoPublicoTests
{
    private readonly Mock<IErroRepositorioPublico> _repositorio = new();
    private readonly Mock<IArtigoRepositorioPublico> _artigoRepositorio = new();
    private readonly ErroServicoPublico _sut;

    public ErroServicoPublicoTests()
    {
        _sut = new ErroServicoPublico(_repositorio.Object, _artigoRepositorio.Object);
    }

    [Fact]
    public async Task ObterDetalheAsync_QuandoErroNaoExiste_LancaEntidadeNaoEncontrada()
    {
        _repositorio.Setup(r => r.ObterPorSlugAsync("inexistente", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Erro?)null);

        var acao = () => _sut.ObterDetalheAsync("inexistente", CancellationToken.None);

        await acao.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterDetalheAsync_QuandoErroDespublicado_LancaConteudoDespublicadoComTipoErro()
    {
        var erro = Erro.Criar("ERR-001", OrigemErro.Schema, "Erro de schema");
        // Nunca chama Publicar() — nasce despublicado (M19).

        _repositorio.Setup(r => r.ObterPorSlugAsync(erro.Slug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(erro);

        var acao = () => _sut.ObterDetalheAsync(erro.Slug, CancellationToken.None);

        var excecao = await acao.Should().ThrowAsync<ConteudoDespublicadoException>();
        excecao.Which.TipoItem.Should().Be(nameof(TipoItemIndexado.erro));
    }

    [Fact]
    public async Task ObterDetalheAsync_SemVersaoAplicavel_RetornaListaVaziaNuncaLancaExcecao()
    {
        var erro = Erro.Criar("ERR-002", OrigemErro.InvoiCy, "Erro geral InvoiCy");
        erro.Publicar();

        _repositorio.Setup(r => r.ObterPorSlugAsync(erro.Slug, It.IsAny<CancellationToken>())).ReturnsAsync(erro);
        _repositorio.Setup(r => r.ListarVariantesAsync(erro.Id, It.IsAny<CancellationToken>())).ReturnsAsync(["mensagem variante"]);
        _repositorio.Setup(r => r.ListarVersoesAplicaveisPublicadasAsync(erro.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]); // U9 — erro InvoiCy sem nenhuma versão aplicável
        _repositorio.Setup(r => r.ListarCamposRelacionadosAsync(
                erro.Id, It.Is<IReadOnlyCollection<VersaoAplicavelBrutaDto>>(l => l.Count == 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _artigoRepositorio.Setup(r => r.ListarRelacionadosAoErroPublicadosAsync(erro.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var resultado = await _sut.ObterDetalheAsync(erro.Slug, CancellationToken.None);

        resultado.VersoesAplicaveis.Should().NotBeNull();
        resultado.VersoesAplicaveis.Should().BeEmpty(because: "U9 — erro sem versão aplicável é permitido, nunca um erro de negócio");
    }

    [Fact]
    public async Task ObterDetalheAsync_ComVersoesAplicaveis_MapeiaSlugsCorretamente()
    {
        var erro = Erro.Criar("ERR-003", OrigemErro.Prefeitura, "Erro de prefeitura");
        erro.Publicar();

        var versaoAplicavel = new VersaoAplicavelBrutaDto
        {
            VersaoProvedorId = 42,
            ProvedorSlug = "acme",
            ProvedorNome = "ACME",
            VersaoSlug = "v1",
            VersaoRotulo = "Versão 1",
        };

        _repositorio.Setup(r => r.ObterPorSlugAsync(erro.Slug, It.IsAny<CancellationToken>())).ReturnsAsync(erro);
        _repositorio.Setup(r => r.ListarVariantesAsync(erro.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repositorio.Setup(r => r.ListarVersoesAplicaveisPublicadasAsync(erro.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([versaoAplicavel]);
        _repositorio.Setup(r => r.ListarCamposRelacionadosAsync(erro.Id, It.IsAny<IReadOnlyCollection<VersaoAplicavelBrutaDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _artigoRepositorio.Setup(r => r.ListarRelacionadosAoErroPublicadosAsync(erro.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var resultado = await _sut.ObterDetalheAsync(erro.Slug, CancellationToken.None);

        resultado.VersoesAplicaveis.Should().ContainSingle();
        resultado.VersoesAplicaveis.Single().ProvedorSlug.Should().Be("acme");
        resultado.VersoesAplicaveis.Single().VersaoSlug.Should().Be("v1");
    }

    [Fact]
    public async Task ListarAsync_TamanhoPaginaAcimaDoCap_EnviaCemAoRepositorio()
    {
        FiltroErroPublico? filtroRecebido = null;
        _repositorio
            .Setup(r => r.ListarAsync(It.IsAny<FiltroErroPublico>(), It.IsAny<CancellationToken>()))
            .Callback<FiltroErroPublico, CancellationToken>((f, _) => filtroRecebido = f)
            .ReturnsAsync(new PaginaResultado<ErroListaItemDto>([], 0, 1, 25));

        await _sut.ListarAsync(new FiltroErroPublico(null, null, null, 1, 500), CancellationToken.None);

        filtroRecebido!.TamanhoPagina.Should().Be(100, because: "AP-SECURITY-005 — pageSize sempre com cap");
    }
}
