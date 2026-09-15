using Codex.API.Jobs;
using Codex.Application.Interfaces;
using Codex.Application.Services;
using Codex.Application.Services.Interno;
using Codex.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Codex.API.Extensoes;

/// <summary>
/// Wiring de comunicados (I30-I35 + job de expiração, analise-backend.md §6/§10): repositório,
/// service e o registro CONDICIONAL do serviço de expiração em segundo plano.
/// </summary>
public static class ComunicadosExtensoes
{
    /// <summary>Liga/desliga o job. Em <c>Development</c> nasce desligado: o predicado de vigência já protege a leitura, e job de segundo plano dentro da suíte de testes é fonte de intermitência.</summary>
    public const string ChaveHabilitada = "Comunicados:ExpiracaoHabilitada";

    public const string ChaveIntervalo = "Comunicados:IntervaloExpiracaoSegundos";

    private const int IntervaloPadraoSegundos = 300;

    public static IServiceCollection AddComunicados(this IServiceCollection services, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        // TimeProvider é a fonte única de "agora" do módulo — service, validators e job leem daqui.
        // TryAdd porque é um tipo do framework: se alguém (teste, outro módulo) já registrou um
        // relógio controlável, este registro não pode sobrescrevê-lo.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IComunicadoRepositorioInterno, ComunicadoRepositorioInterno>();
        services.AddScoped<IComunicadoServicoInterno, ComunicadoServicoInterno>();
        services.AddScoped<IExpiracaoComunicadosService, ExpiracaoComunicadosService>();

        // Registro CONDICIONAL: desligado, o hosted service nem chega a existir no host (§10.3).
        if (!configuracao.GetValue(ChaveHabilitada, defaultValue: true))
            return services;

        var intervalo = TimeSpan.FromSeconds(
            Math.Max(1, configuracao.GetValue(ChaveIntervalo, defaultValue: IntervaloPadraoSegundos)));

        services.AddHostedService(provedor => new ExpiracaoComunicadosJob(
            provedor.GetRequiredService<IServiceScopeFactory>(),
            provedor.GetRequiredService<TimeProvider>(),
            provedor.GetRequiredService<ILogger<ExpiracaoComunicadosJob>>(),
            intervalo));

        return services;
    }
}
