using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Qa;

/// <summary>
/// SONDA DE QA (re-verificação) — a varredura do fix de ISSUE-1 foi feita por <c>grep "Math.Max(1,"</c>.
/// P1 (<c>GET /api/v1/busca</c>) pagina com OUTRO idioma de piso (<c>pagina &gt; 0 ? pagina : 1</c>),
/// que aquele grep não encontra. Esta classe cobre o ponto por comportamento, não por texto.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class QaVarreduraPaginacaoBuscaTests : IAsyncLifetime
{
    private const string RolesDeArtigo = "artigos-adicionar,artigos-publicar,artigos-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private HttpClient _clienteInterno = null!;
    private string _termo = null!;

    public QaVarreduraPaginacaoBuscaTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _clienteInterno = _factory.CreateClient();
        _clienteInterno.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRoles, RolesDeArtigo);

        _termo = $"varredura{Guid.NewGuid():N}"[..20];

        // Criado pelo caminho REAL (I25 indexa; DbContext direto não) e publicado, para que o
        // item exista no corpus que P1 consulta.
        using var criacao = await _clienteInterno.PostAsJsonAsync("/api/v1/interno/artigos", new
        {
            tipo = "Conceito",
            titulo = $"Artigo {_termo}",
            corpo = $"Corpo do artigo {_termo}.",
        });
        criacao.StatusCode.Should().Be(HttpStatusCode.Created);

        var dados = JsonDocument.Parse(await criacao.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/artigos/{dados.GetProperty("id").GetInt32()}/publicacao")
        {
            Content = JsonContent.Create(new { publicado = true }),
        };
        requisicao.Headers.TryAddWithoutValidation("If-Match", $"\"{dados.GetProperty("versaoConcorrencia").GetUInt32()}\"");

        using var publicacao = await _clienteInterno.SendAsync(requisicao);
        publicacao.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _clienteInterno.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Expect: P1 com página altíssima devolve 200, nunca 500.</summary>
    [Theory]
    [InlineData(2_000_000_000)]
    [InlineData(int.MaxValue)]
    public async Task P1_PaginaAlta_NaoDerrubaABusca(int pagina)
    {
        var (status, _) = await BuscarAsync($"pagina={pagina}&tamanhoPagina=50");

        ((int)status).Should().BeLessThan(500);
    }

    /// <summary>
    /// Expect: página altíssima é página VAZIA. O risco específico deste ponto não é 500 — a
    /// coleção é materializada em memória, e <c>Enumerable.Skip(negativo)</c> não lança: trata
    /// como zero. O sintoma é devolver o conteúdo da PÁGINA 1 rotulado como página 2 bilhões.
    /// </summary>
    [Theory]
    [InlineData(2_000_000_000)]
    [InlineData(int.MaxValue)]
    public async Task P1_PaginaAlta_NaoPodeDevolverOConteudoDaPaginaUm(int pagina)
    {
        var (statusPrimeira, primeira) = await BuscarAsync("pagina=1&tamanhoPagina=50");
        statusPrimeira.Should().Be(HttpStatusCode.OK);
        ItensDe(primeira).Should().NotBeEmpty(because: "o cenário publica e indexa um artigo que casa o termo");

        var (status, corpo) = await BuscarAsync($"pagina={pagina}&tamanhoPagina=50");

        status.Should().Be(HttpStatusCode.OK);
        ItensDe(corpo).Should().BeEmpty(
            because: "offset além do acervo é página vazia — devolver a página 1 com outro rótulo é resultado errado em silêncio");
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> BuscarAsync(string query)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/busca?q={_termo}&{query}");
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
        return (resposta.StatusCode, corpo);
    }

    private static List<JsonElement> ItensDe(JsonElement corpo) => corpo
        .GetProperty("data").GetProperty("grupos").EnumerateArray()
        .SelectMany(grupo => grupo.GetProperty("itens").EnumerateArray())
        .ToList();
}
