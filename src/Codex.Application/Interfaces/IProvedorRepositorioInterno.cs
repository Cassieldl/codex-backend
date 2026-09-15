using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>
/// Repositório de leitura/escrita INTERNA de <see cref="Provedor"/> — implementação de
/// referência do padrão de §3.3. Enxerga rascunho (sem filtro de <c>Publicado</c>).
/// </summary>
public interface IProvedorRepositorioInterno
{
    /// <summary>
    /// <paramref name="rastreada"/> = true para o caminho de update/delete (nunca
    /// AsNoTracking — ver <see cref="Codex.Infrastructure.Repositories.RepositorioBase{TDbContext}"/>);
    /// false para leitura pura de detalhe.
    /// </summary>
    Task<Provedor?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    /// <summary>Listagem interna (I1) — enxerga rascunho, filtro de <c>publicado</c> opcional.</summary>
    Task<PaginaResultado<ProvedorAdminListaItemDto>> ListarAsync(FiltroProvedorInterno filtro, CancellationToken ct);

    /// <summary>Todos os provedores ativos, com <see cref="Provedor.Aliases"/> carregado — usado por I22 (reconstrução total do índice).</summary>
    Task<IReadOnlyCollection<Provedor>> ListarTodosAtivosComAliasesAsync(CancellationToken ct);

    /// <summary>Rastreia a nova entidade para o próximo <see cref="SalvarAlteracoesAsync"/>.</summary>
    Task AdicionarAsync(Provedor provedor, CancellationToken ct);

    /// <summary>Persiste as mudanças pendentes (criação, atualização, soft delete, publicação) — traduz UNIQUE (409) do Postgres.</summary>
    Task SalvarAlteracoesAsync(CancellationToken ct);
}
