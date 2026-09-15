namespace Codex.API.Common;

/// <summary>
/// Envelope padrão para recurso único (rules/api.md — D9 arquitetura.md): <c>message</c> é
/// <c>null</c> em leitura, preenchido em escrita. Também usado para coleção NÃO paginada
/// (<c>ApiResponse&lt;T[]&gt;</c>) — nunca confundir com <see cref="PaginatedResponse{T}"/>, que
/// é cru (sem <c>data</c>), reservado para coleção paginada.
/// </summary>
public sealed class ApiResponse<T>
{
    public required T Data { get; init; }

    public string? Message { get; init; }
}
