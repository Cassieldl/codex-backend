using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Qa;

/// <summary>
/// SONDA DE QA (002) — bordas de entrada de P7/P8 que a suíte entregue não exercita.
/// Todo caso tem Expect declarado antes de rodar; nenhum pode produzir 500.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class QaBordaEntradaPublicaTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private string _sufixo = null!;

    public QaBordaEntradaPublicaTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contexto = _fixture.CriarContexto();
        for (var i = 1; i <= 3; i++)
        {
            var artigo = Artigo.Criar(TipoArtigo.Conceito, $"Borda {_sufixo} n{i}", $"Corpo {_sufixo} {i}.");
            artigo.Publicar();
            contexto.Artigos.Add(artigo);
        }
        await contexto.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------- paginação

    [Theory]
    // Expect: página não-positiva é normalizada para 1 — 200, nunca 400/500.
    [InlineData("pagina=0")]
    [InlineData("pagina=-5")]
    // Expect: tamanho não-positivo cai no default 25 — 200.
    [InlineData("tamanhoPagina=0")]
    [InlineData("tamanhoPagina=-10")]
    // Expect: tamanho absurdo é capado em 100 — 200.
    [InlineData("tamanhoPagina=100000")]
    [InlineData("tamanhoPagina=2147483647")]
    public async Task P7_ParametroDePaginacaoForaDeFaixa_Retorna200(string query)
    {
        var (status, _) = await ObterAsync($"/api/v1/artigos?{query}&q={_sufixo}");

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task P7_TamanhoPaginaAcimaDoCap_EEcoadoComoCemENuncaDevolveMaisQueIsso()
    {
        var (status, corpo) = await ObterAsync("/api/v1/artigos?tamanhoPagina=100000");

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("pageSize").GetInt32().Should().Be(100, because: "o cap de 100 é o contrato de §4.2");
        corpo.GetProperty("items").GetArrayLength().Should().BeLessThanOrEqualTo(100);
    }

    // ---------------------------------------------------------------- q

    [Theory]
    // Expect: q vazio/em branco equivale a sem filtro — 200.
    [InlineData("q=")]
    [InlineData("q=%20")]
    [InlineData("q=%20%20%20")]
    // Expect: caractere especial é tratado como texto — 200, sem 500.
    [InlineData("q=%27")]
    [InlineData("q=%25")]
    [InlineData("q=_")]
    [InlineData("q=%5C")]
    [InlineData("q=%3Cscript%3E")]
    [InlineData("q=a%27%20OR%20%271%27%3D%271")]
    public async Task P7_QComEntradaHostil_Retorna200(string query)
    {
        var (status, _) = await ObterAsync($"/api/v1/artigos?{query}");

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task P7_QGigante_Retorna200ComListaVazia()
    {
        var termo = new string('a', 5000);

        var (status, corpo) = await ObterAsync($"/api/v1/artigos?q={termo}");

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    // ---------------------------------------------------------------- tipo

    [Theory]
    // Expect: valor fora do enum é 400 (§4.2 aceita explicitamente o 400 do model binding).
    [InlineData("tipo=Invalido")]
    [InlineData("tipo=Conceito&tipo=Invalido")]
    [InlineData("tipo=Mudan%C3%A7a")]
    public async Task P7_TipoInvalido_Retorna400(string query)
    {
        var (status, _) = await ObterAsync($"/api/v1/artigos?{query}");

        status.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Expect: ordinal fora da faixa do enum não pode virar filtro silencioso nem 500.</summary>
    [Fact]
    public async Task P7_TipoComOrdinalForaDaFaixa_NaoRetornaErroDeServidor()
    {
        var (status, _) = await ObterAsync("/api/v1/artigos?tipo=99");

        status.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------- P8 slug

    [Theory]
    // Expect: slug que não casa exatamente -> 404, nunca 500.
    [InlineData("ARTIGO-EM-MAIUSCULA")]
    [InlineData("artigo-com-acentua%C3%A7%C3%A3o")]
    [InlineData("artigo%20com%20espaco")]
    [InlineData("artigo%27aspas")]
    [InlineData("%3Cscript%3Ealert(1)%3C%2Fscript%3E")]
    [InlineData("..%2F..%2Fetc%2Fpasswd")]
    public async Task P8_SlugHostil_NaoRetornaErroDeServidor(string slug)
    {
        var (status, _) = await ObterAsync($"/api/v1/artigos/{slug}");

        ((int)status).Should().BeLessThan(500, because: "slug malformado é 400/404, nunca falha de servidor");
    }

    [Fact]
    public async Task P8_SlugGigante_NaoRetornaErroDeServidor()
    {
        var slug = new string('x', 3000);

        var (status, _) = await ObterAsync($"/api/v1/artigos/{slug}");

        ((int)status).Should().BeLessThan(500);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ObterAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
        return (resposta.StatusCode, corpo);
    }
}
