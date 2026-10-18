using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// O 400 de byte NUL na query precisa sair com os headers de CORS: sem eles o navegador esconde a
/// resposta e o frontend vê erro de rede em vez do envelope de erro.
/// </summary>
public sealed class CaractereNuloCorsTests
{
    private const string OrigemPermitida = "https://codex-frontend.teste";

    [Fact]
    public async Task NulNaQueryComOrigemPermitida_Responde400ComEnvelopeEHeaderDeCors()
    {
        using var fabrica = new FabricaComOrigemDeclarada();
        using var cliente = fabrica.CreateClient();

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/v1/busca?q=nf%00se");
        requisicao.Headers.Add("Origin", OrigemPermitida);

        using var resposta = await cliente.SendAsync(requisicao);
        var corpo = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: corpo);
        resposta.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal([OrigemPermitida]);

        var erro = JsonDocument.Parse(corpo).RootElement.GetProperty("error");
        erro.GetProperty("code").GetString().Should().NotBeNullOrWhiteSpace();
        erro.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // Banco inalcançável de propósito: o middleware nega antes de qualquer acesso a dados.
    private sealed class FabricaComOrigemDeclarada : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            // UseSetting: a política de CORS lê builder.Configuration durante o Program.
            builder.UseSetting("Cors:OrigemPermitida", OrigemPermitida);
            builder.UseSetting(
                "ConnectionStrings:Default",
                "Host=127.0.0.1;Port=1;Database=inalcancavel;Username=x;Password=x;Timeout=1;Command Timeout=1");
        }
    }
}
