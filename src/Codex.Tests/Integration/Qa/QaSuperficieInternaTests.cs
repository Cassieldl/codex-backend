using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Qa;

/// <summary>
/// SONDA DE QA (002) — superfície interna de artigo e comunicado: autorização no pipeline real,
/// If-Match em TODAS as escritas novas, determinismo de paginação e bordas de I36.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class QaSuperficieInternaTests : IAsyncLifetime
{
    private const string TodasAsRoles =
        "artigos-buscar,artigos-adicionar,artigos-atualizar,artigos-remover,artigos-publicar,"
        + "comunicados-buscar,comunicados-adicionar,comunicados-atualizar,comunicados-remover,comunicados-publicar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private HttpClient _clienteSemToken = null!;
    private HttpClient _clienteRoleErrada = null!;

    public QaSuperficieInternaTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);

        _cliente = _factory.CreateClient();
        _cliente.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRoles, TodasAsRoles);

        _clienteSemToken = _factory.CreateClient();

        _clienteRoleErrada = _factory.CreateClient();
        _clienteRoleErrada.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRoles, "papel-que-nao-existe");

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _clienteSemToken.Dispose();
        _clienteRoleErrada.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ================================================================ autorização

    public static TheoryData<string, string> RotasDeEscritaNovas() => new()
    {
        { "PUT", "/api/v1/interno/artigos/1" },
        { "DELETE", "/api/v1/interno/artigos/1" },
        { "PUT", "/api/v1/interno/artigos/1/publicacao" },
        { "PUT", "/api/v1/interno/artigos/1/verificacao" },
        { "POST", "/api/v1/interno/artigos" },
        { "PUT", "/api/v1/interno/comunicados/1" },
        { "DELETE", "/api/v1/interno/comunicados/1" },
        { "PUT", "/api/v1/interno/comunicados/1/publicacao" },
        { "POST", "/api/v1/interno/comunicados" },
    };

    /// <summary>Expect: sem token → 401, em toda rota de escrita nova.</summary>
    [Theory]
    [MemberData(nameof(RotasDeEscritaNovas))]
    public async Task EscritaNova_SemToken_Retorna401(string metodo, string rota)
    {
        using var resposta = await EnviarCruAsync(_clienteSemToken, metodo, rota, "{}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Expect: role errada → 403, em toda rota de escrita nova.</summary>
    [Theory]
    [MemberData(nameof(RotasDeEscritaNovas))]
    public async Task EscritaNova_ComRoleErrada_Retorna403(string metodo, string rota)
    {
        using var resposta = await EnviarCruAsync(_clienteRoleErrada, metodo, rota, "{}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>I36 — Expect: sem token → 401; role errada → 403; sem tocar o banco.</summary>
    [Fact]
    public async Task I36_Existe_SemToken_Retorna401()
    {
        using var resposta = await _clienteSemToken.GetAsync("/api/v1/interno/artigos/existe?slug=como-funciona");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task I36_Existe_ComRoleErrada_Retorna403()
    {
        using var resposta = await _clienteRoleErrada.GetAsync("/api/v1/interno/artigos/existe?slug=como-funciona");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Expect: role de artigo NÃO abre comunicado e vice-versa — separação por entidade, não um
    /// "qualquer role interna serve".
    /// </summary>
    [Fact]
    public async Task RoleDeArtigo_NaoAbreComunicado()
    {
        using var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRoles, "artigos-buscar,artigos-publicar");

        using var resposta = await cliente.GetAsync("/api/v1/interno/comunicados");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RoleDeComunicado_NaoAbreArtigo()
    {
        using var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRoles, "comunicados-buscar,comunicados-publicar");

        using var resposta = await cliente.GetAsync("/api/v1/interno/artigos");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Expect: quem pode buscar não pode publicar (role por ação, não por entidade).</summary>
    [Fact]
    public async Task RoleDeBusca_NaoPublicaArtigo()
    {
        using var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRoles, "artigos-buscar");

        using var resposta = await EnviarCruAsync(cliente, "PUT", "/api/v1/interno/artigos/1/publicacao", "{\"publicado\":true}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ================================================================ If-Match

    /// <summary>Expect: toda escrita nova sem If-Match → 400 (nunca 200, nunca 500).</summary>
    [Fact]
    public async Task ArtigoTodasAsEscritas_SemIfMatch_Retornam400()
    {
        var artigo = await CriarArtigoAsync();

        var casos = new (string Metodo, string Rota, object Corpo)[]
        {
            ("PUT", $"/api/v1/interno/artigos/{artigo.Id}", new { tipo = "Conceito", titulo = artigo.Titulo, corpo = "x", campoLayoutIds = Array.Empty<int>(), erroIds = Array.Empty<int>() }),
            ("DELETE", $"/api/v1/interno/artigos/{artigo.Id}", new { }),
            ("PUT", $"/api/v1/interno/artigos/{artigo.Id}/publicacao", new { publicado = true }),
            ("PUT", $"/api/v1/interno/artigos/{artigo.Id}/verificacao", new { verificadoEm = "2026-01-01" }),
        };

        foreach (var caso in casos)
        {
            using var resposta = await EnviarAsync(_cliente, caso.Metodo, caso.Rota, caso.Corpo, ifMatch: null);
            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: $"{caso.Metodo} {caso.Rota} sem If-Match");
        }
    }

    [Fact]
    public async Task ComunicadoTodasAsEscritas_SemIfMatch_Retornam400()
    {
        var comunicado = await CriarComunicadoAsync();

        var casos = new (string Metodo, string Rota, object Corpo)[]
        {
            ("PUT", $"/api/v1/interno/comunicados/{comunicado.Id}", new { titulo = "t", corpo = "c", severidade = "Informativo", abrangencia = "Global", expiraEm = DateTimeOffset.UtcNow.AddDays(9) }),
            ("DELETE", $"/api/v1/interno/comunicados/{comunicado.Id}", new { }),
            ("PUT", $"/api/v1/interno/comunicados/{comunicado.Id}/publicacao", new { publicado = true }),
        };

        foreach (var caso in casos)
        {
            using var resposta = await EnviarAsync(_cliente, caso.Metodo, caso.Rota, caso.Corpo, ifMatch: null);
            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: $"{caso.Metodo} {caso.Rota} sem If-Match");
        }
    }

    /// <summary>Expect: If-Match desatualizado → 409 em toda escrita nova.</summary>
    [Fact]
    public async Task ArtigoTodasAsEscritas_ComIfMatchDesatualizado_Retornam409()
    {
        var artigo = await CriarArtigoAsync();
        var desatualizado = $"\"{artigo.Versao - 1}\"";

        var casos = new (string Metodo, string Rota, object Corpo)[]
        {
            ("PUT", $"/api/v1/interno/artigos/{artigo.Id}", new { tipo = "Conceito", titulo = artigo.Titulo, corpo = "x", campoLayoutIds = Array.Empty<int>(), erroIds = Array.Empty<int>() }),
            ("DELETE", $"/api/v1/interno/artigos/{artigo.Id}", new { }),
            ("PUT", $"/api/v1/interno/artigos/{artigo.Id}/publicacao", new { publicado = true }),
            ("PUT", $"/api/v1/interno/artigos/{artigo.Id}/verificacao", new { verificadoEm = "2026-01-01" }),
        };

        foreach (var caso in casos)
        {
            using var resposta = await EnviarAsync(_cliente, caso.Metodo, caso.Rota, caso.Corpo, desatualizado);
            resposta.StatusCode.Should().Be(HttpStatusCode.Conflict, because: $"{caso.Metodo} {caso.Rota} com If-Match velho");
            (await resposta.Content.ReadAsStringAsync()).Should().NotContain("uq_codex_").And.NotContain("at Codex.");
        }
    }

    [Fact]
    public async Task ComunicadoTodasAsEscritas_ComIfMatchDesatualizado_Retornam409()
    {
        var comunicado = await CriarComunicadoAsync();
        var desatualizado = $"\"{comunicado.Versao - 1}\"";

        var casos = new (string Metodo, string Rota, object Corpo)[]
        {
            ("PUT", $"/api/v1/interno/comunicados/{comunicado.Id}", new { titulo = "t", corpo = "c", severidade = "Informativo", abrangencia = "Global", expiraEm = DateTimeOffset.UtcNow.AddDays(9) }),
            ("DELETE", $"/api/v1/interno/comunicados/{comunicado.Id}", new { }),
            ("PUT", $"/api/v1/interno/comunicados/{comunicado.Id}/publicacao", new { publicado = true }),
        };

        foreach (var caso in casos)
        {
            using var resposta = await EnviarAsync(_cliente, caso.Metodo, caso.Rota, caso.Corpo, desatualizado);
            resposta.StatusCode.Should().Be(HttpStatusCode.Conflict, because: $"{caso.Metodo} {caso.Rota} com If-Match velho");
            (await resposta.Content.ReadAsStringAsync()).Should().NotContain("uq_codex_").And.NotContain("at Codex.");
        }
    }

    // ================================================================ paginação determinística

    /// <summary>
    /// Expect: percorrer as páginas de I23 devolve cada artigo EXATAMENTE uma vez. Sete artigos
    /// criados na mesma transação compartilham <c>criadoem</c> (DEFAULT now() é o timestamp da
    /// transação) — sem desempate na ordenação, o OFFSET/LIMIT do Postgres pode repetir e pular
    /// linhas entre páginas.
    ///
    /// <para>
    /// ATENÇÃO — esta sonda é INSUFICIENTE sozinha: com a leitura estática ela passa mesmo sem o
    /// desempate, porque o heap não se reorganiza entre as páginas. A variante que expõe o defeito
    /// edita um artigo da página 1 antes de ler a página 2 (ver relatório de QA da 002, ISSUE-2).
    /// </para>
    /// </summary>
    [Fact]
    public async Task I23_PaginacaoComEmpateDeCriadoEm_NaoRepeteNemPulaArtigo()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        await using (var contexto = _fixture.CriarContexto())
        {
            for (var i = 1; i <= 7; i++)
                contexto.Artigos.Add(Artigo.Criar(TipoArtigo.Conceito, $"Pag {sufixo} item {i}", $"Corpo {i}."));
            await contexto.SaveChangesAsync(); // uma transação -> mesmo criadoem para os 7
        }

        var vistos = new List<int>();
        for (var pagina = 1; pagina <= 3; pagina++)
        {
            var corpo = await LerAsync($"/api/v1/interno/artigos?q={sufixo}&pagina={pagina}&tamanhoPagina=3");
            vistos.AddRange(corpo.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));
        }

        vistos.Should().HaveCount(7, because: "7 artigos em 3 páginas de 3 = 3+3+1");
        vistos.Should().OnlyHaveUniqueItems(
            because: "sem desempate estável na ordenação, a mesma linha aparece em duas páginas e outra some");
    }

    /// <summary>Mesma prova para I30 — comunicado, que empata em <c>expiraem</c>.</summary>
    [Fact]
    public async Task I30_PaginacaoComEmpateDeExpiraEm_NaoRepeteNemPulaComunicado()
    {
        var expiraEm = DateTimeOffset.UtcNow.AddDays(30);
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        await using (var contexto = _fixture.CriarContexto())
        {
            for (var i = 1; i <= 7; i++)
                contexto.Comunicados.Add(Comunicado.CriarGlobal($"Com {sufixo} {i}", "corpo", Severidade.Informativo, expiraEm));
            await contexto.SaveChangesAsync();
        }

        var vistos = new List<int>();
        for (var pagina = 1; pagina <= 20; pagina++)
        {
            var corpo = await LerAsync($"/api/v1/interno/comunicados?pagina={pagina}&tamanhoPagina=3");
            var ids = corpo.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
            if (ids.Count == 0) break;
            vistos.AddRange(ids);
        }

        vistos.Should().OnlyHaveUniqueItems(because: "OrderBy sem desempate estável repete linha entre páginas");
    }

    /// <summary>Expect: cap interno de 200 respeitado e ecoado.</summary>
    [Theory]
    [InlineData("/api/v1/interno/artigos")]
    [InlineData("/api/v1/interno/comunicados")]
    public async Task ListagemInterna_TamanhoPaginaAbsurdo_ECapadoEm200(string rota)
    {
        var corpo = await LerAsync($"{rota}?tamanhoPagina=999999");

        corpo.GetProperty("pageSize").GetInt32().Should().Be(200);
        corpo.GetProperty("items").GetArrayLength().Should().BeLessThanOrEqualTo(200);
    }

    // ================================================================ I36

    [Theory]
    // Expect: slug ausente/vazio/em branco -> 400 SLUG_OBRIGATORIO.
    [InlineData("/api/v1/interno/artigos/existe")]
    [InlineData("/api/v1/interno/artigos/existe?slug=")]
    [InlineData("/api/v1/interno/artigos/existe?slug=%20%20")]
    public async Task I36_SlugAusenteOuEmBranco_Retorna400(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Expect: artigo removido (soft delete) não "existe" — senão a tela 16 recusa recriar um slug livre.</summary>
    [Fact]
    public async Task I36_ArtigoRemovido_NaoContaComoExistente()
    {
        var artigo = await CriarArtigoAsync();

        using var remocao = await EnviarAsync(_cliente, "DELETE", $"/api/v1/interno/artigos/{artigo.Id}", new { }, $"\"{artigo.Versao}\"");
        remocao.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var corpo = await LerAsync($"/api/v1/interno/artigos/existe?slug={artigo.Slug}");

        corpo.GetProperty("data").GetProperty("existe").GetBoolean().Should().BeFalse();
        corpo.GetProperty("data").GetProperty("artigoId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ================================================================ apoio

    private async Task<(int Id, string Slug, uint Versao, string Titulo)> CriarArtigoAsync()
    {
        var titulo = $"QA artigo {Guid.NewGuid():N}";
        using var resposta = await EnviarAsync(_cliente, "POST", "/api/v1/interno/artigos", new
        {
            tipo = "Conceito",
            titulo,
            corpo = "Corpo de apoio da sonda de QA.",
            campoLayoutIds = Array.Empty<int>(),
            erroIds = Array.Empty<int>(),
        }, ifMatch: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        var dados = corpo.GetProperty("data");

        return (dados.GetProperty("id").GetInt32(), dados.GetProperty("slug").GetString()!,
            dados.GetProperty("versaoConcorrencia").GetUInt32(), titulo);
    }

    private async Task<(int Id, uint Versao)> CriarComunicadoAsync()
    {
        using var resposta = await EnviarAsync(_cliente, "POST", "/api/v1/interno/comunicados", new
        {
            titulo = $"QA comunicado {Guid.NewGuid():N}",
            corpo = "Corpo de apoio da sonda de QA.",
            severidade = "Informativo",
            abrangencia = "Global",
            expiraEm = DateTimeOffset.UtcNow.AddDays(10),
        }, ifMatch: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var dados = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("data");

        return (dados.GetProperty("id").GetInt32(), dados.GetProperty("versaoConcorrencia").GetUInt32());
    }

    private async Task<JsonElement> LerAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: rota);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string metodo, string rota, object corpo, string? ifMatch)
    {
        var requisicao = new HttpRequestMessage(new HttpMethod(metodo), rota);

        if (!string.Equals(metodo, "DELETE", StringComparison.OrdinalIgnoreCase))
            requisicao.Content = JsonContent.Create(corpo);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        return cliente.SendAsync(requisicao);
    }

    private static Task<HttpResponseMessage> EnviarCruAsync(HttpClient cliente, string metodo, string rota, string json)
    {
        var requisicao = new HttpRequestMessage(new HttpMethod(metodo), rota);

        if (!string.Equals(metodo, "DELETE", StringComparison.OrdinalIgnoreCase))
            requisicao.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return cliente.SendAsync(requisicao);
    }
}
