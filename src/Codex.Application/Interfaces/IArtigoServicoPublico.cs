using Codex.Application.Common;
using Codex.Application.DTOs.Publico;

namespace Codex.Application.Interfaces;

/// <summary>Regra de negócio da leitura pública de artigo (P7/P8, analise-backend.md §4).</summary>
public interface IArtigoServicoPublico
{
    Task<PaginaResultado<ArtigoListaItemDto>> ListarAsync(FiltroArtigoPublico filtro, CancellationToken ct);

    Task<ArtigoDetalheDto> ObterDetalheAsync(string artigoSlug, CancellationToken ct);

    /// <summary>Estabilidade de URL — resolve slug antigo de artigo, se existir (§0.7, sempre <c>null</c> nesta fatia).</summary>
    Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct);
}
