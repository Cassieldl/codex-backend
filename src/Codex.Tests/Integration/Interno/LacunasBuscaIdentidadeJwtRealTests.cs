using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Quem registrou a lacuna sai de um token VALIDADO de verdade (JwtBearer real, assinatura RS256),
/// não do handler sintético dos demais testes. É a única prova de que <c>sub</c> e <c>name</c> do
/// Keycloak chegam à linha gravada — o mapeamento de claims do JwtBearer decide se o
/// identificador mora em <c>NameIdentifier</c> ou em <c>sub</c>, e nenhum teste de handler vê isso.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class LacunasBuscaIdentidadeJwtRealTests : IAsyncLifetime
{
    private const string Rota = "/api/v1/interno/lacunas-busca";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryJwtReal _factory = null!;

    public LacunasBuscaIdentidadeJwtRealTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new CodexApiFactoryJwtReal { ConnectionString = _fixture.ConnectionString };
        await ExecutarSqlAsync("DELETE FROM codex.lacunasbusca");
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await ExecutarSqlAsync("DELETE FROM codex.lacunasbusca");
    }

    [Fact]
    public async Task SubENameDoToken_ChegamALinhaGravada()
    {
        var token = CriarToken(new Dictionary<string, object>
        {
            ["sub"] = "kc-sub-0001",
            ["name"] = "Maria Souza",
            ["preferred_username"] = "maria.souza",
        });

        var resposta = await RegistrarComTokenAsync(token);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var linha = await LerUnicaLinhaAsync();
        linha.Should().Be(("kc-sub-0001", "Maria Souza"));
    }

    [Fact]
    public async Task SemClaimName_UsaPreferredUsername()
    {
        var token = CriarToken(new Dictionary<string, object>
        {
            ["sub"] = "kc-sub-0002",
            ["preferred_username"] = "joao.lima",
        });

        var resposta = await RegistrarComTokenAsync(token);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        (await LerUnicaLinhaAsync()).Should().Be(("kc-sub-0002", "joao.lima"));
    }

    [Fact]
    public async Task TokenSemSub_Devolve401_ENaoGrava()
    {
        var token = CriarToken(new Dictionary<string, object> { ["name"] = "Sem Sub" }, incluirSub: false);

        var resposta = await RegistrarComTokenAsync(token);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ContarLinhasAsync()).Should().Be(0);
    }

    private async Task<HttpResponseMessage> RegistrarComTokenAsync(string token)
    {
        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, Rota)
        {
            Content = JsonContent.Create(new { termo = "busca sem resultado", registradoPorId = "forjado" }),
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await cliente.SendAsync(requisicao);
    }

    private static string CriarToken(Dictionary<string, object> claims, bool incluirSub = true)
    {
        var todas = new Dictionary<string, object>(claims)
        {
            ["resource_access"] = new Dictionary<string, object>
            {
                [CodexApiFactoryJwtReal.AudienceDeTeste] = new Dictionary<string, object>
                {
                    ["roles"] = new[] { "lacunasbusca-adicionar" },
                },
            },
        };

        if (!incluirSub)
            todas.Remove("sub");

        var agora = DateTime.UtcNow;
        var descritor = new SecurityTokenDescriptor
        {
            Issuer = CodexApiFactoryJwtReal.IssuerDeTeste,
            Audience = CodexApiFactoryJwtReal.AudienceDeTeste,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = agora.AddMinutes(5),
            Claims = todas,
            SigningCredentials = new SigningCredentials(CodexApiFactoryJwtReal.ChaveDoIdp, SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descritor);
    }

    private async Task<(string Id, string? Nome)> LerUnicaLinhaAsync()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT registradoporid, registradopornome FROM codex.lacunasbusca";

        await using var leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue();
        var linha = (leitor.GetString(0), leitor.IsDBNull(1) ? null : leitor.GetString(1));
        (await leitor.ReadAsync()).Should().BeFalse(because: "uma requisição, uma linha");

        return linha;
    }

    private async Task<long> ContarLinhasAsync()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT count(*) FROM codex.lacunasbusca";
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    private async Task ExecutarSqlAsync(string sql)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }
}
