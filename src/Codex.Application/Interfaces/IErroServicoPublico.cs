using Codex.Application.Common;
using Codex.Application.DTOs.Publico;

namespace Codex.Application.Interfaces;

/// <summary>
/// Regra de negócio da leitura pública de erro (P5/P6). <c>ObterDetalheAsync</c> pode devolver
/// <see cref="ErroDetalheDto.VersoesAplicaveis"/> vazio — é um resultado válido (U9), nunca um erro.
/// </summary>
public interface IErroServicoPublico
{
    Task<PaginaResultado<ErroListaItemDto>> ListarAsync(FiltroErroPublico filtro, CancellationToken ct);

    Task<ErroDetalheDto> ObterDetalheAsync(string erroSlug, CancellationToken ct);

    /// <summary>Estabilidade de URL — resolve slug antigo de erro, se existir (§4.1, 308).</summary>
    Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct);
}
