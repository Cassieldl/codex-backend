using Codex.Application.Common;
using Codex.Application.DTOs.Publico;

namespace Codex.Application.Interfaces;

/// <summary>
/// Regra de negócio da leitura pública de provedor/versão (P2/P3/P4). Lança
/// <c>EntidadeNaoEncontradaException</c> (404), <c>ConteudoDespublicadoException</c> (410) e
/// <c>LimiteExcedidoException</c> (422, cap de 2000 campos — M22) — nunca retorna nulo para esses
/// casos.
/// </summary>
public interface IProvedorServicoPublico
{
    Task<PaginaResultado<ProvedorListaItemDto>> ListarAsync(FiltroProvedorPublico filtro, CancellationToken ct);

    /// <summary>P3 — identidade + avisos de provedor + lista de versões (U7/U8).</summary>
    Task<ProvedorDetalheDto> ObterDetalheAsync(string provedorSlug, CancellationToken ct);

    /// <summary>P4 — tabela exaustiva de campos da versão (§4.3), com cascata provedor+versão.</summary>
    Task<VersaoProvedorPaginaDto> ObterVersaoPaginaAsync(string provedorSlug, string versaoSlug, CancellationToken ct);

    /// <summary>Estabilidade de URL — resolve slug antigo de provedor, se existir (§4.1, 308).</summary>
    Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct);
}
