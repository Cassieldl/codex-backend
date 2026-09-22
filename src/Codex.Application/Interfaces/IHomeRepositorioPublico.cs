using Codex.Application.DTOs.Publico;

namespace Codex.Application.Interfaces;

/// <summary>
/// Leitura do agregado da Home (P9, analise-backend.md §6). Cada método devolve um conjunto já
/// filtrado pela visibilidade pública — o service só agrupa e mapeia, nunca reaplica predicado de
/// publicação.
/// </summary>
public interface IHomeRepositorioPublico
{
    /// <summary>
    /// Comunicados no ar (<c>Comunicado.EhVigenteEm</c>) E visíveis: <c>Global</c> sempre passa;
    /// <c>Provedor</c>/<c>ProvedorVersao</c> só passam quando a cascata do ALVO é pública (§6.3).
    /// </summary>
    Task<IReadOnlyCollection<ComunicadoHomeBrutoDto>> ListarComunicadosVisiveisAsync(DateTimeOffset referencia, CancellationToken ct);

    /// <summary>Provedores publicados e ativos, ordenados por nome.</summary>
    Task<IReadOnlyCollection<ProvedorHomeBrutoDto>> ListarProvedoresPublicadosAsync(CancellationToken ct);

    /// <summary>Versões publicadas de TODOS os provedores, na ordenação de §6.4 (layout DESC, id DESC).</summary>
    Task<IReadOnlyCollection<VersaoProvedorHomeBrutoDto>> ListarVersoesPublicadasAsync(CancellationToken ct);

    /// <summary><c>null</c> quando não há nenhuma versão de layout publicada.</summary>
    Task<LayoutHomeBrutoDto?> ObterResumoLayoutAsync(CancellationToken ct);

    /// <summary>Artigos publicados mais recentes, no MESMO shape de P7 (sem o slug reservado).</summary>
    Task<IReadOnlyCollection<ArtigoListaItemDto>> ListarArtigosRecentesAsync(int quantidade, CancellationToken ct);
}
