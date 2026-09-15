using Codex.Application.DTOs.Publico;

namespace Codex.Application.Interfaces;

/// <summary>
/// Cascata de consulta (FORTE → PARCIAL → APROX. → VAZIA, analise-backend.md §6.1–§6.5) —
/// implementação real (<c>Codex.Infrastructure/Busca/ConsultaBuscaPostgres</c>) é da unidade
/// BACK-2. Esta unidade (BACK-1) só declara o contrato e registra um stub que falha alto.
/// </summary>
public interface IConsultaBusca
{
    /// <summary>
    /// <paramref name="incluirRascunho"/> decide qual dos dois índices GIN parciais a consulta
    /// deve casar (idx_..._documento_publico vs. idx_..._documento_interno) — o predicado
    /// precisa ser repetido literalmente na query para o planner escolher o índice certo
    /// (nunca confiar que "o índice já filtra sozinho").
    /// </summary>
    Task<ResultadoBuscaDto> BuscarAsync(
        string termo,
        string? provedorSlug,
        IReadOnlyCollection<string>? tipos,
        int pagina,
        int tamanhoPagina,
        bool incluirRascunho,
        CancellationToken ct);
}
