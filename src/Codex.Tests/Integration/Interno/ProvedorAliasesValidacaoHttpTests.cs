using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// R6 (demanda 008) — alias com mais de 160 caracteres (<c>varchar(160)</c>) e alias repetido na
/// lista (<c>uq_codex_aliasesprovedor_provedorid_alias</c>) tinham de dar 422 em criar e atualizar,
/// em vez de estourar no banco. Os tetos são LITERAIS (rules/tests.md): 160 passa, 161 reprova.
///
/// <para>
/// A regra de duplicata recusa exatamente o que o índice único recusa — comparação exata, sem
/// ignorar caixa nem espaço nas pontas — então "ADN"/"adn" e "ADN"/" ADN" são ACEITOS.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ProvedorAliasesValidacaoHttpTests : IAsyncLifetime
{
    private const string Roles = "provedores-buscar,provedores-adicionar,provedores-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ProvedorAliasesValidacaoHttpTests(PostgresContainerFixture fixture)
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

    // ------------------------------------------------------------------ criar

    [Fact]
    public async Task Criar_ComAliasDe160Caracteres_Aceita()
    {
        var (status, corpo) = await CriarAsync([new string('a', 160)]);

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
    }

    [Fact]
    public async Task Criar_ComAliasDe161Caracteres_Devolve422()
    {
        var (status, _) = await CriarAsync([new string('a', 161)]);

        status.Should().Be(HttpStatusCode.UnprocessableEntity, because: "161 não cabe em varchar(160)");
    }

    [Fact]
    public async Task Criar_ComAliasRepetido_Devolve422()
    {
        var (status, corpo) = await CriarAsync(["ADN", "Sefin", "ADN"]);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        corpo.ToString().Should().Contain("ADN").And.Contain("mais de uma vez");
    }

    [Fact]
    public async Task Criar_ComAliasesQueSoDiferemPorCaixaOuEspaco_AceitaPoisOBancoAceita()
    {
        var (status, corpo) = await CriarAsync(["ADN", "adn", " ADN"]);

        status.Should().Be(HttpStatusCode.Created,
            because: "o índice único compara exato — recusar mais que o banco seria regra inventada: " + corpo);
    }

    // ------------------------------------------------------------------ atualizar

    [Fact]
    public async Task Atualizar_ComAliasDe160Caracteres_Aceita()
    {
        var provedor = await CriarExistenteAsync();

        var (status, corpo) = await AtualizarAsync(provedor, [new string('b', 160)]);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        (await AliasesNoBancoAsync(provedor.Id)).Should().ContainSingle().Which.Length.Should().Be(160);
    }

    [Fact]
    public async Task Atualizar_ComAliasDe161Caracteres_Devolve422EMantemOsAliasesAtuais()
    {
        var provedor = await CriarExistenteAsync();

        var (status, _) = await AtualizarAsync(provedor, [new string('b', 161)]);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await AliasesNoBancoAsync(provedor.Id)).Should().BeEquivalentTo(["ADN"]);
    }

    [Fact]
    public async Task Atualizar_ComAliasRepetido_Devolve422EMantemOsAliasesAtuais()
    {
        var provedor = await CriarExistenteAsync();

        var (status, _) = await AtualizarAsync(provedor, ["Sefin", "Sefin"]);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await AliasesNoBancoAsync(provedor.Id)).Should().BeEquivalentTo(["ADN"]);
    }

    [Fact]
    public async Task Atualizar_ComAliasesQueSoDiferemPorCaixa_Aceita()
    {
        var provedor = await CriarExistenteAsync();

        var (status, corpo) = await AtualizarAsync(provedor, ["Sefin", "SEFIN"]);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        (await AliasesNoBancoAsync(provedor.Id)).Should().BeEquivalentTo(["Sefin", "SEFIN"]);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record ProvedorCriado(int Id, string Nome, uint Versao);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> CriarAsync(string[] aliases)
    {
        var sufixo = NovoSufixo();

        return await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome = $"Provedor validacao alias {sufixo}",
            identificadorInvoicy = $"INV-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases,
        });
    }

    private async Task<ProvedorCriado> CriarExistenteAsync()
    {
        var sufixo = NovoSufixo();
        var nome = $"Provedor validacao alias {sufixo}";

        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome,
            identificadorInvoicy = $"INV-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases = new[] { "ADN" },
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        var criado = corpo.GetProperty("data");

        return new ProvedorCriado(criado.GetProperty("id").GetInt32(), nome, criado.GetProperty("versaoConcorrencia").GetUInt32());
    }

    private Task<(HttpStatusCode Status, JsonElement Corpo)> AtualizarAsync(ProvedorCriado provedor, string[] aliases) =>
        EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}",
            new { nome = provedor.Nome, categoria = nameof(CategoriaProvedor.Nacional), aliases },
            provedor.Versao);

    private async Task<string[]> AliasesNoBancoAsync(int provedorId)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.AliasesProvedor.AsNoTracking()
            .Where(a => a.ProvedorId == provedorId)
            .Select(a => a.Alias)
            .ToArrayAsync();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, Roles);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }
}
