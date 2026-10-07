using System.Security.Claims;
using Codex.Application.Common;

namespace Codex.API.Seguranca;

/// <summary>
/// Extrai do token quem está fazendo a requisição. Fonte única da identidade em escritas
/// auditadas: o corpo da requisição nunca diz quem é o usuário.
/// </summary>
public static class UsuarioDaRequisicaoExtensoes
{
    /// <summary>
    /// <c>sub</c> do Keycloak como identificador (o JwtBearer o expõe como
    /// <see cref="ClaimTypes.NameIdentifier"/>; o nome cru cobre a configuração sem mapeamento) e
    /// o nome de exibição (<c>name</c>, com <c>preferred_username</c> como reserva). <c>null</c>
    /// quando não há identificador — o chamador responde 401.
    /// </summary>
    public static UsuarioDaRequisicao? ObterUsuarioDaRequisicao(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var nome = principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("preferred_username")?.Value;

        return new UsuarioDaRequisicao(id, nome);
    }
}
