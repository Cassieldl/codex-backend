using Codex.API.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codex.API.Controllers.Publico;

/// <summary>Endpoints públicos e anônimos de artigo (P7/P8, analise-backend.md §4.2/§4.3).</summary>
[ApiController]
[Route("api/v1/artigos")]
[AllowAnonymous]
[EnableRateLimiting("vitrine")]
public sealed class ArtigosController : ControllerBase
{
    private readonly IArtigoServicoPublico _servico;

    public ArtigosController(IArtigoServicoPublico servico)
    {
        _servico = servico;
    }

    /// <summary>Índice público de artigos (P7). <paramref name="tipo"/> repetível — sempre 200, mesmo com zero resultados (U-3).</summary>
    /// <response code="200">Página de artigos publicados.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<ArtigoListaItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<ArtigoListaItemDto>>> Listar(
        [FromQuery] TipoArtigo[]? tipo,
        [FromQuery] string? q,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        CancellationToken ct = default)
    {
        var filtro = new FiltroArtigoPublico(tipo, q, pagina, tamanhoPagina);
        var resultado = await _servico.ListarAsync(filtro, ct);

        return Ok(new PaginatedResponse<ArtigoListaItemDto>
        {
            Items = resultado.Itens,
            TotalCount = resultado.TotalCount,
            // Ecoa o que o serviço normalizou — refazer a conta aqui já fez o envelope afirmar uma
            // página que não foi servida.
            Page = resultado.Pagina,
            PageSize = resultado.TamanhoPagina,
        });
    }

    /// <summary>Página do artigo (P8) — inclusive o reservado <c>/artigos/como-funciona</c> quando existir.</summary>
    /// <response code="200">Artigo publicado.</response>
    /// <response code="308">Slug antigo — redireciona para o slug atual.</response>
    /// <response code="404">Artigo nunca existiu (inclusive reservado ainda não criado).</response>
    /// <response code="410">Artigo existe mas está despublicado.</response>
    [HttpGet("{artigoSlug}")]
    [ProducesResponseType(typeof(ApiResponse<ArtigoDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status308PermanentRedirect)]
    public async Task<IActionResult> ObterDetalhe(string artigoSlug, CancellationToken ct)
    {
        try
        {
            var detalhe = await _servico.ObterDetalheAsync(artigoSlug, ct);
            return Ok(new ApiResponse<ArtigoDetalheDto> { Data = detalhe });
        }
        catch (EntidadeNaoEncontradaException)
        {
            var destino = await _servico.ObterRedirecionamentoAsync(artigoSlug, ct);
            // F1 (arquitetura.md §5.F): o Location aponta para a rota da API, não para a rota de
            // app. XMLHttpRequest segue o 308 sozinho e nunca expõe status nem Location ao
            // cliente — apontando para a rota de app, o auto-follow bateria num endereço que a
            // API não serve e devolveria 404. DeCaminhoPublico já valida o caminho interno e
            // devolve null quando o destino não é traduzível: aí segue o 404 honesto.
            var rotaApiDoDestino = RotaApiPublica.DeCaminhoPublico(destino);
            if (rotaApiDoDestino is not null)
                return RedirectPermanentPreserveMethod(rotaApiDoDestino);

            return NotFound(new { error = new { code = "NAO_ENCONTRADO", message = $"Artigo '{artigoSlug}' não encontrado." } });
        }
        catch (ConteudoDespublicadoException excecao)
        {
            return MapeadorErroPublico.Mapear(this, excecao);
        }
    }
}
