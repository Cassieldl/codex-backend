using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>
/// Área interna de avisos — rota única de atualização/remoção/publicação para os dois escopos
/// (I14b/I14c/I14d — analise-backend.md §4.2/§7.4). Criação vive nas rotas aninhadas de
/// <see cref="ProvedoresInternoController"/> (I14a2) e <see cref="VersoesProvedorInternoController"/> (I14a).
/// </summary>
[ApiController]
[Route("api/v1/interno/avisos")]
[Authorize]
public sealed class AvisosInternoController : ControllerBase
{
    private readonly IAvisoServicoInterno _servico;

    public AvisosInternoController(IAvisoServicoInterno servico)
    {
        _servico = servico;
    }

    /// <summary>I14b — atualização (escopo nunca muda; <c>campoVersaoIds</c> só se aplica a aviso de versão).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "avisos-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<AvisoAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<AvisoAdminDto>>> Atualizar(int id, AtualizarAvisoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<AvisoAdminDto> { Data = atualizado, Message = "Aviso atualizado com sucesso." });
    }

    /// <summary>I14c — remoção (soft delete).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "avisos-remover")]
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

    /// <summary>I14d (M19) — publicar/despublicar, com cascata em <c>codex.itensbusca</c>.</summary>
    [HttpPut("{id:int}/publicacao")]
    [Authorize(Roles = "avisos-publicar")]
    [ProducesResponseType(typeof(ApiResponse<AvisoAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AvisoAdminDto>>> AtualizarPublicacao(int id, AtualizarPublicacaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarPublicacaoAsync(id, dto.Publicado, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<AvisoAdminDto> { Data = atualizado });
    }
}
