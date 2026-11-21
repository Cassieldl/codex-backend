using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>Área interna de erros conhecidos (I18-I20c, U9 — analise-backend.md §4.2/§7.4).</summary>
[ApiController]
[Route("api/v1/interno/erros")]
[Authorize]
public sealed class ErrosInternoController : ControllerBase
{
    private readonly IErroServicoInterno _servico;

    public ErrosInternoController(IErroServicoInterno servico)
    {
        _servico = servico;
    }

    /// <summary>I18 - lista paginada (enxerga rascunho). Padrão: mais recentes primeiro.</summary>
    /// <param name="q">Busca por trecho do título ou do código.</param>
    /// <param name="provedor">Slug do provedor.</param>
    /// <param name="origem">Origem do erro.</param>
    /// <param name="publicado">Filtra por publicado ou rascunho; omitido traz os dois.</param>
    /// <param name="sortBy"><c>codigo</c>, <c>titulo</c>, <c>origem</c> ou <c>publicado</c>; vazio ou desconhecido usa o padrão.</param>
    /// <param name="sortDirection"><c>asc</c> ou <c>desc</c> (padrão).</param>
    /// <param name="pagina">Página, a partir de 1.</param>
    /// <param name="tamanhoPagina">Itens por página (padrão 25, máximo 200).</param>
    /// <response code="200">Página de erros.</response>
    [HttpGet]
    [Authorize(Roles = "erros-buscar")]
    [ProducesResponseType(typeof(PaginatedResponse<ErroAdminListaItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<ErroAdminListaItemDto>>> Listar(
        [FromQuery] string? q,
        [FromQuery] string? provedor,
        [FromQuery] OrigemErro? origem,
        [FromQuery] bool? publicado,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        CancellationToken ct = default)
    {
        var filtro = new FiltroErroInterno(q, provedor, origem, publicado, sortBy, sortDirection, pagina, tamanhoPagina);
        var resultado = await _servico.ListarAsync(filtro, ct);

        return Ok(new PaginatedResponse<ErroAdminListaItemDto>
        {
            Items = resultado.Itens,
            TotalCount = resultado.TotalCount,
            // Ecoa o que o serviço normalizou — refazer a conta aqui já fez o envelope afirmar uma
            // página que não foi servida.
            Page = resultado.Pagina,
            PageSize = resultado.TamanhoPagina,
        });
    }

    /// <summary>Detalhe interno do erro (id numérico — não confundir com P6, por slug, público).</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "erros-buscar")]
    [ProducesResponseType(typeof(ApiResponse<ErroAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ErroAdminDetalheDto>>> ObterDetalhe(int id, CancellationToken ct)
    {
        var detalhe = await _servico.ObterDetalheAsync(id, ct);
        Response.PublicarETagConcorrencia(detalhe.VersaoConcorrencia);
        return Ok(new ApiResponse<ErroAdminDetalheDto> { Data = detalhe });
    }

    /// <summary>I19 — criação (sem <c>publicado</c>; <c>versoesProvedorIds</c> vazio só é inválido para <c>Origem=Prefeitura</c>, U9).</summary>
    [HttpPost]
    [Authorize(Roles = "erros-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<ErroAdminDetalheDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ErroAdminDetalheDto>>> Criar(CriarErroDto dto, CancellationToken ct)
    {
        var criado = await _servico.CriarAsync(dto, ct);
        Response.PublicarETagConcorrencia(criado.VersaoConcorrencia);
        return CreatedAtAction(nameof(ObterDetalhe), new { id = criado.Id },
            new ApiResponse<ErroAdminDetalheDto> { Data = criado, Message = "Erro criado com sucesso." });
    }

    /// <summary>I20a — atualização (mesma regra de <c>Origem=Prefeitura</c> da criação).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "erros-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<ErroAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ErroAdminDetalheDto>>> Atualizar(int id, AtualizarErroDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<ErroAdminDetalheDto> { Data = atualizado, Message = "Erro atualizado com sucesso." });
    }

    /// <summary>I43 (P-10) — troca o slug do erro e grava o redirecionamento do slug antigo.</summary>
    /// <response code="200">Slug trocado; o slug antigo passa a responder 308.</response>
    /// <response code="400"><c>If-Match</c> ausente ou malformado.</response>
    /// <response code="404">Erro não encontrado.</response>
    /// <response code="409">Slug já pertence a outro erro, ou conflito de concorrência.</response>
    /// <response code="422">Slug igual ao atual, ou slug que normaliza para vazio.</response>
    [HttpPut("{id:int}/slug")]
    [Authorize(Roles = "erros-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<ErroAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ErroAdminDetalheDto>>> AtualizarSlug(int id, AlterarSlugDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarSlugAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<ErroAdminDetalheDto> { Data = atualizado, Message = "Slug do erro atualizado com sucesso." });
    }

    /// <summary>I20b — remoção (soft delete).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "erros-remover")]
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

    /// <summary>I20c (M19) — publicar/despublicar. M28: erro com vínculos só é público de fato se ao menos uma versão vinculada também estiver publicada — cascata resolvida na leitura pública (fora do escopo desta unidade).</summary>
    [HttpPut("{id:int}/publicacao")]
    [Authorize(Roles = "erros-publicar")]
    [ProducesResponseType(typeof(ApiResponse<ErroAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ErroAdminDetalheDto>>> AtualizarPublicacao(int id, AtualizarPublicacaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarPublicacaoAsync(id, dto.Publicado, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<ErroAdminDetalheDto> { Data = atualizado });
    }
}
