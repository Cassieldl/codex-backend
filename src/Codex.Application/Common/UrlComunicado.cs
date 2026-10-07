using Codex.Domain.Enums;

namespace Codex.Application.Common;

/// <summary>Link do alvo de um comunicado — um só lugar para a Home e para <c>GET /comunicados</c>.</summary>
public static class UrlComunicado
{
    /// <summary>
    /// <c>Global</c> não tem alvo e por isso não tem URL. Nas demais, o slug do provedor só chega
    /// preenchido quando a cascata do alvo é pública — a ausência dele é "sem link", nunca uma URL
    /// pela metade.
    /// </summary>
    public static string? Montar(AbrangenciaComunicado abrangencia, string? provedorSlug, string? versaoSlug)
    {
        if (abrangencia == AbrangenciaComunicado.Global || string.IsNullOrWhiteSpace(provedorSlug))
            return null;

        return string.IsNullOrWhiteSpace(versaoSlug)
            ? $"/provedores/{provedorSlug}"
            : $"/provedores/{provedorSlug}/{versaoSlug}";
    }
}
