using Codex.Domain.Entities;

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

    /// <summary>
    /// Garante que o <c>SaveChanges</c> desta operação emita <c>UPDATE ... WHERE xmin = @esperado</c>
    /// na linha de <paramref name="entidade"/> — o único momento em que o token de concorrência
    /// (If-Match) é conferido na gravação e em que ele AVANÇA. Chamar em todo PUT cuja entidade
    /// tenha filhos ou vínculos editáveis (aliases, variantes, junções): quando só a coleção muda,
    /// o EF não toca a linha do pai, o <c>xmin</c> fica parado e um segundo PUT com o token antigo
    /// passa por cima da edição anterior sem aviso. Sem migration: o <c>UPDATE</c> carrega
    /// <c>atualizadoem</c>, que o trigger <c>set_atualizadoem</c> reescreve de qualquer jeito.
    /// </summary>
    void ForcarConferenciaDeConcorrencia(EntidadeAuditada entidade);
}
