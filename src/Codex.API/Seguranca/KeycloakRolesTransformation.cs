using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Codex.API.Seguranca;

/// <summary>
/// Achata <c>resource_access.&lt;client&gt;.roles</c> (claim customizado do Keycloak) em
/// <see cref="ClaimTypes.Role"/> — sem isso, todo <c>[Authorize(Roles = "...")]</c> deste
/// repositório reprova com <c>403</c> mesmo com a role certa no token (analise-backend.md §7.2).
/// Nome do client vem de <c>Keycloak:resource</c> (mesmo valor usado como <c>ValidAudiences</c>
/// em <see cref="Codex.API.Extensoes.SegurancaExtensoes"/> — o resource server só recebe roles do
/// PRÓPRIO client no token, nunca de outro client do mesmo realm).
///
/// <para>
/// Idempotente por construção: adicionar a mesma role duas vezes ao mesmo <see cref="ClaimsIdentity"/>
/// não duplica a claim (checagem via <see cref="HashSet{T}"/> antes do <c>AddClaim</c>) — importante
/// porque <see cref="IClaimsTransformation"/> pode rodar mais de uma vez por request no pipeline
/// do ASP.NET Core.
/// </para>
/// </summary>
public sealed class KeycloakRolesTransformation : IClaimsTransformation
{
    private const string ClaimResourceAccess = "resource_access";
    private const string PropriedadeRoles = "roles";

    private readonly string _clienteRecurso;

    public KeycloakRolesTransformation(IConfiguration configuracao)
    {
        _clienteRecurso = configuracao["Keycloak:resource"] ?? "codex-backend";
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is not ClaimsIdentity identidade || !identidade.IsAuthenticated)
            return Task.FromResult(principal);

        var resourceAccessJson = principal.FindFirst(ClaimResourceAccess)?.Value;
        if (string.IsNullOrWhiteSpace(resourceAccessJson))
            return Task.FromResult(principal);

        var roles = ExtrairRoles(resourceAccessJson, _clienteRecurso);
        if (roles.Count == 0)
            return Task.FromResult(principal);

        var rolesJaPresentes = identidade.FindAll(identidade.RoleClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var role in roles)
        {
            if (rolesJaPresentes.Add(role))
                identidade.AddClaim(new Claim(identidade.RoleClaimType, role));
        }

        return Task.FromResult(principal);
    }

    /// <summary>
    /// Parse manual e defensivo de <c>resource_access</c> — formato:
    /// <c>{"codex-backend":{"roles":["provedores-buscar", ...]}, "account":{"roles":[...]}}</c>.
    /// Retorna coleção vazia (nunca lança) quando o client não aparece no token ou o JSON é
    /// inesperado — um token sem roles para este client é um cenário válido (usuário autenticado
    /// sem nenhuma permissão), não um erro de parsing.
    ///
    /// <para>
    /// "JSON inesperado" inclui <b>tipo</b> inesperado, não só sintaxe (achado <c>BAIXO-B06</c>):
    /// <c>JsonElement.TryGetProperty</c> e <c>GetString()</c> lançam
    /// <see cref="InvalidOperationException"/> — que não é <see cref="JsonException"/> — quando o
    /// elemento não é objeto/string. A garantia é sustentada pelos guards de
    /// <see cref="JsonValueKind"/> abaixo, que fecham o caso na origem; o <c>catch</c> largo é
    /// segunda camada, para o formato que eu não enumerei (AP-SECURITY-010).
    /// </para>
    ///
    /// <para>
    /// Coleção vazia nunca é "acesso liberado": sem <see cref="ClaimTypes.Role"/> adicionada, todo
    /// <c>[Authorize(Roles = "...")]</c> reprova com <c>403</c>. Tolerar formato malformado
    /// portanto falha FECHADO — só troca um <c>500</c> genérico por um <c>403</c> correto.
    /// </para>
    /// </summary>
    internal static IReadOnlyCollection<string> ExtrairRoles(string resourceAccessJson, string clienteRecurso)
    {
        try
        {
            using var documento = JsonDocument.Parse(resourceAccessJson);

            if (documento.RootElement.ValueKind != JsonValueKind.Object)
                return [];

            if (!documento.RootElement.TryGetProperty(clienteRecurso, out var clienteElemento)
                || clienteElemento.ValueKind != JsonValueKind.Object)
                return [];

            if (!clienteElemento.TryGetProperty(PropriedadeRoles, out var rolesElemento)
                || rolesElemento.ValueKind != JsonValueKind.Array)
                return [];

            return rolesElemento.EnumerateArray()
                .Where(role => role.ValueKind == JsonValueKind.String)
                .Select(role => role.GetString())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role!)
                .ToArray();
        }
        catch (Exception excecao) when (excecao is JsonException or InvalidOperationException)
        {
            return [];
        }
    }
}
