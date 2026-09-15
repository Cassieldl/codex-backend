using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Pipeline real de cabeçalhos de segurança (rules/security.md A05) + achado <c>MED-B04</c>.
///
/// <para>
/// Usa <c>TestServer.SendAsync</c> em vez de <c>HttpClient</c> porque é o único jeito de controlar
/// <c>Connection.RemoteIpAddress</c>: é exatamente o que decide se <c>UseForwardedHeaders</c>
/// honra ou descarta <c>X-Forwarded-Proto</c>. Sem esse controle, o teste não distinguiria "HSTS
/// sai atrás do proxy declarado" de "qualquer cliente consegue se declarar HTTPS", que é a
/// diferença entre a correção e um spoofing de IP.
/// </para>
///
/// <para>
/// A auditoria registrou que nenhum teste cobria estes headers (grep: 0 ocorrências de
/// <c>X-Frame-Options</c>/<c>Content-Security-Policy</c>/<c>nosniff</c> na suíte) — são controles
/// de uma linha, apagáveis numa refatoração de <c>Program.cs</c> sem nenhum vermelho.
/// </para>
/// </summary>
public sealed class CabecalhosSegurancaPipelineTests : IClassFixture<CodexApiFactory>
{
    private readonly CodexApiFactory _factory;

    public CabecalhosSegurancaPipelineTests(CodexApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
    [InlineData("Content-Security-Policy", "default-src 'self'; frame-ancestors 'none'")]
    public async Task RespostaDeQualquerRota_TemOsCabecalhosDeSeguranca(string cabecalho, string valorEsperado)
    {
        var contexto = await EnviarAsync(remoteIp: IPAddress.Loopback);

        contexto.Response.Headers[cabecalho].ToString().Should().Be(valorEsperado);
    }

    [Fact]
    public async Task ComXForwardedProtoDeProxyConfiavel_EmiteStrictTransportSecurity()
    {
        // Loopback é KnownProxy por construção (ProxyReversoExtensoes) — o cenário do ingress que
        // termina TLS. Sem UseForwardedHeaders, Request.IsHttps ficaria false e o header não sai.
        var contexto = await EnviarAsync(remoteIp: IPAddress.Loopback, protocoloEncaminhado: "https");

        contexto.Request.IsHttps.Should().BeTrue();
        contexto.Response.Headers["Strict-Transport-Security"].ToString()
            .Should().Be("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public async Task ComXForwardedProtoDeOrigemNaoConfiavel_IgnoraOCabecalho()
    {
        // Anti-spoofing: um cliente qualquer não pode se declarar HTTPS (nem escolher o IP de
        // partição do rate limiter) só porque mandou o cabeçalho.
        var contexto = await EnviarAsync(
            remoteIp: IPAddress.Parse("203.0.113.5"),
            protocoloEncaminhado: "https",
            ipEncaminhado: "198.51.100.7");

        contexto.Request.IsHttps.Should().BeFalse();
        contexto.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.5"));
        contexto.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task ComXForwardedForDeProxyConfiavel_SubstituiOIpDoClienteUsadoNaParticaoDeRateLimit()
    {
        var contexto = await EnviarAsync(
            remoteIp: IPAddress.Loopback,
            ipEncaminhado: "198.51.100.7");

        contexto.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("198.51.100.7"),
            because: "sem isto a partição por IP do rate limiter vira um balde único para toda a internet");
    }

    private async Task<HttpContext> EnviarAsync(
        IPAddress remoteIp, string? protocoloEncaminhado = null, string? ipEncaminhado = null)
    {
        return await _factory.Server.SendAsync(contexto =>
        {
            contexto.Request.Method = HttpMethods.Get;
            contexto.Request.Path = "/api/v1/interno/provedores"; // 401 sem token — não toca o banco.
            contexto.Connection.RemoteIpAddress = remoteIp;

            if (protocoloEncaminhado is not null)
                contexto.Request.Headers["X-Forwarded-Proto"] = protocoloEncaminhado;

            if (ipEncaminhado is not null)
                contexto.Request.Headers["X-Forwarded-For"] = ipEncaminhado;
        });
    }
}
