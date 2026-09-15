using Codex.Application.Interfaces;

namespace Codex.API.Jobs;

/// <summary>
/// Casca de agendamento da expiração de comunicados (analise-backend.md §10.3) — toda a regra vive
/// em <see cref="IExpiracaoComunicadosService"/>, que é testável sem host. Registrado
/// condicionalmente por <c>AddComunicados</c>: com <c>Comunicados:ExpiracaoHabilitada=false</c>
/// este <see cref="BackgroundService"/> não é instanciado nem agendado — não é "roda e não faz
/// nada", é "não existe no host". É o que permite ao teste do predicado de vigência provar a rede
/// de segurança sozinha, sem depender de timing de job.
/// </summary>
public sealed class ExpiracaoComunicadosJob : BackgroundService
{
    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;
    private readonly ILogger<ExpiracaoComunicadosJob> _logger;
    private readonly TimeSpan _intervalo;

    public ExpiracaoComunicadosJob(
        IServiceScopeFactory escopos,
        TimeProvider relogio,
        ILogger<ExpiracaoComunicadosJob> logger,
        TimeSpan intervalo)
    {
        _escopos = escopos;
        _relogio = relogio;
        _logger = logger;
        _intervalo = intervalo;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Roda uma vez ANTES do primeiro Delay: cobre o processo que subiu de novo depois de ficar
        // fora do ar por mais que o intervalo — converge na hora em vez de servir dado vencido por
        // até um ciclo inteiro.
        while (!stoppingToken.IsCancellationRequested)
        {
            await ConvergirAsync(stoppingToken);

            try
            {
                await Task.Delay(_intervalo, _relogio, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConvergirAsync(CancellationToken ct)
    {
        try
        {
            using var escopo = _escopos.CreateScope();
            var servico = escopo.ServiceProvider.GetRequiredService<IExpiracaoComunicadosService>();

            var convergidos = await servico.ExecutarAsync(_relogio.GetUtcNow(), ct);

            _logger.LogInformation("Expiração de comunicados convergiu {Convergidos} registro(s).", convergidos);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            // Nunca propaga: exceção não tratada em BackgroundService derruba o host inteiro no
            // .NET moderno — banco momentaneamente fora do ar não pode matar a API.
            _logger.LogError(excecao, "Falha ao executar a expiração de comunicados.");
        }
    }
}
