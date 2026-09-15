namespace Codex.Domain.Entities;

/// <summary>N:N artigo × campo canônico (modelo-dados.md §6.3). Modelada, sem migration na 001.</summary>
public class ArtigoCampoLayout : EntidadeAuditada
{
    public int ArtigoId { get; private set; }

    public int CampoLayoutId { get; private set; }

    protected ArtigoCampoLayout()
    {
    }

    public static ArtigoCampoLayout Criar(int artigoId, int campoLayoutId)
    {
        return new ArtigoCampoLayout
        {
            ArtigoId = artigoId,
            CampoLayoutId = campoLayoutId,
        };
    }
}
