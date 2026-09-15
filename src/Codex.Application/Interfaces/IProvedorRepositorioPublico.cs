using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>
/// Repositório de leitura PÚBLICA de <see cref="Provedor"/> — padrão de §3.3 (duas interfaces
/// por entidade com leitura pública, nunca uma única interface com parâmetro booleano
/// <c>incluirRascunho</c>). Completada por BACK-3 sobre a referência deixada por BACK-1.
/// </summary>
public interface IProvedorRepositorioPublico
{
    /// <summary>
    /// Retorna o provedor não desativado INDEPENDENTEMENTE do estado de <c>Publicado</c> — é o
    /// service (não o repositório) quem distingue 404 (não existe) de 410 (existe, despublicado),
    /// conforme a regra unificada de M7/§4.1: "nunca 404 mudo para conteúdo que existe mas está
    /// em rascunho". Nunca usar este método para compor listas — para isso, <see cref="ListarAsync"/>.
    /// </summary>
    Task<Provedor?> ObterPorSlugAsync(string slug, CancellationToken ct);

    /// <summary>Listagem pública (P2) — filtro de publicação direto na própria coluna (M19), nunca EXISTS/derivação.</summary>
    Task<PaginaResultado<ProvedorListaItemDto>> ListarAsync(FiltroProvedorPublico filtro, CancellationToken ct);

    /// <summary>Avisos de escopo PROVEDOR, publicados e não desativados (P3, U8).</summary>
    Task<IReadOnlyCollection<Aviso>> ListarAvisosPublicadosDoProvedorAsync(int provedorId, CancellationToken ct);

    /// <summary>Versões publicadas e não desativadas do provedor, para a lista de abas de P3 (U7).</summary>
    Task<IReadOnlyCollection<VersaoProvedor>> ListarVersoesPublicadasAsync(int provedorId, CancellationToken ct);

    /// <summary>Resolve um slug antigo de provedor para o path atual, via <c>codex.redirecionamentos</c> (estabilidade de URL, §4.1).</summary>
    Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct);
}
