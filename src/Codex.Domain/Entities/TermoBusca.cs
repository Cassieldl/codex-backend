namespace Codex.Domain.Entities;

/// <summary>Léxico para autocorreção por similaridade trigrama (modelo-dados.md §5.7).</summary>
public class TermoBusca : EntidadeAuditada
{
    public int Id { get; private set; }

    public string Termo { get; private set; } = null!;

    public int Ocorrencias { get; private set; }

    protected TermoBusca()
    {
    }

    public static TermoBusca Criar(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo) || termo.Trim().Length < 3)
            throw new ArgumentException("Termo precisa ter ao menos 3 caracteres.", nameof(termo));

        return new TermoBusca
        {
            Termo = termo,
            Ocorrencias = 1,
        };
    }

    public void IncrementarOcorrencia()
    {
        Ocorrencias++;
    }
}
