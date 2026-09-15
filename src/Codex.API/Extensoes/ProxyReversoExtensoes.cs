using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Codex.API.Extensoes;

/// <summary>
/// Opções de <c>X-Forwarded-*</c> (achado <c>MED-B04</c>). Atrás de proxy/ingress que termina TLS,
/// sem este middleware: <c>Request.IsHttps</c> é <c>false</c> — e o
/// <c>SecurityHeadersMiddleware</c> nunca emite <c>Strict-Transport-Security</c> — e
/// <c>Connection.RemoteIpAddress</c> é o IP do PROXY, o que colapsa a partição "por IP" do rate
/// limiter da busca num único balde global para toda a internet (o oposto do que a política
/// pretende).
///
/// <para>
/// <b>A origem é restrita por construção.</b> Honrar <c>X-Forwarded-For</c> de qualquer remoto é
/// spoofing de IP: o cliente escolheria a própria partição de rate limit e o próprio esquema.
/// Por isso a lista de confiança começa VAZIA e é preenchida só com o loopback (dev local e
/// sidecar no mesmo pod) mais o que vier de configuração — nunca um curinga. Endereço ou CIDR
/// inválido na configuração é erro de inicialização, não valor ignorado em silêncio.
/// </para>
/// </summary>
public static class ProxyReversoExtensoes
{
    private const string SecaoProxies = "ProxyReverso:KnownProxies";
    private const string SecaoRedes = "ProxyReverso:KnownNetworks";
    private const string ChaveForwardLimit = "ProxyReverso:ForwardLimit";

    public static ForwardedHeadersOptions MontarOpcoesProxyReverso(IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        var opcoes = new ForwardedHeadersOptions
        {
            // Só XForwardedFor e XForwardedProto: XForwardedHost não é honrado de propósito —
            // reescrever o Host a partir de cabeçalho é vetor de envenenamento de link absoluto.
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = int.TryParse(configuracao[ChaveForwardLimit], out var limite) && limite > 0 ? limite : 1,
        };

        // O default do ASP.NET Core traz só o loopback IPv6; limpamos e reconstruímos para que a
        // lista seja explícita neste arquivo, não implícita no framework.
        opcoes.KnownProxies.Clear();
        opcoes.KnownIPNetworks.Clear();

        opcoes.KnownProxies.Add(IPAddress.Loopback);
        opcoes.KnownProxies.Add(IPAddress.IPv6Loopback);

        foreach (var proxy in ValoresDe(configuracao, SecaoProxies))
        {
            if (!IPAddress.TryParse(proxy, out var endereco))
                throw new InvalidOperationException($"{SecaoProxies} contém um endereço IP inválido: '{proxy}'.");

            opcoes.KnownProxies.Add(endereco);
        }

        foreach (var rede in ValoresDe(configuracao, SecaoRedes))
        {
            if (!System.Net.IPNetwork.TryParse(rede, out var redeConfiavel))
                throw new InvalidOperationException($"{SecaoRedes} contém um CIDR inválido: '{rede}'.");

            opcoes.KnownIPNetworks.Add(redeConfiavel);
        }

        return opcoes;
    }

    /// <summary>
    /// <c>true</c> quando nenhum proxy além do loopback foi declarado — fora de Development isso
    /// significa que, se houver ingress na frente, HSTS e a partição por IP continuam quebrados.
    /// </summary>
    public static bool SemProxyDeclarado(IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return ValoresDe(configuracao, SecaoProxies).Count == 0
            && ValoresDe(configuracao, SecaoRedes).Count == 0;
    }

    private static IReadOnlyList<string> ValoresDe(IConfiguration configuracao, string secao) =>
        configuracao.GetSection(secao).Get<string[]>()
            ?.Where(valor => !string.IsNullOrWhiteSpace(valor))
            .Select(valor => valor.Trim())
            .ToArray()
        ?? [];
}
