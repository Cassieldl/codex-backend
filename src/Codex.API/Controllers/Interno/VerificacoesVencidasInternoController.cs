using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Application.Services.Interno;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>
/// Rotina de verificação (escopo funcional v3.1 §9.1/§11): versões de provedor que nunca foram
/// verificadas ou cuja verificação passou da idade informada, das mais vencidas para as menos.
/// </summary>
[ApiController]
[Route("api/v1/interno/versoes/verificacoes-vencidas")]
[Authorize]
public sealed class VerificacoesVencidasInternoController : ControllerBase
{
    private readonly IVerificacaoVencidaServicoInterno _servico;

    public VerificacoesVencidasInternoController(IVerificacaoVencidaServicoInterno servico)
    {
        _servico = servico;
    }

    /// <summary>
    /// Lista as versões ativas, de provedores ativos, com verificação vencida. Vence quem nunca foi
    /// verificado ou foi verificado há <paramref name="dias"/> dias ou mais (90 dias atrás já está
    /// vencida com <c>dias=90</c>). Ordem fixa: nunca verificadas primeiro, depois da verificação
    /// mais antiga para a mais nova. Versão em rascunho também entra, sinalizada em <c>publicado</c>.
    /// </summary>
    /// <param name="dias">Idade mínima da verificação, em dias (1 a 3650; padrão 90).</param>
    /// <param name="pagina">Página, a partir de 1.</param>
    /// <param name="tamanhoPagina">Itens por página (padrão 25, máximo 200).</param>
    /// <response code="200">Página de versões vencidas.</response>
    /// <response code="422"><paramref name="dias"/> fora da faixa de 1 a 3650.</response>
    [HttpGet]
    [Authorize(Roles = "versoesprovedor-buscar")]
    [ProducesResponseType(typeof(PaginatedResponse<VersaoVerificacaoVencidaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PaginatedResponse<VersaoVerificacaoVencidaDto>>> Listar(
        [FromQuery] int dias = VerificacaoVencidaServicoInterno.DiasPadrao,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        CancellationToken ct = default)
    {
        var resultado = await _servico.ListarAsync(new FiltroVerificacaoVencida(dias, pagina, tamanhoPagina), ct);

        return Ok(new PaginatedResponse<VersaoVerificacaoVencidaDto>
        {
            Items = resultado.Itens,
            TotalCount = resultado.TotalCount,
            // Ecoa o que o serviço normalizou, não o que o cliente pediu.
            Page = resultado.Pagina,
            PageSize = resultado.TamanhoPagina,
        });
    }
}
