namespace Codex.Domain.Entities;

/// <summary>
/// Uma linha do de-para da natureza da operação de uma <see cref="VersaoProvedor"/>: o código
/// <c>natOp</c> da tabela unificada do InvoiCy e o valor que o InvoiCy envia no layout do
/// provedor. A tag de destino não fica aqui: é o <c>destinoXml</c> do campo <c>natOp</c> da versão.
/// </summary>
public class NaturezaOperacao : EntidadeAuditada
{
    public int Id { get; private set; }

    public int VersaoProvedorId { get; private set; }

    public int CodigoInvoicy { get; private set; }

    public string Descricao { get; private set; } = null!;

    public string ValorProvedor { get; private set; } = null!;

    public string? Observacao { get; private set; }

    /// <summary>Conversão ainda não confirmada na fonte; a página mostra o selo "a confirmar".</summary>
    public bool AConfirmar { get; private set; }

    public int Ordem { get; private set; }

    protected NaturezaOperacao()
    {
    }

    public static NaturezaOperacao Criar(
        int versaoProvedorId,
        int codigoInvoicy,
        string descricao,
        string valorProvedor,
        string? observacao,
        bool aConfirmar,
        int ordem)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("Descrição da natureza da operação é obrigatória.", nameof(descricao));
        if (string.IsNullOrWhiteSpace(valorProvedor))
            throw new ArgumentException("Valor no layout do provedor é obrigatório.", nameof(valorProvedor));

        return new NaturezaOperacao
        {
            VersaoProvedorId = versaoProvedorId,
            CodigoInvoicy = codigoInvoicy,
            Descricao = descricao,
            ValorProvedor = valorProvedor,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao,
            AConfirmar = aConfirmar,
            Ordem = ordem,
        };
    }
}
