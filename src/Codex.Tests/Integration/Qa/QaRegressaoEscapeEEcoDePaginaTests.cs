using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Qa;

/// <summary>
/// SONDA DE QA (re-verificação) — duas perguntas que o fix levanta:
/// <list type="number">
/// <item>escapar <c>_</c> quebrou alguma busca LEGÍTIMA por termo com sublinhado?</item>
/// <item>o <c>page</c> ecoado no envelope ainda concorda com a página realmente servida?</item>
/// </list>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class QaRegressaoEscapeEEcoDePaginaTests : IAsyncLifetime
{
    private const string Roles = "artigos-buscar,artigos-adicionar,artigos-publicar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private HttpClient _clienteInterno = null!;
    private string _sufixo = null!;

    public QaRegressaoEscapeEEcoDePaginaTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _clienteInterno = _factory.CreateClient();
        _clienteInterno.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRoles, Roles);

        _sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contexto = _fixture.CriarContexto();

        // Par adversarial: um título com sublinhado LITERAL e outro com um caractere qualquer no
        // mesmo lugar. Só o escape correto distingue os dois.
        var comSublinhado = Artigo.Criar(TipoArtigo.Conceito, $"Campo_Obrigatorio {_sufixo}", "corpo a");
        comSublinhado.Publicar();
        var comOutroCaractere = Artigo.Criar(TipoArtigo.Conceito, $"CampoXObrigatorio {_sufixo}", "corpo b");
        comOutroCaractere.Publicar();

        contexto.Artigos.AddRange(comSublinhado, comOutroCaractere);
        await contexto.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _clienteInterno.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------- regressão do escape

    /// <summary>
    /// Expect: buscar por um termo que CONTÉM sublinhado continua achando o registro que tem
    /// sublinhado. É a regressão que o escape poderia ter causado — se o <c>_</c> tivesse sido
    /// escapado errado, o casamento legítimo sumiria.
    /// </summary>
    [Fact]
    public async Task P7_TermoComSublinhadoLiteral_ContinuaAchandoORegistroQueTemSublinhado()
    {
        var titulos = await TitulosAsync($"q=Campo_Obrigatorio%20{_sufixo}");

        titulos.Should().Contain($"Campo_Obrigatorio {_sufixo}",
            because: "escapar o curinga não pode custar a busca literal — é o caso de uso normal do editor");
    }

    /// <summary>Expect: o sublinhado deixou de ser curinga — não casa 'X' no lugar dele.</summary>
    [Fact]
    public async Task P7_SublinhadoNaoCasaMaisQualquerCaractere()
    {
        var titulos = await TitulosAsync($"q=Campo_Obrigatorio%20{_sufixo}");

        titulos.Should().NotContain($"CampoXObrigatorio {_sufixo}",
            because: "'_' é texto digitado, não o curinga de um caractere do LIKE");
    }

    /// <summary>Expect: o mesmo par na listagem INTERNA (I23), que tem o próprio filtro <c>q</c>.</summary>
    [Fact]
    public async Task I23_SublinhadoTambemETextoNaListagemInterna()
    {
        using var resposta = await _clienteInterno.GetAsync(
            $"/api/v1/interno/artigos?q=Campo_Obrigatorio%20{_sufixo}&tamanhoPagina=100");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var titulos = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement
            .GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("titulo").GetString()!)
            .ToList();

        titulos.Should().Contain($"Campo_Obrigatorio {_sufixo}");
        titulos.Should().NotContain($"CampoXObrigatorio {_sufixo}");
    }

    /// <summary>
    /// Expect: a busca full-text (P1) NÃO foi afetada pelo escape — ela passa por tsvector, não
    /// por ILIKE. Um termo com separador continua encontrando o item indexado.
    /// </summary>
    [Fact]
    public async Task P1_BuscaFullText_NaoRegrediuComOEscape()
    {
        var termo = $"escape{Guid.NewGuid():N}"[..18];

        using var criacao = await _clienteInterno.PostAsJsonAsync("/api/v1/interno/artigos", new
        {
            tipo = "Conceito",
            titulo = $"Titulo {termo}",
            corpo = $"Corpo {termo}.",
        });
        criacao.StatusCode.Should().Be(HttpStatusCode.Created);

        var dados = JsonDocument.Parse(await criacao.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        var publicar = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/artigos/{dados.GetProperty("id").GetInt32()}/publicacao")
        {
            Content = JsonContent.Create(new { publicado = true }),
        };
        publicar.Headers.TryAddWithoutValidation("If-Match", $"\"{dados.GetProperty("versaoConcorrencia").GetUInt32()}\"");
        using var publicacao = await _clienteInterno.SendAsync(publicar);
        publicacao.StatusCode.Should().Be(HttpStatusCode.OK);

        using var busca = await _cliente.GetAsync($"/api/v1/busca?q={termo}&tamanhoPagina=50");
        busca.StatusCode.Should().Be(HttpStatusCode.OK);

        var grupos = JsonDocument.Parse(await busca.Content.ReadAsStringAsync()).RootElement
            .GetProperty("data").GetProperty("grupos").EnumerateArray()
            .SelectMany(g => g.GetProperty("itens").EnumerateArray())
            .ToList();

        grupos.Should().NotBeEmpty(because: "P1 usa tsvector; o escape de ILIKE não pode ter alcançado esse caminho");
    }

    // ---------------------------------------------------------------- eco de `page`

    /// <summary>
    /// Expect: o <c>page</c> do envelope é a página REALMENTE servida. O serviço agora prende a
    /// página pelo teto; o controller continua ecoando <c>Math.Max(1, pagina)</c>, então os dois
    /// podem discordar para páginas altas — e o cliente pagina em cima do número ecoado.
    /// </summary>
    [Fact]
    public async Task P7_PageEcoado_ConcordaComAPaginaRealmenteServida()
    {
        using var resposta = await _cliente.GetAsync("/api/v1/artigos?pagina=2000000000&tamanhoPagina=100");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;

        corpo.GetProperty("page").GetInt32().Should().BeLessThanOrEqualTo(int.MaxValue / 100,
            because: "ecoar uma página que o serviço não serviu faz o envelope afirmar algo falso sobre a própria resposta");
    }

    private async Task<List<string>> TitulosAsync(string query)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/artigos?{query}&tamanhoPagina=100");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement
            .GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("titulo").GetString()!)
            .ToList();
    }
}
