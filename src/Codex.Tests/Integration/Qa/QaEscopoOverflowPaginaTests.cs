using System.Net;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Qa;

/// <summary>
/// SONDA DE QA — mede o ALCANCE do 500 de <c>pagina</c> alto: é só dos endpoints novos da 002 ou
/// atinge também os herdados da 001? A resposta muda a severidade e o dono da correção.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class QaEscopoOverflowPaginaTests : IAsyncLifetime
{
    private const string RolesDeLeitura =
        "artigos-buscar,comunicados-buscar,erros-buscar,provedores-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public QaEscopoOverflowPaginaTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _cliente.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRoles, RolesDeLeitura);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Expect (contrato): lista vazia, 200. O que a sonda quer saber é quais rotas dão 500.</summary>
    [Theory]
    // 002 — público
    [InlineData("/api/v1/artigos")]
    // 002 — interno
    [InlineData("/api/v1/interno/artigos")]
    [InlineData("/api/v1/interno/comunicados")]
    // 001 — herdados
    [InlineData("/api/v1/erros")]
    [InlineData("/api/v1/interno/erros")]
    [InlineData("/api/v1/provedores")]
    [InlineData("/api/v1/interno/provedores")]
    public async Task PaginaAlta_NaoPodeDerrubarNenhumaListagem(string rota)
    {
        using var resposta = await _cliente.GetAsync($"{rota}?pagina=2000000000&tamanhoPagina=100");

        ((int)resposta.StatusCode).Should().BeLessThan(500,
            because: $"{rota} com offset fora do acervo é lista vazia, nunca falha de servidor");
    }

    /// <summary>
    /// Limiar exato medido com o cap público (100): a página 21_474_837 ainda cabe em Int32
    /// (offset 2_147_483_600) e devolve 200; a 21_474_838 estoura.
    /// </summary>
    [Theory]
    [InlineData(21_474_837, HttpStatusCode.OK)]
    [InlineData(21_474_838, HttpStatusCode.OK)]
    public async Task P7_LimiarExatoDoEstouro(int pagina, HttpStatusCode esperado)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/artigos?pagina={pagina}&tamanhoPagina=100");

        resposta.StatusCode.Should().Be(esperado);
    }
}
