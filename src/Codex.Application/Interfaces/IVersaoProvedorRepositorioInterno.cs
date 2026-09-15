using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Repositório de leitura/escrita INTERNA de <see cref="VersaoProvedor"/> — enxerga rascunho.</summary>
public interface IVersaoProvedorRepositorioInterno
{
    Task<VersaoProvedor?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    /// <summary>I6 — lista completa (sem paginação, <c>ApiResponse&lt;T[]&gt;</c>), enxerga rascunho.</summary>
    Task<IReadOnlyCollection<VersaoAdminListaItemDto>> ListarPorProvedorAsync(int provedorId, CancellationToken ct);

    /// <summary>Todas as versões ativas — usado por I22 (reconstrução total do índice).</summary>
    Task<IReadOnlyCollection<VersaoProvedor>> ListarTodosAtivosAsync(CancellationToken ct);

    Task AdicionarAsync(VersaoProvedor versao, CancellationToken ct);

    Task SalvarAlteracoesAsync(CancellationToken ct);
}
