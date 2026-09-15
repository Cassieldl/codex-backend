using Codex.Application.DTOs.Publico;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>
/// Repositório de leitura PÚBLICA de <see cref="VersaoProvedor"/> (P4) — extensão do par
/// Público/Interno estabelecido por BACK-1 em <c>IProvedorRepositorioPublico</c>, aplicado aqui
/// à sub-entidade que sustenta a página exaustiva de campos. Não existe navegação
/// <c>VersaoProvedor.Provedor</c> no mapeamento real (analise-backend.md §3.3) — todo acesso é
/// via join explícito, nunca <c>Include</c>.
/// </summary>
public interface IVersaoProvedorRepositorioPublico
{
    /// <summary>
    /// Join explícito provedor + versão + rótulo da versão de layout, pelos dois slugs. Não
    /// filtra <c>Publicado</c> em nenhum dos dois lados — M7 (404 vs 410) é responsabilidade do
    /// service. Filtra só <c>DesativadoEm</c> (soft-deleted = 404 mudo, correto).
    /// </summary>
    Task<ProvedorVersaoBrutaDto?> ObterPorSlugsAsync(string provedorSlug, string versaoSlug, CancellationToken ct);

    /// <summary>Contagem de <c>CampoVersao</c> ativos da versão — defesa de leitura do cap de 2000 (M22).</summary>
    Task<int> ContarCamposAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>Tabela exaustiva de campos da versão, ordenada por <c>CampoLayout.Ordem</c> (§4.3).</summary>
    Task<IReadOnlyCollection<CampoVersaoProjecaoDto>> ListarCamposAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>Avisos de escopo VERSÃO desta versão, publicados e não desativados (U8 — nunca duplica os avisos de provedor de P3).</summary>
    Task<IReadOnlyCollection<Aviso>> ListarAvisosPublicadosDaVersaoAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>Erros publicados aplicáveis a esta versão (via <c>errosversoes</c>), para <c>errosResumo</c>.</summary>
    Task<IReadOnlyCollection<ErroResumoPublicoDto>> ListarErrosAplicaveisPublicadosAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>Demais versões publicadas do mesmo provedor, excluindo a atual — para <c>outrasVersoes</c>.</summary>
    Task<IReadOnlyCollection<VersaoProvedor>> ListarOutrasVersoesPublicadasAsync(int provedorId, int versaoProvedorIdAtual, CancellationToken ct);
}
