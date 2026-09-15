namespace Codex.Domain.Entities;

/// <summary>
/// Base para toda entidade com as três colunas de auditoria obrigatórias
/// (rules/database.md): <c>criadoem</c>/<c>atualizadoem</c> (DEFAULT now(), carimbados também
/// por trigger no banco) e <c>desativadoem</c> (soft delete). Soft delete é filtrado
/// explicitamente por query em cada repositório — nunca <c>HasQueryFilter()</c> global
/// (analise-backend.md §3.1): a área interna precisa listar desativados em alguns fluxos.
/// </summary>
public abstract class EntidadeAuditada
{
    public DateTimeOffset CriadoEm { get; protected set; }

    public DateTimeOffset AtualizadoEm { get; protected set; }

    public DateTimeOffset? DesativadoEm { get; protected set; }

    public bool EstaDesativado => DesativadoEm is not null;

    public virtual void Desativar()
    {
        DesativadoEm = DateTimeOffset.UtcNow;
    }

    public virtual void Reativar()
    {
        DesativadoEm = null;
    }
}
