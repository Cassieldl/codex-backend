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

    /// <summary><c>normaorigem</c>: <c>varchar(60)</c>, como "NT 009 v1.01".</summary>
    public const int NormaOrigem = 60;

    /// <summary>Itens em <c>mudancas</c> (<c>jsonb</c>) de uma versão.</summary>
    public const int MudancasPorVersao = 100;

    /// <summary>Caminho canônico em <c>antes</c> ou <c>depois</c> de uma mudança.</summary>
    public const int CaminhoMudanca = 300;

    /// <summary>Texto em <c>descricao</c> de uma mudança.</summary>
    public const int DescricaoMudanca = 500;
}
