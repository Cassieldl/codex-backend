using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Filtro <c>q</c> das listagens internas por HTTP real: o termo acha o título com ou sem acento e
/// em qualquer caixa. Cada cenário semeia um título acentuado e outro sem acento, então a asserção
/// cai se o acento for ignorado só de um dos lados (coluna ou termo).
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class BuscaSemAcentoInternaHttpTests : IAsyncLifetime
{
    private const string RolesDeLeitura = "erros-buscar,artigos-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public BuscaSemAcentoInternaHttpTests(PostgresContainerFixture fixture)
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
    [InlineData("aliquota")]
    [InlineData("ALIQUOTA")]
    [InlineData("alíquota")]
    public async Task Erros_TermoComOuSemAcentoEmQualquerCaixa_AchaOsDoisTitulos(string termo)
    {
        var sufixo = NovoSufixo();

        await using (var contexto = _fixture.CriarContexto())
        {
            contexto.Erros.Add(Erro.Criar($"AC1{sufixo}", OrigemErro.Schema, $"Alíquota {sufixo} do ISS"));
            contexto.Erros.Add(Erro.Criar($"AC2{sufixo}", OrigemErro.Schema, $"Aliquota {sufixo} do IBS"));
            await contexto.SaveChangesAsync();
        }

        var (status, corpo) = await ListarAsync($"/api/v1/interno/erros?q={Uri.EscapeDataString($"{termo} {sufixo}")}");

        status.Should().Be(HttpStatusCode.OK);
        Titulos(corpo).Should().BeEquivalentTo([$"Alíquota {sufixo} do ISS", $"Aliquota {sufixo} do IBS"]);
    }

    [Fact]
    public async Task Artigos_TermoSemAcento_AchaTituloAcentuado()
    {
        var sufixo = NovoSufixo();

        await using (var contexto = _fixture.CriarContexto())
        {
            contexto.Artigos.Add(Artigo.Criar(TipoArtigo.Conceito, $"Emissão {sufixo} da nota", "Corpo."));
            contexto.Artigos.Add(Artigo.Criar(TipoArtigo.Conceito, $"Emissao {sufixo} da guia", "Corpo."));
            await contexto.SaveChangesAsync();
        }

        var (status, corpo) = await ListarAsync($"/api/v1/interno/artigos?q={Uri.EscapeDataString($"EMISSAO {sufixo}")}");

        status.Should().Be(HttpStatusCode.OK);
        Titulos(corpo).Should().BeEquivalentTo([$"Emissão {sufixo} da nota", $"Emissao {sufixo} da guia"]);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ListarAsync(string rota)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeLeitura);

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone());
    }

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static List<string?> Titulos(JsonElement corpo) =>
        [.. corpo.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("titulo").GetString())];
}
