using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// P9 (analise-backend.md §6) medido pelo <b>JSON literal</b> da resposta HTTP contra um Postgres
/// real. Objeto C# correto não é contrato HTTP correto: enum vira número, caixa de propriedade
/// muda e coleção vazia vira <c>null</c> só no wire — e o array <c>versoes</c> é exatamente o que
/// o widget "escolha o provedor → escolha a versão" do frontend já consome no shape
/// <c>{ slug, rotulo }[]</c> (decisoes.md rodada 4).
///
/// <para>
/// O banco é compartilhado pela <see cref="PostgresCollection"/>: toda asserção de conjunto é
/// restrita ao cenário (sufixo único), nunca a contagens globais. As exceções são o bloco de
/// layout — que ocupa o slot GLOBAL de <c>Estado = Vigente</c> e por isso é determinístico
/// enquanto esta classe roda — e <c>artigosRecentes</c>, cujos itens são os três mais recentes do
/// acervo e portanto os três semeados aqui (classes da mesma Collection rodam em série).
/// </para>
///
/// <para>
/// Limpeza em <c>DisposeAsync</c> é <b>hard delete</b>: soft delete manteria o slot de
/// <c>Vigente</c> ocupado para as demais classes da Collection.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class HomeHttpTests : IAsyncLifetime
{
    private const string RotaHome = "/api/v1/home";

    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private Cenario _cenario = null!;

    public HomeHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _cenario = await SemearAsync();
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        await LimparAsync();
    }

    // ------------------------------------------------------- junção do contrato (rodada 4)

    [Fact]
    public async Task Home_ProvedorComTresVersoesPublicadas_ExpoeOArrayVersoesNoShapeQueOWidgetConsome()
    {
        var provedor = await LocalizarProvedorAsync(_cenario.ProvedorPublicadoSlug);

        var versoes = provedor.GetProperty("versoes");
        versoes.ValueKind.Should().Be(JsonValueKind.Array,
            because: "o contrato da rodada 4 é a LISTA das versões publicadas, não a contagem `outrasVersoes`");
        versoes.GetArrayLength().Should().Be(3, because: "o provedor tem três versões publicadas e uma em rascunho");

        // Shape literal de cada item: exatamente { slug, rotulo }, nada além.
        foreach (var versao in versoes.EnumerateArray())
        {
            versao.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["slug", "rotulo"]);
            versao.GetProperty("slug").GetString().Should().NotBeNullOrWhiteSpace();
            versao.GetProperty("rotulo").GetString().Should().NotBeNullOrWhiteSpace();
        }

        // Ordenação por VersaoLayout.Ordem DESC. Três elementos semeados fora de ordem (A, B, C):
        // o esperado (B, C, A) não coincide nem com a inserção nem com o inverso dela.
        versoes.EnumerateArray().Select(v => v.GetProperty("slug").GetString()).Should().Equal(
            _cenario.VersaoBSlug, _cenario.VersaoCSlug, _cenario.VersaoASlug);

        versoes.EnumerateArray().Select(v => v.GetProperty("slug").GetString())
            .Should().NotContain(_cenario.VersaoRascunhoSlug, because: "versão em rascunho não é publicada");
    }

    [Fact]
    public async Task Home_UltimaVersao_EhSempreOPrimeiroItemDeVersoes()
    {
        var provedor = await LocalizarProvedorAsync(_cenario.ProvedorPublicadoSlug);

        var ultima = provedor.GetProperty("ultimaVersao");
        var primeira = provedor.GetProperty("versoes").EnumerateArray().First();

        ultima.GetProperty("slug").GetString().Should().Be(primeira.GetProperty("slug").GetString());
        ultima.GetProperty("rotulo").GetString().Should().Be(primeira.GetProperty("rotulo").GetString());
        ultima.GetProperty("slug").GetString().Should().Be(_cenario.VersaoBSlug);
    }

    [Fact]
    public async Task Home_CardDoProvedor_TemExatamenteOsCamposDoContrato()
    {
        var provedor = await LocalizarProvedorAsync(_cenario.ProvedorPublicadoSlug);

        provedor.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["slug", "nome", "ultimaVersao", "versoes", "verificadoEm", "temComunicado"]);

        // MAX(VerificadoEm) entre as versões publicadas — Provedor não tem coluna própria.
        provedor.GetProperty("verificadoEm").GetString().Should().Be("2026-01-15");
        provedor.GetProperty("temComunicado").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Home_ProvedorPublicadoSemVersaoPublicada_TemVersoesVaziasESemUltimaVersao()
    {
        var provedor = await LocalizarProvedorAsync(_cenario.ProvedorSemVersaoSlug);

        provedor.GetProperty("versoes").GetArrayLength().Should().Be(0,
            because: "coleção vazia é [] no wire, nunca null — o cliente não distingue ausente de vazio");
        provedor.GetProperty("ultimaVersao").ValueKind.Should().Be(JsonValueKind.Null);
        provedor.GetProperty("verificadoEm").ValueKind.Should().Be(JsonValueKind.Null,
            because: "MAX sobre conjunto vazio é ausência de data, nunca erro");
        provedor.GetProperty("temComunicado").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Home_ProvedorEmRascunho_NaoApareceEmNenhumaCategoria()
    {
        var home = await ObterHomeAsync();

        TodosOsProvedores(home).Select(p => p.GetProperty("slug").GetString())
            .Should().NotContain(_cenario.ProvedorRascunhoSlug);
    }

    [Fact]
    public async Task Home_AgrupaPorCategoria_ComTotalCoerenteComOArrayDeProvedores()
    {
        var home = await ObterHomeAsync();

        var categorias = home.GetProperty("provedoresPorCategoria").EnumerateArray().ToList();
        categorias.Should().NotBeEmpty();

        foreach (var categoria in categorias)
        {
            categoria.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["categoria", "total", "provedores"]);
            categoria.GetProperty("total").GetInt32().Should().Be(categoria.GetProperty("provedores").GetArrayLength(),
                because: "`total` é a contagem do próprio grupo — divergir faria a tela anunciar provedor que não entrega");
            categoria.GetProperty("categoria").GetString().Should().BeOneOf(
                ["Nacional", "Proprio"],
                "enum sai como NOME do enum, nunca como ordinal nem como rótulo de exibição");
        }

        CategoriaDoProvedor(home, _cenario.ProvedorPublicadoSlug).Should().Be("Nacional");
        CategoriaDoProvedor(home, _cenario.ProvedorSemVersaoSlug).Should().Be("Proprio");
    }

    // --------------------------------------------------- visibilidade de comunicado (PQ-04)

    [Fact]
    public async Task Home_ComunicadoDeProvedorEmRascunho_NaoAparece_MasODeProvedorPublicadoAparece()
    {
        var home = await ObterHomeAsync();
        var titulos = TitulosDeComunicado(home);

        // Controle positivo: sem ele, um filtro que esconde TUDO passaria neste teste.
        titulos.Should().Contain(_cenario.TituloComunicadoDeProvedor,
            because: "o comunicado do provedor PUBLICADO tem de aparecer");

        titulos.Should().NotContain(_cenario.TituloComunicadoDeProvedorRascunho,
            because: "PQ-04 — comunicado de provedor em rascunho vazaria o nome de um provedor pré-lançamento para um anônimo");

        titulos.Should().NotContain(_cenario.TituloComunicadoDeVersaoRascunho,
            because: "PQ-04 — a cascata vale também para a VERSÃO alvo, não só para o provedor");
    }

    [Fact]
    public async Task Home_ComunicadoNaoPublicadoOuExpirado_NaoAparece()
    {
        var home = await ObterHomeAsync();
        var titulos = TitulosDeComunicado(home);

        titulos.Should().Contain(_cenario.TituloComunicadoGlobal, because: "controle positivo do predicado de vigência");
        titulos.Should().NotContain(_cenario.TituloComunicadoRascunho, because: "Comunicado.EhVigenteEm exige Publicado");
        titulos.Should().NotContain(_cenario.TituloComunicadoExpirado, because: "Comunicado.EhVigenteEm exige ExpiraEm > agora");
        titulos.Should().NotContain(_cenario.TituloComunicadoRemovido, because: "Comunicado.EhVigenteEm exige DesativadoEm == null");
    }

    [Fact]
    public async Task Home_ComunicadoGlobal_NaoTemAlvoNemUrl()
    {
        var comunicado = await LocalizarComunicadoAsync(_cenario.TituloComunicadoGlobal);

        comunicado.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["titulo", "corpo", "severidade", "abrangencia", "expiraEm", "provedorNome", "url"]);

        comunicado.GetProperty("abrangencia").GetString().Should().Be("Global");
        comunicado.GetProperty("severidade").GetString().Should().Be("Critico",
            because: "enum sai como nome — o frontend tipa isso como união de literais");
        comunicado.GetProperty("provedorNome").ValueKind.Should().Be(JsonValueKind.Null);
        comunicado.GetProperty("url").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Home_ComunicadoDeProvedor_TemUrlDaPaginaDoProvedor()
    {
        var comunicado = await LocalizarComunicadoAsync(_cenario.TituloComunicadoDeProvedor);

        comunicado.GetProperty("abrangencia").GetString().Should().Be("Provedor");
        comunicado.GetProperty("provedorNome").GetString().Should().Be(_cenario.ProvedorPublicadoNome);
        comunicado.GetProperty("url").GetString().Should().Be($"/provedores/{_cenario.ProvedorPublicadoSlug}");
    }

    [Fact]
    public async Task Home_ComunicadoDeVersao_TemUrlDaPaginaDoProvedor()
    {
        var comunicado = await LocalizarComunicadoAsync(_cenario.TituloComunicadoDeVersao);

        comunicado.GetProperty("abrangencia").GetString().Should().Be("ProvedorVersao");
        comunicado.GetProperty("url").GetString().Should().Be($"/provedores/{_cenario.ProvedorPublicadoSlug}");
    }

    // ------------------------------------------------------------------ bloco de layout (B-6)

    [Fact]
    public async Task Home_BlocoDeLayout_TrazAVigenteEAsContagens_SemDataDePublicacao()
    {
        var home = await ObterHomeAsync();
        var layout = home.GetProperty("layout");

        layout.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            [
                "vigente", "prevista", "descricao", "camposNovos", "camposDescontinuados",
                "normaOrigem", "homologacaoDesde", "liberadaNoInvoicyEm", "obrigatoriaEm",
            ],
            because: "B-6: `publicadaEm` não existe em versoeslayout; as datas são colunas próprias da vigente, nunca `criadoem`");

        layout.GetProperty("vigente").GetProperty("slug").GetString().Should().Be(_cenario.LayoutVigenteSlug);
        layout.GetProperty("descricao").GetString().Should().Be(_cenario.LayoutVigenteDescricao);
        layout.GetProperty("camposNovos").GetInt32().Should().Be(1, because: "só campoNovo tem a Vigente como versão de origem");
        layout.GetProperty("camposDescontinuados").GetInt32().Should().Be(1, because: "só campoSaindo é descontinuado NA Vigente");

        layout.GetProperty("normaOrigem").GetString().Should().Be("NT 009 v1.01");
        layout.GetProperty("homologacaoDesde").GetString().Should().Be("2026-10-01");
        layout.GetProperty("liberadaNoInvoicyEm").GetString().Should().Be("2026-10-22");
        layout.GetProperty("obrigatoriaEm").ValueKind.Should().Be(JsonValueKind.Null, because: "data ausente sai como null, com a chave presente");
    }

    // ---------------------------------------------------------------------- artigos (P7)

    [Fact]
    public async Task Home_ArtigosRecentes_TrazemOMesmoShapeDeP7_MaisRecentePrimeiro()
    {
        var home = await ObterHomeAsync();

        var artigos = home.GetProperty("artigosRecentes").EnumerateArray().ToList();
        artigos.Should().HaveCount(3, because: "a Home traz os três artigos mais recentes, como o artboard da tela 1 pede");

        foreach (var artigo in artigos)
        {
            artigo.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
                ["slug", "tipo", "titulo", "resumo", "verificadoEm"],
                because: "é o MESMO ArtigoListaItemDto de P7, nunca um gêmeo divergente");
        }

        // Três artigos semeados em sequência: ordem esperada (terceiro, segundo, primeiro) é o
        // inverso da inserção, e o desempate por id sustenta o caso de CriadoEm empatado.
        artigos.Select(a => a.GetProperty("slug").GetString()).Should().Equal(
            _cenario.ArtigoTerceiroSlug, _cenario.ArtigoSegundoSlug, _cenario.ArtigoPrimeiroSlug);

        artigos.Select(a => a.GetProperty("slug").GetString())
            .Should().NotContain(_cenario.ArtigoRascunhoSlug, because: "artigo em rascunho não é conteúdo público");
    }

    // ---------------------------------------------------------- envelope, cache e anonimato

    [Fact]
    public async Task Home_RespondeEnvelopeApiResponse_ComAsQuatroChavesSempre()
    {
        using var resposta = await _cliente.GetAsync(RotaHome);
        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        corpo.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["data", "message"],
            because: "recurso único vai no envelope ApiResponse — listagem crua é só de coleção paginada");

        corpo.GetProperty("data").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["comunicados", "provedoresPorCategoria", "layout", "artigosRecentes"]);
    }

    [Fact]
    public async Task Home_PublicaCacheControl()
    {
        using var resposta = await _cliente.GetAsync(RotaHome);

        resposta.Headers.CacheControl.Should().NotBeNull(
            because: "é a rota mais chamada do portal — sem Cache-Control todo carregamento bate no banco");
        resposta.Headers.CacheControl!.Public.Should().BeTrue();
        resposta.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
        resposta.Headers.CacheControl.ToString().Should().Contain("stale-while-revalidate=300");
    }

    [Fact]
    public async Task Home_RespondeSemAuthorization()
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaHome);
        using var resposta = await _cliente.SendAsync(requisicao);

        requisicao.Headers.Authorization.Should().BeNull();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "o portal público é 100% aberto — a tela inicial nunca exige token");
    }

    // ------------------------------------------------------------------------------ apoio

    private sealed record Cenario
    {
        public required string ProvedorPublicadoSlug { get; init; }
        public required string ProvedorPublicadoNome { get; init; }
        public required string ProvedorSemVersaoSlug { get; init; }
        public required string ProvedorRascunhoSlug { get; init; }
        public required string VersaoASlug { get; init; }
        public required string VersaoBSlug { get; init; }
        public required string VersaoCSlug { get; init; }
        public required string VersaoRascunhoSlug { get; init; }
        public required string LayoutVigenteSlug { get; init; }
        public required string LayoutVigenteDescricao { get; init; }
        public required string TituloComunicadoGlobal { get; init; }
        public required string TituloComunicadoDeProvedor { get; init; }
        public required string TituloComunicadoDeVersao { get; init; }
        public required string TituloComunicadoDeProvedorRascunho { get; init; }
        public required string TituloComunicadoDeVersaoRascunho { get; init; }
        public required string TituloComunicadoRascunho { get; init; }
        public required string TituloComunicadoExpirado { get; init; }
        public required string TituloComunicadoRemovido { get; init; }
        public required string ArtigoPrimeiroSlug { get; init; }
        public required string ArtigoSegundoSlug { get; init; }
        public required string ArtigoTerceiroSlug { get; init; }
        public required string ArtigoRascunhoSlug { get; init; }
    }

    private async Task<Cenario> SemearAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => (int?)v.Ordem) ?? 0;

        var layoutBaixo = VersaoLayout.Criar($"Home L1 {_sufixo}", maiorOrdem + 1);
        var layoutVigente = VersaoLayout.Criar($"Home L2 {_sufixo}", maiorOrdem + 2, $"Notas da vigente {_sufixo}");
        var layoutAlto = VersaoLayout.Criar($"Home L3 {_sufixo}", maiorOrdem + 3);

        layoutBaixo.Publicar();
        layoutVigente.Publicar();
        layoutVigente.MarcarVigente();
        layoutVigente.AtualizarCalendario("NT 009 v1.01", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 22), obrigatoriaEm: null);
        layoutAlto.Publicar();

        contexto.VersoesLayout.AddRange(layoutBaixo, layoutVigente, layoutAlto);
        await contexto.SaveChangesAsync();

        var campoNovo = CampoLayout.Criar(layoutVigente.Id, $"campo novo {_sufixo}", $"//raiz/Novo{_sufixo}", "Nasce na vigente", ordem: 10);
        var campoSaindo = CampoLayout.Criar(layoutBaixo.Id, $"campo saindo {_sufixo}", $"//raiz/Saindo{_sufixo}", "Sai na vigente", ordem: 20);
        campoNovo.Publicar();
        campoSaindo.Publicar();
        contexto.CamposLayout.AddRange(campoNovo, campoSaindo);
        await contexto.SaveChangesAsync();

        var saindoRastreado = await contexto.CamposLayout
            .Include(c => c.VersaoLayoutOrigem)
            .FirstAsync(c => c.Id == campoSaindo.Id);
        saindoRastreado.Descontinuar(layoutVigente);
        await contexto.SaveChangesAsync();

        var provedorPublicado = Provedor.Criar($"Home Provedor {_sufixo}", $"HOM-{_sufixo}", CategoriaProvedor.Nacional);
        var provedorSemVersao = Provedor.Criar($"Home Sem Versao {_sufixo}", $"HSV-{_sufixo}", CategoriaProvedor.Proprio);
        var provedorRascunho = Provedor.Criar($"Home Rascunho {_sufixo}", $"HRA-{_sufixo}", CategoriaProvedor.Nacional);
        provedorPublicado.Publicar();
        provedorSemVersao.Publicar();
        contexto.Provedores.AddRange(provedorPublicado, provedorSemVersao, provedorRascunho);
        await contexto.SaveChangesAsync();

        // Inserção A, B, C de propósito: a ordem esperada por Ordem DESC (B, C, A) não coincide
        // nem com a de inserção nem com o inverso dela.
        var versaoA = VersaoProvedor.Criar(provedorPublicado.Id, layoutBaixo.Id, $"Home VA {_sufixo}");
        var versaoB = VersaoProvedor.Criar(provedorPublicado.Id, layoutAlto.Id, $"Home VB {_sufixo}");
        var versaoC = VersaoProvedor.Criar(provedorPublicado.Id, layoutVigente.Id, $"Home VC {_sufixo}");
        var versaoRascunho = VersaoProvedor.Criar(provedorPublicado.Id, layoutAlto.Id, $"Home VR {_sufixo}");

        versaoA.RegistrarVerificacao(new DateOnly(2025, 6, 1));
        versaoC.RegistrarVerificacao(new DateOnly(2026, 1, 15));
        versaoA.Publicar();
        versaoB.Publicar();
        versaoC.Publicar();

        // Versão publicada de provedor em RASCUNHO: não pode puxar o provedor para a Home.
        var versaoDoRascunho = VersaoProvedor.Criar(provedorRascunho.Id, layoutAlto.Id, $"Home VDR {_sufixo}");
        versaoDoRascunho.Publicar();

        contexto.VersoesProvedor.AddRange(versaoA, versaoB, versaoC, versaoRascunho, versaoDoRascunho);
        await contexto.SaveChangesAsync();

        var agora = DateTimeOffset.UtcNow;

        var comunicadoGlobal = Comunicado.CriarGlobal(
            $"Home global {_sufixo}", "Instabilidade geral.", Severidade.Critico, agora.AddDays(5));
        var comunicadoDeProvedor = Comunicado.CriarDeProvedor(
            provedorPublicado.Id, $"Home provedor {_sufixo}", "Janela do provedor.", Severidade.Alerta, agora.AddDays(3));
        var comunicadoDeVersao = Comunicado.CriarDeVersaoProvedor(
            provedorPublicado.Id, versaoC.Id, $"Home versao {_sufixo}", "Ajuste de layout.", Severidade.Informativo, agora.AddDays(4));
        var comunicadoDeProvedorRascunho = Comunicado.CriarDeProvedor(
            provedorRascunho.Id, $"Home provedor oculto {_sufixo}", "Nao pode vazar.", Severidade.Critico, agora.AddDays(6));
        var comunicadoDeVersaoRascunho = Comunicado.CriarDeVersaoProvedor(
            provedorPublicado.Id, versaoRascunho.Id, $"Home versao oculta {_sufixo}", "Nao pode vazar.", Severidade.Critico, agora.AddDays(7));
        var comunicadoRascunho = Comunicado.CriarGlobal(
            $"Home rascunho {_sufixo}", "Ainda nao publicado.", Severidade.Alerta, agora.AddDays(8));
        var comunicadoExpirado = Comunicado.CriarGlobal(
            $"Home expirado {_sufixo}", "Ja venceu.", Severidade.Alerta, agora.AddDays(-1));
        var comunicadoRemovido = Comunicado.CriarGlobal(
            $"Home removido {_sufixo}", "Soft delete.", Severidade.Alerta, agora.AddDays(9));

        foreach (var comunicado in new[]
                 {
                     comunicadoGlobal, comunicadoDeProvedor, comunicadoDeVersao, comunicadoDeProvedorRascunho,
                     comunicadoDeVersaoRascunho, comunicadoExpirado, comunicadoRemovido,
                 })
        {
            comunicado.Publicar();
        }

        comunicadoRemovido.Desativar();

        contexto.Comunicados.AddRange(
            comunicadoGlobal, comunicadoDeProvedor, comunicadoDeVersao, comunicadoDeProvedorRascunho,
            comunicadoDeVersaoRascunho, comunicadoRascunho, comunicadoExpirado, comunicadoRemovido);
        await contexto.SaveChangesAsync();

        // Artigos em SaveChanges separados: CriadoEm é `now()` da transação, e salvar os três
        // juntos empataria o carimbo — o desempate por id ainda resolveria, mas o caso deixaria
        // de exercitar a ordenação por data.
        var artigoPrimeiro = Artigo.Criar(TipoArtigo.Procedimento, $"Home artigo um {_sufixo}", "Primeiro parágrafo do guia.");
        artigoPrimeiro.Publicar();
        contexto.Artigos.Add(artigoPrimeiro);
        await contexto.SaveChangesAsync();

        var artigoSegundo = Artigo.Criar(TipoArtigo.Conceito, $"Home artigo dois {_sufixo}", "Primeiro parágrafo do conceito.");
        artigoSegundo.Publicar();
        contexto.Artigos.Add(artigoSegundo);
        await contexto.SaveChangesAsync();

        var artigoTerceiro = Artigo.Criar(TipoArtigo.Procedimento, $"Home artigo tres {_sufixo}", "Primeiro parágrafo do terceiro.");
        artigoTerceiro.Publicar();
        var artigoRascunho = Artigo.Criar(TipoArtigo.Procedimento, $"Home artigo rascunho {_sufixo}", "Nunca publicado.");
        contexto.Artigos.AddRange(artigoTerceiro, artigoRascunho);
        await contexto.SaveChangesAsync();

        return new Cenario
        {
            ProvedorPublicadoSlug = provedorPublicado.Slug,
            ProvedorPublicadoNome = provedorPublicado.Nome,
            ProvedorSemVersaoSlug = provedorSemVersao.Slug,
            ProvedorRascunhoSlug = provedorRascunho.Slug,
            VersaoASlug = versaoA.Slug,
            VersaoBSlug = versaoB.Slug,
            VersaoCSlug = versaoC.Slug,
            VersaoRascunhoSlug = versaoRascunho.Slug,
            LayoutVigenteSlug = layoutVigente.Slug,
            LayoutVigenteDescricao = layoutVigente.Descricao!,
            TituloComunicadoGlobal = comunicadoGlobal.Titulo,
            TituloComunicadoDeProvedor = comunicadoDeProvedor.Titulo,
            TituloComunicadoDeVersao = comunicadoDeVersao.Titulo,
            TituloComunicadoDeProvedorRascunho = comunicadoDeProvedorRascunho.Titulo,
            TituloComunicadoDeVersaoRascunho = comunicadoDeVersaoRascunho.Titulo,
            TituloComunicadoRascunho = comunicadoRascunho.Titulo,
            TituloComunicadoExpirado = comunicadoExpirado.Titulo,
            TituloComunicadoRemovido = comunicadoRemovido.Titulo,
            ArtigoPrimeiroSlug = artigoPrimeiro.Slug,
            ArtigoSegundoSlug = artigoSegundo.Slug,
            ArtigoTerceiroSlug = artigoTerceiro.Slug,
            ArtigoRascunhoSlug = artigoRascunho.Slug,
        };
    }

    /// <summary>
    /// Hard delete: o cenário ocupa o slot global de <c>Vigente</c> e soft delete o manteria
    /// ocupado para as demais classes da <c>Collection</c>.
    /// </summary>
    private async Task LimparAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var comunicados = await contexto.Comunicados.Where(c => c.Titulo.Contains(_sufixo)).ToListAsync();
        var artigos = await contexto.Artigos.Where(a => a.Titulo.Contains(_sufixo)).ToListAsync();
        var provedores = await contexto.Provedores.Where(p => p.Nome.Contains(_sufixo)).ToListAsync();
        var idsProvedor = provedores.Select(p => p.Id).ToList();
        var versoesProvedor = await contexto.VersoesProvedor.Where(v => idsProvedor.Contains(v.ProvedorId)).ToListAsync();
        var versoesLayout = await contexto.VersoesLayout.Where(v => v.Rotulo.Contains(_sufixo)).ToListAsync();
        var idsLayout = versoesLayout.Select(v => v.Id).ToList();
        var campos = await contexto.CamposLayout.Where(c => idsLayout.Contains(c.VersaoLayoutId)).ToListAsync();

        contexto.Comunicados.RemoveRange(comunicados);
        contexto.Artigos.RemoveRange(artigos);
        contexto.VersoesProvedor.RemoveRange(versoesProvedor);
        contexto.Provedores.RemoveRange(provedores);
        contexto.CamposLayout.RemoveRange(campos);
        contexto.VersoesLayout.RemoveRange(versoesLayout);

        await contexto.SaveChangesAsync();
    }

    private async Task<JsonElement> ObterHomeAsync()
    {
        using var resposta = await _cliente.GetAsync(RotaHome);
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: $"GET {RotaHome} deveria responder 200");

        return JsonDocument.Parse(texto).RootElement.GetProperty("data").Clone();
    }

    private async Task<JsonElement> LocalizarProvedorAsync(string slug)
    {
        var home = await ObterHomeAsync();

        var provedor = TodosOsProvedores(home).FirstOrDefault(p => p.GetProperty("slug").GetString() == slug);

        provedor.ValueKind.Should().Be(JsonValueKind.Object, because: $"o provedor '{slug}' deveria estar na Home");
        return provedor;
    }

    private async Task<JsonElement> LocalizarComunicadoAsync(string titulo)
    {
        var home = await ObterHomeAsync();

        var comunicado = home.GetProperty("comunicados").EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("titulo").GetString() == titulo);

        comunicado.ValueKind.Should().Be(JsonValueKind.Object, because: $"o comunicado '{titulo}' deveria estar na Home");
        return comunicado;
    }

    private static IEnumerable<JsonElement> TodosOsProvedores(JsonElement home) =>
        home.GetProperty("provedoresPorCategoria").EnumerateArray()
            .SelectMany(categoria => categoria.GetProperty("provedores").EnumerateArray());

    private static List<string?> TitulosDeComunicado(JsonElement home) =>
        home.GetProperty("comunicados").EnumerateArray()
            .Select(c => c.GetProperty("titulo").GetString())
            .ToList();

    private static string? CategoriaDoProvedor(JsonElement home, string slug) =>
        home.GetProperty("provedoresPorCategoria").EnumerateArray()
            .Single(categoria => categoria.GetProperty("provedores").EnumerateArray()
                .Any(p => p.GetProperty("slug").GetString() == slug))
            .GetProperty("categoria").GetString();
}
