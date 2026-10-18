namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos das colunas de <c>codex.artigos</c>: fonte única entre o schema (<c>ArtigoConfiguration</c>)
/// e os validators.
/// </summary>
public static class LimitesArtigo
{
    /// <summary><c>titulo</c>: <c>varchar(200)</c>.</summary>
    public const int Titulo = 200;

    /// <summary><c>slug</c>: <c>varchar(200)</c>. Nasce do título, que tem o mesmo teto.</summary>
    public const int Slug = 200;
}
