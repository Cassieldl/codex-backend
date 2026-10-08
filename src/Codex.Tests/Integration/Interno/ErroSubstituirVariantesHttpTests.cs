using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Complementa <c>ErroVariantesRoundTripHttpTests</c> (que prova o
/// caso "mesmas variantes" não crasha mais) com o caso que troca o CONJUNTO: menos variantes, e
/// nenhuma repetida. Confirma não só que o PUT não derruba a requisição, mas que o banco fica com
/// EXATAMENTE o conjunto novo — sem as antigas órfãs (<c>ErroRepositorioInterno.RemoverVariantesAsync</c>
/// marca as linhas antigas como <c>Deleted</c> explicitamente antes de <c>SubstituirVariantes</c>
/// recriar a coleção).
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ErroSubstituirVariantesHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita = "erros-buscar,erros-adicionar,erros-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ErroSubstituirVariantesHttpTests(PostgresContainerFixture fixture)
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
    public async Task Put_ComConjuntoDeVariantesMenorEDiferente_SubstituiSemDeixarOrfas()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        var (statusCriar, corpoCriado, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", new
        {
            codigo = $"E-{sufixo}",
            origem = nameof(OrigemErro.InvoiCy),
            titulo = $"Erro {sufixo}",
            variantes = new[] { $"velha um {sufixo}", $"velha dois {sufixo}" },
        });
        statusCriar.Should().Be(HttpStatusCode.Created);
        var id = corpoCriado.GetProperty("data").GetProperty("id").GetInt32();
        var versao = corpoCriado.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

        var varianteNova = $"nova unica {sufixo}";
        var (statusPut, corpoPut, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/erros/{id}",
            new
            {
                origem = nameof(OrigemErro.InvoiCy),
                titulo = $"Erro editado {sufixo}",
                variantes = new[] { varianteNova },
                versoesProvedorIds = Array.Empty<int>(),
                camposLayoutIds = Array.Empty<int>(),
            },
            versao);

        statusPut.Should().Be(HttpStatusCode.OK, corpoPut.ToString());

        var (statusGet, corpoGet, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/erros/{id}");
        statusGet.Should().Be(HttpStatusCode.OK);
        var variantesNoGet = corpoGet.GetProperty("data").GetProperty("variantes")
            .EnumerateArray().Select(v => v.GetString()).ToArray();
        variantesNoGet.Should().BeEquivalentTo([varianteNova]);

        await using var contexto = _fixture.CriarContexto();
        var variantesNoBanco = await contexto.VariantesMensagem
            .Where(v => v.ErroId == id && v.DesativadoEm == null)
            .Select(v => v.Texto)
            .ToArrayAsync();

        variantesNoBanco.Should().BeEquivalentTo([varianteNova],
            because: "as duas variantes antigas têm de ser removidas de verdade — não só ocultas do GET");
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeEscrita);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }
}
