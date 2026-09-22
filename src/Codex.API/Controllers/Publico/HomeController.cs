using Codex.API.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Codex.API.Controllers.Publico;

/// <summary>
/// Agregado da Home (P9, analise-backend.md §6.1) — a porta de entrada anônima do portal. Um
/// round-trip só: comunicados no ar, provedores por categoria, resumo do Layout InvoiCy e os
/// artigos mais recentes.
///
/// <para>
/// Sem <c>try/catch</c> local: o serviço não lança para acervo vazio (é o estado inicial do
/// produto, <c>200</c> com coleções vazias) e não há slug a resolver, logo não existe caso de
/// 404/410/308 a interceptar aqui.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/home")]
[AllowAnonymous]
public sealed class HomeController : ControllerBase
{
    /// <summary>
    /// Conteúdo editorial que muda em minutos, não em segundos: 60 s de cache compartilhado com
    /// mais 5 min servindo a cópia velha enquanto revalida. Publicar um comunicado leva até um
    /// minuto para aparecer, e é o trade-off aceito para a rota mais chamada do portal.
    /// </summary>
    private const string PoliticaDeCache = "public, max-age=60, stale-while-revalidate=300";

    private readonly IHomeServicoPublico _servico;

    public HomeController(IHomeServicoPublico servico)
    {
        _servico = servico;
    }

    /// <summary>P9 — agregado da tela inicial. Sempre 200, mesmo com o acervo inteiro vazio.</summary>
    /// <response code="200">Agregado da Home.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HomeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HomeDto>>> Obter(CancellationToken ct)
    {
        var home = await _servico.ObterAsync(ct);

        Response.Headers[HeaderNames.CacheControl] = PoliticaDeCache;

        return Ok(new ApiResponse<HomeDto> { Data = home });
    }
}
