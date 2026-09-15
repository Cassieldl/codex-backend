namespace Codex.Domain.Entities;

/// <summary>Alias de mercado de um provedor — peso B na busca (modelo-dados.md §4.4).</summary>
public class AliasProvedor : EntidadeAuditada
{
    public int Id { get; private set; }

    public int ProvedorId { get; private set; }

    public string Alias { get; private set; } = null!;

    protected AliasProvedor()
    {
    }

    public static AliasProvedor Criar(int provedorId, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias não pode ser vazio.", nameof(alias));

        return new AliasProvedor
        {
            ProvedorId = provedorId,
            Alias = alias,
        };
    }
}
