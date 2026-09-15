namespace Codex.API.Common;

/// <summary>
/// Envelope CRU (sem <c>data</c>) para coleção paginada (rules/api.md). Usado por P2/P5 (e pelos
/// equivalentes internos, quando existirem) — nunca <see cref="ApiResponse{T}"/> para listagem
/// paginada.
/// </summary>
public sealed class PaginatedResponse<T>
{
    public IEnumerable<T> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
