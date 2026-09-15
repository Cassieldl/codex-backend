namespace Codex.Domain.Entities;

/// <summary>N:N erro × campo canônico (modelo-dados.md §4.12).</summary>
public class ErroCampo : EntidadeAuditada
{
    public int ErroId { get; private set; }

    public int CampoLayoutId { get; private set; }

    protected ErroCampo()
    {
    }

    public static ErroCampo Criar(int erroId, int campoLayoutId)
    {
        return new ErroCampo
        {
            ErroId = erroId,
            CampoLayoutId = campoLayoutId,
        };
    }
}
