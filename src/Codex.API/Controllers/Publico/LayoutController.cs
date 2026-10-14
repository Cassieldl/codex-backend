using Codex.API.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Codex.API.Controllers.Publico;

/// <summary>
/// Endpoints públicos e anônimos do vocabulário canônico do Layout InvoiCy (P10/P11/P12,
/// analise-backend.md §4.3). Predicado de visibilidade é o mesmo da cascata de busca (decisão A1,
/// §4.1) — sem regra derivada, sem migration. Nenhum <c>try/catch</c> local aqui: os slugs de
/// versão/campo são imutáveis (nenhuma troca de slug nesta demanda), então não há caso de
/// redirect 308 a interceptar — as exceções de domínio sobem direto para
/// <see cref="Codex.API.Middleware.TratamentoErrosMiddleware"/>, que é quem conhece os códigos
/// <c>VERSAO_LAYOUT_DESPUBLICADA</c>/<c>CAMPO_LAYOUT_DESPUBLICADO</c> (analise-backend.md §0.7).
/// <see cref="MapeadorErroPublico"/> NÃO é usado aqui e não conhece esses dois códigos.
/// </summary>
[ApiController]
[Route("api/v1/layout")]
[AllowAnonymous]
[EnableRateLimiting("vitrine")]
public sealed class LayoutController : ControllerBase
{
    /// <summary>
    /// Mesma política da Home irmã. Rota anônima cujo custo é lido do banco a cada chamada: sem
    /// cache, quem controla a taxa controla a carga. O vocabulário muda por promoção de versão, um
    /// evento de dias — 60 s de atraso na propagação é folgado para o pior caso.
    /// </summary>
    private const string PoliticaDeCache = "public, max-age=60, stale-while-revalidate=300";

    private readonly ILayoutServicoPublico _servico;

    public LayoutController(ILayoutServicoPublico servico)
    {
        _servico = servico;
    }

    /// <summary>P10 — índice de versões publicadas do layout, com a Vigente destacada.</summary>
    /// <response code="200">Versões publicadas do layout.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<LayoutIndiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LayoutIndiceDto>>> ObterIndice(CancellationToken ct)
    {
        var indice = await _servico.ObterIndiceAsync(ct);

        Response.Headers[HeaderNames.CacheControl] = PoliticaDeCache;

        return Ok(new ApiResponse<LayoutIndiceDto> { Data = indice });
    }

    /// <summary>P11 — dicionário completo de uma versão publicada do layout.</summary>
    /// <response code="200">Versão publicada.</response>
    /// <response code="404">Versão nunca existiu.</response>
    /// <response code="410">Versão existe mas está despublicada.</response>
    /// <response code="422">Versão excede o limite de 2000 campos (M22, defesa de leitura).</response>
    [HttpGet("versoes/{versaoSlug}")]
    [ProducesResponseType(typeof(ApiResponse<LayoutVersaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<LayoutVersaoDto>>> ObterVersao(string versaoSlug, CancellationToken ct)
    {
        var versao = await _servico.ObterVersaoAsync(versaoSlug, ct);

        Response.Headers[HeaderNames.CacheControl] = PoliticaDeCache;

        return Ok(new ApiResponse<LayoutVersaoDto> { Data = versao });
    }

    /// <summary>P12 — resolução do campo canônico e provedores/versões publicados que o usam (com destino no XML e obrigatoriedade).</summary>
    /// <response code="200">Campo visível, com a versão em que o frontend deve abri-lo.</response>
    /// <response code="404">Campo nunca existiu.</response>
    /// <response code="410">Campo existe mas não está visível.</response>
    [HttpGet("campos/{campoSlug}")]
    [ProducesResponseType(typeof(ApiResponse<CampoLayoutVerbeteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<ActionResult<ApiResponse<CampoLayoutVerbeteDto>>> ObterCampo(string campoSlug, CancellationToken ct)
    {
        var campo = await _servico.ObterCampoAsync(campoSlug, ct);

        Response.Headers[HeaderNames.CacheControl] = PoliticaDeCache;

        return Ok(new ApiResponse<CampoLayoutVerbeteDto> { Data = campo });
    }
}
