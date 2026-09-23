using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Codex.API.Extensoes;

/// <summary>
/// Rate limiting nomeado "busca" (analise-backend.md §6, proteção de custo do endpoint anônimo e
/// caro <c>GET /busca</c> — A04 de rules/security.md). Particionado por IP do cliente (nunca um
/// balde global compartilhado — um único IP abusivo não deveria conseguir travar a busca para
/// todo mundo, nem o inverso: tráfego legítimo de muitos IPs não deveria ser descartado por um
/// limite pensado para um único cliente). Rejeição sempre <c>429</c> (analise-backend.md §4.4) —
/// o default do middleware é <c>503</c>, por isso <see cref="RateLimiterOptions.RejectionStatusCode"/>
/// é sobrescrito explicitamente.
///
/// <para>
/// O IP só é confiável se <c>X-Forwarded-For</c> for honrado a partir de um proxy declarado — ver
/// <see cref="ProxyReversoExtensoes"/> (achado <c>MED-B04</c>): atrás de ingress sem aquele
/// wiring, <c>RemoteIpAddress</c> é o IP do proxy e a partição "por IP" vira um balde único.
/// Enquanto isso, a partição de IP desconhecido recebe orçamento reduzido, porque é compartilhada
/// por todos os clientes que caem nela.
/// </para>
/// </summary>
public static class RateLimitingExtensoes
{
    private const string ParticaoDesconhecida = "desconhecido";
    private const int LimitePorIp = 30;
    private const int LimiteSemIp = 5;

    /// <summary>
    /// Agregadores anônimos da vitrine (<c>GET /home</c>, <c>GET /layout*</c>): leitura de catálogo,
    /// barata por requisição mas sem teto de taxa — quem ignora o <c>Cache-Control</c> de 60 s
    /// transforma a taxa em carga direta no banco. Orçamento bem maior que o da busca porque a
    /// Home é a porta de entrada do portal e um escritório inteiro atrás de NAT chega como um IP
    /// só; ainda assim corta o abuso barato.
    /// </summary>
    private const int LimiteVitrinePorIp = 120;
    private const int LimiteVitrineSemIp = 20;

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Sem OnRejected o 429 sai com CORPO VAZIO e sem Retry-After (achado BAIXO-B09): o
            // cliente não sabe por que foi barrado nem quando pode voltar, e o envelope único de
            // erro de rules/api.md quebra justamente no status que mais precisa ser interpretado.
            options.OnRejected = async (contexto, cancelamento) =>
            {
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var esperar))
                {
                    contexto.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(esperar.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                contexto.HttpContext.Response.ContentType = "application/json";

                await contexto.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        error = new
                        {
                            code = "LIMITE_REQUISICOES",
                            message = "Muitas requisições. Tente novamente em instantes.",
                        },
                    },
                    cancelamento);
            };

            options.AddPolicy("busca", contexto => ParticionarPorIp(contexto, LimitePorIp, LimiteSemIp));

            options.AddPolicy(
                "vitrine",
                contexto => ParticionarPorIp(contexto, LimiteVitrinePorIp, LimiteVitrineSemIp));
        });

        return services;
    }

    /// <summary>
    /// Janela fixa de um minuto particionada pelo IP do cliente. As políticas diferem só no
    /// orçamento: a chave de partição e o comportamento da partição compartilhada são os mesmos, e
    /// duplicá-los por política é como o balde global de <c>AP-BACKEND-010</c> voltaria sem ninguém
    /// notar.
    /// </summary>
    private static RateLimitPartition<string> ParticionarPorIp(
        HttpContext httpContext,
        int limitePorIp,
        int limiteSemIp)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip ?? ParticaoDesconhecida,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                // A partição de IP desconhecido é compartilhada por TODOS quem cai nela — não pode
                // receber o mesmo orçamento de um cliente identificado, ou basta um abusador para
                // gastá-lo em nome de todo mundo.
                PermitLimit = ip is null ? limiteSemIp : limitePorIp,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    }
}
