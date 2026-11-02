using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>Área interna de versões do vocabulário canônico (I15a-I15d, I16 — analise-backend.md §4.2/§7.4).</summary>
[ApiController]
[Route("api/v1/interno/layout/versoes")]
[Authorize]
public sealed class LayoutVersoesInternoController : ControllerBase
{
    private readonly IVersaoLayoutServicoInterno _servico;
    private readonly ICampoLayoutServicoInterno _campoServico;

    public LayoutVersoesInternoController(IVersaoLayoutServicoInterno servico, ICampoLayoutServicoInterno campoServico)
    {
        _servico = servico;
        _campoServico = campoServico;
    }

    /// <summary>I15a — lista paginada.</summary>
    [HttpGet]
    [Authorize(Roles = "versoeslayout-buscar")]
    [ProducesResponseType(typeof(PaginatedResponse<VersaoLayoutAdminDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<VersaoLayoutAdminDto>>> Listar(
        [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 25, CancellationToken ct = default)
    {
        var resultado = await _servico.ListarAsync(pagina, tamanhoPagina, ct);

        return Ok(new PaginatedResponse<VersaoLayoutAdminDto>
        {
            Items = resultado.Itens,
            TotalCount = resultado.TotalCount,
            // Ecoa o que o serviço normalizou — refazer a conta aqui já fez o envelope afirmar uma
            // página que não foi servida.
            Page = resultado.Pagina,
            PageSize = resultado.TamanhoPagina,
        });
    }

    /// <summary>I15b — criação (sem <c>ordem</c> — auto-atribuída — nem <c>publicado</c>).</summary>
    [HttpPost]
    [Authorize(Roles = "versoeslayout-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<VersaoLayoutAdminDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<VersaoLayoutAdminDto>>> Criar(CriarVersaoLayoutDto dto, CancellationToken ct)
    {
        var criada = await _servico.CriarAsync(dto, ct);
        Response.PublicarETagConcorrencia(criada.VersaoConcorrencia);
        return Created($"/api/v1/interno/layout/versoes/{criada.Id}",
            new ApiResponse<VersaoLayoutAdminDto> { Data = criada, Message = "Versão de layout criada com sucesso." });
    }

    /// <summary>I15c — atualização; <c>ordem</c> presente é reordenação editorial (409 se colidir).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "versoeslayout-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<VersaoLayoutAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<VersaoLayoutAdminDto>>> Atualizar(int id, AtualizarVersaoLayoutDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<VersaoLayoutAdminDto> { Data = atualizado, Message = "Versão de layout atualizada com sucesso." });
    }

    /// <summary>I15d (M19) — publicar/despublicar, com cascata para os <c>camposlayout</c> descendentes.</summary>
    [HttpPut("{id:int}/publicacao")]
    [Authorize(Roles = "versoeslayout-publicar")]
    [ProducesResponseType(typeof(ApiResponse<VersaoLayoutAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<VersaoLayoutAdminDto>>> AtualizarPublicacao(int id, AtualizarPublicacaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarPublicacaoAsync(id, dto.Publicado, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<VersaoLayoutAdminDto> { Data = atualizado });
    }

    /// <summary>Grava a estrutura da versão: as árvores do documento em XML (SOAP) e em JSON (REST).</summary>
    /// <response code="200">Estrutura gravada; o ETag é a nova versão de concorrência.</response>
    /// <response code="400">Sem If-Match.</response>
    /// <response code="404">Versão não encontrada.</response>
    /// <response code="409">A versão mudou desde a leitura.</response>
    /// <response code="422">Árvore inválida, grande ou funda demais.</response>
    [HttpPut("{id:int}/estrutura")]
    [Authorize(Roles = "versoeslayout-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<VersaoLayoutAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<VersaoLayoutAdminDto>>> AtualizarEstrutura(int id, AtualizarEstruturaVersaoLayoutDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarEstruturaAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<VersaoLayoutAdminDto> { Data = atualizado, Message = "Estrutura da versão de layout gravada com sucesso." });
    }

    /// <summary>I16 GET — campos canônicos da versão de layout.</summary>
    [HttpGet("{id:int}/campos")]
    [Authorize(Roles = "campolayout-buscar")]
    [ProducesResponseType(typeof(ApiResponse<CampoLayoutResponseDto[]>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CampoLayoutResponseDto>>>> ListarCampos(int id, CancellationToken ct)
    {
        var campos = await _campoServico.ListarPorVersaoLayoutAsync(id, ct);
        return Ok(new ApiResponse<IReadOnlyCollection<CampoLayoutResponseDto>> { Data = campos });
    }

    /// <summary>I16 POST — cria campo canônico (sem <c>publicado</c>).</summary>
    [HttpPost("{id:int}/campos")]
    [Authorize(Roles = "campolayout-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<CampoLayoutResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CampoLayoutResponseDto>>> CriarCampo(int id, CriarCampoLayoutDto dto, CancellationToken ct)
    {
        var criado = await _campoServico.CriarAsync(id, dto, ct);
        Response.PublicarETagConcorrencia(criado.VersaoConcorrencia);
        return Created($"/api/v1/interno/campos-layout/{criado.Id}",
            new ApiResponse<CampoLayoutResponseDto> { Data = criado, Message = "Campo canônico criado com sucesso." });
    }
}
