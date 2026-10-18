namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos das colunas de <c>codex.versoeslayout</c>: fonte única entre o schema
/// (<c>VersaoLayoutConfiguration</c>) e os validators.
/// </summary>
public static class LimitesVersaoLayout
{
    /// <summary><c>rotulo</c>: <c>varchar(80)</c>.</summary>
    public const int Rotulo = 80;

    /// <summary><c>slug</c>: <c>varchar(120)</c>. Nasce do rótulo, que é mais curto.</summary>
    public const int Slug = 120;
}
