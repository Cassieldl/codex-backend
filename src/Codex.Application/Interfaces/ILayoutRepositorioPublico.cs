using Codex.Application.DTOs.Publico;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>
/// Repositório de leitura PÚBLICA do vocabulário canônico do Layout InvoiCy (P10/P11/P12,
/// analise-backend.md §4.3). Nunca filtra pelo predicado de visibilidade nos métodos de resolução
/// por slug (<see cref="ObterVersaoPorSlugAsync"/>/<see cref="ObterCampoPorSlugAsync"/>) — só
/// <c>DesativadoEm</c> — porque a distinção 404 (nunca existiu) × 410 (existe, não visível) é
/// responsabilidade do service (mesmo padrão de M7 em <c>IVersaoProvedorRepositorioPublico</c>).
/// </summary>
public interface ILayoutRepositorioPublico
{
    /// <summary>Versões de layout publicadas e não desativadas — sem ordenação própria (o service ordena).</summary>
    Task<IReadOnlyCollection<VersaoLayout>> ListarVersoesPublicadasAsync(CancellationToken ct);

    Task<VersaoLayout?> ObterVersaoPorSlugAsync(string versaoSlug, CancellationToken ct);

    Task<VersaoLayout?> ObterVersaoPorIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Todo <c>CampoLayout</c> VISÍVEL (§4.1: campo e versão de ORIGEM publicados e não
    /// desativados) — a base sobre a qual a fórmula M12 (§4.2) é aplicada em memória, por versão-
    /// alvo, no service.
    /// </summary>
    Task<IReadOnlyCollection<CampoLayoutVisivelBrutoDto>> ListarCamposVisiveisAsync(CancellationToken ct);

    /// <summary>Com <c>VersaoLayoutOrigem</c> carregada (Include) — necessária para a fórmula M12 e para <c>VersaoOrigem</c> do verbete (P12).</summary>
    Task<CampoLayout?> ObterCampoPorSlugAsync(string campoSlug, CancellationToken ct);

    /// <summary>"Onde é usado" (P12, A3) — provedores/versões PUBLICADOS que usam este campo via <c>CampoVersao</c>.</summary>
    Task<IReadOnlyCollection<UsoCampoLayoutDto>> ListarUsosPublicosAsync(int campoLayoutId, CancellationToken ct);
}
