namespace Codex.Domain.Entities;

/// <summary>
/// N:N aviso de versão × campo afetado (modelo-dados.md §4.8). Só faz sentido para
/// <see cref="Aviso"/> de escopo versão — não há CHECK de banco garantindo isso (exigiria
/// trigger cruzando tabelas); é o formulário de aviso de provedor que não oferece o seletor.
/// </summary>
public class AvisoCampo : EntidadeAuditada
{
    public int AvisoId { get; private set; }

    public int CampoVersaoId { get; private set; }

    protected AvisoCampo()
    {
    }

    public static AvisoCampo Criar(int avisoId, int campoVersaoId)
    {
        return new AvisoCampo
        {
            AvisoId = avisoId,
            CampoVersaoId = campoVersaoId,
        };
    }
}
