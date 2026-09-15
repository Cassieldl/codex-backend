using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Junção INT-1, lado interno: o JSON literal de I21 e o que o separa de P1.
///
/// <para>
/// Por que existe: a busca interna e a pública compartilham serviço, DTO e mapa de rótulos, e
/// diferem em duas coisas que **só aparecem no wire** — a interna enxerga rascunho e marca
/// <c>publicado</c> por item (PQ-09). O cliente interno decide o link a copiar a partir desse
/// campo: copiar a URL pública de um rascunho manda o parceiro para um 404/410.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class WireLiteralBuscaInternaTests : IAsyncLifetime
{
    private const string RoleDeBusca = "indicebusca-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public WireLiteralBuscaInternaTests(PostgresContainerFixture fixture)
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

    [Fact]
    public async Task I21_UsaOsMesmosLiteraisDeGrupoQueP1()
    {
        var cenario = await SemearAsync();

        var grupo = await GrupoDeArtigoInternoAsync(cenario.Termo);

        grupo.GetProperty("tipo").GetString().Should().Be("artigo");
        grupo.GetProperty("rotulo").GetString().Should().Be("Artigos",
            because: "as duas superfícies leem o mesmo mapa de rótulos — divergir aqui significaria que alguém duplicou o mapa");
    }

    [Fact]
    public async Task I21_EnxergaORascunhoQueP1Esconde()
    {
        var cenario = await SemearAsync();

        var titulosInternos = (await ItensDeArtigoInternoAsync(cenario.Termo))
            .Select(item => item.GetProperty("titulo").GetString())
            .ToList();

        titulosInternos.Should().Contain(cenario.TituloPublicado);
        titulosInternos.Should().Contain(cenario.TituloRascunho,
            because: "a busca interna existe para o editor achar o que ainda não publicou");

        using var respostaPublica = await _cliente.GetAsync($"/api/v1/busca?q={cenario.Termo}&tamanhoPagina=50");
        var textoPublico = await respostaPublica.Content.ReadAsStringAsync();

        textoPublico.Should().NotContain(cenario.TituloRascunho,
            because: "o mesmo termo, na superfície pública, não pode revelar o rascunho — é o par que prova a diferença entre as duas");
    }

    [Fact]
    public async Task I21_MarcaPublicadoPorItem_NuncaPorPagina()
    {
        var cenario = await SemearAsync();

        var itens = await ItensDeArtigoInternoAsync(cenario.Termo);

        PublicadoDe(itens, cenario.TituloPublicado).Should().BeTrue();
        PublicadoDe(itens, cenario.TituloRascunho).Should().BeFalse(
            because: "PQ-09 — o cliente escolhe o link a copiar item a item; um flag de página faria o rascunho herdar o estado do vizinho");
    }

    [Fact]
    public async Task I21_VemEnvelopadaEmData_ComoOResto()
    {
        var cenario = await SemearAsync();

        using var resposta = await EnviarInternaAsync($"/api/v1/interno/busca?q={cenario.Termo}&tamanhoPagina=50");
        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        corpo.TryGetProperty("data", out _).Should().BeTrue(
            because: "o corpo real é enveloped, ao contrário do que o ProducesResponseType do controller declara — cliente gerado do Swagger leria o shape errado");
        corpo.TryGetProperty("grupos", out _).Should().BeFalse(
            because: "se o recurso subisse para a raiz, todo cliente que lê .data quebraria de uma vez");
    }

    // ------------------------------------------------------------------ apoio

    private static bool PublicadoDe(IReadOnlyList<JsonElement> itens, string titulo) =>
        itens.First(item => item.GetProperty("titulo").GetString() == titulo)
            .GetProperty("publicado").GetBoolean();

    private async Task<HttpResponseMessage> EnviarInternaAsync(string rota)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RoleDeBusca);
        return await _cliente.SendAsync(requisicao);
    }

    private async Task<JsonElement> GrupoDeArtigoInternoAsync(string termo)
    {
        using var resposta = await EnviarInternaAsync($"/api/v1/interno/busca?q={termo}&tamanhoPagina=50");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var dados = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        var grupo = dados.GetProperty("grupos").EnumerateArray()
            .FirstOrDefault(g => g.GetProperty("tipo").GetString() == "artigo");

        grupo.ValueKind.Should().Be(JsonValueKind.Object, because: "o cenário semeia dois artigos que casam com o termo");
        return grupo.Clone();
    }

    private async Task<IReadOnlyList<JsonElement>> ItensDeArtigoInternoAsync(string termo)
    {
        var grupo = await GrupoDeArtigoInternoAsync(termo);
        return grupo.GetProperty("itens").EnumerateArray().Select(item => item.Clone()).ToList();
    }

    private sealed record Cenario(string Termo, string TituloPublicado, string TituloRascunho);

    private async Task<Cenario> SemearAsync()
    {
        var termo = $"buscainterna{Guid.NewGuid():N}"[..22];
        await using var contexto = _fixture.CriarContexto();

        var publicado = Artigo.Criar(TipoArtigo.Conceito, $"Publicado {termo}", $"Corpo publicado {termo}.");
        publicado.Publicar();

        var rascunho = Artigo.Criar(TipoArtigo.Procedimento, $"Rascunho {termo}", $"Corpo rascunho {termo}.");

        contexto.Artigos.AddRange(publicado, rascunho);
        await contexto.SaveChangesAsync();

        var writer = new IndiceBuscaWriter(contexto);

        foreach (var artigo in new[] { publicado, rascunho })
        {
            await writer.AtualizarPesosAsync(
                TipoItemIndexado.artigo, artigo.Id,
                pesoA: artigo.Titulo, pesoB: null, pesoC: artigo.Corpo, pesoD: artigo.Tipo.ToString(),
                CancellationToken.None);
            await writer.RecalcularVisibilidadeAsync(TipoItemBusca.Artigo, artigo.Id, CancellationToken.None);
        }

        return new Cenario(termo, publicado.Titulo, rascunho.Titulo);
    }
}
