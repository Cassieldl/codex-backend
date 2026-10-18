namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos das colunas de <c>codex.provedores</c> e <c>codex.aliasesprovedor</c>: fonte única entre o
/// schema (<c>ProvedorConfiguration</c>, <c>AliasProvedorConfiguration</c>) e os validators.
/// </summary>
public static class LimitesProvedor
{
    /// <summary><c>nome</c>: <c>varchar(160)</c>.</summary>
    public const int Nome = 160;

    /// <summary><c>identificadorinvoicy</c>: <c>varchar(80)</c>.</summary>
    public const int IdentificadorInvoicy = 80;

    /// <summary><c>slug</c>: <c>varchar(160)</c>. Nasce do nome, que tem o mesmo teto.</summary>
    public const int Slug = 160;

    /// <summary><c>aliasesprovedor.alias</c>: <c>varchar(160)</c>.</summary>
    public const int Alias = 160;
}
