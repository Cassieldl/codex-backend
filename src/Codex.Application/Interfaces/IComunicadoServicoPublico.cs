using Codex.Application.DTOs.Publico;

namespace Codex.Application.Interfaces;

public interface IComunicadoServicoPublico
{
    /// <summary>
    /// Slug desconhecido ou de provedor/versão não público não é erro: a lista só traz o que vale
    /// e é público (no mínimo, os globais).
    /// </summary>
    /// <exception cref="Codex.Application.Common.ParametroConsultaInvalidoException">Parâmetros malformados.</exception>
    Task<IReadOnlyCollection<ComunicadoPublicoDto>> ListarAsync(
        IReadOnlyCollection<string>? provedorSlugs, string? versaoSlug, CancellationToken ct);
}
