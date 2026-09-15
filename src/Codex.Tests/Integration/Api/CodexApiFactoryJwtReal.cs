using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Factory que mantém o <b>JwtBearer real</b> de <c>SegurancaExtensoes.AddSeguranca</c> — ao
/// contrário de <see cref="CodexApiFactory"/>, que troca o esquema inteiro por
/// <see cref="TestAuthHandler"/> e portanto nunca exercita uma linha sequer da validação de token.
///
/// <para>
/// <b>Por que existe:</b> a demanda inteira provou 401/403 com um handler sintético. A validação
/// de assinatura, issuer, audience, expiração e algoritmo — as garantias de A07 de
/// <c>rules/security.md</c> — não eram tocadas por teste nenhum: caíram no vão entre o QA (que
/// testa comportamento pelo handler substituto) e a Segurança (que audita código, não execução).
/// </para>
///
/// <para>
/// <b>O que é substituído, e só isso:</b> a <i>fonte</i> da chave pública. Em produção ela vem da
/// descoberta OIDC do Keycloak; aqui vem de um par RSA gerado no processo, porque um teste não
/// pode depender de um Keycloak no ar. Todo o resto de
/// <see cref="TokenValidationParameters"/> — <c>ValidateIssuer</c>, <c>ValidateAudience</c>,
/// <c>ValidAudiences</c>, <c>ValidateLifetime</c>, <c>ValidateIssuerSigningKey</c> e
/// <c>ValidAlgorithms = ["RS256"]</c> — é o objeto que a produção montou, intocado. Se alguém
/// afrouxar uma dessas flags no wiring, estes testes ficam vermelhos.
/// </para>
/// </summary>
public sealed class CodexApiFactoryJwtReal : WebApplicationFactory<Program>
{
    /// <summary>Issuer que o token de teste declara e que a validação passa a exigir.</summary>
    public const string IssuerDeTeste = "https://keycloak-de-teste/realms/codex";

    /// <summary>
    /// Audience aceita — o mesmo <c>codex-backend</c> de <c>Keycloak:ValidAudiences</c> no
    /// <c>appsettings.Development.json</c>, que é a configuração que a produção carrega.
    /// </summary>
    public const string AudienceDeTeste = "codex-backend";

    /// <summary>Par RSA do "Keycloak" de teste: assina os tokens e valida as assinaturas.</summary>
    public static readonly RsaSecurityKey ChaveDoIdp = new(RSA.Create(2048)) { KeyId = "chave-idp-de-teste" };

    /// <summary>
    /// Chave adicional aceita como emissora. Serve a um único teste — o de confusão de algoritmo:
    /// entregando ao servidor a própria chave simétrica do atacante, o que sobra entre ele e um
    /// token válido é <b>exclusivamente</b> a restrição <c>ValidAlgorithms = ["RS256"]</c>. Sem
    /// isso, um HS256 seria recusado por assinatura inválida e o teste passaria sem provar a
    /// restrição de algoritmo.
    ///
    /// <para>
    /// É propriedade <c>init</c>, e não parâmetro de construtor, porque <c>IClassFixture</c> do
    /// xUnit exige que a factory tenha um único construtor público sem parâmetros.
    /// </para>
    /// </summary>
    public SecurityKey? ChaveExtraConfiavel { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // Sem descoberta OIDC: zerar Authority e ConfigurationManager impede o handler de
                // sair buscando jwks_uri em localhost:8080 (que não sobe durante o teste) e o faz
                // usar os TokenValidationParameters como estão.
                options.Authority = null;
                options.MetadataAddress = null!;
                options.ConfigurationManager = null;
                options.RequireHttpsMetadata = false;

                options.TokenValidationParameters.ValidIssuer = IssuerDeTeste;
                options.TokenValidationParameters.IssuerSigningKeys = ChaveExtraConfiavel is null
                    ? [ChaveDoIdp]
                    : [ChaveDoIdp, ChaveExtraConfiavel];
            });
        });
    }
}
