namespace Codex.API.Extensoes;

/// <summary>
/// Validação de configuração de segurança na inicialização (achado <c>BAIXO-B10</c>).
/// <c>rules/security.md</c> manda <i>fail securely</i>: aplicação que sobe saudável e não pode
/// autenticar ninguém é o oposto disso — é degradação silenciosa. Aqui a falha é barulhenta e
/// diagnosticável, no start, em vez de virar "a área interna devolve 401 para todo mundo e
/// ninguém sabe por quê".
///
/// <list type="bullet">
/// <item><b>Keycloak</b> — sem <c>auth-server-url</c>/<c>realm</c> a <c>Authority</c> nunca é
/// atribuída e, com <c>ValidateIssuer</c>/<c>ValidateIssuerSigningKey</c> ligados, TODO token é
/// reprovado. Obrigatório fora de Development.</item>
/// <item><b>Keycloak:MetadataAddress</b>: em HTTP fora de Development, só com a mesma dispensa
/// explícita da Authority.</item>
/// <item><b>Cors:OrigemPermitida</b>: uma origem exata, nunca <c>"*"</c>. Obrigatória fora de
/// Development.</item>
/// <item><b>AllowedHosts</b> — <c>"*"</c> desliga o host filtering (A05). Proibido fora de
/// Development.</item>
/// <item><b>Busca:TimeoutMs</b> — <c>0</c> DESLIGA o <c>statement_timeout</c> no Postgres, que é
/// a proteção de custo do endpoint anônimo; negativo faz todo <c>SET LOCAL</c> falhar. Validado
/// em qualquer ambiente quando o valor está declarado.</item>
/// <item><b>Busca:OrcamentoRequisicaoMs</b> (MED-B05) — orçamento de PAREDE por requisição de
/// busca, distinto do <c>statement_timeout</c> (que protege cada <i>statement</i>, não a cascata
/// inteira). Mesmo risco de degradação silenciosa que <c>Busca:TimeoutMs</c>: <c>0</c> ou negativo
/// desligaria a única proteção contra uma cascata de autocorreção de até 18 statements numa única
/// requisição anônima.</item>
/// </list>
/// </summary>
public static class ConfiguracaoSegurancaExtensoes
{
    internal const int TimeoutBuscaMinimoMs = 250;
    internal const int TimeoutBuscaMaximoMs = 30_000;

    internal const int OrcamentoBuscaMinimoMs = 250;
    internal const int OrcamentoBuscaMaximoMs = 30_000;

    public static void ValidarConfiguracaoDeSeguranca(IConfiguration configuracao, IHostEnvironment ambiente)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(ambiente);

        var falhas = new List<string>();

        if (!ambiente.IsDevelopment())
        {
            var authority = configuracao["Keycloak:auth-server-url"];

            if (string.IsNullOrWhiteSpace(authority)
                || string.IsNullOrWhiteSpace(configuracao["Keycloak:realm"]))
            {
                falhas.Add("Keycloak:auth-server-url e Keycloak:realm são obrigatórios fora de Development — "
                    + "sem eles nenhum token é aceito e a área interna fica inacessível.");
            }
            else if (!authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && !ExigenciaDeHttpsFoiDispensada(configuracao))
            {
                // Sem esta checagem a aplicação SOBE e morre em toda requisição: o JwtBearer só
                // resolve a Authority no primeiro request e lança `InvalidOperationException`
                // ("Authority must use HTTPS"), que o middleware de autenticação propaga. Como ele
                // roda antes de qualquer rota, até `GET /saude/vivo` responde 500 — o orquestrador
                // mata e recria a instância em laço sem nunca dizer o motivo. Medido no smoke.
                falhas.Add($"Keycloak:auth-server-url precisa ser HTTPS fora de Development; veio '{authority}'. "
                    + "Se o Keycloak fica numa rede interna com TLS terminado antes dele, declare "
                    + "Keycloak:RequireHttpsMetadata=false para assumir o risco explicitamente.");
            }

            // Mesmo efeito da Authority em HTTP: o JwtBearer lança no primeiro request e toda rota,
            // inclusive a sonda de liveness, responde 500.
            var enderecoMetadata = configuracao["Keycloak:MetadataAddress"];
            if (!string.IsNullOrWhiteSpace(enderecoMetadata)
                && !enderecoMetadata.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && !ExigenciaDeHttpsFoiDispensada(configuracao))
            {
                falhas.Add($"Keycloak:MetadataAddress precisa ser HTTPS fora de Development; veio '{enderecoMetadata}'. "
                    + "Declare Keycloak:RequireHttpsMetadata=false para assumir o risco explicitamente.");
            }

            var falhaCors = ValidarOrigemCors(configuracao["Cors:OrigemPermitida"]);
            if (falhaCors is not null)
                falhas.Add(falhaCors);

            var hostsPermitidos = configuracao["AllowedHosts"];
            if (string.IsNullOrWhiteSpace(hostsPermitidos) || hostsPermitidos.Split(';').Any(host => host.Trim() == "*"))
            {
                falhas.Add("AllowedHosts não pode ser vazio nem \"*\" fora de Development — "
                    + "declare os hosts servidos por este ambiente.");
            }
        }

