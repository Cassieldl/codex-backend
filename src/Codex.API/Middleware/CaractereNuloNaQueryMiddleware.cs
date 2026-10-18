namespace Codex.API.Middleware;

/// <summary>
/// Recusa com 400 a query string que traz o byte NUL, que o Postgres não aceita em texto e
/// derrubaria a rota com 500. Responde sem corpo: o envelope de erro vem de
/// <c>UseEnvelopeErroDeStatus</c>, registrado antes deste middleware.
/// </summary>
public sealed class CaractereNuloNaQueryMiddleware
{
    private readonly RequestDelegate _proximo;

    public CaractereNuloNaQueryMiddleware(RequestDelegate proximo)
    {
        _proximo = proximo;
    }

    public Task InvokeAsync(HttpContext contexto)
    {
        if (!QueryTemCaractereNulo(contexto.Request))
            return _proximo(contexto);

        contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
        return Task.CompletedTask;
    }

    private static bool QueryTemCaractereNulo(HttpRequest requisicao)
    {
        if (!requisicao.QueryString.HasValue)
            return false;

        foreach (var (chave, valores) in requisicao.Query)
        {
            if (chave.Contains('\0', StringComparison.Ordinal))
                return true;

            foreach (var valor in valores)
            {
                if (valor is not null && valor.Contains('\0', StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }
}
