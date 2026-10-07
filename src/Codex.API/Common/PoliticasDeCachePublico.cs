namespace Codex.API.Common;

/// <summary>Valores de <c>Cache-Control</c> das rotas públicas, num lugar só para não divergirem.</summary>
public static class PoliticasDeCachePublico
{
    /// <summary>
    /// Conteúdo editorial que muda em minutos, não em segundos: 60 s de cache compartilhado com
    /// mais 5 min servindo a cópia velha enquanto revalida. Publicar um comunicado leva até um
    /// minuto para aparecer, e é o trade-off aceito para a rota mais chamada do portal. Serve
    /// também às rotas cuja resposta muda só pela passagem do tempo (comunicado que vence), que
    /// por isso não têm ETag de conteúdo.
    /// </summary>
    public const string ConteudoEditorialCurto = "public, max-age=60, stale-while-revalidate=300";
}
