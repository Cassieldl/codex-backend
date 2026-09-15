using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// Junção INT-1: o **JSON literal** de P1 para o tipo `artigo`, comparado byte a byte contra as
/// strings que o cliente declara.
///
/// <para>
/// Por que existe: os rótulos de grupo e de item de artigo estavam em produção
/// (<see cref="ConsultaBuscaPostgres"/>) e os testes que citam os literais consultam a busca
/// direto, sem atravessar o pipeline HTTP. Cada lado provou a própria metade — o backend que o
/// mapa tem a entrada, o frontend que a união de tipos casa — e ninguém provou que a string que
/// sai no wire é a mesma que entra no cliente. É a classe de defeito que mais custou nesta
/// demanda.
/// </para>
///
/// <para>Decisão M-5: o valor de contrato é <c>Mudanca</c> em ASCII; "Mudança" com cedilha é
/// rótulo de exibição e vive só no cliente.</para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class WireLiteralBuscaArtigoTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public WireLiteralBuscaArtigoTests(PostgresContainerFixture fixture)
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
    public async Task GrupoDeArtigo_TemOsLiteraisExatosDoContrato()
    {
        var termo = await SemearArtigoPublicadoAsync(TipoArtigo.Conceito);

        var grupo = await GrupoDeArtigoAsync(termo);

        grupo.GetProperty("tipo").GetString().Should().Be("artigo",
            because: "o cliente casa o grupo por esta string; 'artigos' no plural ou 'Artigo' com maiúscula quebra o agrupamento sem erro de compilação dos dois lados");
        grupo.GetProperty("rotulo").GetString().Should().Be("Artigos",
            because: "é o cabeçalho que o leitor vê na tela 2 — vem do servidor, não do cliente");
    }

    [Fact]
    public async Task ItemDeArtigo_TemORotuloDaReferenciaCruzada()
    {
        var termo = await SemearArtigoPublicadoAsync(TipoArtigo.Procedimento);

        var item = await PrimeiroItemDeArtigoAsync(termo);

        item.GetProperty("rotulo").GetString().Should().Be("ARTIGO",
            because: "mesma família de REJEIÇÃO e CAMPO DO LAYOUT — maiúsculas, sem acento neste caso, e o cliente exibe cru");
    }

    [Fact]
    public async Task TipoMudanca_AtravessaOWireEmAscii_SemCedilha()
    {
        var termo = await SemearArtigoPublicadoAsync(TipoArtigo.Mudanca);

        using var resposta = await _cliente.GetAsync($"/api/v1/artigos?q={termo}&tamanhoPagina=50");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        texto.Should().Contain("\"Mudanca\"",
            because: "M-5 fixou o valor de contrato em ASCII — a cedilha vive só no rótulo de exibição do cliente");
        texto.Should().NotContain("Mudança",
            because: "se a cedilha vazar para o wire, a união de literais do TypeScript deixa de casar e o filtro da tela 7 para de achar o tipo");
    }

    [Fact]
    public async Task FiltroPorMudanca_UsaOMesmoLiteralQueOWireDevolve()
    {
        var termo = await SemearArtigoPublicadoAsync(TipoArtigo.Mudanca);

        using var resposta = await _cliente.GetAsync($"/api/v1/artigos?tipo=Mudanca&q={termo}&tamanhoPagina=50");
        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("items").GetArrayLength().Should().Be(1,
            because: "o valor que o servidor devolve tem de ser aceito de volta como filtro — ida e volta com a mesma string é o que fecha o contrato");
    }

    // ------------------------------------------------------------------ apoio

    private async Task<JsonElement> GrupoDeArtigoAsync(string termo)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/busca?q={termo}&tamanhoPagina=50");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var dados = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        var grupo = dados.GetProperty("grupos").EnumerateArray()
            .FirstOrDefault(g => g.GetProperty("tipo").GetString() == "artigo");

        grupo.ValueKind.Should().Be(JsonValueKind.Object, because: "o cenário publica um artigo que casa com o termo");
        return grupo.Clone();
    }

    private async Task<JsonElement> PrimeiroItemDeArtigoAsync(string termo)
    {
        var grupo = await GrupoDeArtigoAsync(termo);
        return grupo.GetProperty("itens").EnumerateArray().First().Clone();
    }

    /// <summary>Semeia um artigo publicado e indexado, devolvendo o termo exclusivo dele.</summary>
    private async Task<string> SemearArtigoPublicadoAsync(TipoArtigo tipo)
    {
        var termo = $"wireliteral{Guid.NewGuid():N}"[..20];
        await using var contexto = _fixture.CriarContexto();

        var artigo = Artigo.Criar(tipo, $"Titulo {termo}", $"Corpo do {termo}.");
        artigo.Publicar();
        contexto.Artigos.Add(artigo);
        await contexto.SaveChangesAsync();

        var writer = new IndiceBuscaWriter(contexto);
        await writer.AtualizarPesosAsync(
            TipoItemIndexado.artigo, artigo.Id,
            pesoA: artigo.Titulo, pesoB: null, pesoC: artigo.Corpo, pesoD: artigo.Tipo.ToString(),
            CancellationToken.None);
        await writer.RecalcularVisibilidadeAsync(TipoItemBusca.Artigo, artigo.Id, CancellationToken.None);

        return termo;
    }
}
