using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>Área interna de comunicados (I30-I35 — analise-backend.md §6).</summary>
[ApiController]
[Route("api/v1/interno/comunicados")]
[Authorize]
public sealed class ComunicadosInternoController : ControllerBase
{
    private readonly IComunicadoServicoInterno _servico;

    public ComunicadosInternoController(IComunicadoServicoInterno servico)
    {
        _servico = servico;
    }

    /// <summary>I30 — lista paginada. <c>incluirEncerrados=false</c> (default) esconde o que já venceu; rascunho continua visível.</summary>
    [HttpGet]
    [Authorize(Roles = "comunicados-buscar")]
    [ProducesResponseType(typeof(PaginatedResponse<ComunicadoAdminListaItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<ComunicadoAdminListaItemDto>>> Listar(
        [FromQuery] Severidade? severidade,
        [FromQuery] AbrangenciaComunicado? abrangencia,
        [FromQuery] int? provedorId,
        [FromQuery] bool incluirEncerrados = false,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        CancellationToken ct = default)
    {
        var filtro = new FiltroComunicadoInterno(severidade, abrangencia, provedorId, incluirEncerrados, pagina, tamanhoPagina);
        var resultado = await _servico.ListarAsync(filtro, ct);

        return Ok(new PaginatedResponse<ComunicadoAdminListaItemDto>
        {
            Items = resultado.Itens,
            TotalCount = resultado.TotalCount,
            // Ecoa o que o serviço normalizou — refazer a conta aqui já fez o envelope afirmar uma
            // página que não foi servida.
            Page = resultado.Pagina,
            PageSize = resultado.TamanhoPagina,
        });
    }

    /// <summary>I31 — detalhe (traz <c>corpo</c> e os ids brutos de escopo, que a listagem não tem).</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "comunicados-buscar")]
    [ProducesResponseType(typeof(ApiResponse<ComunicadoAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ComunicadoAdminDto>>> ObterDetalhe(int id, CancellationToken ct)
    {
        var detalhe = await _servico.ObterDetalheAsync(id, ct);
        Response.PublicarETagConcorrencia(detalhe.VersaoConcorrencia);
        return Ok(new ApiResponse<ComunicadoAdminDto> { Data = detalhe });
    }

    /// <summary>
    /// I32 — criação, opcionalmente já publicada (<c>publicar: true</c>, D-1). A role
    /// <c>comunicados-publicar</c> é exigida em cima disso: a condição é um OR entre duas roles e
    /// por isso não cabe no atributo, tem de ser checada aqui.
    /// </summary>
    /// <response code="201">Comunicado criado (publicado ou em rascunho — ver <c>data.publicado</c>).</response>
    /// <response code="403">Pediu <c>publicar: true</c> sem a role <c>comunicados-publicar</c>.</response>
    /// <response code="422">Validação de campo ou regra de negócio.</response>
    [HttpPost]
    [Authorize(Roles = "comunicados-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<ComunicadoAdminDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ComunicadoAdminDto>>> Criar(CriarComunicadoDto dto, CancellationToken ct)
    {
        // 403 explícito, nunca criar em rascunho em silêncio: quem pediu para publicar precisa
        // saber que não publicou (decisoes.md D-1).
        if (dto.Publicar && !User.IsInRole("comunicados-publicar"))
            return Forbid();

        var criado = await _servico.CriarAsync(dto, ct);
        Response.PublicarETagConcorrencia(criado.VersaoConcorrencia);

        // A mensagem descreve o que ACONTECEU — lida de criado.Publicado, nunca do dto.Publicar
        // que o cliente enviou (PQ-16).
        var mensagem = criado.Publicado ? "Comunicado publicado." : "Comunicado criado como rascunho.";

        return CreatedAtAction(nameof(ObterDetalhe), new { id = criado.Id },
            new ApiResponse<ComunicadoAdminDto> { Data = criado, Message = mensagem });
    }

    /// <summary>I33 — atualização. É por aqui que se estende a expiração de um comunicado encerrado antes de republicar.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "comunicados-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<ComunicadoAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ComunicadoAdminDto>>> Atualizar(int id, AtualizarComunicadoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<ComunicadoAdminDto> { Data = atualizado, Message = "Comunicado atualizado com sucesso." });
    }

    /// <summary>I34 — remoção (soft delete). Sem cascata de busca: comunicado nunca é indexado (M-1).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "comunicados-remover")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        await _servico.RemoverAsync(id, versaoEsperada, ct);
        return NoContent();
    }

    /// <summary>
    /// I35 — publicar/despublicar. Publicar um comunicado já expirado é <c>422 COMUNICADO_EXPIRADO</c>
    /// (decisoes.md B-4); despublicar é sempre permitido.
    /// </summary>
    [HttpPut("{id:int}/publicacao")]
    [Authorize(Roles = "comunicados-publicar")]
    [ProducesResponseType(typeof(ApiResponse<ComunicadoAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ComunicadoAdminDto>>> AtualizarPublicacao(
        int id, AtualizarPublicacaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();

        // Publicar comunicado já expirado é 422 COMUNICADO_EXPIRADO (B-4): a RegraDeNegocioException
        // lançada pelo service carrega o código, e o middleware global o publica no envelope.
        var atualizado = await _servico.AtualizarPublicacaoAsync(id, dto.Publicado, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);

        return Ok(new ApiResponse<ComunicadoAdminDto> { Data = atualizado });
    }
}
