using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>I22 — reconstrução total do índice de busca (analise-backend.md §4.2/§7.4).</summary>
[ApiController]
[Route("api/v1/interno/indice-busca")]
[Authorize]
public sealed class IndiceBuscaInternoController : ControllerBase
{
    private readonly IIndiceBuscaReconstrucaoServico _servico;

    public IndiceBuscaInternoController(IIndiceBuscaReconstrucaoServico servico)
    {
        _servico = servico;
    }

    /// <summary>I22 — reprocessa toda entidade ativa das 5 tabelas de origem e recalcula visibilidade.</summary>
    [HttpPost("reconstrucao")]
    [Authorize(Roles = "indicebusca-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<ReconstrucaoIndiceBuscaResultadoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReconstrucaoIndiceBuscaResultadoDto>>> Reconstruir(CancellationToken ct)
    {
        var resultado = await _servico.ReconstruirAsync(ct);
        return Ok(new ApiResponse<ReconstrucaoIndiceBuscaResultadoDto> { Data = resultado });
    }
}
