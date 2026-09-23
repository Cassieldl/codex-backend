using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Sondas do orquestrador (004). O que importa provar aqui é a <b>separação</b>: com o banco fora
/// do ar, <c>vivo</c> continua <c>200</c> e <c>pronto</c> vira <c>503</c>. Se as duas caíssem
/// juntas, o orquestrador mataria e recriaria a instância em laço por causa de uma dependência
/// externa — o processo está perfeitamente vivo.
/// </summary>
public sealed class SaudeHttpTests : IClassFixture<PostgresContainerFixture>
{
    private const string RotaVivo = "/api/v1/saude/vivo";
    private const string RotaPronto = "/api/v1/saude/pronto";

    private readonly PostgresContainerFixture _postgres;

    public SaudeHttpTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task Vivo_RespondeSemToken_ESemTocarOBanco()
    {
        using var fabrica = new FabricaSemBancoAlcancavel();
        using var cliente = fabrica.CreateClient();

        using var resposta = await cliente.GetAsync(RotaVivo);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "liveness não depende de dependência externa — banco fora do ar não é motivo para reiniciar o processo");

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("status").GetString().Should().Be("saudavel");
    }

    [Fact]
    public async Task Pronto_ComBancoInalcancavel_Devolve503()
    {
        using var fabrica = new FabricaSemBancoAlcancavel();
        using var cliente = fabrica.CreateClient();

        using var resposta = await cliente.GetAsync(RotaPronto);

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
            because: "readiness existe para tirar do balanceador a instância que não consegue servir");
    }

    /// <summary>
    /// O corpo da sonda não é canal de diagnóstico (D4 do PRD): nome de dependência, host,
    /// exceção e stack ficam no log do servidor. A asserção é de AUSÊNCIA de padrão, replicada
    /// nos dois caminhos — asserir só o `status` não pegaria vazamento no resto do corpo.
    /// </summary>
    [Fact]
    public async Task Pronto_QuandoFalha_NaoVazaDetalheInterno()
    {
        using var fabrica = new FabricaSemBancoAlcancavel();
        using var cliente = fabrica.CreateClient();

        using var resposta = await cliente.GetAsync(RotaPronto);
        var corpoCru = await resposta.Content.ReadAsStringAsync();

        corpoCru.Should().NotContain("banco", because: "o nome do check é detalhe interno");
        corpoCru.Should().NotContain("Npgsql");
        corpoCru.Should().NotContain("inalcancavel", because: "o host do banco nunca vai na resposta");
        corpoCru.Should().NotContain("at Codex.");
    }

    [Fact]
    public async Task Sondas_NuncaSaoCacheadas()
    {
        using var fabrica = new FabricaSemBancoAlcancavel();
        using var cliente = fabrica.CreateClient();

        foreach (var rota in new[] { RotaVivo, RotaPronto })
        {
            using var resposta = await cliente.GetAsync(rota);

            // Nunca `CacheControl?.NoStore.Should()`: com o header AUSENTE — o defeito que este
            // teste existe para pegar — o `?.` curto-circuita a cadeia inteira e a asserção não
            // chega a rodar. A ausência é asseverada primeiro, e sozinha.
            resposta.Headers.CacheControl.Should().NotBeNull(
                because: $"{rota} informa o estado de AGORA — um 'saudável' servido de cache descreve a instância que já caiu");
            resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Pronto_ComBancoDePe_Devolve200()
    {
        await using var fabrica = new CodexApiFactoryComBanco(_postgres.ConnectionString);
        using var cliente = fabrica.CreateClient();

        using var resposta = await cliente.GetAsync(RotaPronto);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Connection string apontando para porta fechada, com timeout curto: o `503` chega em
    /// segundos em vez do default do Npgsql.
    /// </summary>
    private sealed class FabricaSemBancoAlcancavel : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configuracao) =>
            {
                configuracao.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] =
                        "Host=127.0.0.1;Port=1;Database=inalcancavel;Username=x;Password=x;Timeout=1;Command Timeout=1",
                });
            });
        }
    }
}
