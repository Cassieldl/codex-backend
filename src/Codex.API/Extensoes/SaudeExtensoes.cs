using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Codex.API.Extensoes;

/// <summary>
/// Sondas de prontidão (004, PRD §2.2). Registra o <see cref="HealthCheckService"/> que
/// <see cref="Codex.API.Controllers.Publico.SaudeController"/> consome.
/// </summary>
public static class SaudeExtensoes
{
    public static IServiceCollection AddSaude(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<BancoDeDadosHealthCheck>("banco");

        return services;
    }
}

/// <summary>
/// Verifica que o Postgres aceita conexão. Escrito à mão em vez de <c>AddDbContextCheck</c>
/// (`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`): são quatro linhas com o
/// que o projeto já tem, e o `CLAUDE.md` manda não adicionar dependência que o existente resolve.
///
/// <para>
/// <c>CanConnectAsync</c> não executa consulta de negócio — só abre a conexão. É o que
/// readiness precisa responder: "posso receber tráfego", não "o acervo está correto". O tempo
/// máximo de espera é o <c>Timeout</c> da connection string; com banco inalcançável, a sonda
/// demora isso antes de devolver <c>503</c>.
/// </para>
/// </summary>
public sealed class BancoDeDadosHealthCheck : IHealthCheck
{
    private readonly CodexDbContext _contexto;

    public BancoDeDadosHealthCheck(CodexDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _contexto.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception excecao)
        {
            // A exceção vai para o log do servidor pelo relatório; o corpo da resposta nunca a
            // carrega (D4 do PRD) — sonda anônima não é canal de diagnóstico.
            return HealthCheckResult.Unhealthy(exception: excecao);
        }
    }
}
