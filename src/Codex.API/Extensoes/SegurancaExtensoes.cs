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

                // Endereço INTERNO do documento de descoberta, quando a API alcança o Keycloak por
                // um nome que o navegador não conhece (service do cluster, rede privada). O issuer
                // continua sendo o do metadata — ou seja, o público, o mesmo que vem no token.
                //
                // Sem isto, a única saída seria apontar a `Authority` para o endereço interno, e aí
                // o token (emitido com o issuer público) é recusado: **401 em toda rota interna**,
                // com o usuário autenticado na tela e nenhum log dizendo o motivo. Medido no smoke.
                var enderecoMetadata = configuration["Keycloak:MetadataAddress"];
                if (!string.IsNullOrWhiteSpace(enderecoMetadata))
                    options.MetadataAddress = enderecoMetadata;

                // Por ambiente — nunca constante (rules/security.md): só Development pode validar
                // metadata OIDC sem HTTPS (Keycloak local roda em http://localhost:8080). Fora
                // dele, dispensar exige declarar `Keycloak:RequireHttpsMetadata=false`, e a
                // validação de partida recusa subir sem essa declaração: sem ela, a Authority em
                // HTTP derruba TODA requisição com 500, inclusive as sondas de saúde.
                options.RequireHttpsMetadata = !ambiente.IsDevelopment()
                    && !ConfiguracaoSegurancaExtensoes.ExigenciaDeHttpsFoiDispensada(configuration);

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
