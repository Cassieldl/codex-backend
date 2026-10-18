using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// O byte NUL não chega ao Postgres, que o recusa em texto e responderia 500: na query string e
/// no corpo JSON a API responde 400 no envelope de erro, sem detalhe interno.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CaractereNuloHttpTests : IAsyncLifetime
{
    private const string RoleCriarProvedor = "provedores-adicionar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public CaractereNuloHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("/api/v1/busca?q=%00%00abc")]
    [InlineData("/api/v1/artigos?q=a%00")]
    [InlineData("/api/v1/provedores?q=%00")]
    [InlineData("/api/v1/erros?provedor=%00")]
    [InlineData("/api/v1/comunicados?provedor=a%00")]
    [InlineData("/api/v1/artigos?q%00=a")]
    public async Task NulNaQueryString_Responde400ComEnvelope(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        var corpo = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: corpo);
        AfirmarEnvelopeSemDetalheInterno(corpo);
    }

    [Fact]
    public async Task NulNoCorpoJson_Responde400MalformedRequest()
    {
        var json = $$"""
            {"nome":"Provedor a\u0000b","identificadorInvoicy":"INV-NUL-{{Guid.NewGuid():N}}","categoria":"Nacional","aliases":[]}
            """;

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interno/provedores")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RoleCriarProvedor);

        using var resposta = await _cliente.SendAsync(requisicao);
        var corpo = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: corpo);
        AfirmarEnvelopeSemDetalheInterno(corpo);
        JsonDocument.Parse(corpo).RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("MALFORMED_REQUEST");
    }

    private static void AfirmarEnvelopeSemDetalheInterno(string corpo)
    {
        var erro = JsonDocument.Parse(corpo).RootElement.GetProperty("error");

        erro.GetProperty("code").GetString().Should().NotBeNullOrWhiteSpace();
        erro.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        corpo.Should().NotContain("at Codex.").And.NotContain("Exception").And.NotContain("Npgsql").And.NotContain("0x00");
    }
}