        var timeoutBusca = configuracao["Busca:TimeoutMs"];
        if (!string.IsNullOrWhiteSpace(timeoutBusca))
        {
            if (!int.TryParse(timeoutBusca, out var timeoutMs))
            {
                falhas.Add($"Busca:TimeoutMs precisa ser um inteiro em milissegundos; veio '{timeoutBusca}'.");
            }
            else if (timeoutMs < TimeoutBuscaMinimoMs || timeoutMs > TimeoutBuscaMaximoMs)
            {
                falhas.Add($"Busca:TimeoutMs precisa estar entre {TimeoutBuscaMinimoMs} e {TimeoutBuscaMaximoMs} ms; "
                    + $"veio {timeoutMs} (0 desliga o statement_timeout e remove a proteção de custo do /busca).");
            }
        }

        var orcamentoBusca = configuracao["Busca:OrcamentoRequisicaoMs"];
        if (!string.IsNullOrWhiteSpace(orcamentoBusca))
        {
            if (!int.TryParse(orcamentoBusca, out var orcamentoMs))
            {
                falhas.Add($"Busca:OrcamentoRequisicaoMs precisa ser um inteiro em milissegundos; veio '{orcamentoBusca}'.");
            }
            else if (orcamentoMs < OrcamentoBuscaMinimoMs || orcamentoMs > OrcamentoBuscaMaximoMs)
            {
                falhas.Add($"Busca:OrcamentoRequisicaoMs precisa estar entre {OrcamentoBuscaMinimoMs} e {OrcamentoBuscaMaximoMs} ms; "
                    + $"veio {orcamentoMs} (0 ou negativo remove o orçamento de parede da cascata de autocorreção).");
            }
        }

        if (falhas.Count > 0)
        {
            throw new InvalidOperationException(
                "Configuração de segurança inválida: " + string.Join(" | ", falhas));
        }
    }

    /// <summary>
    /// Uma origem só, no formato exato do header <c>Origin</c> do navegador: esquema, host e porta,
    /// sem caminho nem barra final (com eles o CORS nunca casa e o portal quebra sem diagnóstico).
    /// <c>"*"</c> liga <c>AllowAnyOrigin</c>. HTTP só em loopback, que é o ambiente de smoke local.
    /// </summary>
    private static string? ValidarOrigemCors(string? origem)
    {
        const string formato = "Cors:OrigemPermitida precisa ser UMA origem absoluta, sem caminho, sem barra final "
            + "e nunca \"*\" (ex.: https://codex.exemplo.com.br); HTTP só é aceito em loopback";

        if (string.IsNullOrWhiteSpace(origem))
            return $"{formato}; veio vazio.";

        var ehOrigemValida = Uri.TryCreate(origem, UriKind.Absolute, out var uri)
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.Equals(uri.GetLeftPart(UriPartial.Authority), origem, StringComparison.OrdinalIgnoreCase)
            && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));

        return ehOrigemValida ? null : $"{formato}; veio '{origem}'.";
    }

    /// <summary>
    /// Opt-in explícito para Authority sem HTTPS — o caso legítimo é Keycloak numa rede interna,
    /// com o TLS terminando antes dele. Precisa ser declarado: o default nunca dispensa HTTPS, e
    /// o valor tem de ser exatamente <c>false</c>, não qualquer coisa que "pareça" falso.
    /// </summary>
    internal static bool ExigenciaDeHttpsFoiDispensada(IConfiguration configuracao) =>
        bool.TryParse(configuracao["Keycloak:RequireHttpsMetadata"], out var exigir) && !exigir;
}
