using Codex.API.Common;
using Codex.API.Seguranca;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>
/// Fila editorial (escopo funcional v3.1 §9.2): buscas da área interna que não encontraram
/// resultado, com parceiro e chamado. Quem registra e quem resolve vêm sempre do token.
/// </summary>
[ApiController]
[Route("api/v1/interno/lacunas-busca")]
[Authorize]
public sealed class LacunasBuscaInternoController : ControllerBase
{
    private readonly ILacunaBuscaServicoInterno _servico;

    public LacunasBuscaInternoController(ILacunaBuscaServicoInterno servico)
    {
        _servico = servico;
    }

    /// <summary>
    /// Lista paginada da fila. Padrão: mais recentes primeiro. <c>sortBy</c> aceita
    /// <c>criadoEm</c>, <c>ocorrencias</c> (prioridade: quantas vezes o termo está aberto) e <c>termo</c>.
    /// </summary>
    /// <param name="estado">Filtra por estado (<c>Aberta</c>, <c>Resolvida</c>, <c>Descartada</c>); omitido traz todos.</param>
    /// <param name="sortBy">Coluna de ordenação; valor desconhecido usa o padrão.</param>
    /// <param name="sortDirection"><c>asc</c> ou <c>desc</c> (padrão).</param>
    /// <param name="pagina">Página, a partir de 1.</param>
    /// <param name="tamanhoPagina">Itens por página (padrão 25, máximo 200).</param>
    /// <response code="200">Página de lacunas.</response>
    [HttpGet]
    [Authorize(Roles = "lacunasbusca-buscar")]
    [ProducesResponseType(typeof(PaginatedResponse<LacunaBuscaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<LacunaBuscaDto>>> Listar(
        [FromQuery] EstadoLacunaBusca? estado,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        CancellationToken ct = default)
    {
        var resultado = await _servico.ListarAsync(
            new FiltroLacunaBusca(estado, sortBy, sortDirection, pagina, tamanhoPagina), ct);

        return Ok(new PaginatedResponse<LacunaBuscaDto>
        {
            Items = resultado.Itens,
            TotalCount = resultado.TotalCount,
            // Ecoa o que o serviço normalizou, não o que o cliente pediu.
            Page = resultado.Pagina,
            PageSize = resultado.TamanhoPagina,
        });
    }

    /// <summary>
    /// Fila agrupada por termo: uma linha por termo normalizado, com a recorrência e os contextos
    /// (parceiros e chamados) agregados. A página é de GRUPOS. Para tratar o grupo, a tela usa
    /// <c>lacunaReferenciaId</c> e <c>versaoConcorrencia</c> em <c>PUT /{id}/estado</c> com
    /// <c>incluirMesmoTermo</c>.
    /// </summary>
    /// <param name="estado">Estado das ocorrências agrupadas (padrão <c>Aberta</c>).</param>
    /// <param name="sortBy"><c>ocorrencias</c> (padrão), <c>recente</c> ou <c>termo</c>; valor desconhecido usa o padrão.</param>
    /// <param name="sortDirection"><c>asc</c> ou <c>desc</c> (padrão).</param>
    /// <param name="pagina">Página, a partir de 1.</param>
    /// <param name="tamanhoPagina">Grupos por página (padrão 25, máximo 200).</param>
    /// <response code="200">Página de termos.</response>
    [HttpGet("termos")]
    [Authorize(Roles = "lacunasbusca-buscar")]
    [ProducesResponseType(typeof(PaginatedResponse<LacunaBuscaTermoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<LacunaBuscaTermoDto>>> ListarTermos(
        [FromQuery] EstadoLacunaBusca estado = EstadoLacunaBusca.Aberta,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        CancellationToken ct = default)
    {
        var resultado = await _servico.ListarTermosAsync(
            new FiltroLacunaBuscaTermo(estado, sortBy, sortDirection, pagina, tamanhoPagina), ct);

        return Ok(new PaginatedResponse<LacunaBuscaTermoDto>
        {
            Items = resultado.Itens,
            TotalCount = resultado.TotalCount,
            Page = resultado.Pagina,
            PageSize = resultado.TamanhoPagina,
        });
    }

    /// <summary>Detalhe de uma lacuna; devolve o <c>ETag</c> para o <c>If-Match</c> de <c>PUT /{id}/estado</c>.</summary>
    /// <response code="200">Lacuna encontrada.</response>
    /// <response code="404">Lacuna não encontrada.</response>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "lacunasbusca-buscar")]
    [ProducesResponseType(typeof(ApiResponse<LacunaBuscaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<LacunaBuscaDto>>> Obter(int id, CancellationToken ct)
    {
        var lacuna = await _servico.ObterAsync(id, ct);
        Response.PublicarETagConcorrencia(lacuna.VersaoConcorrencia);

        return Ok(new ApiResponse<LacunaBuscaDto> { Data = lacuna });
    }

    /// <summary>
    /// Registra uma busca sem resultado. Quem registrou vem do token (<c>sub</c> e <c>name</c>) e a
    /// data é a do servidor — qualquer campo extra no corpo é ignorado.
    /// </summary>
    /// <response code="201">Lacuna registrada, no estado <c>Aberta</c>.</response>
    /// <response code="422">Termo ausente ou curto demais; parceiro, chamado ou termo acima do limite.</response>
    [HttpPost]
    [Authorize(Roles = "lacunasbusca-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<LacunaBuscaDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<LacunaBuscaDto>>> Registrar(RegistrarLacunaBuscaDto dto, CancellationToken ct)
    {
        var usuario = User.ObterUsuarioDaRequisicao();
        if (usuario is null)
            return Unauthorized();

        var registrada = await _servico.RegistrarAsync(dto, usuario, ct);
        Response.PublicarETagConcorrencia(registrada.VersaoConcorrencia);

        return CreatedAtAction(nameof(Obter), new { id = registrada.Id },
            new ApiResponse<LacunaBuscaDto> { Data = registrada, Message = "Lacuna registrada." });
    }

    /// <summary>
    /// Resolve, descarta ou reabre a lacuna. Com <c>incluirMesmoTermo</c>, as demais ocorrências
    /// abertas do mesmo termo mudam junto, na mesma transação.
    /// </summary>
    /// <response code="200">Estado alterado.</response>
    /// <response code="400"><c>If-Match</c> ausente ou inválido.</response>
    /// <response code="404">Lacuna não encontrada.</response>
    /// <response code="409">A lacuna foi alterada por outra requisição.</response>
    /// <response code="422">Estado igual ao atual, nota acima do limite ou <c>incluirMesmoTermo</c> ao reabrir.</response>
    [HttpPut("{id:int}/estado")]
    [Authorize(Roles = "lacunasbusca-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<AlteracaoEstadoLacunaBuscaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<AlteracaoEstadoLacunaBuscaDto>>> AlterarEstado(
        int id, AlterarEstadoLacunaBuscaDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();

        var usuario = User.ObterUsuarioDaRequisicao();
        if (usuario is null)
            return Unauthorized();

        var alteracao = await _servico.AlterarEstadoAsync(id, dto, versaoEsperada, usuario, ct);
        Response.PublicarETagConcorrencia(alteracao.Lacuna.VersaoConcorrencia);

        return Ok(new ApiResponse<AlteracaoEstadoLacunaBuscaDto> { Data = alteracao });
    }
}
