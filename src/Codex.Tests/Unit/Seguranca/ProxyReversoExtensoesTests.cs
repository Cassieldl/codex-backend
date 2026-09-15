using System.Net;
using Codex.API.Extensoes;
using FluentAssertions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Unit.Seguranca;

/// <summary>
/// Achado <c>MED-B04</c>: honrar <c>X-Forwarded-*</c> é necessário atrás de ingress (senão
/// <c>Request.IsHttps</c> é <c>false</c>, HSTS nunca sai, e a partição "por IP" do rate limiter
/// colapsa num balde global), mas honrá-lo de origem arbitrária é spoofing de IP — o cliente
/// escolheria a própria partição. Estes testes fixam que a lista de confiança é EXPLÍCITA e que
/// configuração inválida é erro de inicialização, nunca valor ignorado em silêncio.
/// </summary>
public sealed class ProxyReversoExtensoesTests
{
    private static IConfiguration Configuracao(params (string Chave, string? Valor)[] pares) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pares.ToDictionary(par => par.Chave, par => par.Valor))
            .Build();

    [Fact]
    public void SemConfiguracao_ConfiaSomenteNoLoopback()
    {
        var opcoes = ProxyReversoExtensoes.MontarOpcoesProxyReverso(Configuracao());

        opcoes.KnownProxies.Should().BeEquivalentTo([IPAddress.Loopback, IPAddress.IPv6Loopback]);
        opcoes.KnownIPNetworks.Should().BeEmpty(because: "curinga de rede seria confiar em qualquer origem");
        opcoes.ForwardLimit.Should().Be(1);
    }

    [Fact]
    public void HonraSomenteXForwardedForEXForwardedProto()
    {
        var opcoes = ProxyReversoExtensoes.MontarOpcoesProxyReverso(Configuracao());

        opcoes.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        opcoes.ForwardedHeaders.Should().NotHaveFlag(ForwardedHeaders.XForwardedHost,
            because: "reescrever o Host a partir de cabeçalho é vetor de envenenamento de link absoluto");
    }

    [Fact]
    public void ComProxiesERedesDeclarados_AdicionaAoLadoDoLoopback()
    {
        var opcoes = ProxyReversoExtensoes.MontarOpcoesProxyReverso(Configuracao(
            ("ProxyReverso:KnownProxies:0", "10.1.2.3"),
            ("ProxyReverso:KnownNetworks:0", "10.0.0.0/8"),
            ("ProxyReverso:ForwardLimit", "2")));

        opcoes.KnownProxies.Should().Contain(IPAddress.Parse("10.1.2.3"));
        opcoes.KnownIPNetworks.Should().ContainSingle()
            .Which.ToString().Should().Be("10.0.0.0/8");
        opcoes.ForwardLimit.Should().Be(2);
    }

    [Theory]
    [InlineData("ProxyReverso:KnownProxies:0", "nao-e-ip")]
    [InlineData("ProxyReverso:KnownProxies:0", "10.0.0.0/8")]
    [InlineData("ProxyReverso:KnownNetworks:0", "10.0.0.0")]
    [InlineData("ProxyReverso:KnownNetworks:0", "*")]
    public void ComValorInvalido_Lanca_NuncaIgnoraEmSilencio(string chave, string valor)
    {
        var act = () => ProxyReversoExtensoes.MontarOpcoesProxyReverso(Configuracao((chave, valor)));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{valor}*");
    }

    [Fact]
    public void SemProxyDeclarado_EhVerdadeiroSoQuandoNadaFoiConfigurado()
    {
        ProxyReversoExtensoes.SemProxyDeclarado(Configuracao()).Should().BeTrue();

        ProxyReversoExtensoes.SemProxyDeclarado(Configuracao(("ProxyReverso:KnownProxies:0", "10.1.2.3")))
            .Should().BeFalse();

        ProxyReversoExtensoes.SemProxyDeclarado(Configuracao(("ProxyReverso:KnownNetworks:0", "10.0.0.0/8")))
            .Should().BeFalse();
    }
}
