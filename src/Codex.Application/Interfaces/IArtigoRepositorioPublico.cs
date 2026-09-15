using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Repositório de leitura PÚBLICA de <see cref="Artigo"/> (P7/P8) — mesmo padrão de <c>IErroRepositorioPublico</c>.</summary>
public interface IArtigoRepositorioPublico
{
    /// <summary>Não filtra <c>Publicado</c> — M7 (404 vs 410) fica a cargo do service. Filtra só <c>DesativadoEm</c>.</summary>
    Task<Artigo?> ObterPorSlugAsync(string slug, CancellationToken ct);

    /// <summary>Listagem pública (P7) — só <c>Publicado = true</c>, exclui sempre o slug reservado.</summary>
    Task<PaginaResultado<ArtigoListaItemDto>> ListarAsync(FiltroArtigoPublico filtro, CancellationToken ct);

    /// <summary>
    /// Campos do layout relacionados, só publicados (campo E versão de origem), ordenados por nome
    /// (§4.3).
    /// </summary>
    Task<IReadOnlyCollection<ReferenciaCampoLayoutDto>> ListarCamposLayoutRelacionadosPublicadosAsync(int artigoId, CancellationToken ct);

    /// <summary>Erros relacionados, só publicados, ordenados por título (§4.3).</summary>
    Task<IReadOnlyCollection<ReferenciaErroDto>> ListarErrosRelacionadosPublicadosAsync(int artigoId, CancellationToken ct);

    /// <summary>
    /// Bloco aditivo de P6 (§4.4) — artigos relacionados a um erro, só publicados, ordenados por
    /// título. Vive aqui (não em <c>IErroRepositorioPublico</c>) para não editar uma interface fora
    /// do escopo desta unidade; <c>ErroServicoPublico</c> passa a depender também deste contrato.
    /// </summary>
    Task<IReadOnlyCollection<ReferenciaArtigoDto>> ListarRelacionadosAoErroPublicadosAsync(int erroId, CancellationToken ct);

    /// <summary>Resolve um slug antigo de artigo para o path atual, via <c>codex.redirecionamentos</c> (§0.7 — sempre <c>null</c> nesta fatia).</summary>
    Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct);
}
