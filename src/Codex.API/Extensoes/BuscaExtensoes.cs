using Codex.Application.Interfaces;
using Codex.Application.UseCases.Busca;
using Codex.Infrastructure.Busca;

namespace Codex.API.Extensoes;

/// <summary>
/// Wiring da busca (BACK-2): cascata de consulta real (<see cref="ConsultaBuscaPostgres"/>),
/// writer real da cascata de publicação (<see cref="IndiceBuscaWriter"/>) e o serviço fino de
/// validação/proteção de custo (<see cref="BuscaService"/>) que os controllers consomem.
/// </summary>
public static class BuscaExtensoes
{
    public static IServiceCollection AddBusca(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IIndiceBuscaWriter, IndiceBuscaWriter>();
        services.AddScoped<IConsultaBusca, ConsultaBuscaPostgres>();
        services.AddScoped<IBuscaService, BuscaService>();

        return services;
    }
}
