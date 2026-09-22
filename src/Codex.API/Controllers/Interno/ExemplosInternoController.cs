using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>
/// Área interna de exemplos (I38-I42 — analise-backend.md §5.1). Sem rota de publicação: exemplo
/// não tem publicação própria (decisão C1), a visibilidade cascateia da versão/provedor.
///
/// <para>
/// As rotas não compartilham um <c>[Route]</c> de classe porque a coleção é aninhada na versão
/// (<c>/versoes/{id}/exemplos</c>) e o item é de primeiro nível (<c>/exemplos/{id}</c>) — mesma
/// dupla de caminhos que campos de versão usam, só que ali dividida entre dois controllers. O
/// parâmetro de rota da coleção chama-se <c>id</c> (e não <c>versaoProvedorId</c>) por convenção
/// já estabelecida em <see cref="VersoesProvedorInternoController"/> para todas as sub-rotas de
/// versão.
/// </para>
/// </summary>
[ApiController]
[Authorize]
public sealed class ExemplosInternoController : ControllerBase
{
    private readonly IExemploServicoInterno _servico;

    public ExemplosInternoController(IExemploServicoInterno servico)
    {
        _servico = servico;
    }

    /// <summary>I38 — lista completa de exemplos da versão (sem paginação: o cap é de 10 por versão).</summary>
    /// <param name="id">Id da versão de provedor.</param>
    /// <response code="200">Lista devolvida (vazia quando a versão não tem exemplos).</response>
    /// <response code="404">Versão de provedor não encontrada.</response>
    [HttpGet("api/v1/interno/versoes/{id:int}/exemplos")]
    [Authorize(Roles = "exemplos-buscar")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<ExemploAdminDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ExemploAdminDto>>>> ListarPorVersao(int id, CancellationToken ct)
    {
        var exemplos = await _servico.ListarPorVersaoAsync(id, ct);
        return Ok(new ApiResponse<IReadOnlyCollection<ExemploAdminDto>> { Data = exemplos });
    }

    /// <summary>I39 — cria exemplo na versão (422 se já no cap de 10, C3).</summary>
    /// <param name="id">Id da versão de provedor.</param>
    /// <param name="dto">Nome, descrição e XML do cenário — <c>slug</c> nunca vem do cliente.</param>
    /// <response code="201">Exemplo criado.</response>
    /// <response code="404">Versão de provedor não encontrada.</response>
    /// <response code="409">Já existe exemplo com o mesmo nome na versão.</response>
    /// <response code="422">Validação de entrada ou cap de 10 exemplos excedido.</response>
    [HttpPost("api/v1/interno/versoes/{id:int}/exemplos")]
    [Authorize(Roles = "exemplos-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<ExemploAdminDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ExemploAdminDto>>> Criar(int id, CriarExemploDto dto, CancellationToken ct)
    {
        var criado = await _servico.CriarAsync(id, dto, ct);
        Response.PublicarETagConcorrencia(criado.VersaoConcorrencia);
        return Created($"/api/v1/interno/exemplos/{criado.Id}",
            new ApiResponse<ExemploAdminDto> { Data = criado, Message = "Exemplo criado com sucesso." });
    }

    /// <summary>I40 — detalhe do exemplo.</summary>
    /// <param name="id">Id do exemplo.</param>
    /// <response code="200">Exemplo encontrado.</response>
    /// <response code="404">Exemplo não encontrado.</response>
    [HttpGet("api/v1/interno/exemplos/{id:int}")]
    [Authorize(Roles = "exemplos-buscar")]
    [ProducesResponseType(typeof(ApiResponse<ExemploAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ExemploAdminDto>>> ObterPorId(int id, CancellationToken ct)
    {
        var exemplo = await _servico.ObterPorIdAsync(id, ct);
        Response.PublicarETagConcorrencia(exemplo.VersaoConcorrencia);
        return Ok(new ApiResponse<ExemploAdminDto> { Data = exemplo });
    }

    /// <summary>I41 — atualiza nome, descrição e XML (slug nunca é recalculado).</summary>
    /// <param name="id">Id do exemplo.</param>
    /// <param name="dto">Conteúdo novo do cenário.</param>
    /// <response code="200">Exemplo atualizado.</response>
    /// <response code="400"><c>If-Match</c> ausente ou malformado.</response>
    /// <response code="404">Exemplo não encontrado.</response>
    /// <response code="409">Conflito de concorrência ou nome duplicado na versão.</response>
    /// <response code="422">Validação de entrada.</response>
    [HttpPut("api/v1/interno/exemplos/{id:int}")]
    [Authorize(Roles = "exemplos-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<ExemploAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ExemploAdminDto>>> Atualizar(int id, AtualizarExemploDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<ExemploAdminDto> { Data = atualizado, Message = "Exemplo atualizado com sucesso." });
    }

    /// <summary>I42 — remoção (soft delete); não toca <c>codex.itensbusca</c>, exemplo nunca é indexado.</summary>
    /// <param name="id">Id do exemplo.</param>
    /// <response code="204">Exemplo removido.</response>
    /// <response code="400"><c>If-Match</c> ausente ou malformado.</response>
    /// <response code="404">Exemplo não encontrado.</response>
    /// <response code="409">Conflito de concorrência.</response>
    [HttpDelete("api/v1/interno/exemplos/{id:int}")]
    [Authorize(Roles = "exemplos-remover")]
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
