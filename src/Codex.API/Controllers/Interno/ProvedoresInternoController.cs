using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>
/// Área interna de provedores (I1-I5, I3b, I6, I7, I14a2 — analise-backend.md §4.2/§7.4). Toda
/// rota exige role <c>&lt;entidade&gt;-&lt;acao&gt;</c> — nunca <c>[AllowAnonymous]</c>, nunca
/// endpoint sem <c>[Authorize]</c> (AP-SECURITY-003).
/// </summary>
[ApiController]
[Route("api/v1/interno/provedores")]
[Authorize]
public sealed class ProvedoresInternoController : ControllerBase
{
    private readonly IProvedorServicoInterno _servico;
    private readonly IVersaoProvedorServicoInterno _versaoServico;
    private readonly IAvisoServicoInterno _avisoServico;

    public ProvedoresInternoController(
        IProvedorServicoInterno servico, IVersaoProvedorServicoInterno versaoServico, IAvisoServicoInterno avisoServico)
    {
        _servico = servico;
        _versaoServico = versaoServico;
        _avisoServico = avisoServico;
    }

    /// <summary>I1 — lista provedores (enxerga rascunho).</summary>
    [HttpGet]
    [Authorize(Roles = "provedores-buscar")]
    [ProducesResponseType(typeof(PaginatedResponse<ProvedorAdminListaItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<ProvedorAdminListaItemDto>>> Listar(
        [FromQuery] string? q,
        [FromQuery] CategoriaProvedor? categoria,
        [FromQuery] bool? publicado,
        [FromQuery] string? ordenarPor,
        [FromQuery] string? direcao,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        CancellationToken ct = default)
    {
        var filtro = new FiltroProvedorInterno(q, categoria, publicado, ordenarPor, direcao, pagina, tamanhoPagina);
        var resultado = await _servico.ListarAsync(filtro, ct);

        return Ok(new PaginatedResponse<ProvedorAdminListaItemDto>
        {
            Items = resultado.Itens,
            TotalCount = resultado.TotalCount,
            // Ecoa o que o serviço normalizou — refazer a conta aqui já fez o envelope afirmar uma
            // página que não foi servida.
            Page = resultado.Pagina,
            PageSize = resultado.TamanhoPagina,
        });
    }

    /// <summary>I2 — detalhe (inclui todos os avisos de provedor, publicados e rascunho).</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "provedores-buscar")]
    [ProducesResponseType(typeof(ApiResponse<ProvedorAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProvedorAdminDetalheDto>>> ObterDetalhe(int id, CancellationToken ct)
    {
        var detalhe = await _servico.ObterDetalheAsync(id, ct);
        Response.PublicarETagConcorrencia(detalhe.VersaoConcorrencia);
        return Ok(new ApiResponse<ProvedorAdminDetalheDto> { Data = detalhe });
    }

    /// <summary>I3 — criação (nunca aceita <c>publicado</c>).</summary>
    [HttpPost]
    [Authorize(Roles = "provedores-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<ProvedorAdminDetalheDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ProvedorAdminDetalheDto>>> Criar(CriarProvedorDto dto, CancellationToken ct)
    {
        var criado = await _servico.CriarAsync(dto, ct);
        Response.PublicarETagConcorrencia(criado.VersaoConcorrencia);
        return CreatedAtAction(nameof(ObterDetalhe), new { id = criado.Id },
            new ApiResponse<ProvedorAdminDetalheDto> { Data = criado, Message = "Provedor criado com sucesso." });
    }

    /// <summary>I4 — atualização (sem <c>identificadorInvoicy</c>/<c>publicado</c>).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "provedores-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<ProvedorAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ProvedorAdminDetalheDto>>> Atualizar(int id, AtualizarProvedorDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<ProvedorAdminDetalheDto> { Data = atualizado, Message = "Provedor atualizado com sucesso." });
    }

    /// <summary>I37 (P-10) — troca o slug do provedor e grava o redirecionamento do slug antigo.</summary>
    /// <response code="200">Slug trocado; o slug antigo passa a responder 308.</response>
    /// <response code="400"><c>If-Match</c> ausente ou malformado.</response>
    /// <response code="404">Provedor não encontrado.</response>
    /// <response code="409">Slug já pertence a outro provedor, ou conflito de concorrência.</response>
    /// <response code="422">Slug igual ao atual, ou slug que normaliza para vazio.</response>
    [HttpPut("{id:int}/slug")]
    [Authorize(Roles = "provedores-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<ProvedorAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ProvedorAdminDetalheDto>>> AtualizarSlug(int id, AlterarSlugDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarSlugAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<ProvedorAdminDetalheDto> { Data = atualizado, Message = "Slug do provedor atualizado com sucesso." });
    }

    /// <summary>I5 — remoção (soft delete).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "provedores-remover")]
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

    /// <summary>I3b (M19) — publicar/despublicar, com cascata em <c>codex.itensbusca</c>.</summary>
    [HttpPut("{id:int}/publicacao")]
    [Authorize(Roles = "provedores-publicar")]
    [ProducesResponseType(typeof(ApiResponse<ProvedorAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ProvedorAdminDetalheDto>>> AtualizarPublicacao(int id, AtualizarPublicacaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarPublicacaoAsync(id, dto.Publicado, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<ProvedorAdminDetalheDto> { Data = atualizado });
    }

    /// <summary>I6 — versões do provedor (lista completa, sem paginação).</summary>
    [HttpGet("{id:int}/versoes")]
    [Authorize(Roles = "versoesprovedor-buscar")]
    [ProducesResponseType(typeof(ApiResponse<VersaoAdminListaItemDto[]>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<VersaoAdminListaItemDto>>>> ListarVersoes(int id, CancellationToken ct)
    {
        var versoes = await _versaoServico.ListarPorProvedorAsync(id, ct);
        return Ok(new ApiResponse<IReadOnlyCollection<VersaoAdminListaItemDto>> { Data = versoes });
    }

    /// <summary>I7 — cria versão do provedor.</summary>
    [HttpPost("{id:int}/versoes")]
    [Authorize(Roles = "versoesprovedor-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<VersaoAdminDetalheDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<VersaoAdminDetalheDto>>> CriarVersao(int id, CriarVersaoDto dto, CancellationToken ct)
    {
        var criada = await _versaoServico.CriarAsync(id, dto, ct);
        Response.PublicarETagConcorrencia(criada.VersaoConcorrencia);
        return CreatedAtAction(
            nameof(VersoesProvedorInternoController.ObterDetalhe), "VersoesProvedorInterno", new { id = criada.Id },
            new ApiResponse<VersaoAdminDetalheDto> { Data = criada, Message = "Versão criada com sucesso." });
    }

    /// <summary>I14a2 (U8) — cria aviso de escopo PROVEDOR (nunca aceita <c>campoVersaoIds</c>, propriedade nem existe no DTO).</summary>
    [HttpPost("{id:int}/avisos")]
    [Authorize(Roles = "avisos-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<AvisoAdminDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<AvisoAdminDto>>> CriarAvisoDeProvedor(int id, CriarAvisoDeProvedorDto dto, CancellationToken ct)
    {
        var criado = await _avisoServico.CriarDeProvedorAsync(id, dto, ct);
        Response.PublicarETagConcorrencia(criado.VersaoConcorrencia);

        // Sem GET /interno/avisos/{id} dedicado no contrato (I14a/a2/b/c/d são só
        // criação/atualização/remoção/publicação) — Location aponta para o recurso mesmo sem
        // uma action de leitura correspondente para o CreatedAtAction resolver.
        return Created($"/api/v1/interno/avisos/{criado.Id}",
            new ApiResponse<AvisoAdminDto> { Data = criado, Message = "Aviso criado com sucesso." });
    }
}
