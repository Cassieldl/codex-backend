using System.Net;
using System.Text.Json;
using Codex.Application.Common;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Qa;

/// <summary>
/// ISSUE-1 do QA da 002: `pagina` alto derrubava **todas** as listagens com 500.
///
/// <para>
/// O offset que o EF calcula (<c>(pagina - 1) * tamanhoPagina</c>) é aritmética <c>int</c> sem
/// verificação de estouro: com <c>pagina=2000000000</c> e <c>tamanhoPagina=100</c> o produto
/// transborda para negativo e o Postgres recusa com <c>2201X: OFFSET must not be negative</c>.
/// Duas das rotas afetadas são anônimas e sem rate limit.
/// </para>
///
/// <para>
/// A regra de segurança do projeto pedia cap em `pageSize` e ela estava cumprida em oito lugares
/// — o parâmetro que estourava era o outro. Por isso a normalização virou
/// <see cref="Paginacao"/>, um ponto só.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class PaginaAltaNaoDerrubaListagemTests : IAsyncLifetime
{
    private const string TodasAsRoles =
        "provedores-buscar,versoes-buscar,campolayout-buscar,camposversao-buscar," +
        "erros-buscar,artigos-buscar,comunicados-buscar,indicebusca-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public PaginaAltaNaoDerrubaListagemTests(PostgresContainerFixture fixture)
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

    [Theory]
    [InlineData("/api/v1/artigos")]
    [InlineData("/api/v1/erros")]
    [InlineData("/api/v1/provedores")]
    [InlineData("/api/v1/interno/artigos")]
    [InlineData("/api/v1/interno/erros")]
    [InlineData("/api/v1/interno/provedores")]
    [InlineData("/api/v1/interno/comunicados")]
    public async Task PaginaAlemDoQueCabeEmInt_DevolveListaVazia_NuncaCincoCentos(string rota)
    {
        var (status, corpo) = await ObterAsync($"{rota}?pagina=2000000000&tamanhoPagina=100");

        status.Should().Be(HttpStatusCode.OK,
            because: "página além do acervo é resultado legítimo — 500 expõe falha de servidor a quem só mandou um número grande");
        corpo.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("/api/v1/artigos")]
    [InlineData("/api/v1/interno/comunicados")]
    public async Task PaginaNoMaximoDeInt_TambemNaoDerruba(string rota)
    {
        var (status, _) = await ObterAsync($"{rota}?pagina={int.MaxValue}&tamanhoPagina=100");

        status.Should().Be(HttpStatusCode.OK,
            because: "o valor extremo do tipo é o primeiro que alguém tenta — e era o que estourava");
    }

    [Fact]
    public void OTetoDaPagina_MantemOOffsetDentroDeInt()
    {
        foreach (var tamanho in new[] { 1, 25, 100, 200 })
        {
            var pagina = Paginacao.NormalizarPagina(int.MaxValue, tamanho);
            var offset = (long)(pagina - 1) * tamanho;

            offset.Should().BeLessThanOrEqualTo(int.MaxValue,
                because: $"com tamanhoPagina={tamanho}, o offset da maior página aceita tem de caber no int que o EF usa");
        }
    }

    [Fact]
    public void PaginaValida_NaoEAfetadaPeloTeto()
    {
        Paginacao.NormalizarPagina(3, 25).Should().Be(3);
        Paginacao.NormalizarPagina(0, 25).Should().Be(1, because: "o piso continua valendo");
        Paginacao.NormalizarPagina(-7, 25).Should().Be(1);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ObterAsync(string rota)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, TodasAsRoles);

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }
}
