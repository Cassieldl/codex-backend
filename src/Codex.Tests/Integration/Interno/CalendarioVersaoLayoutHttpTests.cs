using System.Net;
using System.Text;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Norma e datas da versão de layout pela edição interna, em JSON literal: é o mesmo corpo que a
/// carga envia, e data serializada pelo próprio .NET binda mesmo com o contrato errado.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CalendarioVersaoLayoutHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public CalendarioVersaoLayoutHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();

        await using var contexto = _fixture.CriarContexto();
        contexto.VersoesLayout.RemoveRange(await contexto.VersoesLayout.Where(v => v.Rotulo.Contains(_sufixo)).ToListAsync());
        await contexto.SaveChangesAsync();
    }

    [Fact]
    public async Task Put_GravaNormaEDatas_EPutSemElasMantemOQueEstava()
    {
        var (id, etag) = await CriarVersaoAsync();

        var (status, corpo, novaEtag) = await AtualizarAsync(id, etag, """
            {"normaOrigem": "NT 009 v1.01", "homologacaoDesde": "2026-10-01", "liberadaNoInvoicyEm": "2026-10-22", "obrigatoriaEm": "2026-12-01"}
            """);
        status.Should().Be(HttpStatusCode.OK, because: corpo.ToString());

        var (statusSemDatas, corpoSemDatas, _) = await AtualizarAsync(id, novaEtag, """{"descricao": "Nota nova."}""");
        statusSemDatas.Should().Be(HttpStatusCode.OK, because: corpoSemDatas.ToString());

        foreach (var dados in new[] { corpo.GetProperty("data"), corpoSemDatas.GetProperty("data") })
        {
            dados.GetProperty("normaOrigem").GetString().Should().Be("NT 009 v1.01");
            dados.GetProperty("homologacaoDesde").GetString().Should().Be("2026-10-01");
            dados.GetProperty("liberadaNoInvoicyEm").GetString().Should().Be("2026-10-22");
            dados.GetProperty("obrigatoriaEm").GetString().Should().Be("2026-12-01");
        }
    }

    [Fact]
    public async Task Put_ComOsQuatroEmNull_ApagaNormaEDatas_EOGetConfirma()
    {
        var (id, etag) = await CriarVersaoAsync();

        var (statusGravacao, corpoGravacao, etagGravada) = await AtualizarAsync(id, etag, """
            {"normaOrigem": "NT 009 v1.01", "homologacaoDesde": "2026-10-01", "liberadaNoInvoicyEm": "2026-10-22", "obrigatoriaEm": "2026-12-01"}
            """);
        statusGravacao.Should().Be(HttpStatusCode.OK, because: corpoGravacao.ToString());

        var (status, corpo, _) = await AtualizarAsync(id, etagGravada, """
            {"normaOrigem": null, "homologacaoDesde": null, "liberadaNoInvoicyEm": null, "obrigatoriaEm": null}
            """);
        status.Should().Be(HttpStatusCode.OK, because: corpo.ToString());

        foreach (var dados in new[] { corpo.GetProperty("data"), await ObterDaListagemAsync(id) })
        {
            foreach (var campo in new[] { "normaOrigem", "homologacaoDesde", "liberadaNoInvoicyEm", "obrigatoriaEm" })
                dados.GetProperty(campo).ValueKind.Should().Be(JsonValueKind.Null, because: $"null no PUT apaga {campo}");
        }
    }

    [Fact]
    public async Task Put_NormaAcimaDoTeto_Responde422NoCampo()
    {
        var (id, etag) = await CriarVersaoAsync();

        var (status, corpo, _) = await AtualizarAsync(id, etag, $$"""{"normaOrigem": "{{new string('N', 61)}}"}""");

        status.Should().Be(HttpStatusCode.UnprocessableEntity, because: "teto de coluna segue o validador do projeto, como o do rótulo");
        corpo.ToString().Should().Contain("normaOrigem", because: "o erro aponta o campo que estourou o teto");
    }

    // ---------------------------------------------------------------------------------- apoio

    private async Task<(int Id, string ETag)> CriarVersaoAsync()
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interno/layout/versoes")
        {
            Content = new StringContent($$"""{"rotulo": "Calendario {{Guid.NewGuid():N}} {{_sufixo}}"}""", Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "versoeslayout-adicionar");

        using var resposta = await _cliente.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);

        var dados = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        return (dados.GetProperty("id").GetInt32(), resposta.Headers.ETag!.ToString());
    }

    private async Task<JsonElement> ObterDaListagemAsync(int id)
    {
        for (var pagina = 1; ; pagina++)
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/interno/layout/versoes?pagina={pagina}&tamanhoPagina=200");
            requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "versoeslayout-buscar");

            using var resposta = await _cliente.SendAsync(requisicao);
            var texto = await resposta.Content.ReadAsStringAsync();
            resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: texto);

            var itens = JsonDocument.Parse(texto).RootElement.GetProperty("items").EnumerateArray().ToList();
            itens.Should().NotBeEmpty(because: $"a versão {id} deveria estar na listagem");

            var achada = itens.FirstOrDefault(item => item.GetProperty("id").GetInt32() == id);
            if (achada.ValueKind == JsonValueKind.Object)
                return achada.Clone();
        }
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, string ETag)> AtualizarAsync(int id, string etag, string corpoJson)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/layout/versoes/{id}")
        {
            Content = new StringContent(corpoJson, Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "versoeslayout-atualizar");
        requisicao.Headers.TryAddWithoutValidation("If-Match", etag);

        using var resposta = await _cliente.SendAsync(requisicao);
        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta.Headers.ETag?.ToString() ?? string.Empty);
    }
}
