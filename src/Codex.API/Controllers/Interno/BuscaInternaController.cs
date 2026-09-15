using Codex.API.Common;
using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>
/// I21 — busca da área interna (M15, M21): mesmo shape de resposta de P1, mas enxerga rascunho
/// (<see cref="IBuscaService.BuscarAsync"/> chamado com <c>incluirRascunho: true</c>). Endpoint
/// próprio — nunca reusar o público, exatamente pela diferença de visibilidade (U1).
/// </summary>
[ApiController]
[Route("api/v1/interno/busca")]
[Authorize(Roles = "indicebusca-buscar")]
public sealed class BuscaInternaController : ControllerBase
{
    private readonly IBuscaService _buscaService;

    public BuscaInternaController(IBuscaService buscaService)
    {
        _buscaService = buscaService;
    }

    /// <summary>Busca interna — mesmos parâmetros de P1, mas resultado inclui rascunho.</summary>
    /// <response code="200">Sempre — inclusive quando não há resultado (qualidade "vazia").</response>
    /// <response code="400">Termo ausente ou fora do intervalo de 2–300 caracteres.</response>
    /// <response code="401">Sem token.</response>
    /// <response code="403">Token sem a role <c>indicebusca-buscar</c>.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ResultadoBuscaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? q,
        [FromQuery] string? provedor,
        [FromQuery(Name = "tipo")] string[]? tipo,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default)
    {
        try
        {
            var entrada = new BuscaConsultaEntrada
            {
                Termo = q ?? string.Empty,
                ProvedorSlug = provedor,
                Tipos = tipo,
                Pagina = pagina,
                TamanhoPagina = tamanhoPagina,
            };

            var resultado = await _buscaService.BuscarAsync(entrada, incluirRascunho: true, ct);
            return Ok(new { data = resultado, message = (string?)null });
        }
        // Mesmo catch específico de P1 (BAIXO-B08) — a superfície é autenticada, mas o motivo é o
        // mesmo: só mensagem nossa sai no corpo; o inesperado vira 500 genérico.
        catch (TermoBuscaInvalidoException ex)
        {
            return BadRequest(new { error = new { code = "PARAMETRO_INVALIDO", message = ex.Message } });
        }
    }
}
