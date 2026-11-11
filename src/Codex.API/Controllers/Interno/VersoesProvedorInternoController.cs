using Codex.API.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Controllers.Interno;

/// <summary>Área interna de versões de provedor (I8a-I10, I11, I12, I14a — analise-backend.md §4.2/§7.4).</summary>
[ApiController]
[Route("api/v1/interno/versoes")]
[Authorize]
public sealed class VersoesProvedorInternoController : ControllerBase
{
    private readonly IVersaoProvedorServicoInterno _servico;
    private readonly ICampoVersaoServicoInterno _campoServico;
    private readonly IAvisoServicoInterno _avisoServico;

    public VersoesProvedorInternoController(
        IVersaoProvedorServicoInterno servico, ICampoVersaoServicoInterno campoServico, IAvisoServicoInterno avisoServico)
    {
        _servico = servico;
        _campoServico = campoServico;
        _avisoServico = avisoServico;
    }

    /// <summary>I8a — detalhe da versão.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "versoesprovedor-buscar")]
    [ProducesResponseType(typeof(ApiResponse<VersaoAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<VersaoAdminDetalheDto>>> ObterDetalhe(int id, CancellationToken ct)
    {
        var detalhe = await _servico.ObterDetalheAsync(id, ct);
        Response.PublicarETagConcorrencia(detalhe.VersaoConcorrencia);
        return Ok(new ApiResponse<VersaoAdminDetalheDto> { Data = detalhe });
    }

    /// <summary>
    /// I8b — atualização. <c>versaoLayoutId</c> é opcional: omitido ou igual ao atual, mantém o
    /// layout de origem; informado e diferente, troca-o — e só é aceito quando o layout de destino
    /// existe/está ativo (senão <c>422 VERSAO_LAYOUT_INVALIDA</c>) e esta versão não tem nenhum
    /// campo cadastrado (senão <c>422 VERSAO_COM_CAMPOS</c>).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "versoesprovedor-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<VersaoAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<VersaoAdminDetalheDto>>> Atualizar(int id, AtualizarVersaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<VersaoAdminDetalheDto> { Data = atualizado, Message = "Versão atualizada com sucesso." });
    }

    /// <summary>I8c — remoção (soft delete).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "versoesprovedor-remover")]
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

    /// <summary>I9 (M19) — publicar/despublicar, com cascata em <c>codex.itensbusca</c>.</summary>
    [HttpPut("{id:int}/publicacao")]
    [Authorize(Roles = "versoesprovedor-publicar")]
    [ProducesResponseType(typeof(ApiResponse<VersaoAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<VersaoAdminDetalheDto>>> AtualizarPublicacao(int id, AtualizarPublicacaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.AtualizarPublicacaoAsync(id, dto.Publicado, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<VersaoAdminDetalheDto> { Data = atualizado });
    }

    /// <summary>I10 (U3) — registra verificação, sem <c>ambiente</c>.</summary>
    [HttpPut("{id:int}/verificacao")]
    [Authorize(Roles = "versoesprovedor-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<VersaoAdminDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<VersaoAdminDetalheDto>>> RegistrarVerificacao(int id, AtualizarVerificacaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var atualizado = await _servico.RegistrarVerificacaoAsync(id, dto, versaoEsperada, ct);
        Response.PublicarETagConcorrencia(atualizado.VersaoConcorrencia);
        return Ok(new ApiResponse<VersaoAdminDetalheDto> { Data = atualizado });
    }

    /// <summary>Substitui o de-para da natureza da operação da versão.</summary>
    /// <response code="200">Lista gravada, na ordem enviada.</response>
    /// <response code="400">Sem If-Match.</response>
    /// <response code="404">Versão não encontrada.</response>
    /// <response code="409">A versão mudou desde a leitura.</response>
    /// <response code="422">Código repetido, fora da faixa ou campo obrigatório vazio.</response>
    [HttpPut("{id:int}/naturezas-operacao")]
    [Authorize(Roles = "versoesprovedor-atualizar")]
    [ProducesResponseType(typeof(ApiResponse<ItemNaturezaOperacaoDto[]>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ItemNaturezaOperacaoDto>>>> SubstituirNaturezasOperacao(
        int id, AtualizarNaturezasOperacaoDto dto, CancellationToken ct)
    {
        var versaoEsperada = Request.ExigirIfMatch();
        var naturezas = await _servico.SubstituirNaturezasOperacaoAsync(id, dto, versaoEsperada, ct);
        return Ok(new ApiResponse<IReadOnlyCollection<ItemNaturezaOperacaoDto>>
        {
            Data = naturezas,
            Message = "Naturezas da operação gravadas com sucesso.",
        });
    }

    /// <summary>I11 — campos preenchidos + campos canônicos ainda ausentes.</summary>
    [HttpGet("{id:int}/campos")]
    [Authorize(Roles = "camposversao-buscar")]
    [ProducesResponseType(typeof(ApiResponse<CamposVersaoAgregadoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CamposVersaoAgregadoDto>>> ListarCampos(int id, CancellationToken ct)
    {
        var agregado = await _campoServico.ListarAgregadoAsync(id, ct);
        return Ok(new ApiResponse<CamposVersaoAgregadoDto> { Data = agregado });
    }

    /// <summary>I12 — cria campo de versão (422 se já no cap de 2000, M22).</summary>
    [HttpPost("{id:int}/campos")]
    [Authorize(Roles = "camposversao-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<CampoVersaoAdminDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CampoVersaoAdminDto>>> CriarCampo(int id, CriarCampoVersaoDto dto, CancellationToken ct)
    {
        var criado = await _campoServico.CriarAsync(id, dto, ct);
        Response.PublicarETagConcorrencia(criado.VersaoConcorrencia);
        return Created($"/api/v1/interno/campos-versao/{criado.Id}",
            new ApiResponse<CampoVersaoAdminDto> { Data = criado, Message = "Campo criado com sucesso." });
    }

    /// <summary>I14a — cria aviso de escopo VERSÃO (único ponto de entrada que aceita <c>campoVersaoIds</c>).</summary>
    [HttpPost("{id:int}/avisos")]
    [Authorize(Roles = "avisos-adicionar")]
    [ProducesResponseType(typeof(ApiResponse<AvisoAdminDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<AvisoAdminDto>>> CriarAvisoDeVersao(int id, CriarAvisoDeVersaoDto dto, CancellationToken ct)
    {
        var criado = await _avisoServico.CriarDeVersaoAsync(id, dto, ct);
        Response.PublicarETagConcorrencia(criado.VersaoConcorrencia);
        return Created($"/api/v1/interno/avisos/{criado.Id}",
            new ApiResponse<AvisoAdminDto> { Data = criado, Message = "Aviso criado com sucesso." });
    }
}
