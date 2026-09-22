using Codex.API.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Publico;

/// <summary>Endpoints públicos e anônimos de erro (P5/P6, analise-backend.md §4.1).</summary>
[ApiController]
[Route("api/v1/erros")]
[AllowAnonymous]
public sealed class ErrosController : ControllerBase
{
    private readonly IErroServicoPublico _servico;

    public ErrosController(IErroServicoPublico servico)
    {
        _servico = servico;
    }

    /// <summary>Lista erros publicados (P5). <paramref name="codigo"/> é o atalho exato de M2.</summary>
    /// <response code="200">Página de erros publicados.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<ErroListaItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<ErroListaItemDto>>> Listar(
        [FromQuery] string? codigo,
        [FromQuery] string? provedor,
        [FromQuery] OrigemErro? origem,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        CancellationToken ct = default)
    {
        var filtro = new FiltroErroPublico(codigo, provedor, origem, pagina, tamanhoPagina);
        var resultado = await _servico.ListarAsync(filtro, ct);

        return Ok(new PaginatedResponse<ErroListaItemDto>
        {
            Items = resultado.Itens,
            TotalCount = resultado.TotalCount,
            // Ecoa o que o serviço normalizou — refazer a conta aqui já fez o envelope afirmar uma
            // página que não foi servida.
            Page = resultado.Pagina,
            PageSize = resultado.TamanhoPagina,
        });
    }

    /// <summary>Página do erro (P6) — <c>versoesAplicaveis</c> pode vir vazio (U9), nunca é erro.</summary>
    /// <response code="200">Erro publicado.</response>
    /// <response code="308">Slug antigo — redireciona para o slug atual.</response>
    /// <response code="404">Erro nunca existiu.</response>
    /// <response code="410">Erro existe mas está despublicado.</response>
    [HttpGet("{erroSlug}")]
    [ProducesResponseType(typeof(ApiResponse<ErroDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status308PermanentRedirect)]
    public async Task<IActionResult> ObterDetalhe(string erroSlug, CancellationToken ct)
    {
        try
        {
            var detalhe = await _servico.ObterDetalheAsync(erroSlug, ct);
            return Ok(new ApiResponse<ErroDetalheDto> { Data = detalhe });
        }
        catch (EntidadeNaoEncontradaException)
        {
            var destino = await _servico.ObterRedirecionamentoAsync(erroSlug, ct);
            // F1 (arquitetura.md §5.F): o Location aponta para a rota da API, não para a rota de
            // app. XMLHttpRequest segue o 308 sozinho e nunca expõe status nem Location ao
            // cliente — apontando para a rota de app, o auto-follow bateria num endereço que a
            // API não serve e devolveria 404. DeCaminhoPublico já valida o caminho interno e
            // devolve null quando o destino não é traduzível: aí segue o 404 honesto.
            var rotaApiDoDestino = RotaApiPublica.DeCaminhoPublico(destino);
            if (rotaApiDoDestino is not null)
                return RedirectPermanentPreserveMethod(rotaApiDoDestino);

            return NotFound(new { error = new { code = "NAO_ENCONTRADO", message = $"Erro '{erroSlug}' não encontrado." } });
        }
        catch (ConteudoDespublicadoException excecao)
        {
            return MapeadorErroPublico.Mapear(this, excecao);
        }
    }
}
