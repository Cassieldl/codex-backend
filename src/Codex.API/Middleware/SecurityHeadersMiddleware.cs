namespace Codex.API.Middleware;

/// <summary>
/// Headers de segurança obrigatórios (rules/security.md A05) — genérico, sem lógica de negócio,
/// por isso escopo da fundação (BACK-1). Nunca <c>X-Powered-By</c>/<c>Server</c> (removidos por
/// padrão do Kestrel/ASP.NET Core).
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _proximo;

    public SecurityHeadersMiddleware(RequestDelegate proximo)
    {
        _proximo = proximo;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        contexto.Response.OnStarting(() =>
        {
            var headers = contexto.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'";

            if (contexto.Request.IsHttps)
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            return Task.CompletedTask;
        });

        await _proximo(contexto);
    }
}
