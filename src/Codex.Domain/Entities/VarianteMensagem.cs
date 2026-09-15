namespace Codex.Domain.Entities;

/// <summary>Variante de mensagem observada do erro — é o texto que a busca precisa alcançar (peso A, modelo-dados.md §4.10).</summary>
public class VarianteMensagem : EntidadeAuditada
{
    public int Id { get; private set; }

    public int ErroId { get; private set; }

    public string Texto { get; private set; } = null!;

    protected VarianteMensagem()
    {
    }

    public static VarianteMensagem Criar(int erroId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("Texto da variante não pode ser vazio.", nameof(texto));

        return new VarianteMensagem
        {
            ErroId = erroId,
            Texto = texto,
        };
    }
}
