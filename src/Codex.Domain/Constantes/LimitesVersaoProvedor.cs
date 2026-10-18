namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos das colunas de <c>codex.versoesprovedor</c>: fonte única entre o schema
/// (<c>VersaoProvedorConfiguration</c>) e os validators.
/// </summary>
public static class LimitesVersaoProvedor
{
    /// <summary><c>rotulo</c>: <c>varchar(80)</c>.</summary>
    public const int Rotulo = 80;

    /// <summary><c>slug</c>: <c>varchar(160)</c>. Nasce do rótulo, que é mais curto.</summary>
    public const int Slug = 160;
}
