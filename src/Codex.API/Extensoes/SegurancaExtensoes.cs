using Codex.API.Seguranca;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Codex.API.Extensoes;

/// <summary>
/// Wiring completo de autenticação/autorização (BACK-4, analise-backend.md §7.1/§7.2): JWT Bearer
/// com validação completa (issuer/audience/lifetime/signing key, algoritmo fixado em RS256 —
/// nunca <c>none</c>) e <see cref="KeycloakRolesTransformation"/> registrada para achatar
/// <c>resource_access.&lt;client&gt;.roles</c> em <see cref="System.Security.Claims.ClaimTypes.Role"/>
/// — sem isso, todo <c>[Authorize(Roles=...)]</c> do repositório é decorativo (AP-SECURITY-003).
/// </summary>
public static class SegurancaExtensoes
{
    public static IServiceCollection AddSeguranca(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment ambiente)
    {
        var clienteRecurso = configuration["Keycloak:resource"] ?? "codex-backend";

        // ValidAudiences explícito: confirmado contra docker/keycloak/realm-codex.json — o
        // client "codex-backend" é bearerOnly (resource server), sem defaultClientScopes/mapper
        // de audience próprios; o token emitido para "codex-front" recebe "codex-backend" em
        // "aud" via o mapper embutido "audience resolve" do client scope padrão "roles" do
        // Keycloak (adiciona automaticamente à audience qualquer client para o qual o usuário
        // tenha roles em resource_access — que é exatamente o caso aqui, ver clientRoles do
        // usuário codex.dev no realm). Configurável via Keycloak:ValidAudiences para ambientes
        // com realm diferente (M4 — realm real da Migrate), com fallback para o próprio resource.
        var validAudiences = configuration.GetSection("Keycloak:ValidAudiences").Get<string[]>()
            is { Length: > 0 } audiencesConfiguradas
                ? audiencesConfiguradas
                : [clienteRecurso];

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var authority = configuration["Keycloak:auth-server-url"];
                var realm = configuration["Keycloak:realm"];

                if (!string.IsNullOrWhiteSpace(authority) && !string.IsNullOrWhiteSpace(realm))
                    options.Authority = $"{authority}/realms/{realm}";

                // Por ambiente — nunca constante (rules/security.md): só Development pode validar
                // metadata OIDC sem HTTPS (Keycloak local roda em http://localhost:8080).
                options.RequireHttpsMetadata = !ambiente.IsDevelopment();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudiences = validAudiences,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = ["RS256"], // nunca "none" — rules/security.md A07.
                };
            });

        // IClaimsTransformation roda a cada request autenticado — é onde a role do Keycloak vira
        // ClaimTypes.Role, único jeito de [Authorize(Roles=...)] enxergar a role real do token.
        services.AddTransient<IClaimsTransformation, KeycloakRolesTransformation>();

        services.AddAuthorization();

        return services;
    }
}
