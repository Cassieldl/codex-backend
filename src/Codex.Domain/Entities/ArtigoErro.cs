namespace Codex.Domain.Entities;

/// <summary>N:N artigo × erro (modelo-dados.md §6.4). Modelada, sem migration na 001.</summary>
public class ArtigoErro : EntidadeAuditada
{
    public int ArtigoId { get; private set; }

    public int ErroId { get; private set; }

    protected ArtigoErro()
    {
    }

    public static ArtigoErro Criar(int artigoId, int erroId)
    {
        return new ArtigoErro
        {
            ArtigoId = artigoId,
            ErroId = erroId,
        };
    }
}
