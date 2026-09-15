using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// Junção INT-1: o smoke que percorre a referência cruzada **como o leitor percorre** — pega a
/// `url` que o servidor devolveu e navega nela de verdade.
///
/// <para>
/// Por que existe: até aqui cada lado provava a própria metade. O contrato de P6/P8 tem teste
/// (o campo existe, com o shape certo) e a rota canônica tem teste (a fórmula está certa), mas
/// ninguém tinha provado que **a string entregue ao cliente resolve**. É a diferença entre "o
/// campo `url` está preenchido" e "o link funciona", e é exatamente onde a URL denormalizada
/// morde: o valor é gravado uma vez e continua parecendo válido depois que a rota muda.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class SmokeReferenciaCruzadaTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public SmokeReferenciaCruzadaTests(PostgresContainerFixture fixture)
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
    public async Task DoErro_ParaOArtigo_AUrlEntregueResolveNoArtigoCerto()
    {
        var cenario = await SemearAsync();

        var erro = await ObterDadosAsync($"/api/v1/erros/{cenario.ErroSlug}");
        var referencia = erro.GetProperty("artigosRelacionados").EnumerateArray().Single();
        var url = referencia.GetProperty("url").GetString()!;

        var artigo = await SeguirAsync(url);

        artigo.GetProperty("slug").GetString().Should().Be(cenario.ArtigoSlug,
            because: "o leitor clica no que o servidor mandou — se a URL resolve noutro recurso, o bug é invisível em qualquer teste de shape");
        artigo.GetProperty("titulo").GetString().Should().Be(referencia.GetProperty("titulo").GetString(),
            because: "o título mostrado no bloco e o da página de destino são o mesmo conteúdo; divergir significa que a referência aponta para outro artigo");
    }

    [Fact]
    public async Task DoArtigo_ParaOErro_AUrlEntregueResolveNoErroCerto()
    {
        var cenario = await SemearAsync();

        var artigo = await ObterDadosAsync($"/api/v1/artigos/{cenario.ArtigoSlug}");
        var referencia = artigo.GetProperty("errosRelacionados").EnumerateArray().Single();
        var url = referencia.GetProperty("url").GetString()!;

        var erro = await SeguirAsync(url);

        erro.GetProperty("slug").GetString().Should().Be(cenario.ErroSlug);
        erro.GetProperty("codigo").GetString().Should().Be(referencia.GetProperty("codigo").GetString(),
            because: "fecha o ciclo nos dois sentidos — a ida já tinha teste de shape, a volta não tinha nem isso");
    }

    [Fact]
    public async Task UrlCanonicaDoArtigo_ResolveNoProprioArtigo()
    {
        var cenario = await SemearAsync();

        var artigo = await ObterDadosAsync($"/api/v1/artigos/{cenario.ArtigoSlug}");
        var urlCanonica = artigo.GetProperty("urlCanonica").GetString()!;

        var pelaCanonica = await SeguirAsync(urlCanonica);

        pelaCanonica.GetProperty("slug").GetString().Should().Be(cenario.ArtigoSlug,
            because: "a canônica vai para <link rel=\"canonical\"> e para o sitemap — uma que não resolve é erro que só aparece em indexador");
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>
    /// Converte a URL de navegação (o caminho que o leitor abre) na rota de API do mesmo recurso.
    /// É o único ponto do teste que "sabe" o desenho das rotas; o resto usa o valor cru do wire.
    /// </summary>
    private static string RotaDeApiPara(string urlDeNavegacao) => urlDeNavegacao switch
    {
        var url when url.StartsWith("/artigos/") => $"/api/v1{url}",
        var url when url.StartsWith("/erros/") => $"/api/v1{url}",
        // Artigo reservado: a rota de navegação é "/como-funciona", mas o recurso continua sendo
        // o artigo daquele slug (SlugsReservadosArtigo.RotaCanonica).
        var url when url.StartsWith('/') => $"/api/v1/artigos{url}",
        _ => throw new InvalidOperationException($"URL fora do formato esperado: '{urlDeNavegacao}'."),
    };

    private async Task<JsonElement> SeguirAsync(string urlDeNavegacao) =>
        await ObterDadosAsync(RotaDeApiPara(urlDeNavegacao));

    private async Task<JsonElement> ObterDadosAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: $"'{rota}' precisa resolver");

        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        return corpo.GetProperty("data").Clone();
    }

    private sealed record Cenario(string ErroSlug, string ArtigoSlug);

    private async Task<Cenario> SemearAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        await using var contexto = _fixture.CriarContexto();

        var erro = Erro.Criar($"SMOKE-{sufixo}", OrigemErro.InvoiCy, $"Erro do smoke {sufixo}");
        erro.SubstituirVariantes([$"variante smoke {sufixo}"]);
        erro.Publicar();
        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        var artigo = Artigo.Criar(TipoArtigo.Conceito, $"Artigo do smoke {sufixo}", $"Corpo do smoke {sufixo}.");
        artigo.Publicar();
        contexto.Artigos.Add(artigo);
        await contexto.SaveChangesAsync();

        contexto.ArtigosErros.Add(ArtigoErro.Criar(artigo.Id, erro.Id));
        await contexto.SaveChangesAsync();

        return new Cenario(erro.Slug, artigo.Slug);
    }
}
