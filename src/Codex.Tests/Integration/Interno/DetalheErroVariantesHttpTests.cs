using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Demanda 008-campos-emissor-nacional (D2) ponta a ponta por HTTP real: o detalhe interno do erro
/// (I18b, <c>GET /api/v1/interno/erros/{id}</c>) precisa trazer <c>variantes</c> — antes do fix,
/// <c>ErroRepositorioInterno.ObterPorIdAsync(rastreada: false, ...)</c> nunca incluía a coleção, o
/// formulário interno abria vazio e o PUT seguinte (<c>SubstituirVariantes</c>) apagava as
/// variantes reais do banco.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class DetalheErroVariantesHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita = "erros-buscar,erros-adicionar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public DetalheErroVariantesHttpTests(PostgresContainerFixture fixture)
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
    public async Task CriarErroComDuasVariantes_DetalheInternoTrazAsDuas()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];
        var variante1 = $"mensagem um {sufixo}";
        var variante2 = $"mensagem dois {sufixo}";

        var (statusCriar, corpoCriado, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", new
        {
            codigo = $"E-{sufixo}",
            origem = nameof(OrigemErro.InvoiCy),
            titulo = $"Erro {sufixo}",
            variantes = new[] { variante1, variante2 },
        });

        statusCriar.Should().Be(HttpStatusCode.Created);
        var id = corpoCriado.GetProperty("data").GetProperty("id").GetInt32();

        var (statusDetalhe, corpoDetalhe, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/erros/{id}");

        statusDetalhe.Should().Be(HttpStatusCode.OK);
        var variantes = corpoDetalhe.GetProperty("data").GetProperty("variantes")
            .EnumerateArray().Select(v => v.GetString()).ToArray();

        variantes.Should().BeEquivalentTo([variante1, variante2],
            because: "rastreada: false não pode significar 'sem variantes' — é o shape que o formulário de edição consome");
    }

    /// <summary>
    /// Controle de contagem (U9 exige ao menos uma variante para QUALQUER origem — nunca lista
    /// vazia): com exatamente UMA variante, o detalhe traz exatamente uma — não a mesma contagem
    /// fixa do outro caso, nem zero.
    /// </summary>
    [Fact]
    public async Task CriarErroComUmaVariante_DetalheInternoTrazExatamenteUma()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];
        var variante = $"mensagem única {sufixo}";

        var (statusCriar, corpoCriado, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", new
        {
            codigo = $"E-{sufixo}",
            origem = nameof(OrigemErro.InvoiCy),
            titulo = $"Erro {sufixo}",
            variantes = new[] { variante },
        });

        statusCriar.Should().Be(HttpStatusCode.Created);
        var id = corpoCriado.GetProperty("data").GetProperty("id").GetInt32();

        var (statusDetalhe, corpoDetalhe, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/erros/{id}");

        statusDetalhe.Should().Be(HttpStatusCode.OK);
        var variantes = corpoDetalhe.GetProperty("data").GetProperty("variantes")
            .EnumerateArray().Select(v => v.GetString()).ToArray();

        variantes.Should().BeEquivalentTo([variante]);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeEscrita);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }
}
