using System.Text.Json.Serialization;
using Codex.API.Extensoes;
using Codex.API.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Fail-fast de configuração ANTES de qualquer wiring (BAIXO-B10): subir sem poder autenticar, ou
// com host filtering desligado, é degradação silenciosa — rules/security.md manda fail securely.
ConfiguracaoSegurancaExtensoes.ValidarConfiguracaoDeSeguranca(builder.Configuration, builder.Environment);

builder.Host.UseSerilog(
    (context, services, loggerConfiguration) => loggerConfiguration
        // WriteTo em CÓDIGO, antes do ReadFrom (MED-B03): UseSerilog substitui todo o provedor de
        // logging, e sem sink declarado aqui o ReadFrom.Configuration não configura nenhum — todo
        // evento (inclusive 401/403/5xx) é descartado em silêncio e o traceId devolvido ao cliente
        // não corresponde a registro nenhum. A seção "Serilog" do appsettings ajusta NÍVEL por
        // ambiente; o sink de console JSON é piso, não configuração opcional.
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .WriteTo.Console(new CompactJsonFormatter())
        .ReadFrom.Configuration(context.Configuration)
        // Sinks/enrichers registrados no contêiner entram aqui — é o que permite aos testes
        // anexarem um sink em memória e provarem que o evento foi realmente emitido.
        .ReadFrom.Services(services)
        .Enrich.FromLogContext(),

    // O logger fica NO CONTÊINER, não no Log.Logger estático. Com o default (false) o
    // SerilogLoggerFactory resolve pelo estático, e dois hosts no mesmo processo — o que a suíte
    // de integração faz o tempo todo — sobrescrevem o logger um do outro: os eventos de um host
    // vão para os sinks do outro. Por isso o registro de acesso é feito pelo
    // RegistroAcessoMiddleware, com ILogger<T> do contêiner, e não pelo UseSerilogRequestLogging,
    // que escreve no logger estático.
    preserveStaticLogger: true);

builder.Services
    .AddPersistencia(builder.Configuration)
    .AddSeguranca(builder.Configuration, builder.Environment)
    .AddBusca(builder.Configuration)
    .AddValidacao()
    .AddRateLimiting()
    .AddSaude()
    .AddComunicados(builder.Configuration)
    .AddEnvelopeErroDeStatus()
    .AddControllers()
    .AddJsonOptions(opcoes => opcoes.JsonSerializerOptions.Converters.Add(
        // Enum no corpo JSON viaja como NOME ("Nacional"), não como ordinal (0): é o que o
        // cliente real — TypeScript, com união de literais 'Nacional' | 'Proprio' — emite. Sem
        // este converter, todo POST/PUT interno com campo de enum falha na desserialização.
        //
        // allowIntegerValues: true — o ordinal continua aceito na ENTRADA. Não é laxidão: é
        // compatibilidade com os testes/clientes que já mandavam número, e o converter só amplia
        // o que binda, nunca restringe.
        //
        // Sem naming policy (null): o nome sai exatamente como declarado no enum
        // (CategoriaProvedor.Nacional -> "Nacional", OrigemErro.InvoiCy -> "InvoiCy"), idêntico
        // ao que o caminho de LEITURA já produz com enum.ToString(). É o que mantém entrada e
        // saída na mesma grafia — qualquer policy (camelCase etc.) divergiria da resposta e
        // quebraria o frontend em silêncio.
        new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: true)));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // M24 — Swashbuckle. Exposto só em Development (ver abaixo).

// CORS restritivo — uma única origem, nunca "*", sem AllowCredentials (token vai no header
// Authorization, não em cookie — rules/security.md A05).
var origemPermitida = builder.Configuration["Cors:OrigemPermitida"];
builder.Services.AddCors(options => options.AddPolicy("PadraoFrontend", policy =>
{
    if (!string.IsNullOrWhiteSpace(origemPermitida))
    {
        // `WithExposedHeaders("ETag")` e nada mais (AP-SECURITY-012): `ETag` NÃO é header de
        // resposta CORS-safelisted, então sem o expose o navegador devolve
        // `response.headers.etag` como `undefined` cross-origin e o cliente nunca consegue
        // mandar de volta o `If-Match` que os writes internos exigem — emitir o header no
        // backend resolve o protocolo, não o cliente. Não afeta o `304` de P4 (a revalidação
        // por `If-None-Match` é do próprio navegador e não passa por
        // `Access-Control-Expose-Headers`), e `AllowAnyHeader()` já cobre o `If-Match` de
        // REQUISIÇÃO. Nada além do expose: origem única, sem `AllowCredentials`.
        policy.WithOrigins(origemPermitida).AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("ETag");
    }
}));

var app = builder.Build();

// Antes de tudo: quem lê esquema (Request.IsHttps, base do HSTS) ou IP do cliente (partição do
// rate limiter) precisa ver o valor traduzido de X-Forwarded-* (MED-B04). A confiança é restrita
// a loopback + o que estiver declarado em ProxyReverso:* — nunca origem arbitrária.
app.UseForwardedHeaders(ProxyReversoExtensoes.MontarOpcoesProxyReverso(app.Configuration));

if (!app.Environment.IsDevelopment() && ProxyReversoExtensoes.SemProxyDeclarado(app.Configuration))
{
    app.Logger.LogWarning(
        "Nenhum proxy confiável declarado em ProxyReverso:KnownProxies/KnownNetworks. Se houver ingress "
        + "terminando TLS na frente, Strict-Transport-Security não será emitido e o rate limiting do /busca "
        + "colapsa numa única partição.");
}

// Correlação e registro de acesso: empurra o TraceId da requisição para o contexto ANTES do
// tratamento de erros, para que o traceId devolvido no corpo do 5xx apareça no evento
// registrado (MED-B03).
app.UseMiddleware<RegistroAcessoMiddleware>();

// Primeiro middleware do miolo — captura tudo abaixo (analise-backend.md §1.6).
app.UseMiddleware<TratamentoErrosMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

// Envelope único nos status que o framework produz sem corpo (415/405/404/401/403 — BAIXO-B09).
// Depois do tratamento de erros: respostas que já têm corpo não são tocadas.
app.UseEnvelopeErroDeStatus();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("PadraoFrontend");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Necessário para Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory<Program>) nos testes de integração.
public partial class Program;
