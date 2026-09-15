using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Factory de pipeline (AP-BACKEND-011 — factory por teste para isolar estado de middleware):
/// substitui o esquema de autenticação padrão por <see cref="TestAuthHandler"/>, para que testes
/// de 401/403 rodem sem depender de um Keycloak real (assinatura de token JWT não é o que está
/// sendo provado aqui — o que está sendo provado é que a rota tem <c>[Authorize]</c> com a role
/// certa). Nunca usada para testar o wiring do JwtBearer em si — isso é
/// <see cref="JwtRealPipelineTests"/>, sobre <see cref="CodexApiFactoryJwtReal"/>, que mantém o
/// esquema real e só troca a fonte da chave.
///
/// <para>
/// Este ponteiro já apontou para <c>Seguranca/KeycloakRolesTransformationTests.cs</c>, que testa
/// a transformação de claims e <b>nenhuma linha</b> do wiring. Foi o que sustentou, por toda a
/// demanda, a impressão de que a validação de token estava coberta quando não havia um único
/// teste dela — ponteiro errado em comentário custa mais caro que comentário ausente.
/// </para>
/// </summary>
public sealed class CodexApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });
        });
    }
}
