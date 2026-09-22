using Codex.Application.DTOs.Publico;

namespace Codex.Application.Interfaces;

/// <summary>
/// Regra de negócio da leitura pública do vocabulário canônico do Layout InvoiCy (P10/P11/P12,
/// analise-backend.md §4.3). Lança <c>EntidadeNaoEncontradaException</c> (404),
/// <c>ConteudoDespublicadoException</c> (410) e <c>LimiteExcedidoException</c> (422, cap de 2000
/// campos por versão — M22, reusado de P4) — nunca retorna nulo para esses casos.
/// </summary>
public interface ILayoutServicoPublico
{
    /// <summary>P10 — índice de versões publicadas, com a Vigente destacada.</summary>
    Task<LayoutIndiceDto> ObterIndiceAsync(CancellationToken ct);

    /// <summary>P11 — dicionário completo de uma versão publicada (404/410/422).</summary>
    Task<LayoutVersaoDto> ObterVersaoAsync(string versaoSlug, CancellationToken ct);

    /// <summary>P12 — verbete de resolução do campo canônico, redirect-only (A3, 404/410).</summary>
    Task<CampoLayoutVerbeteDto> ObterCampoAsync(string campoSlug, CancellationToken ct);
}
