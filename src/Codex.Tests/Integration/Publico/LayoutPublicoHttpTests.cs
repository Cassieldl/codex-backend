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
/// P10/P11/P12 (analise-backend.md §4) medidos pelo JSON literal da resposta HTTP, contra um
/// Postgres real — objeto C# correto não é contrato HTTP correto, e o predicado A1 (§4.1) e a
/// fórmula M12 (§4.2) vivem parte em SQL (o JOIN da versão de origem) e parte em memória.
///
/// <para>
/// O cenário é desenhado para que cada limite de M12 tenha um campo dedicado, e para que a
/// remoção de qualquer metade do predicado A1 deixe um campo visível que não deveria estar lá:
/// </para>
///
/// <list type="bullet">
/// <item><b>Voculta</b> (menor ordem, NÃO publicada) hospeda <b>campoOculto</b>, que está
/// publicado. Só a metade "origem publicada" de A1 o mantém invisível — e a ordem dele é a menor
/// de todas, então sem A1 ele apareceria nas TRÊS versões.</item>
/// <item><b>campoSempre</b> nasce em V1 e nunca é descontinuado.</item>
/// <item><b>campoNovoEmV2</b> nasce em V2 (limites "origem == Y" e "origem posterior a Y").</item>
/// <item><b>campoDescontinuadoEmV2</b> nasce em V1 e é descontinuado em V2 (limites
/// "descontinuado exatamente em Y" e "descontinuado na versão anterior a Y").</item>
/// <item><b>campoFuturoEmV3</b> nasce em V3, depois da Vigente — é o ramo 2 de A3 em P12
/// (<c>versaoExibicao</c> cai na versão de origem porque o campo não está ativo na Vigente).</item>
/// </list>
///
/// <para>
/// O cenário ocupa o slot GLOBAL de <c>Estado = Vigente</c> (índice único parcial
/// <c>uq_codex_versoeslayout_estado_vigente</c>) e por isso limpa tudo que semeia no
/// <see cref="DisposeAsync"/> — hard delete, porque soft delete deixaria o slot ocupado para as
/// outras classes da mesma <c>Collection</c>.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class LayoutPublicoHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private Cenario _cenario = null!;

    public LayoutPublicoHttpTests(PostgresContainerFixture fixture)
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

    // ------------------------------------------------------------------------------------ P10

    [Fact]
    public async Task P10_ListaAsVersoesPublicadas_EmOrdemDecrescenteDeOrdem_ComAVigenteDestacada()
    {
        var indice = await ObterDadosAsync("/api/v1/layout");

        var slugs = indice.GetProperty("versoes").EnumerateArray()
            .Select(v => v.GetProperty("slug").GetString()!)
            .ToList();

        // Três elementos, semeados fora de ordem (V2, V1, V3): a ordem esperada não coincide nem
        // com a de inserção nem com o inverso dela.
        var posicoes = new[] { _cenario.V3Slug, _cenario.V2Slug, _cenario.V1Slug }
            .Select(slug => slugs.IndexOf(slug))
            .ToList();

        posicoes.Should().NotContain(-1, because: "as três versões publicadas do cenário precisam estar no índice");
        posicoes.Should().BeInAscendingOrder(because: "P10 ordena por Ordem DESC — V3, depois V2, depois V1");

        slugs.Should().NotContain(_cenario.VocultaSlug, because: "versão não publicada não entra no índice público");

        indice.GetProperty("vigente").GetProperty("slug").GetString().Should().Be(_cenario.V2Slug);
        indice.GetProperty("vigente").GetProperty("estado").GetString().Should().Be("Vigente");
    }

    [Theory]
    // V1: campoSempre + campoDescontinuadoEmV2.
    // V2: campoSempre + campoDescontinuadoEmV2 (ainda ativo na versão que o descontinua) + campoNovoEmV2.
    // V3: campoSempre + campoNovoEmV2 + campoFuturoEmV3 (campoDescontinuadoEmV2 já saiu).
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    public async Task P10_TotalCamposDoResumo_ESempreOTotalDeP11DaMesmaVersao(int numeroDaVersao, int camposDoCenarioEsperados)
    {
        var versaoSlug = numeroDaVersao switch
        {
            1 => _cenario.V1Slug,
            2 => _cenario.V2Slug,
            _ => _cenario.V3Slug,
        };

        var indice = await ObterDadosAsync("/api/v1/layout");
        var versao = await ObterDadosAsync($"/api/v1/layout/versoes/{versaoSlug}");

        TotalCamposDe(indice, versaoSlug).Should().Be(versao.GetProperty("totalCampos").GetInt32(),
            because: "o resumo do índice e o dicionário da versão contam o MESMO conjunto (M12 sobre A1)");

        // Contagem restrita ao cenário: o banco é compartilhado pela Collection, e o total global
        // depende do que outras classes publicaram.
        TodosOsSlugs(versao).Count(slug => _cenario.SlugsDeCampo.Contains(slug))
            .Should().Be(camposDoCenarioEsperados);
    }

    // ------------------------------------------------------------------------------------ P11

    [Fact]
    public async Task P11_MontaODicionarioDaVersao_ComAnteriorEAsFlagsDeFronteiraDeM12()
    {
        var versao = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V2Slug}");

        versao.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["versao", "descricao", "anterior", "totalCampos", "camposPorGrupo"]);

        versao.GetProperty("versao").GetProperty("slug").GetString().Should().Be(_cenario.V2Slug);
        versao.GetProperty("anterior").GetProperty("slug").GetString().Should().Be(_cenario.V1Slug);

        // M12, limite "origem == Y": aparece e é o novo da versão.
        var novo = LocalizarCampo(versao, _cenario.CampoNovoEmV2Slug);
        novo.GetProperty("novoNestaVersao").GetBoolean().Should().BeTrue();
        novo.GetProperty("descontinuadoNestaVersao").GetBoolean().Should().BeFalse();
        novo.GetProperty("urlCanonica").GetString().Should().Be($"/layout/campos/{_cenario.CampoNovoEmV2Slug}");

        // M12, limite "descontinuado exatamente em Y": ainda aparece, sinalizado.
        var descontinuado = LocalizarCampo(versao, _cenario.CampoDescontinuadoEmV2Slug);
        descontinuado.GetProperty("descontinuadoNestaVersao").GetBoolean().Should().BeTrue();
        descontinuado.GetProperty("novoNestaVersao").GetBoolean().Should().BeFalse();

        // Campo herdado de uma versão anterior: aparece sem nenhuma das duas flags.
        var sempre = LocalizarCampo(versao, _cenario.CampoSempreSlug);
        sempre.GetProperty("novoNestaVersao").GetBoolean().Should().BeFalse();
        sempre.GetProperty("descontinuadoNestaVersao").GetBoolean().Should().BeFalse();

        // R3 — vocabulário canônico puro: comportamento (tipo/tamanho/máscara/obrigatoriedade)
        // é de CampoVersao e não pode vazar para o dicionário do layout.
        sempre.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["slug", "nome", "caminhoXml", "significado", "ordem", "novoNestaVersao", "descontinuadoNestaVersao", "urlCanonica"]);

        TodosOsSlugs(versao).Should().NotContain(_cenario.CampoFuturoEmV3Slug,
            because: "M12: campo cuja origem é POSTERIOR a Y não aparece em Y");
    }

    [Fact]
    public async Task P11_CampoDescontinuadoNaVersaoAnterior_NaoApareceNaVersaoSeguinte()
    {
        var v3 = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V3Slug}");
        var slugs = TodosOsSlugs(v3);

        // M12, limite "descontinuado na versão ANTERIOR a Y".
        slugs.Should().NotContain(_cenario.CampoDescontinuadoEmV2Slug);
        slugs.Should().Contain(_cenario.CampoSempreSlug);
        slugs.Should().Contain(_cenario.CampoNovoEmV2Slug);
        slugs.Should().Contain(_cenario.CampoFuturoEmV3Slug);
    }

    [Fact]
    public async Task P11_CampoComOrigemPosteriorAVersao_NaoApareceNaVersaoAnterior()
    {
        var v1 = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V1Slug}");
        var slugs = TodosOsSlugs(v1);

        // M12, limite "origem POSTERIOR a Y".
        slugs.Should().NotContain(_cenario.CampoNovoEmV2Slug);
        slugs.Should().NotContain(_cenario.CampoFuturoEmV3Slug);
        slugs.Should().Contain(_cenario.CampoSempreSlug);
        slugs.Should().Contain(_cenario.CampoDescontinuadoEmV2Slug);
    }

    [Fact]
    public async Task P11_TotalCampos_BateComOSomatorioDosGrupos()
    {
        var versao = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V2Slug}");

        var somaDosGrupos = versao.GetProperty("camposPorGrupo").EnumerateArray()
            .Sum(grupo => grupo.GetProperty("campos").GetArrayLength());

        versao.GetProperty("totalCampos").GetInt32().Should().Be(somaDosGrupos);
    }

    [Fact]
    public async Task P11_VersaoInexistente_Responde404()
    {
        var (status, corpo) = await ObterAsync($"/api/v1/layout/versoes/nao-existe-{_sufixo}");

        status.Should().Be(HttpStatusCode.NotFound);
        Codigo(corpo).Should().Be("NAO_ENCONTRADO");
    }

    [Fact]
    public async Task P11_VersaoExistenteNaoPublicada_Responde410ComCodigoEspecifico()
    {
        var (status, corpo) = await ObterAsync($"/api/v1/layout/versoes/{_cenario.VocultaSlug}");

        status.Should().Be(HttpStatusCode.Gone);
        Codigo(corpo).Should().Be("VERSAO_LAYOUT_DESPUBLICADA",
            because: "o código genérico CONTEUDO_DESPUBLICADO não permite ao cliente distinguir o caso (§0.7)");
    }

    /// <summary>
    /// Soft delete não é despublicação: a linha existe, mas para o público a versão nunca existiu.
    /// Devolver <c>410</c> aqui confirmaria a um anônimo que aquele slug já foi usado.
    /// </summary>
    [Fact]
    public async Task P11_VersaoSoftDeletada_Responde404()
    {
        var (status, corpo) = await ObterAsync($"/api/v1/layout/versoes/{_cenario.VdesativadaSlug}");

        status.Should().Be(HttpStatusCode.NotFound);
        Codigo(corpo).Should().Be("NAO_ENCONTRADO");
    }

    // ------------------------------------------------------------------------------------ P12

    [Fact]
    public async Task P12_CampoAtivoNaVigente_ResolveVersaoExibicaoParaAVigente_EListaOndeEUsado()
    {
        var verbete = await ObterDadosAsync($"/api/v1/layout/campos/{_cenario.CampoSempreSlug}");

        verbete.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["slug", "nome", "caminhoXml", "significado", "estado", "versaoExibicao", "versaoOrigem", "versaoDescontinuado", "ondeEUsado"]);

        verbete.GetProperty("estado").GetString().Should().Be("Ativo");
        verbete.GetProperty("versaoExibicao").GetProperty("slug").GetString().Should().Be(_cenario.V2Slug);
        verbete.GetProperty("versaoOrigem").GetProperty("slug").GetString().Should().Be(_cenario.V1Slug);
        verbete.GetProperty("versaoDescontinuado").ValueKind.Should().Be(JsonValueKind.Null);

        var uso = verbete.GetProperty("ondeEUsado").EnumerateArray().Single();
        uso.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["provedorSlug", "provedorNome", "versaoSlug", "versaoRotulo"]);
        uso.GetProperty("provedorSlug").GetString().Should().Be(_cenario.ProvedorSlug);
        uso.GetProperty("versaoSlug").GetString().Should().Be(_cenario.VersaoProvedorSlug);
    }

    [Fact]
    public async Task P12_CampoNaoAtivoNaVigente_ResolveVersaoExibicaoParaAOrigemPublicada()
    {
        // campoFuturoEmV3 nasce DEPOIS da Vigente: o ramo 1 de A3 não se aplica e a resolução cai
        // na versão de origem.
        var verbete = await ObterDadosAsync($"/api/v1/layout/campos/{_cenario.CampoFuturoEmV3Slug}");

        verbete.GetProperty("versaoExibicao").GetProperty("slug").GetString().Should().Be(_cenario.V3Slug);
        verbete.GetProperty("versaoOrigem").GetProperty("slug").GetString().Should().Be(_cenario.V3Slug);
        verbete.GetProperty("ondeEUsado").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task P12_CampoDescontinuado_TrazAVersaoDeDescontinuacao()
    {
        var verbete = await ObterDadosAsync($"/api/v1/layout/campos/{_cenario.CampoDescontinuadoEmV2Slug}");

        verbete.GetProperty("estado").GetString().Should().Be("Descontinuado");
        verbete.GetProperty("versaoOrigem").GetProperty("slug").GetString().Should().Be(_cenario.V1Slug);
        verbete.GetProperty("versaoDescontinuado").GetProperty("slug").GetString().Should().Be(_cenario.V2Slug);
    }

    [Fact]
    public async Task P12_CampoInexistente_Responde404()
    {
        var (status, corpo) = await ObterAsync($"/api/v1/layout/campos/nao-existe-{_sufixo}");

        status.Should().Be(HttpStatusCode.NotFound);
        Codigo(corpo).Should().Be("NAO_ENCONTRADO");
    }

    /// <summary>
    /// O 410 tinha detector só para o campo invisível por herança da origem (A1). Campo
    /// despublicado por si, dentro de versão publicada, é a outra metade do mesmo predicado — e
    /// nenhum teste a exercitava.
    /// </summary>
    [Fact]
    public async Task P12_CampoDespublicadoPorSi_Responde410()
    {
        var (status, corpo) = await ObterAsync($"/api/v1/layout/campos/{_cenario.CampoDespublicadoSlug}");

        status.Should().Be(HttpStatusCode.Gone);
        Codigo(corpo).Should().Be("CAMPO_LAYOUT_DESPUBLICADO");
    }

    [Fact]
    public async Task P11_CampoDespublicadoPorSi_NaoApareceNoDicionarioDaVersaoPublicada()
    {
        var versao = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V1Slug}");

        TodosOsSlugs(versao).Should().NotContain(_cenario.CampoDespublicadoSlug);
    }

    // --------------------------------------------------------------------- predicado A1 (§4.1)

    [Fact]
    public async Task A1_CampoPublicadoComOrigemDespublicada_EInvisivelNosTresEndpoints()
    {
        // P12: existe, mas não é visível — 410, nunca 404 nem 200.
        var (status, corpo) = await ObterAsync($"/api/v1/layout/campos/{_cenario.CampoOcultoSlug}");
        status.Should().Be(HttpStatusCode.Gone);
        Codigo(corpo).Should().Be("CAMPO_LAYOUT_DESPUBLICADO");

        // P11: a origem dele tem a MENOR ordem do cenário — sem A1 ele apareceria nas três.
        foreach (var versaoSlug in new[] { _cenario.V1Slug, _cenario.V2Slug, _cenario.V3Slug })
        {
            var versao = await ObterDadosAsync($"/api/v1/layout/versoes/{versaoSlug}");
            TodosOsSlugs(versao).Should().NotContain(_cenario.CampoOcultoSlug);
        }

        // P10: não pode ser contado em totalCampos de nenhuma versão. Como a contagem é global,
        // a prova é a igualdade com o somatório de P11, que já não o contém.
        var indice = await ObterDadosAsync("/api/v1/layout");
        var v2 = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V2Slug}");
        TotalCamposDe(indice, _cenario.V2Slug).Should().Be(v2.GetProperty("totalCampos").GetInt32());
    }

    // ------------------------------------------------------------------------------- anonimato

    [Theory]
    [InlineData("/api/v1/layout")]
    [InlineData("/api/v1/layout/versoes/{versao}")]
    [InlineData("/api/v1/layout/campos/{campo}")]
    public async Task RotasDeLayout_RespondemSemAuthorization(string molde)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaDe(molde));
        using var resposta = await _cliente.SendAsync(requisicao);

        requisicao.Headers.Authorization.Should().BeNull();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "o portal público é 100% aberto — leitura de layout nunca exige token");
    }

    // ----------------------------------------------------------------------------------- cache

    /// <summary>
    /// As três rotas são anônimas e leem do banco a cada chamada. A Home, agregador anônimo irmão,
    /// publica <c>Cache-Control</c>; o Layout ficou sem — e é quem serve a tabela de campos
    /// inteira. A asserção cobre as três de uma vez porque o esquecimento foi por action.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/layout")]
    [InlineData("/api/v1/layout/versoes/{versao}")]
    [InlineData("/api/v1/layout/campos/{campo}")]
    public async Task RotasDeLayout_PublicamCacheControl(string molde)
    {
        using var resposta = await _cliente.GetAsync(RotaDe(molde));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Headers.CacheControl.Should().NotBeNull(
            because: "rota anônima sem cache deixa a taxa de requisições virar carga direta no banco");
        resposta.Headers.CacheControl!.Public.Should().BeTrue();
        resposta.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
        resposta.Headers.CacheControl.ToString().Should().Contain("stale-while-revalidate=300");
    }

    /// <summary>
    /// Despublicar uma versão precisa propagar: o <c>410</c> não pode ser cacheado junto com o
    /// caminho feliz. O header é escrito depois do serviço, então a exceção sobe antes dele.
    /// </summary>
    [Fact]
    public async Task VersaoDespublicada_NaoPublicaCacheControl()
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/layout/versoes/{_cenario.VocultaSlug}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Gone);
        resposta.Headers.CacheControl.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------- apoio

    private string RotaDe(string molde) => molde
        .Replace("{versao}", _cenario.V2Slug, StringComparison.Ordinal)
        .Replace("{campo}", _cenario.CampoSempreSlug, StringComparison.Ordinal);

    private sealed record Cenario
    {
        public required string VocultaSlug { get; init; }
        public required string VdesativadaSlug { get; init; }
        public required string CampoDespublicadoSlug { get; init; }
        public required string V1Slug { get; init; }
        public required string V2Slug { get; init; }
        public required string V3Slug { get; init; }
        public required string CampoOcultoSlug { get; init; }
        public required string CampoSempreSlug { get; init; }
        public required string CampoNovoEmV2Slug { get; init; }
        public required string CampoDescontinuadoEmV2Slug { get; init; }
        public required string CampoFuturoEmV3Slug { get; init; }
        public required string ProvedorSlug { get; init; }
        public required string VersaoProvedorSlug { get; init; }

        /// <summary>Todos os campos semeados, inclusive os invisíveis — a contagem restrita ao cenário precisa notar se um deles vazar.</summary>
        public IReadOnlyCollection<string> SlugsDeCampo =>
        [
            CampoOcultoSlug, CampoDespublicadoSlug, CampoSempreSlug, CampoNovoEmV2Slug,
            CampoDescontinuadoEmV2Slug, CampoFuturoEmV3Slug,
        ];
    }

    private async Task<Cenario> SemearAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => (int?)v.Ordem) ?? 0;

        var voculta = VersaoLayout.Criar($"Layout Oculto {_sufixo}", maiorOrdem + 1);
        var v1 = VersaoLayout.Criar($"Layout Um {_sufixo}", maiorOrdem + 2);
        var v2 = VersaoLayout.Criar($"Layout Dois {_sufixo}", maiorOrdem + 3);
        var v3 = VersaoLayout.Criar($"Layout Tres {_sufixo}", maiorOrdem + 4);

        // Publicada e depois soft-deletada: o caso que distingue "nunca existiu" (404) de
        // "existe e está despublicada" (410) — sem ele, remover o filtro de DesativadoEm da
        // consulta por slug passaria em silêncio.
        var vdesativada = VersaoLayout.Criar($"Layout Removido {_sufixo}", maiorOrdem + 5);

        v1.Publicar();
        v2.Publicar();
        v2.MarcarVigente();
        v3.Publicar();
        vdesativada.Publicar();
        vdesativada.Desativar();

        // Inserção fora de ordem de propósito: ordenação por Ordem DESC (V3, V2, V1) não pode
        // coincidir nem com a ordem de inserção nem com o inverso dela.
        contexto.VersoesLayout.AddRange(v2, v1, v3, voculta, vdesativada);
        await contexto.SaveChangesAsync();

        var campoOculto = CampoLayout.Criar(voculta.Id, $"campo oculto {_sufixo}", $"//raiz/Oculto{_sufixo}", "Campo de versão não publicada", ordem: 10);
        var campoSempre = CampoLayout.Criar(v1.Id, $"campo sempre {_sufixo}", $"//raiz/Sempre{_sufixo}", "Campo presente em todas as versões", ordem: 20, grupo: "Identificação");
        var campoDescontinuado = CampoLayout.Criar(v1.Id, $"campo saindo {_sufixo}", $"//raiz/Saindo{_sufixo}", "Campo que sai em V2", ordem: 30, grupo: "Identificação");
        var campoNovo = CampoLayout.Criar(v2.Id, $"campo novo {_sufixo}", $"//raiz/Novo{_sufixo}", "Campo que nasce em V2", ordem: 40);
        var campoFuturo = CampoLayout.Criar(v3.Id, $"campo futuro {_sufixo}", $"//raiz/Futuro{_sufixo}", "Campo que nasce depois da vigente", ordem: 50);

        // Campo despublicado por si, dentro de versão publicada — distinto do campoOculto, que é
        // invisível por herança da origem. Cobre o 410 do próprio campo em P12.
        var campoDespublicado = CampoLayout.Criar(v1.Id, $"campo rascunho {_sufixo}", $"//raiz/Rascunho{_sufixo}", "Campo ainda não publicado", ordem: 60);

        foreach (var campo in new[] { campoOculto, campoSempre, campoDescontinuado, campoNovo, campoFuturo })
            campo.Publicar();

        contexto.CamposLayout.AddRange(campoOculto, campoSempre, campoDescontinuado, campoNovo, campoFuturo, campoDespublicado);
        await contexto.SaveChangesAsync();

        // Descontinuar exige a navegação de origem carregada (invariante de M12 na entidade).
        var descontinuadoRastreado = await contexto.CamposLayout
            .Include(c => c.VersaoLayoutOrigem)
            .FirstAsync(c => c.Id == campoDescontinuado.Id);
        descontinuadoRastreado.Descontinuar(v2);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor Layout {_sufixo}", $"LAY-{_sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versaoProvedor = VersaoProvedor.Criar(provedor.Id, v2.Id, $"Versao Layout {_sufixo}");
        versaoProvedor.Publicar();
        contexto.VersoesProvedor.Add(versaoProvedor);
        await contexto.SaveChangesAsync();

        contexto.CamposVersao.Add(
            CampoVersao.Criar(versaoProvedor.Id, campoSempre.Id, "C", EstadoPreenchimento.Obrigatorio, ordem: 1));
        await contexto.SaveChangesAsync();

        return new Cenario
        {
            VocultaSlug = voculta.Slug,
            VdesativadaSlug = vdesativada.Slug,
            CampoDespublicadoSlug = campoDespublicado.Slug,
            V1Slug = v1.Slug,
            V2Slug = v2.Slug,
            V3Slug = v3.Slug,
            CampoOcultoSlug = campoOculto.Slug,
            CampoSempreSlug = campoSempre.Slug,
            CampoNovoEmV2Slug = campoNovo.Slug,
            CampoDescontinuadoEmV2Slug = campoDescontinuado.Slug,
            CampoFuturoEmV3Slug = campoFuturo.Slug,
            ProvedorSlug = provedor.Slug,
            VersaoProvedorSlug = versaoProvedor.Slug,
        };
    }

    /// <summary>
    /// Hard delete: o cenário ocupa o slot global de <c>Vigente</c> e soft delete o manteria
    /// ocupado para as demais classes da <c>Collection</c>.
    /// </summary>
    private async Task LimparAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var versoesLayout = await contexto.VersoesLayout.Where(v => v.Rotulo.Contains(_sufixo)).ToListAsync();
        var idsLayout = versoesLayout.Select(v => v.Id).ToList();

        var campos = await contexto.CamposLayout.Where(c => idsLayout.Contains(c.VersaoLayoutId)).ToListAsync();
        var idsCampo = campos.Select(c => c.Id).ToList();

        var camposVersao = await contexto.CamposVersao.Where(cv => idsCampo.Contains(cv.CampoLayoutId)).ToListAsync();
        var versoesProvedor = await contexto.VersoesProvedor.Where(v => idsLayout.Contains(v.VersaoLayoutId)).ToListAsync();
        var idsProvedor = versoesProvedor.Select(v => v.ProvedorId).Distinct().ToList();
        var provedores = await contexto.Provedores.Where(p => idsProvedor.Contains(p.Id)).ToListAsync();

        contexto.CamposVersao.RemoveRange(camposVersao);
        contexto.VersoesProvedor.RemoveRange(versoesProvedor);
        contexto.Provedores.RemoveRange(provedores);
        contexto.CamposLayout.RemoveRange(campos);
        contexto.VersoesLayout.RemoveRange(versoesLayout);

        await contexto.SaveChangesAsync();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ObterAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        var texto = await resposta.Content.ReadAsStringAsync();

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone());
    }

    private async Task<JsonElement> ObterDadosAsync(string rota)
    {
        var (status, corpo) = await ObterAsync(rota);

        status.Should().Be(HttpStatusCode.OK, because: $"{rota} deveria responder 200");
        return corpo.GetProperty("data").Clone();
    }

    private static string? Codigo(JsonElement corpo) =>
        corpo.GetProperty("error").GetProperty("code").GetString();

    private static int TotalCamposDe(JsonElement indice, string versaoSlug) =>
        indice.GetProperty("versoes").EnumerateArray()
            .Single(v => v.GetProperty("slug").GetString() == versaoSlug)
            .GetProperty("totalCampos").GetInt32();

    private static List<string> TodosOsSlugs(JsonElement versao) =>
        versao.GetProperty("camposPorGrupo").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("campos").EnumerateArray())
            .Select(campo => campo.GetProperty("slug").GetString()!)
            .ToList();

    private static JsonElement LocalizarCampo(JsonElement versao, string slug)
    {
        var campo = versao.GetProperty("camposPorGrupo").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("campos").EnumerateArray())
            .FirstOrDefault(c => c.GetProperty("slug").GetString() == slug);

        campo.ValueKind.Should().Be(JsonValueKind.Object, because: $"o campo '{slug}' deveria estar visível nesta versão");
        return campo;
    }
}
