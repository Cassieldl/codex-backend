namespace Codex.Domain.Entities;

/// <summary>N:N erro × versão de provedor (modelo-dados.md §4.11) — cardinalidade mínima zero (U9).</summary>
public class ErroVersao : EntidadeAuditada
{
    public int ErroId { get; private set; }

    public int VersaoProvedorId { get; private set; }

    protected ErroVersao()
    {
    }

    public static ErroVersao Criar(int erroId, int versaoProvedorId)
    {
        return new ErroVersao
        {
            ErroId = erroId,
            VersaoProvedorId = versaoProvedorId,
        };
    }
}
