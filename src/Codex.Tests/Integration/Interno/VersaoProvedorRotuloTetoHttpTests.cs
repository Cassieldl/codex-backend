using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// <c>versoesprovedor.rotulo</c> é <c>varchar(80)</c>
/// (<c>VersaoProvedorConfiguration.cs</c>), mas os validators aceitavam até 160 — um rótulo de
/// 81 a 160 caracteres passava na validação e morria em <c>22001</c> no banco (<c>500 ERRO_INTERNO</c>
/// genérico em vez de <c>422</c>). O teste de fronteira usa o LITERAL (80/81), nunca a constante
/// do validator (rules/tests.md) — copiar o número do código testaria a própria afirmação, não o
/// contrato real com a coluna.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class VersaoProvedorRotuloTetoHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita =
        "provedores-buscar,provedores-adicionar,versoesprovedor-buscar,versoesprovedor-adicionar,versoesprovedor-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public VersaoProvedorRotuloTetoHttpTests(PostgresContainerFixture fixture)
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
    public async Task CriarVersao_ComRotuloDe80Caracteres_Aceita()
    {
        var provedorId = await CriarProvedorAsync();
        var versaoLayoutId = await CriarVersaoLayoutAsync();

        var (status, _, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{provedorId}/versoes", new
        {
            versaoLayoutId,
            rotulo = new string('a', 80),
        });

        status.Should().Be(HttpStatusCode.Created, because: "80 é exatamente o teto da coluna — tem de caber");
    }

    [Fact]
    public async Task CriarVersao_ComRotuloDe81Caracteres_Devolve422NuncaCrashaNoBanco()
    {
        var provedorId = await CriarProvedorAsync();
        var versaoLayoutId = await CriarVersaoLayoutAsync();

        var (status, _, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{provedorId}/versoes", new
        {
            versaoLayoutId,
            rotulo = new string('a', 81),
        });

        status.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "81 não cabe em varchar(80) — a validação tem de recusar ANTES do banco, não deixar estourar 22001/500");
    }

    [Fact]
    public async Task AtualizarVersao_ComRotuloDe80Caracteres_Aceita()
    {
        var provedorId = await CriarProvedorAsync();
        var versaoLayoutId = await CriarVersaoLayoutAsync();
        var versao = await CriarVersaoAsync(provedorId, versaoLayoutId);

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}", new { rotulo = new string('b', 80) }, versao.Versao);

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AtualizarVersao_ComRotuloDe81Caracteres_Devolve422NuncaCrashaNoBanco()
    {
        var provedorId = await CriarProvedorAsync();
        var versaoLayoutId = await CriarVersaoLayoutAsync();
        var versao = await CriarVersaoAsync(provedorId, versaoLayoutId);

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}", new { rotulo = new string('b', 81) }, versao.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record VersaoCriada(int Id, uint Versao);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private async Task<int> CriarVersaoLayoutAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout teto rotulo {NovoSufixo()}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        return versaoLayout.Id;
    }

    private async Task<int> CriarProvedorAsync()
    {
        var sufixo = NovoSufixo();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome = $"Provedor teto rotulo {sufixo}",
            identificadorInvoicy = $"INV-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases = Array.Empty<string>(),
        });

        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<VersaoCriada> CriarVersaoAsync(int provedorId, int versaoLayoutId)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{provedorId}/versoes", new
        {
            versaoLayoutId,
            rotulo = $"Versao teto rotulo {NovoSufixo()}",
        });

        status.Should().Be(HttpStatusCode.Created);
        var criada = corpo.GetProperty("data");

        return new VersaoCriada(criada.GetProperty("id").GetInt32(), criada.GetProperty("versaoConcorrencia").GetUInt32());
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
