using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Repositório de leitura PÚBLICA de <see cref="Erro"/> (P5/P6) — mesmo padrão de <c>IProvedorRepositorioPublico</c>.</summary>
public interface IErroRepositorioPublico
{
    /// <summary>Não filtra <c>Publicado</c> — M7 (404 vs 410) fica a cargo do service. Filtra só <c>DesativadoEm</c>.</summary>
    Task<Erro?> ObterPorSlugAsync(string slug, CancellationToken ct);

    /// <summary>Listagem pública (P5) — só <c>Publicado = true</c>. <c>codigo</c> é o atalho exato de M2.</summary>
    Task<PaginaResultado<ErroListaItemDto>> ListarAsync(FiltroErroPublico filtro, CancellationToken ct);

    Task<IReadOnlyCollection<string>> ListarVariantesAsync(int erroId, CancellationToken ct);

    /// <summary>
    /// Versões aplicáveis PUBLICADAS (e cujo provedor também está publicado — cascata) via
    /// <c>errosversoes</c>. Pode vir vazia (U9) — não é erro, é resultado válido.
    /// </summary>
    Task<IReadOnlyCollection<VersaoAplicavelBrutaDto>> ListarVersoesAplicaveisPublicadasAsync(int erroId, CancellationToken ct);

    /// <summary>
    /// Campos canônicos relacionados (via <c>erroscampos</c>) com âncoras resolvidas para as
    /// versões aplicáveis públicas informadas (M10) — âncora só existe quando o campo canônico
    /// tem um <c>CampoVersao</c> ativo dentro daquela versão.
    /// </summary>
    Task<IReadOnlyCollection<CampoRelacionadoPublicoDto>> ListarCamposRelacionadosAsync(
        int erroId, IReadOnlyCollection<VersaoAplicavelBrutaDto> versoesAplicaveisPublicas, CancellationToken ct);

    /// <summary>Resolve um slug antigo de erro para o path atual, via <c>codex.redirecionamentos</c> (estabilidade de URL, §4.1).</summary>
    Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct);
}
