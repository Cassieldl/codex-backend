using Codex.Domain.Enums;

namespace Codex.Application.Interfaces;

/// <summary>
/// Resolve o risco #1 do DBA (modelo-dados.md §5.6.1, analise-backend.md §6.6): nada no banco
/// garante a propagação de <c>publicado</c> pela cascata — é 100% responsabilidade da
/// implementação real deste contrato. A implementação concreta é da unidade BACK-2
/// (<c>Codex.Infrastructure/Busca/IndiceBuscaWriter</c>); esta unidade (BACK-1) só declara a
/// interface e registra um stub que falha alto — nunca falha silenciosa (AP-SECURITY-003:
/// registrado ≠ provado).
/// </summary>
public interface IIndiceBuscaWriter
{
    /// <summary>
    /// Recalcula, dentro da MESMA transação da mudança de <c>Publicado</c>, a visibilidade da
    /// própria linha de <paramref name="id"/> em <c>codex.itensbusca</c> e, quando
    /// <paramref name="tipoQueMudou"/> tiver descendentes (Provedor, VersaoProvedor), propaga
    /// via <c>UPDATE ... WHERE</c> em lote — nunca percorrendo linha a linha em C#. Grafo de
    /// cascata completo em modelo-dados.md §5.6.1.
    /// </summary>
    Task RecalcularVisibilidadeAsync(TipoItemBusca tipoQueMudou, int id, CancellationToken ct);

    /// <summary>Upsert de uma linha de projeção a partir da entidade de origem — chamado na mesma transação da escrita de conteúdo (D2).</summary>
    Task AtualizarPesosAsync(TipoItemIndexado tipoItem, int itemId, string? pesoA, string? pesoB, string? pesoC, string? pesoD, CancellationToken ct);

    /// <summary>Remove (soft) a linha de projeção quando a entidade de origem é removida.</summary>
    Task DesativarAsync(TipoItemIndexado tipoItem, int itemId, CancellationToken ct);
}
