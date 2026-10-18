using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>
/// Repositório de leitura/escrita INTERNA de <see cref="Artigo"/> — enxerga rascunho (I23-I29,
/// I36). As duas famílias de referência cruzada (<c>ArtigoCampoLayout</c>/<c>ArtigoErro</c>) não
/// são coleções do agregado (analise-backend.md §2.1) — este repositório expõe métodos dedicados
/// para ler/substituir essas duas coleções diretamente, mesmo padrão de <c>IErroRepositorioInterno</c>.
/// </summary>
public interface IArtigoRepositorioInterno
{
    Task<Artigo?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    Task<Artigo?> ObterPorSlugAsync(string slug, CancellationToken ct);

    Task<PaginaResultado<ArtigoAdminListaItemDto>> ListarAsync(FiltroArtigoInterno filtro, CancellationToken ct);

    /// <summary>Todos os artigos ativos — usado por I22 (reconstrução total do índice de busca).</summary>
    Task<IReadOnlyCollection<Artigo>> ListarTodosAtivosAsync(CancellationToken ct);

    /// <summary>Todo vínculo do artigo, inclusive com campo removido, ordenado por ordem da versão do layout, nome e id.</summary>
    Task<IReadOnlyList<CampoLayoutVinculadoArtigoDto>> ListarCamposLayoutVinculadosAsync(int artigoId, CancellationToken ct);

    /// <summary>Todo vínculo do artigo, inclusive com erro removido, ordenado por código e id.</summary>
    Task<IReadOnlyList<ErroVinculadoArtigoDto>> ListarErrosVinculadosAsync(int artigoId, CancellationToken ct);

    /// <summary>Substitui a coleção inteira (PUT substitui por inteiro) — não salva sozinho, entra no mesmo <see cref="SalvarAlteracoesAsync"/> do agregado.</summary>
    Task SubstituirCamposLayoutRelacionadosAsync(int artigoId, IReadOnlyCollection<int> campoLayoutIds, CancellationToken ct);

    Task SubstituirErrosRelacionadosAsync(int artigoId, IReadOnlyCollection<int> erroIds, CancellationToken ct);

    Task AdicionarAsync(Artigo artigo, CancellationToken ct);

    Task SalvarAlteracoesAsync(CancellationToken ct);
}
