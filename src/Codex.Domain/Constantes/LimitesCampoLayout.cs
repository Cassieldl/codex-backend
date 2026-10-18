namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos das colunas de <c>codex.camposlayout</c>: fonte única entre o schema
/// (<c>CampoLayoutConfiguration</c>) e os validators.
/// </summary>
public static class LimitesCampoLayout
{
    /// <summary><c>nome</c>: <c>varchar(120)</c>.</summary>
    public const int Nome = 120;

    /// <summary><c>grupo</c>: <c>varchar(120)</c>.</summary>
    public const int Grupo = 120;

    /// <summary><c>slug</c>: <c>varchar(160)</c>. Nasce do caminho XML.</summary>
    public const int Slug = 160;

    /// <summary>
    /// Teto de entrada do caminho XML: o do slug que nasce dele. A normalização só encurta, então um
    /// caminho de até 160 sempre gera slug que cabe.
    /// </summary>
    public const int CaminhoXml = Slug;

    /// <summary><c>caminhoxml</c>: <c>varchar(400)</c>, mais larga que o teto de entrada.</summary>
    public const int ColunaCaminhoXml = 400;
}
