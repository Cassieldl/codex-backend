using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>Área interna de artigos (I23-I29, I36 — analise-backend.md §5).</summary>
[ApiController]
[Route("api/v1/interno/artigos")]
[Authorize]
public sealed class ArtigosInternoController : ControllerBase
{
    private readonly IArtigoServicoInterno _servico;

    public ArtigosInternoController(IArtigoServicoInterno servico)
    {
        _servico = servico;
    }

    /// <summary>I23 — lista paginada (enxerga rascunho).</summary>
    [HttpGet]
    [Authorize(Roles = "artigos-buscar")]
    [ProducesResponseType(typeof(PaginatedResponse<ArtigoAdminListaItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<ArtigoAdminListaItemDto>>> Listar(
        [FromQuery] string? q,
        [FromQuery] TipoArtigo? tipo,
        [FromQuery] bool? publicado,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        CancellationToken ct = default)
    {
        var filtro = new FiltroArtigoInterno(q, tipo, publicado, pagina, tamanhoPagina);
        var resultado = await _servico.ListarAsync(filtro, ct);

        return Ok(new PaginatedResponse<ArtigoAdminListaItemDto>
        {
            Items = resultado.Itens,
            TotalCount = resultado.TotalCount,
            // Ecoa o que o serviço normalizou — refazer a conta aqui já fez o envelope afirmar uma
            // página que não foi servida.
            Page = resultado.Pagina,
            PageSize = resultado.TamanhoPagina,
        });
    }

    /// <summary>I36 — resolução de F-2: existência do slug reservado (ou qualquer outro) sem varredura de página.</summary>
    [HttpGet("existe")]
    [Authorize(Roles = "artigos-buscar")]
    [ProducesResponseType(typeof(ApiResponse<ArtigoExisteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ArtigoExisteDto>>> Existe([FromQuery] string? slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return BadRequest(new { error = new { code = "SLUG_OBRIGATORIO", message = "O parâmetro 'slug' é obrigatório." } });

        var resultado = await _servico.ExisteAsync(slug, ct);
        return Ok(new ApiResponse<ArtigoExisteDto> { Data = resultado });
    }

    /// <summary>I24 — detalhe interno do artigo (id numérico — não confundir com P8, por slug, público).</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "artigos-buscar")]
    [ProducesResponseType(typeof(ApiResponse<ArtigoAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ArtigoAdminDto>>> ObterDetalhe(int id, CancellationToken ct)
    {
        var detalhe = await _servico.ObterDetalheAsync(id, ct);
        Response.PublicarETagConcorrencia(detalhe.VersaoConcorrencia);
        return Ok(new ApiResponse<ArtigoAdminDto> { Data = detalhe });
    }

    /// <summary>I25 — criação (sem <c>publicado</c>).</summary>
    [HttpPost]
    [Authorize(Roles = "artigos-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<ArtigoAdminDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ArtigoAdminDto>>> Criar(CriarArtigoDto dto, CancellationToken ct)
    {
        var criado = await _servico.CriarAsync(dto, ct);
        Response.PublicarETagConcorrencia(criado.VersaoConcorrencia);
        return CreatedAtAction(nameof(ObterDetalhe), new { id = criado.Id },
            new ApiResponse<ArtigoAdminDto> { Data = criado, Message = "Artigo criado com sucesso." });
    }

    /// <summary>I26 — atualização. <c>tipo</c> é editável (decisoes.md B-2) e dispara reindexação.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "artigos-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<ArtigoAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ArtigoAdminDto>>> Atualizar(int id, AtualizarArtigoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<ArtigoAdminDto> { Data = atualizado, Message = "Artigo atualizado com sucesso." });
    }

    /// <summary>I27 — remoção (soft delete). Artigo reservado nunca pode ser removido (422 <c>VALIDACAO_NEGOCIO</c>).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "artigos-remover")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        await _servico.RemoverAsync(id, versaoEsperada, ct);
        return NoContent();
    }

    /// <summary>I28 — publicar/despublicar. Despublicar o artigo reservado é 422 <c>VALIDACAO_NEGOCIO</c>; publicar é sempre permitido.</summary>
    [HttpPut("{id:int}/publicacao")]
    [Authorize(Roles = "artigos-publicar")]
    [ProducesResponseType(typeof(ApiResponse<ArtigoAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ArtigoAdminDto>>> AtualizarPublicacao(int id, AtualizarPublicacaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarPublicacaoAsync(id, dto.Publicado, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<ArtigoAdminDto> { Data = atualizado });
    }

    /// <summary>I29 — registro de verificação (data pura, nunca no futuro).</summary>
    [HttpPut("{id:int}/verificacao")]
    [Authorize(Roles = "artigos-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<ArtigoAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ArtigoAdminDto>>> AtualizarVerificacao(int id, AtualizarVerificacaoArtigoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarVerificacaoAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<ArtigoAdminDto> { Data = atualizado });
    }
}
