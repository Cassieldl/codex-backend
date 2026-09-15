namespace Codex.Application.Interfaces;

/// <summary>
/// Abstração mínima de transação explícita (analise-backend.md §3.4/§6.6): garante que a mutação
/// de conteúdo (criação/edição/soft delete/publicação) e a atualização correspondente de
/// <c>codex.itensbusca</c> via <see cref="IIndiceBuscaWriter"/> rodem na MESMA transação de
/// banco — nunca dois <c>SaveChanges</c>/comandos SQL em transações separadas. Implementada em
/// <c>Codex.Infrastructure</c> sobre <c>DbContext.Database.BeginTransactionAsync</c>.
/// </summary>
public interface IUnidadeDeTrabalho
{
    Task ExecutarAsync(Func<CancellationToken, Task> operacao, CancellationToken ct);
}
