using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Qa;

/// <summary>
/// ISSUE-3 do QA da 002: <c>%</c> e <c>_</c> digitados pelo usuário chegavam ao Postgres como
/// **operadores** de <c>ILIKE</c>, não como texto.
///
/// <para>
/// Não era injeção de SQL — a query sempre foi parametrizada —, era resultado errado: buscar
/// "50%" devolvia o acervo inteiro em vez dos títulos que contêm "50%", e <c>q=%</c> varria a
/// tabela toda de graça numa rota anônima.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class BuscaTrataCuringaComoTextoTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public BuscaTrataCuringaComoTextoTests(PostgresContainerFixture fixture)
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
    public async Task PorcentagemNoTermo_CasaOTituloQueAContem_ENaoOResto()
    {
        var cenario = await SemearAsync();

        var comDesconto = await SlugsAsync($"{cenario.Sufixo} 50%");

        comDesconto.Should().ContainSingle(because: "o termo é texto: só o título que contém '50%' casa")
            .Which.Should().Be(cenario.SlugComPorcentagem);
    }

    [Fact]
    public async Task PorcentagemNoMeio_NaoViraCuringa()
    {
        var cenario = await SemearAsync();

        var slugs = await SlugsAsync($"{cenario.Sufixo} 5%0");

        slugs.Should().BeEmpty(
            because: "'5%0' como curinga casaria 'Desconto 50%' — como texto literal não casa nada, que é o comportamento certo");
    }

    [Fact]
    public async Task TermoQueESoUmCuringa_NaoDevolveOAcervoInteiro()
    {
        var cenario = await SemearAsync();

        var porPorcentagem = await SlugsAsync("%");
        var porSublinhado = await SlugsAsync("_");

        porPorcentagem.Should().NotContain(cenario.SlugSimples,
            because: "q=% varrendo o acervo inteiro é resultado errado e ainda é varredura gratuita numa rota anônima");
        porSublinhado.Should().NotContain(cenario.SlugSimples);
    }

    [Fact]
    public async Task SublinhadoNoTermo_ECaractereLiteral()
    {
        var cenario = await SemearAsync();

        var slugs = await SlugsAsync($"{cenario.Sufixo} campo_nome");

        slugs.Should().ContainSingle().Which.Should().Be(cenario.SlugComSublinhado,
            because: "'campo_nome' com o sublinhado como curinga casaria também 'campoXnome' — aqui só o literal vale");
    }

    // ------------------------------------------------------------------ apoio

    private async Task<IReadOnlyList<string>> SlugsAsync(string termo)
    {
        using var resposta = await _cliente.GetAsync(
            $"/api/v1/artigos?q={Uri.EscapeDataString(termo)}&tamanhoPagina=100");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        return corpo.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("slug").GetString()!)
            .ToList();
    }

    private sealed record Cenario(string Sufixo, string SlugComPorcentagem, string SlugComSublinhado, string SlugSimples);

    private async Task<Cenario> SemearAsync()
    {
        var sufixo = $"curinga{Guid.NewGuid():N}"[..14];
        await using var contexto = _fixture.CriarContexto();

        var comPorcentagem = Artigo.Criar(TipoArtigo.Conceito, $"{sufixo} 50% de desconto", $"Corpo {sufixo} a.");
        comPorcentagem.Publicar();

        var comSublinhado = Artigo.Criar(TipoArtigo.Conceito, $"{sufixo} campo_nome do layout", $"Corpo {sufixo} b.");
        comSublinhado.Publicar();

        var simples = Artigo.Criar(TipoArtigo.Conceito, $"{sufixo} titulo sem simbolo", $"Corpo {sufixo} c.");
        simples.Publicar();

        contexto.Artigos.AddRange(comPorcentagem, comSublinhado, simples);
        await contexto.SaveChangesAsync();

        return new Cenario(sufixo, comPorcentagem.Slug, comSublinhado.Slug, simples.Slug);
    }
}
