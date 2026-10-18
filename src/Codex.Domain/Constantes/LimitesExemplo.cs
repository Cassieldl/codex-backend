namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos das colunas de <c>codex.exemplos</c>: fonte única entre o schema
/// (<c>ExemploConfiguration</c>) e os validators.
/// </summary>
public static class LimitesExemplo
{
    /// <summary><c>nome</c>: <c>varchar(160)</c>.</summary>
    public const int Nome = 160;

    /// <summary><c>slug</c>: <c>varchar(180)</c>. Nasce do nome e ganha sufixo numérico quando colide.</summary>
    public const int Slug = 180;
}
