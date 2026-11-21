using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// A resposta não revela o servidor (header <c>Server</c>) nem o motivo exato da recusa do token
/// (<c>error_description</c> no <c>WWW-Authenticate</c>), que serve de oráculo para quem forja token.
///
/// <para>
/// Roda em Kestrel de verdade: o <c>TestServer</c> nunca emite <c>Server</c>, e o teste passaria
/// sem provar nada. Roda em Production porque o detalhe do erro fica ligado só em Development.
/// </para>
/// </summary>
public sealed class RespostaSemFingerprintEmProducaoTests : IClassFixture<RespostaSemFingerprintEmProducaoTests.FabricaProducaoKestrel>
{
    private const string RotaProtegida = "/api/v1/interno/provedores";
    private const string RotaVivo = "/api/v1/saude/vivo";

    private readonly FabricaProducaoKestrel _fabrica;

    public RespostaSemFingerprintEmProducaoTests(FabricaProducaoKestrel fabrica)
    {
        _fabrica = fabrica;
    }

    [Fact]
    public async Task RespostaDoKestrel_NaoTemHeaderServer()
    {
        using var cliente = _fabrica.CreateClient();

        using var resposta = await cliente.GetAsync(RotaVivo);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Headers.Server.Should().BeEmpty(because: "o header Server só ajuda no fingerprinting do servidor");
    }

    [Fact]
    public async Task TokenComAssinaturaForjada_Responde401SemErrorDescription()
    {
        using var cliente = _fabrica.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaProtegida);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenAssinadoPorChaveDesconhecida());

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resposta.Headers.WwwAuthenticate.Should().ContainSingle()
            .Which.ToString().Should().NotContain("error_description",
                because: "o motivo exato da recusa ensina ao atacante qual parte do token forjado ajustar");
    }

    private static string TokenAssinadoPorChaveDesconhecida()
    {
        var chaveDoAtacante = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "chave-do-atacante" };

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = CodexApiFactoryJwtReal.IssuerDeTeste,
            Audience = CodexApiFactoryJwtReal.AudienceDeTeste,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(chaveDoAtacante, SecurityAlgorithms.RsaSha256),
        });
    }

    /// <summary>
    /// Production com a configuração mínima que passa no fail-fast de partida. A fonte da chave do
    /// JwtBearer é trocada como em <see cref="CodexApiFactoryJwtReal"/>, para não depender de um
    /// Keycloak no ar; o resto da configuração de produção fica intocado.
    /// </summary>
    public sealed class FabricaProducaoKestrel : WebApplicationFactory<Program>
    {
        public FabricaProducaoKestrel()
        {
            UseKestrel(0);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Production);

            // UseSetting, e não ConfigureAppConfiguration: o fail-fast lê a configuração no início
            // do Program, antes do Build.
            builder.UseSetting("Keycloak:auth-server-url", "https://keycloak-de-teste");
            builder.UseSetting("Keycloak:realm", "codex");
            builder.UseSetting("AllowedHosts", "localhost;127.0.0.1;[::1]");
            builder.UseSetting("Cors:OrigemPermitida", "https://codex.exemplo.com.br");
            builder.UseSetting(
                "ConnectionStrings:Default",
                "Host=127.0.0.1;Port=1;Database=inalcancavel;Username=x;Password=x;Timeout=1;Command Timeout=1");

            builder.ConfigureServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = null;
                    options.MetadataAddress = null!;
                    options.ConfigurationManager = null;
                    options.TokenValidationParameters.ValidIssuer = CodexApiFactoryJwtReal.IssuerDeTeste;
                    options.TokenValidationParameters.IssuerSigningKeys = [CodexApiFactoryJwtReal.ChaveDoIdp];
                });
            });
        }
    }
}
