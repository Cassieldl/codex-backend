using Codex.API.Common;
using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codex.API.Controllers.Publico;

/// <summary>
/// P1 — busca pública (analise-backend.md §4.1). Endpoint anônimo, caro e sujeito a rate limit
/// por IP (política "busca", ver <c>RateLimitingExtensoes</c>). Nunca enxerga rascunho —
/// <see cref="IBuscaService.BuscarAsync"/> é chamado sempre com <c>incluirRascunho: false</c>.
/// </summary>
[ApiController]
[Route("api/v1/busca")]
[AllowAnonymous]
[EnableRateLimiting("busca")]
public sealed class BuscaController : ControllerBase
{
    private readonly IBuscaService _buscaService;

    public BuscaController(IBuscaService buscaService)
    {
        _buscaService = buscaService;
    }

    /// <summary>Busca pública em erros, provedores, avisos e artigos. Versão de provedor não entra: o conteúdo dela fica na página do provedor.</summary>
    /// <param name="q">Termo de busca (2–300 caracteres).</param>
    /// <param name="provedor">Slug do provedor para restringir o resultado.</param>
    /// <param name="tipo">
    /// Tipos de item a incluir (repetível). Valor desconhecido é ignorado; se NENHUM dos valores
    /// existir, o recorte continua valendo e o resultado é vazio — pedir um recorte inexistente
    /// nunca devolve o conjunto inteiro.
    /// </param>
    /// <param name="pagina">Página, começando em 1.</param>
    /// <param name="tamanhoPagina">Itens por página (cap 50).</param>
    /// <response code="200">Sempre — inclusive quando não há resultado (qualidade "vazia").</response>
    /// <response code="400">Termo ausente ou fora do intervalo de 2–300 caracteres.</response>
    /// <response code="429">Limite de requisições por IP excedido.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ResultadoBuscaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
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

            var resultado = await _buscaService.BuscarAsync(entrada, incluirRascunho: false, ct);
            return Ok(new { data = resultado, message = (string?)null });
        }
        // Tipo específico, nunca ArgumentException (BAIXO-B08): a mensagem devolvida aqui é sempre
        // texto nosso, e qualquer argumento inválido inesperado da pilha abaixo cai no middleware
        // global como 500 genérico em vez de virar 400 com detalhe interno.
        catch (TermoBuscaInvalidoException ex)
        {
            return BadRequest(new { error = new { code = "PARAMETRO_INVALIDO", message = ex.Message } });
        }
    }
}
