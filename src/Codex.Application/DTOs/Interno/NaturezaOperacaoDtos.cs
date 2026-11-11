namespace Codex.Application.DTOs.Interno;

/// <summary>
/// De-para da natureza da operação de uma versão de provedor (PUT interno). Substitui a lista
/// inteira; a ordem dos itens é a ordem de exibição.
/// </summary>
public sealed record AtualizarNaturezasOperacaoDto
{
    public required IReadOnlyList<ItemNaturezaOperacaoDto> Itens { get; init; }
}

/// <summary>Uma linha do de-para: o <c>natOp</c> do InvoiCy e o valor enviado no layout do provedor.</summary>
public sealed record ItemNaturezaOperacaoDto
{
    public required int CodigoInvoicy { get; init; }

    public required string Descricao { get; init; }

    public required string ValorProvedor { get; init; }

    public string? Observacao { get; init; }

    public bool AConfirmar { get; init; }
}
