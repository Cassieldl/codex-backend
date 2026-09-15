using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Substitui o JwtBearer real nos testes de pipeline (AP-BACKEND-011): 401/403 são decisões do
/// middleware de autenticação/autorização, testáveis sem depender de um Keycloak real de
/// verdade — só precisamos de um <see cref="ClaimsPrincipal"/> controlável.
///
/// <list type="bullet">
/// <item>Sem o header <see cref="HeaderRoles"/> → <see cref="AuthenticateResult.NoResult"/> (sem
/// principal autenticado) → <c>[Authorize]</c> devolve <c>401</c>.</item>
/// <item>Com o header presente (mesmo vazio) → autentica com as roles informadas (separadas por
/// vírgula) → <c>[Authorize(Roles = "...")]</c> devolve <c>403</c> se a role não bater, ou deixa
/// passar se bater.</item>
/// </list>
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TesteCodex";
    public const string HeaderRoles = "X-Test-Roles";

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderRoles, out var rolesHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var roles = rolesHeader.ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "teste-usuario") };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identidade = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identidade);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
