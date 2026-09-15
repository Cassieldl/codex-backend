using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>Área interna de campos de versão (I13a/I13b — analise-backend.md §4.2/§7.4).</summary>
[ApiController]
[Route("api/v1/interno/campos-versao")]
[Authorize]
public sealed class CamposVersaoInternoController : ControllerBase
{
    private readonly ICampoVersaoServicoInterno _servico;

    public CamposVersaoInternoController(ICampoVersaoServicoInterno servico)
    {
        _servico = servico;
    }

    /// <summary>I13a — atualização (sem <c>campoLayoutId</c>).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "camposversao-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<CampoVersaoAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CampoVersaoAdminDto>>> Atualizar(int id, AtualizarCampoVersaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<CampoVersaoAdminDto> { Data = atualizado, Message = "Campo atualizado com sucesso." });
    }

    /// <summary>I13b — remoção (soft delete).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "camposversao-remover")]
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
}
