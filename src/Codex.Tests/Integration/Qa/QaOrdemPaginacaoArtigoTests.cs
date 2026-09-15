using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Qa;

/// <summary>
/// SONDA DE QA — I23/P7 ordenam só por <c>criadoem</c>, sem desempate. I30 (comunicado) tem
/// <c>ThenByDescending(Id)</c>; artigo não. A pergunta desta sonda é se a ausência do desempate é
/// observável, e não apenas teórica.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class QaOrdemPaginacaoArtigoTests : IAsyncLifetime
{
    private const string Roles = "artigos-buscar,artigos-adicionar,artigos-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public QaOrdemPaginacaoArtigoTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        _cliente.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRoles, Roles);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Pré-condição do risco: artigos criados na mesma transação compartilham <c>criadoem</c>
    /// (o DEFAULT <c>now()</c> é o timestamp da transação, não do statement).
    /// Expect: os 6 artigos têm UM único valor distinto de <c>criadoem</c>.
    /// </summary>
    [Fact]
    public async Task ArtigosDaMesmaTransacao_CompartilhamCriadoEm()
    {
        var sufixo = await SemearSeisArtigosNaMesmaTransacaoAsync();

        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(DISTINCT criadoem) FROM codex.artigos WHERE titulo LIKE @padrao";
        comando.Parameters.AddWithValue("padrao", $"%{sufixo}%");

        var distintos = (long)(await comando.ExecuteScalarAsync())!;

        distintos.Should().Be(1, because: "é a pré-condição que torna o desempate necessário");
    }

    /// <summary>
    /// Reprodução do risco no gesto real do editor: abre a página 1, alguém edita um artigo
    /// (o UPDATE reescreve a tupla no fim do heap), o editor vai para a página 2.
    /// Expect: as duas páginas juntas mostram 6 artigos distintos — nenhum repetido, nenhum perdido.
    /// </summary>
    [Fact]
    public async Task I23_EdicaoEntreAsPaginas_NaoPodeRepetirNemPerderArtigo()
    {
        var sufixo = await SemearSeisArtigosNaMesmaTransacaoAsync();

        var pagina1 = await IdsDaPaginaAsync(sufixo, pagina: 1);
        pagina1.Should().HaveCount(3);

        await TocarArtigoAsync(pagina1[0]);

        var pagina2 = await IdsDaPaginaAsync(sufixo, pagina: 2);

        pagina1.Concat(pagina2).Should().OnlyHaveUniqueItems(
            because: "sem desempate estável, a linha reescrita muda de lugar no heap e reaparece na página seguinte");
        pagina1.Concat(pagina2).Should().HaveCount(6,
            because: "6 artigos em 2 páginas de 3 — repetir um significa que outro ficou invisível para o editor");
    }

    // ------------------------------------------------------------------ apoio

    private async Task<string> SemearSeisArtigosNaMesmaTransacaoAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        await using var contexto = _fixture.CriarContexto();

        for (var i = 1; i <= 6; i++)
            contexto.Artigos.Add(Artigo.Criar(TipoArtigo.Conceito, $"Ordem {sufixo} item {i}", $"Corpo {i}."));

        await contexto.SaveChangesAsync();
        return sufixo;
    }

    private async Task<List<int>> IdsDaPaginaAsync(string sufixo, int pagina)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/interno/artigos?q={sufixo}&pagina={pagina}&tamanhoPagina=3");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        return corpo.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
    }

    private async Task TocarArtigoAsync(int id)
    {
        using var detalhe = await _cliente.GetAsync($"/api/v1/interno/artigos/{id}");
        var dados = JsonDocument.Parse(await detalhe.Content.ReadAsStringAsync()).RootElement.GetProperty("data");

        var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/artigos/{id}")
        {
            Content = JsonContent.Create(new
            {
                tipo = dados.GetProperty("tipo").GetString(),
                titulo = dados.GetProperty("titulo").GetString(),
                corpo = dados.GetProperty("corpo").GetString() + " (editado)",
            }),
        };
        requisicao.Headers.TryAddWithoutValidation("If-Match", $"\"{dados.GetProperty("versaoConcorrencia").GetUInt32()}\"");

        using var resposta = await _cliente.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
