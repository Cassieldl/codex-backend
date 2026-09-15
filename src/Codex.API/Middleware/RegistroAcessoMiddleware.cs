using System.Diagnostics;
using System.Security.Claims;
using Serilog.Context;

namespace Codex.API.Middleware;

/// <summary>
/// Correlação e registro de acesso (rules/security.md A09 — achado <c>MED-B03</c>): empurra o
/// <c>TraceId</c> da requisição para o contexto de log ANTES de qualquer outro middleware, para
/// que todo evento emitido abaixo (inclusive o <c>Error</c> de 5xx do
/// <see cref="TratamentoErrosMiddleware"/>) carregue o mesmo identificador que a API devolve ao
/// cliente no corpo do erro — sem isso, o <c>traceId</c> da resposta não corresponde a registro
/// nenhum. Emite um evento de conclusão por requisição e sobe para <c>Warning</c> em
/// <c>401</c>/<c>403</c>, que não passam por exceção e portanto não seriam registrados por mais
/// ninguém.
///
/// <para>
/// Substitui o <c>UseSerilogRequestLogging</c> de propósito: aquele middleware escreve no
/// <c>Log.Logger</c> ESTÁTICO, enquanto este usa <see cref="ILogger{TCategoryName}"/> do
/// contêiner — determinístico quando existe mais de um host no processo.
/// </para>
///
/// <para>
/// Nunca loga token, cabeçalho <c>Authorization</c>, corpo da requisição, query string nem claim
/// sensível: só método, path, status, duração e o identificador opaco do usuário (<c>sub</c>).
/// </para>
/// </summary>
public sealed class RegistroAcessoMiddleware
{
    private readonly RequestDelegate _proximo;
    private readonly ILogger<RegistroAcessoMiddleware> _logger;

    public RegistroAcessoMiddleware(RequestDelegate proximo, ILogger<RegistroAcessoMiddleware> logger)
    {
        _proximo = proximo;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        using (LogContext.PushProperty("TraceId", contexto.TraceIdentifier))
        {
            var cronometro = Stopwatch.StartNew();

            try
            {
                await _proximo(contexto);
            }
            finally
            {
                cronometro.Stop();
                Registrar(contexto, cronometro.Elapsed.TotalMilliseconds);
            }
        }
    }

    private void Registrar(HttpContext contexto, double duracaoMs)
    {
        var status = contexto.Response.StatusCode;

        // 5xx no nível Error mesmo quando não houve exceção (o TratamentoErrosMiddleware só
        // registra o que passou por exceção); 401/403 em Warning, como manda rules/backend.md.
        var (nivel, descricao) = status switch
        {
            StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden =>
                (LogLevel.Warning, "Acesso negado"),

            >= 500 =>
                (LogLevel.Error, "Erro ao responder"),

            _ =>
                (LogLevel.Information, "Requisição concluída"),
        };

        _logger.Log(
            nivel,
            "{Descricao}: {Metodo} {Path} respondeu {StatusCode} em {DuracaoMs:0.0} ms para o usuário {UsuarioId}",
            descricao,
            contexto.Request.Method,
            contexto.Request.Path.Value,
            status,
            duracaoMs,
            IdentificadorUsuario(contexto));
    }

    /// <summary>
    /// Identificador opaco do usuário (<c>sub</c> do Keycloak) — nunca e-mail, nome ou token.
    /// <c>"anonimo"</c> quando não há principal autenticado (caso típico do <c>401</c>).
    /// </summary>
    private static string IdentificadorUsuario(HttpContext contexto) =>
        contexto.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? contexto.User.FindFirst("sub")?.Value
        ?? "anonimo";
}
