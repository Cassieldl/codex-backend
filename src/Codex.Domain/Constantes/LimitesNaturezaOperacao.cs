namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos de <c>codex.naturezasoperacao</c>: fonte única entre o schema
/// (<c>NaturezaOperacaoConfiguration</c>) e os validators.
/// </summary>
public static class LimitesNaturezaOperacao
{
    /// <summary><c>natOp</c> do layout InvoiCy é <c>N(3)</c>.</summary>
    public const int CodigoInvoicyMaximo = 999;

    public const int Descricao = 200;

    public const int ValorProvedor = 20;

    public const int Observacao = 500;

    /// <summary>A maior tabela conhecida (IPM) tem 14 linhas.</summary>
    public const int ItensPorVersao = 100;
}
