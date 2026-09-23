using Codex.API.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Net.Http.Headers;

namespace Codex.API.Controllers.Publico;

/// <summary>
/// Sondas do orquestrador (004, PRD §2.2). É <b>controller</b>, e não <c>MapHealthChecks</c>, de
/// propósito: a auditoria da 003 registrou como propriedade verificada que <c>MapControllers()</c>
/// é o único ponto de registro de rota do backend, e
/// <see cref="Codex.Tests.Integration.Api.AllowlistRotasAnonimasTests"/> enumera
/// <c>ControllerActionDescriptor</c> — uma rota de minimal API seria anônima e **invisível** para
/// o detector. Pelo controller, a sonda entra na allowlist explícita e passa a ser coberta.
///
/// <para>
/// Sem rate limiting: o orquestrador sonda em intervalo curto e do mesmo IP do ingress, e barrar o
/// probe derrubaria a instância saudável. Em compensação, <c>vivo</c> não toca dependência nenhuma
/// e <c>pronto</c> só responde status — nunca nome de dependência, mensagem de exceção ou tempo
/// (D4 do PRD; mesma régua CWE-209 do <c>409</c> corrigido na 003).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/saude")]
[AllowAnonymous]
public sealed class SaudeController : ControllerBase
{
    /// <summary>
    /// Sonda é sempre o estado de AGORA. Sem isto, um proxy ou CDN no caminho pode servir um
    /// "saudável" de um minuto atrás para uma instância que já caiu.
    /// </summary>
    private const string SemCache = "no-store";

    private readonly HealthCheckService _verificacoes;

    public SaudeController(HealthCheckService verificacoes)
    {
        _verificacoes = verificacoes;
    }

    /// <summary>Liveness — o processo está de pé e responde. Não toca banco nem serviço externo.</summary>
    /// <response code="200">Processo vivo.</response>
    [HttpGet("vivo")]
    [ProducesResponseType(typeof(SaudeDto), StatusCodes.Status200OK)]
    public ActionResult<SaudeDto> Vivo()
    {
        Response.Headers[HeaderNames.CacheControl] = SemCache;

        return Ok(new SaudeDto { Status = SaudeDto.Saudavel });
    }

    /// <summary>
    /// Readiness — a instância pode receber tráfego: as dependências registradas respondem.
    /// Separado de <see cref="Vivo"/> porque banco fora do ar é motivo para tirar do balanceador,
    /// nunca para o orquestrador matar e recriar o processo em laço.
    /// </summary>
    /// <response code="200">Dependências respondendo.</response>
    /// <response code="503">Alguma dependência indisponível.</response>
    [HttpGet("pronto")]
    [ProducesResponseType(typeof(SaudeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SaudeDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SaudeDto>> Pronto(CancellationToken ct)
    {
        var relatorio = await _verificacoes.CheckHealthAsync(ct);

        Response.Headers[HeaderNames.CacheControl] = SemCache;

        return relatorio.Status == HealthStatus.Healthy
            ? Ok(new SaudeDto { Status = SaudeDto.Saudavel })
            : StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new SaudeDto { Status = SaudeDto.Indisponivel });
    }
}
