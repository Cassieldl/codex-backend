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
/// Prova a regra unificada de M7/§4.1 (404 vs 410 vs cascata), U7 (sem "versão vigente"), U8
/// (avisos de provedor separados dos de versão) e M22 (cap de 2000 campos também na leitura) —
/// tudo no nível do service, com repositórios mockados (a prova contra Postgres real está em
/// <c>Codex.Tests.Integration.Publico</c>, não executável nesta máquina por falta de Docker).
/// </summary>
public sealed class ProvedorServicoPublicoTests
{
    private readonly Mock<IProvedorRepositorioPublico> _provedorRepositorio = new();
    private readonly Mock<IVersaoProvedorRepositorioPublico> _versaoRepositorio = new();
    private readonly ProvedorServicoPublico _sut;

    public ProvedorServicoPublicoTests()
    {
        _sut = new ProvedorServicoPublico(_provedorRepositorio.Object, _versaoRepositorio.Object);

        // Aditivo de exemplos em P4 (003/§5.5): default vazio para os casos que não falam de
        // exemplo — mock solto devolveria null e o DTO sairia com a coleção nula em vez de [].
        _versaoRepositorio.Setup(r => r.ListarExemplosAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task ObterDetalheAsync_QuandoProvedorNaoExiste_LancaEntidadeNaoEncontrada()
    {
        _provedorRepositorio.Setup(r => r.ObterPorSlugAsync("inexistente", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Provedor?)null);

        var acao = () => _sut.ObterDetalheAsync("inexistente", CancellationToken.None);

        await acao.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterDetalheAsync_QuandoProvedorDespublicado_LancaConteudoDespublicadoComTipoProvedor()
    {
        var provedor = Provedor.Criar("Prefeitura X", "INV-1", CategoriaProvedor.Nacional);
        // Nunca chama Publicar() — nasce despublicado (M19).

        _provedorRepositorio.Setup(r => r.ObterPorSlugAsync("prefeitura-x", It.IsAny<CancellationToken>()))
            .ReturnsAsync(provedor);

        var acao = () => _sut.ObterDetalheAsync("prefeitura-x", CancellationToken.None);

        var excecao = await acao.Should().ThrowAsync<ConteudoDespublicadoException>();
        excecao.Which.TipoItem.Should().Be(nameof(TipoItemIndexado.provedor));
    }

    [Fact]
    public async Task ObterDetalheAsync_QuandoPublicado_OrdenaAvisosPorSeveridadeDepoisVigenciaDesc()
    {
        var provedor = Provedor.Criar("Prefeitura X", "INV-1", CategoriaProvedor.Nacional);
        provedor.Publicar();

        var avisoAlertaAntigo = Aviso.CriarDeProvedor(provedor.Id, Severidade.Alerta, "Alerta antigo", "corpo", new DateOnly(2026, 1, 1));
        var avisoCriticoRecente = Aviso.CriarDeProvedor(provedor.Id, Severidade.Critico, "Crítico recente", "corpo", new DateOnly(2026, 6, 1));
        var avisoInformativo = Aviso.CriarDeProvedor(provedor.Id, Severidade.Informativo, "Informativo", "corpo", new DateOnly(2026, 3, 1));

        _provedorRepositorio.Setup(r => r.ObterPorSlugAsync("prefeitura-x", It.IsAny<CancellationToken>()))
            .ReturnsAsync(provedor);
        _provedorRepositorio.Setup(r => r.ListarAvisosPublicadosDoProvedorAsync(provedor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([avisoAlertaAntigo, avisoCriticoRecente, avisoInformativo]);
        _provedorRepositorio.Setup(r => r.ListarVersoesPublicadasAsync(provedor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var resultado = await _sut.ObterDetalheAsync("prefeitura-x", CancellationToken.None);

        resultado.Avisos.Select(a => a.Titulo).Should().ContainInOrder("Crítico recente", "Alerta antigo", "Informativo");
    }

    [Fact]
    public async Task ObterDetalheAsync_UrlDaVersao_EhSempreUmPathInternoRelativo()
    {
        // Restrição de segurança (achado BAIXO-02, auditoria de frontend): o campo `url` vai
        // direto para <Link to>/<a href> sem validação lá — nunca pode ser absoluto. A garantia
        // aqui é estrutural: Slug.DeTexto só produz [a-z0-9-], nunca vazio, nunca contém '/'.
        var provedor = Provedor.Criar("Prefeitura X", "INV-1", CategoriaProvedor.Nacional);
        provedor.Publicar();
        var versao = VersaoProvedor.Criar(provedor.Id, 1, "v1");
        versao.Publicar();

        _provedorRepositorio.Setup(r => r.ObterPorSlugAsync("prefeitura-x", It.IsAny<CancellationToken>())).ReturnsAsync(provedor);
        _provedorRepositorio.Setup(r => r.ListarAvisosPublicadosDoProvedorAsync(provedor.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _provedorRepositorio.Setup(r => r.ListarVersoesPublicadasAsync(provedor.Id, It.IsAny<CancellationToken>())).ReturnsAsync([versao]);

        var resultado = await _sut.ObterDetalheAsync("prefeitura-x", CancellationToken.None);

        var url = resultado.Versoes.Single().Url;
        url.Should().StartWith("/");
        url.Should().NotContain("://");
        url.Should().NotStartWith("//");
    }

    [Fact]
    public async Task ObterVersaoPaginaAsync_QuandoJoinNaoEncontraNada_LancaEntidadeNaoEncontrada()
    {
        _versaoRepositorio.Setup(r => r.ObterPorSlugsAsync("acme", "v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProvedorVersaoBrutaDto?)null);

        var acao = () => _sut.ObterVersaoPaginaAsync("acme", "v1", CancellationToken.None);

        await acao.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterVersaoPaginaAsync_QuandoProvedorDespublicado_LancaConteudoDespublicadoComTipoProvedor_MesmoComVersaoPublicada()
    {
        var provedor = Provedor.Criar("ACME", "INV-2", CategoriaProvedor.Proprio);
        var versao = VersaoProvedor.Criar(provedor.Id, 1, "v1");
        versao.Publicar(); // versão publicada, mas o provedor pai não — cascata deve barrar (M19)

        _versaoRepositorio.Setup(r => r.ObterPorSlugsAsync("acme", "v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvedorVersaoBrutaDto(provedor, versao, "Layout 2024"));

        var acao = () => _sut.ObterVersaoPaginaAsync("acme", "v1", CancellationToken.None);

        var excecao = await acao.Should().ThrowAsync<ConteudoDespublicadoException>();
        excecao.Which.TipoItem.Should().Be(nameof(TipoItemIndexado.provedor));
    }

    [Fact]
    public async Task ObterVersaoPaginaAsync_QuandoVersaoDespublicada_LancaConteudoDespublicadoComTipoVersaoProvedor()
    {
        var provedor = Provedor.Criar("ACME", "INV-2", CategoriaProvedor.Proprio);
        provedor.Publicar();
        var versao = VersaoProvedor.Criar(provedor.Id, 1, "v1"); // nunca publicada

        _versaoRepositorio.Setup(r => r.ObterPorSlugsAsync("acme", "v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvedorVersaoBrutaDto(provedor, versao, "Layout 2024"));

        var acao = () => _sut.ObterVersaoPaginaAsync("acme", "v1", CancellationToken.None);

        var excecao = await acao.Should().ThrowAsync<ConteudoDespublicadoException>();
        excecao.Which.TipoItem.Should().Be(nameof(TipoItemIndexado.versaoprovedor));
    }

    [Fact]
    public async Task ObterVersaoPaginaAsync_QuandoExcedeCapDe2000Campos_LancaLimiteExcedido()
    {
        var provedor = Provedor.Criar("ACME", "INV-2", CategoriaProvedor.Proprio);
        provedor.Publicar();
        var versao = VersaoProvedor.Criar(provedor.Id, 1, "v1");
        versao.Publicar();

        _versaoRepositorio.Setup(r => r.ObterPorSlugsAsync("acme", "v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvedorVersaoBrutaDto(provedor, versao, "Layout 2024"));
        _versaoRepositorio.Setup(r => r.ContarCamposAsync(versao.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2001);

        var acao = () => _sut.ObterVersaoPaginaAsync("acme", "v1", CancellationToken.None);

        await acao.Should().ThrowAsync<LimiteExcedidoException>();
    }

    [Fact]
    public async Task ObterVersaoPaginaAsync_AgrupaCamposPorGrupoOrdenadoPelaMenorOrdemEOrdenaCamposDentroDoGrupo()
    {
        var provedor = Provedor.Criar("ACME", "INV-2", CategoriaProvedor.Proprio);
        provedor.Publicar();
        var versao = VersaoProvedor.Criar(provedor.Id, 1, "v1");
        versao.Publicar();

        _versaoRepositorio.Setup(r => r.ObterPorSlugsAsync("acme", "v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvedorVersaoBrutaDto(provedor, versao, "Layout 2024"));
        _versaoRepositorio.Setup(r => r.ContarCamposAsync(versao.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        CampoVersaoProjecaoDto Campo(string slug, int ordem, string? grupo, string? grupoSlug) => new()
        {
            CampoLayoutSlug = slug,
            Nome = slug,
            CaminhoXml = $"/{slug}",
            Significado = "significado",
            Tipo = "texto",
            EstadoPreenchimento = "Obrigatorio",
            Ordem = ordem,
            Grupo = grupo,
            GrupoSlug = grupoSlug,
        };

        // Grupo "financeiro" tem menor ordem mínima (2) que "fiscal" (5) — deve vir primeiro,
        // mesmo que dentro dele o campo de ordem 10 exista.
        _versaoRepositorio.Setup(r => r.ListarCamposAsync(versao.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Campo("campo-fiscal", 5, "Fiscal", "fiscal"),
                Campo("campo-financeiro-b", 10, "Financeiro", "financeiro"),
                Campo("campo-financeiro-a", 2, "Financeiro", "financeiro"),
            ]);
        _versaoRepositorio.Setup(r => r.ListarAvisosPublicadosDaVersaoAsync(versao.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _versaoRepositorio.Setup(r => r.ListarErrosAplicaveisPublicadosAsync(versao.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _versaoRepositorio.Setup(r => r.ListarOutrasVersoesPublicadasAsync(provedor.Id, versao.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var resultado = await _sut.ObterVersaoPaginaAsync("acme", "v1", CancellationToken.None);

        resultado.CamposPorGrupo.Should().HaveCount(2);
        resultado.CamposPorGrupo.First().Grupo.Should().Be("Financeiro");
        resultado.CamposPorGrupo.First().Campos.Select(c => c.CampoLayoutSlug)
            .Should().ContainInOrder("campo-financeiro-a", "campo-financeiro-b");
        resultado.CamposPorGrupo.Last().Grupo.Should().Be("Fiscal");
        resultado.TotalCampos.Should().Be(3);
    }

    /// <summary>
    /// Aditivo de exemplos em P4 (003/§5.5) — o service repassa a coleção do repositório para o
    /// DTO sem reordenar nem filtrar: a ordem é responsabilidade do repositório (âncora estável) e
    /// a visibilidade já foi decidida pela cascata provedor → versão (exemplo não tem publicação
    /// própria, C1).
    /// </summary>
    [Fact]
    public async Task ObterVersaoPaginaAsync_RepassaOsExemplosDoRepositorioNaMesmaOrdem()
    {
        var provedor = Provedor.Criar("ACME", "INV-3", CategoriaProvedor.Proprio);
        provedor.Publicar();
        var versao = VersaoProvedor.Criar(provedor.Id, 1, "v1");
        versao.Publicar();

        _versaoRepositorio.Setup(r => r.ObterPorSlugsAsync("acme", "v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvedorVersaoBrutaDto(provedor, versao, "Layout 2024"));
        _versaoRepositorio.Setup(r => r.ContarCamposAsync(versao.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _versaoRepositorio.Setup(r => r.ListarCamposAsync(versao.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _versaoRepositorio.Setup(r => r.ListarAvisosPublicadosDaVersaoAsync(versao.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _versaoRepositorio.Setup(r => r.ListarErrosAplicaveisPublicadosAsync(versao.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _versaoRepositorio.Setup(r => r.ListarOutrasVersoesPublicadasAsync(provedor.Id, versao.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        ExemploPublicoDto Exemplo(string slug) => new() { Slug = slug, Nome = slug, Descricao = null, Xml = "<x/>" };

        _versaoRepositorio.Setup(r => r.ListarExemplosAsync(versao.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Exemplo("zebra"), Exemplo("alfa"), Exemplo("meio")]);

        var resultado = await _sut.ObterVersaoPaginaAsync("acme", "v1", CancellationToken.None);

        resultado.Exemplos.Select(e => e.Slug).Should().ContainInOrder("zebra", "alfa", "meio");
    }

    [Fact]
    public async Task ObterVersaoPaginaAsync_SemExemplos_DevolveColecaoVaziaNuncaNula()
    {
        var provedor = Provedor.Criar("ACME", "INV-4", CategoriaProvedor.Proprio);
        provedor.Publicar();
        var versao = VersaoProvedor.Criar(provedor.Id, 1, "v1");
        versao.Publicar();

        _versaoRepositorio.Setup(r => r.ObterPorSlugsAsync("acme", "v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvedorVersaoBrutaDto(provedor, versao, "Layout 2024"));
        _versaoRepositorio.Setup(r => r.ContarCamposAsync(versao.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _versaoRepositorio.Setup(r => r.ListarCamposAsync(versao.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _versaoRepositorio.Setup(r => r.ListarAvisosPublicadosDaVersaoAsync(versao.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _versaoRepositorio.Setup(r => r.ListarErrosAplicaveisPublicadosAsync(versao.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _versaoRepositorio.Setup(r => r.ListarOutrasVersoesPublicadasAsync(provedor.Id, versao.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var resultado = await _sut.ObterVersaoPaginaAsync("acme", "v1", CancellationToken.None);

        resultado.Exemplos.Should().NotBeNull().And.BeEmpty(because: "U9 — [] quando não há, nunca ausente");
    }

    [Fact]
    public async Task ListarAsync_TamanhoPaginaAcimaDoCap_EnviaCemAoRepositorio()
    {
        FiltroProvedorPublico? filtroRecebido = null;
        _provedorRepositorio
            .Setup(r => r.ListarAsync(It.IsAny<FiltroProvedorPublico>(), It.IsAny<CancellationToken>()))
            .Callback<FiltroProvedorPublico, CancellationToken>((f, _) => filtroRecebido = f)
            .ReturnsAsync(new PaginaResultado<ProvedorListaItemDto>([], 0, 1, 25));

        await _sut.ListarAsync(new FiltroProvedorPublico(null, null, null, null, 1, 99999), CancellationToken.None);

        filtroRecebido.Should().NotBeNull();
        filtroRecebido!.TamanhoPagina.Should().Be(100, because: "AP-SECURITY-005 — pageSize sempre com cap, nunca o valor bruto do cliente");
    }

    [Fact]
    public async Task ListarAsync_PaginaMenorQueUm_NormalizaParaUm()
    {
        FiltroProvedorPublico? filtroRecebido = null;
        _provedorRepositorio
            .Setup(r => r.ListarAsync(It.IsAny<FiltroProvedorPublico>(), It.IsAny<CancellationToken>()))
            .Callback<FiltroProvedorPublico, CancellationToken>((f, _) => filtroRecebido = f)
            .ReturnsAsync(new PaginaResultado<ProvedorListaItemDto>([], 0, 1, 25));

        await _sut.ListarAsync(new FiltroProvedorPublico(null, null, null, null, -5, 25), CancellationToken.None);

        filtroRecebido!.Pagina.Should().Be(1);
    }
}
