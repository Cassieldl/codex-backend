using Codex.API.Common;
using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Codex.API.Controllers.Publico;

/// <summary>
/// Comunicados no ar para o contexto da página aberta — provedor, versão ou os provedores
/// aplicáveis a um erro. Rota própria, e não campo de P3/P4/P6, porque a ETag dessas páginas vem
/// do <c>xmin</c> da linha: comunicado que vence pela passagem do tempo não toca linha nenhuma, e
/// embutido na página ele seguiria aparecendo depois de vencido para quem revalida por ETag. Aqui
/// a resposta não tem ETag de conteúdo, só <c>Cache-Control</c> curto.
/// </summary>
[ApiController]
[Route("api/v1/comunicados")]
[AllowAnonymous]
[EnableRateLimiting("vitrine")]
public sealed class ComunicadosController : ControllerBase
{
    private readonly IComunicadoServicoPublico _servico;

    public ComunicadosController(IComunicadoServicoPublico servico)
    {
        _servico = servico;
    }

    /// <summary>
    /// Comunicados no ar. Sem parâmetros, só os globais. Com <c>provedor</c> (repetível, até 10),
    /// somam-se os do provedor e os das versões dele; com <c>versao</c> (exige exatamente um
    /// <c>provedor</c>), somam-se só os da versão. Slug desconhecido ou de conteúdo não público
    /// não é erro: a lista traz o que vale e é público.
    /// </summary>
    /// <param name="provedor">Slug do provedor (repetível, até 10).</param>
    /// <param name="versao">Slug da versão — só junto de exatamente um provedor.</param>
    /// <response code="200">Comunicados, severidade crítica primeiro e mais recente antes; lista vazia quando não há.</response>
    /// <response code="400">Parâmetros malformados.</response>
    /// <response code="429">Limite de requisições por IP excedido.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<ComunicadoPublicoDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Listar(
        [FromQuery(Name = "provedor")] string[]? provedor,
        [FromQuery] string? versao,
        CancellationToken ct = default)
    {
        try
        {
            var comunicados = await _servico.ListarAsync(provedor, versao, ct);

            Response.Headers[HeaderNames.CacheControl] = PoliticasDeCachePublico.ConteudoEditorialCurto;

            return Ok(new ApiResponse<IReadOnlyCollection<ComunicadoPublicoDto>> { Data = comunicados });
        }
        catch (ParametroConsultaInvalidoException excecao)
        {
            return BadRequest(new { error = new { code = "PARAMETRO_INVALIDO", message = excecao.Message } });
        }
    }
}
