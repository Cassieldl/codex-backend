using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>Área interna de campos canônicos do layout (I17a-I17c, risco #2 do DBA — analise-backend.md §4.2/§7.4).</summary>
[ApiController]
[Route("api/v1/interno/campos-layout")]
[Authorize]
public sealed class CamposLayoutInternoController : ControllerBase
{
    private readonly ICampoLayoutServicoInterno _servico;

    public CamposLayoutInternoController(ICampoLayoutServicoInterno servico)
    {
        _servico = servico;
    }

    /// <summary>
    /// I17a — atualização; nunca <c>tipo</c>/<c>tamanho</c>/<c>mascara</c>/<c>obrigatoriedade</c>
    /// (R3). <c>versaoLayoutDescontinuadoId</c> obrigatório se <c>estado="Descontinuado"</c>,
    /// proibido caso contrário.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "campolayout-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<CampoLayoutResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CampoLayoutResponseDto>>> Atualizar(int id, AtualizarCampoLayoutDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<CampoLayoutResponseDto> { Data = atualizado, Message = "Campo canônico atualizado com sucesso." });
    }

    /// <summary>I17b — remoção (soft delete).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "campolayout-remover")]
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

    /// <summary>I17c (M19) — publicar/despublicar.</summary>
    [HttpPut("{id:int}/publicacao")]
    [Authorize(Roles = "campolayout-publicar")]
    [ProducesResponseType(typeof(ApiResponse<CampoLayoutResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CampoLayoutResponseDto>>> AtualizarPublicacao(int id, AtualizarPublicacaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarPublicacaoAsync(id, dto.Publicado, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<CampoLayoutResponseDto> { Data = atualizado });
    }
}
