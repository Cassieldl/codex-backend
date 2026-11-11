using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// Bloco aditivo de P6 (analise-backend.md §4.4): <c>artigosRelacionados</c> no detalhe público do
/// erro, exercitado por **HTTP real** contra Postgres real.
///
/// <para>
/// Por que existe: o bloco foi implementado em produção sem nenhum teste — nem unitário. Remover
/// o filtro de publicação, o filtro de soft delete ou a ordenação por título não derrubava nada.
/// </para>
///
/// <para>Vocabulário isolado por sufixo aleatório: o corpus é compartilhado entre classes de
/// teste, e termo repetido faz um teste casar o dado de outro.</para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ContratoErroArtigosRelacionadosHttpTests : IAsyncLifetime
{
    /// <summary>A união de tipos que o cliente declara — ASCII, decisão M-5.</summary>
    private static readonly string[] TiposDeContrato = ["Conceito", "Procedimento", "Mudanca", "Configuracao"];

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ContratoErroArtigosRelacionadosHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ErroSemNenhumArtigo_TrazOCampoComoListaVazia_NuncaOmitidoNemNulo()
    {
        var cenario = await SemearAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/erros/{cenario.ErroSemArtigosSlug}");

        status.Should().Be(HttpStatusCode.OK);
        var detalhe = corpo.GetProperty("data");
        detalhe.TryGetProperty("artigosRelacionados", out var artigos).Should().BeTrue(
            because: "campo sempre presente é o que permite ao cliente fazer .map() sem guarda de nulo (U9)");
        artigos.ValueKind.Should().Be(JsonValueKind.Array);
        artigos.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task SoTrazArtigoPublicado_ENuncaORascunho()
    {
        var cenario = await SemearAsync();

        var slugs = await ObterSlugsRelacionadosAsync(cenario.ErroComArtigosSlug);

        slugs.Should().Contain(cenario.ArtigoAlfaSlug);
        slugs.Should().NotContain(cenario.ArtigoRascunhoSlug,
            because: "superfície pública nunca revela conteúdo não publicado (B-3)");
    }

    [Fact]
    public async Task NaoTrazArtigoDesativado_AindaQuePublicado()
    {
        var cenario = await SemearAsync();

        var slugs = await ObterSlugsRelacionadosAsync(cenario.ErroComArtigosSlug);

        slugs.Should().NotContain(cenario.ArtigoDesativadoSlug,
            because: "soft delete some da superfície pública mesmo com publicado=true — os dois filtros são independentes");
    }

    [Fact]
    public async Task VemOrdenadoPorTitulo()
    {
        var cenario = await SemearAsync();

        var titulos = (await ObterRelacionadosAsync(cenario.ErroComArtigosSlug))
            .Select(artigo => artigo.GetProperty("titulo").GetString())
            .ToList();

        titulos.Should().Equal([cenario.ArtigoAlfaTitulo, cenario.ArtigoMeioTitulo, cenario.ArtigoZebraTitulo],
            because: "a tela 6 lista os artigos sem controle de ordenação própria — a ordem vem do servidor");
    }

    [Fact]
    public async Task ItemRelacionado_TemExatamenteAsPropriedadesDoContrato()
    {
        var cenario = await SemearAsync();

        var artigo = await PrimeiroRelacionadoAsync(cenario.ErroComArtigosSlug, cenario.ArtigoAlfaSlug);

        NomesDe(artigo).Should().BeEquivalentTo(["rotulo", "slug", "tipo", "titulo", "url"]);
    }

    [Fact]
    public async Task Rotulo_ChegaNoWireComOTextoDaReferenciaCruzada()
    {
        var cenario = await SemearAsync();

        var artigo = await PrimeiroRelacionadoAsync(cenario.ErroComArtigosSlug, cenario.ArtigoAlfaSlug);

        artigo.GetProperty("rotulo").GetString().Should().Be("ARTIGO",
            because: "é propriedade calculada sem setter — se o serializador a ignorasse, o cliente perderia o rótulo do bloco e ninguém veria erro");
    }

    [Fact]
    public async Task Tipo_ESerializadoComoStringAsciiDaUniaoDoCliente()
    {
        var cenario = await SemearAsync();

        var tipo = (await PrimeiroRelacionadoAsync(cenario.ErroComArtigosSlug, cenario.ArtigoAlfaSlug))
            .GetProperty("tipo");

        tipo.ValueKind.Should().Be(JsonValueKind.String,
            because: "enum serializado como número quebra a união de literais do TypeScript");
        tipo.GetString().Should().BeOneOf(TiposDeContrato);
    }

    [Fact]
    public async Task Url_EARotaCanonicaDoArtigo_ProntaParaNavegacao()
    {
        var cenario = await SemearAsync();

        var artigo = await PrimeiroRelacionadoAsync(cenario.ErroComArtigosSlug, cenario.ArtigoAlfaSlug);

        artigo.GetProperty("url").GetString().Should().Be($"/artigos/{cenario.ArtigoAlfaSlug}",
            because: "o cliente navega com o valor cru — montar a rota no front duplicaria a regra de canônica");
    }

    [Fact]
    public async Task BlocoEAditivo_NenhumCampoDoDetalheDeErroFoiPerdido()
    {
        var cenario = await SemearAsync();

        var (_, corpo) = await ObterAsync($"/api/v1/erros/{cenario.ErroComArtigosSlug}");

        NomesDe(corpo.GetProperty("data")).Should().BeEquivalentTo([
            "slug", "codigo", "origem", "titulo", "causaProvavel", "solucaoSugerida",
            "variantes", "versoesAplicaveis", "camposRelacionados", "artigosRelacionados",
        ], because: "P6 acrescenta um bloco ao detalhe de erro da 001 — nada do contrato anterior pode sumir junto");
    }

    // ------------------------------------------------------------------ apoio

    private static IEnumerable<string> NomesDe(JsonElement objeto) =>
        objeto.EnumerateObject().Select(propriedade => propriedade.Name);

    private async Task<IReadOnlyList<JsonElement>> ObterRelacionadosAsync(string erroSlug)
    {
        var (status, corpo) = await ObterAsync($"/api/v1/erros/{erroSlug}");
        status.Should().Be(HttpStatusCode.OK);

        return corpo.GetProperty("data").GetProperty("artigosRelacionados").EnumerateArray().ToList();
    }

    private async Task<IReadOnlyList<string?>> ObterSlugsRelacionadosAsync(string erroSlug) =>
        (await ObterRelacionadosAsync(erroSlug))
            .Select(artigo => artigo.GetProperty("slug").GetString())
            .ToList();

    private async Task<JsonElement> PrimeiroRelacionadoAsync(string erroSlug, string artigoSlug)
    {
        var artigo = (await ObterRelacionadosAsync(erroSlug))
            .FirstOrDefault(item => item.GetProperty("slug").GetString() == artigoSlug);

        artigo.ValueKind.Should().Be(JsonValueKind.Object, because: $"o cenário relaciona o artigo '{artigoSlug}' ao erro");
        return artigo;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ObterAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
        return (resposta.StatusCode, corpo);
    }

    private sealed record Cenario(
        string ErroComArtigosSlug,
        string ErroSemArtigosSlug,
        string ArtigoAlfaSlug,
        string ArtigoAlfaTitulo,
        string ArtigoMeioTitulo,
        string ArtigoZebraSlug,
        string ArtigoZebraTitulo,
        string ArtigoRascunhoSlug,
        string ArtigoDesativadoSlug);

    private async Task<Cenario> SemearAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        await using var contexto = _fixture.CriarContexto();

        var erroComArtigos = Erro.Criar($"REL-{sufixo}", OrigemErro.InvoiCy, $"Erro com artigos {sufixo}");
        erroComArtigos.AtualizarConteudo($"Erro com artigos {sufixo}", $"Causa {sufixo}.", $"Solucao {sufixo}.");
        erroComArtigos.SubstituirVariantes([$"variante rel {sufixo}"]);
        erroComArtigos.Publicar();

        var erroSemArtigos = Erro.Criar($"SEM-{sufixo}", OrigemErro.InvoiCy, $"Erro sem artigos {sufixo}");
        erroSemArtigos.SubstituirVariantes([$"variante sem {sufixo}"]);
        erroSemArtigos.Publicar();

        contexto.Erros.AddRange(erroComArtigos, erroSemArtigos);
        await contexto.SaveChangesAsync();

        // Três publicados, inseridos em Meio → Zebra → Alfa: a ordem alfabética esperada
        // (Alfa, Meio, Zebra) não coincide nem com a ordem de id nem com o inverso dela. Com dois
        // artigos só, `orderby a.Id descending` no repositório reproduzia o resultado certo e o
        // teste passava sem detectar a mutação — medido.
        var meio = Artigo.Criar(TipoArtigo.Conceito, $"Meio relacionada {sufixo}", $"Corpo meio {sufixo}.");
        meio.Publicar();

        var zebra = Artigo.Criar(TipoArtigo.Conceito, $"Zebra relacionada {sufixo}", $"Corpo zebra {sufixo}.");
        zebra.Publicar();

        var alfa = Artigo.Criar(TipoArtigo.Procedimento, $"Alfa relacionada {sufixo}", $"Corpo alfa {sufixo}.");
        alfa.Publicar();

        var rascunho = Artigo.Criar(TipoArtigo.Conceito, $"Rascunho relacionado {sufixo}", $"Corpo rascunho {sufixo}.");

        var desativado = Artigo.Criar(TipoArtigo.Conceito, $"Desativado relacionado {sufixo}", $"Corpo desativado {sufixo}.");
        desativado.Publicar();
        desativado.Desativar();

        contexto.Artigos.AddRange(meio, zebra, alfa, rascunho, desativado);
        await contexto.SaveChangesAsync();

        contexto.ArtigosErros.AddRange(
            ArtigoErro.Criar(meio.Id, erroComArtigos.Id),
            ArtigoErro.Criar(zebra.Id, erroComArtigos.Id),
            ArtigoErro.Criar(alfa.Id, erroComArtigos.Id),
            ArtigoErro.Criar(rascunho.Id, erroComArtigos.Id),
            ArtigoErro.Criar(desativado.Id, erroComArtigos.Id));
        await contexto.SaveChangesAsync();

        return new Cenario(
            erroComArtigos.Slug, erroSemArtigos.Slug,
            alfa.Slug, alfa.Titulo, meio.Titulo,
            zebra.Slug, zebra.Titulo,
            rascunho.Slug, desativado.Slug);
    }
}
