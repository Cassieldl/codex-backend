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

    /// <summary>
    /// Envio modelo InvoiCy da versão: a estrutura do layout recortada ao alcance. Versão ou provedor
    /// inexistente ou despublicado responde 404, como a estrutura; layout sem estrutura, 404 com
    /// <c>ESTRUTURA_INDISPONIVEL</c>; cenário que a versão não sustenta, 422 com <c>CENARIO_INDISPONIVEL</c>.
    /// </summary>
    Task<EnvioModeloDto> ObterEnvioModeloAsync(
        string provedorSlug, string versaoSlug, AlcanceEnvioModelo alcance, CenarioEnvioModelo cenario, CancellationToken ct);

    /// <summary>Estabilidade de URL — resolve slug antigo de provedor, se existir (§4.1, 308).</summary>
    Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct);
}
