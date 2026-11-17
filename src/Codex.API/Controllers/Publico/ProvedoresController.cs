using System.ComponentModel.DataAnnotations;
using Codex.API.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Application.Services;
using Codex.Domain.Constantes;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codex.API.Controllers.Publico;

/// <summary>
/// Endpoints públicos e anônimos de provedor (P2/P3/P4, analise-backend.md §4.1). Nunca exige
/// role — leitura pública é sempre só conteúdo publicado (cascata provedor → versão).
/// </summary>
[ApiController]
[Route("api/v1/provedores")]
[AllowAnonymous]
[EnableRateLimiting("vitrine")]
public sealed class ProvedoresController : ControllerBase
{
    private readonly IProvedorServicoPublico _servico;

    public ProvedoresController(IProvedorServicoPublico servico)
    {
        _servico = servico;
    }

    /// <summary>Lista provedores publicados (P2).</summary>
    /// <param name="q">Busca textual (ILIKE) no nome.</param>
    /// <param name="categoria">Filtro exato de categoria.</param>
    /// <param name="ordenarPor">"nome" ou "verificadoEm" — qualquer outro valor cai no default (CriadoEm desc).</param>
    /// <param name="direcao">"asc" ou "desc" (default "desc").</param>
    /// <response code="200">Página de provedores publicados.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<ProvedorListaItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<ProvedorListaItemDto>>> Listar(
        [FromQuery] string? q,
        [FromQuery] CategoriaProvedor? categoria,
        [FromQuery] string? ordenarPor,
        [FromQuery] string? direcao,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25,
        CancellationToken ct = default)
    {
        var filtro = new FiltroProvedorPublico(q, categoria, ordenarPor, direcao, pagina, tamanhoPagina);
        var resultado = await _servico.ListarAsync(filtro, ct);

        return Ok(new PaginatedResponse<ProvedorListaItemDto>
        {
            Items = resultado.Itens,
            TotalCount = resultado.TotalCount,
            // Ecoa o que o serviço normalizou — refazer a conta aqui já fez o envelope afirmar uma
            // página que não foi servida.
            Page = resultado.Pagina,
            PageSize = resultado.TamanhoPagina,
        });
    }

    /// <summary>Página do provedor — identidade, avisos de provedor e lista de versões (P3, U7/U8).</summary>
    /// <response code="200">Provedor publicado.</response>
    /// <response code="308">Slug antigo — redireciona para o slug atual.</response>
    /// <response code="404">Provedor nunca existiu.</response>
    /// <response code="410">Provedor existe mas está despublicado.</response>
    [HttpGet("{provedorSlug}")]
    [ProducesResponseType(typeof(ApiResponse<ProvedorDetalheDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status308PermanentRedirect)]
    public async Task<IActionResult> ObterDetalhe(string provedorSlug, CancellationToken ct)
    {
        try
        {
            var detalhe = await _servico.ObterDetalheAsync(provedorSlug, ct);
            return Ok(new ApiResponse<ProvedorDetalheDto> { Data = detalhe });
        }
        catch (EntidadeNaoEncontradaException)
        {
            var destino = await _servico.ObterRedirecionamentoAsync(provedorSlug, ct);
            // F1 (arquitetura.md §5.F): o Location aponta para a rota da API, não para a rota de
            // app. XMLHttpRequest segue o 308 sozinho e nunca expõe status nem Location ao
            // cliente — apontando para a rota de app, o auto-follow bateria num endereço que a
            // API não serve e devolveria 404. DeCaminhoPublico já valida o caminho interno e
            // devolve null quando o destino não é traduzível: aí segue o 404 honesto.
            var rotaApiDoDestino = RotaApiPublica.DeCaminhoPublico(destino);
            if (rotaApiDoDestino is not null)
                return RedirectPermanentPreserveMethod(rotaApiDoDestino);

            return NotFound(new { error = new { code = "NAO_ENCONTRADO", message = $"Provedor '{provedorSlug}' não encontrado." } });
        }
        catch (ConteudoDespublicadoException excecao)
        {
            return MapeadorErroPublico.Mapear(this, excecao);
        }
    }

    /// <summary>Página exaustiva da versão de provedor — aba com URL própria (P4, §4.3).</summary>
    /// <response code="200">Versão publicada, com cascata (provedor publicado).</response>
    /// <response code="304">If-None-Match casou — conteúdo não mudou.</response>
    /// <response code="404">Provedor/versão nunca existiram (join por slug não casou).</response>
    /// <response code="410">Versão ou provedor existe mas está despublicado.</response>
    /// <response code="422">Versão excede o limite de 2000 campos (M22, defesa de leitura).</response>
    [HttpGet("{provedorSlug}/versoes/{versaoSlug}")]
    [ProducesResponseType(typeof(ApiResponse<VersaoProvedorPaginaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterVersaoPagina(string provedorSlug, string versaoSlug, CancellationToken ct)
    {
        try
        {
            var pagina = await _servico.ObterVersaoPaginaAsync(provedorSlug, versaoSlug, ct);

            // ETag do corpo: a página reúne avisos, campos e erros, que mudam sem tocar a linha da
            // versão. Tirada só do xmin dela, publicar um erro devolvia 304 com o corpo antigo.
            var etag = ETagDeConteudo.MontarDoCorpo(pagina);
            var ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == etag)
                return StatusCode(StatusCodes.Status304NotModified);

            Response.Headers.ETag = etag;
            return Ok(new ApiResponse<VersaoProvedorPaginaDto> { Data = pagina });
        }
        catch (Exception excecao) when (excecao is EntidadeNaoEncontradaException or ConteudoDespublicadoException or LimiteExcedidoException)
        {
            return MapeadorErroPublico.Mapear(this, excecao);
        }
    }

    /// <summary>Envio modelo InvoiCy da versão, em XML (SOAP) e JSON (REST), recortado ao alcance e ao cenário.</summary>
    /// <param name="provedorSlug">Slug do provedor.</param>
    /// <param name="versaoSlug">Slug da versão do provedor.</param>
    /// <param name="alcance"><c>minimo</c> (só os obrigatórios) ou <c>maximo</c> (tudo o que a versão aceita).</param>
    /// <param name="cenario">
    /// <c>nacional</c> (padrão), <c>tomador-estrangeiro</c> (tomador com NIF e endereço no exterior),
    /// <c>exportacao-servico</c> (tomador estrangeiro com comércio exterior), <c>construcao-civil</c> (obra com
    /// endereço e materiais) ou <c>retencoes-federais</c> (ISS retido e retenções federais).
    /// </param>
    /// <response code="200">Envio modelo da versão publicada.</response>
    /// <response code="304">If-None-Match casou: o envio modelo não mudou.</response>
    /// <response code="400">Alcance ausente ou fora de <c>minimo</c> e <c>maximo</c>; cenário fora dos cinco aceitos.</response>
    /// <response code="404">Provedor ou versão inexistente ou despublicado; <c>ESTRUTURA_INDISPONIVEL</c> quando o layout da versão não tem estrutura.</response>
    /// <response code="422"><c>CENARIO_INDISPONIVEL</c>: a versão não aceita os campos do cenário pedido.</response>
    [HttpGet("{provedorSlug}/versoes/{versaoSlug}/envio-modelo")]
    [ProducesResponseType(typeof(ApiResponse<EnvioModeloDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterEnvioModelo(
        string provedorSlug,
        string versaoSlug,
        [FromQuery, Required, AllowedValues(AlcancesEnvioModelo.Minimo, AlcancesEnvioModelo.Maximo)] string alcance,
        [FromQuery, AllowedValues(
            null,
            CenariosEnvioModelo.Nacional,
            CenariosEnvioModelo.TomadorEstrangeiro,
            CenariosEnvioModelo.ExportacaoServico,
            CenariosEnvioModelo.ConstrucaoCivil,
            CenariosEnvioModelo.RetencoesFederais)] string? cenario,
        CancellationToken ct)
    {
        try
        {
            var envioModelo = await _servico.ObterEnvioModeloAsync(
                provedorSlug, versaoSlug, AlcancesEnvioModelo.DeTexto(alcance), CenariosEnvioModelo.DeTexto(cenario), ct);

            // ETag do corpo: campos da versão e estrutura do layout mudam sem tocar a linha da versão.
            var etag = ETagDeConteudo.MontarDoCorpo(envioModelo);
            if (Request.Headers.IfNoneMatch.ToString() == etag)
                return StatusCode(StatusCodes.Status304NotModified);

            Response.Headers.ETag = etag;
            return Ok(new ApiResponse<EnvioModeloDto> { Data = envioModelo });
        }
        catch (EntidadeNaoEncontradaException excecao)
        {
            return MapeadorErroPublico.Mapear(this, excecao);
        }
    }
}
