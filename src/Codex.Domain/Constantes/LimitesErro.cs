namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos das colunas de <c>codex.erros</c>: fonte única entre o schema (<c>ErroConfiguration</c>) e
/// os validators. Coluna mais estreita que o validator vira 500 (22001) em vez de 422.
/// </summary>
public static class LimitesErro
{
    /// <summary><c>codigo</c>: <c>varchar(40)</c>.</summary>
    public const int Codigo = 40;

    /// <summary><c>titulo</c>: <c>varchar(200)</c>.</summary>
    public const int Titulo = 200;

    /// <summary><c>slug</c>: <c>varchar(200)</c>. Nasce de <c>"{codigo}-{titulo}"</c> e nunca é truncado.</summary>
    public const int Slug = 200;
}
