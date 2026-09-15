using Codex.Application.Interfaces;
using Codex.Application.Services;
using Codex.Application.Services.Interno;
using Codex.Infrastructure.Data;
using Codex.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Codex.API.Extensoes;

/// <summary>
/// Wiring de persistência: DbContext + repositórios + services de leitura pública (BACK-1 —
/// fundação; repositórios/services públicos completados por BACK-3) e de escrita interna
/// (BACK-4 — I1-I22, analise-backend.md §4.2).
/// </summary>
public static class PersistenciaExtensoes
{
    public static IServiceCollection AddPersistencia(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.");

        services.AddDbContext<CodexDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", CodexDbContext.Schema)));

        // Leitura pública (BACK-3, analise-backend.md §4.1).
        services.AddScoped<IProvedorRepositorioPublico, ProvedorRepositorioPublico>();
        services.AddScoped<IVersaoProvedorRepositorioPublico, VersaoProvedorRepositorioPublico>();
        services.AddScoped<IErroRepositorioPublico, ErroRepositorioPublico>();
        services.AddScoped<IArtigoRepositorioPublico, ArtigoRepositorioPublico>();
        services.AddScoped<IProvedorServicoPublico, ProvedorServicoPublico>();
        services.AddScoped<IErroServicoPublico, ErroServicoPublico>();
        services.AddScoped<IArtigoServicoPublico, ArtigoServicoPublico>();

        // Escrita interna (BACK-4, analise-backend.md §4.2) — repositórios.
        services.AddScoped<IProvedorRepositorioInterno, ProvedorRepositorioInterno>();
        services.AddScoped<IVersaoProvedorRepositorioInterno, VersaoProvedorRepositorioInterno>();
        services.AddScoped<ICampoVersaoRepositorioInterno, CampoVersaoRepositorioInterno>();
        services.AddScoped<IAvisoRepositorioInterno, AvisoRepositorioInterno>();
        services.AddScoped<IVersaoLayoutRepositorioInterno, VersaoLayoutRepositorioInterno>();
        services.AddScoped<ICampoLayoutRepositorioInterno, CampoLayoutRepositorioInterno>();
        services.AddScoped<IErroRepositorioInterno, ErroRepositorioInterno>();
        services.AddScoped<IArtigoRepositorioInterno, ArtigoRepositorioInterno>();
        services.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalhoInterna>();

        // Escrita interna — services (regra de negócio + template de publicação de §3.4).
        services.AddScoped<IProvedorServicoInterno, ProvedorServicoInterno>();
        services.AddScoped<IVersaoProvedorServicoInterno, VersaoProvedorServicoInterno>();
        services.AddScoped<ICampoVersaoServicoInterno, CampoVersaoServicoInterno>();
        services.AddScoped<IAvisoServicoInterno, AvisoServicoInterno>();
        services.AddScoped<IVersaoLayoutServicoInterno, VersaoLayoutServicoInterno>();
        services.AddScoped<ICampoLayoutServicoInterno, CampoLayoutServicoInterno>();
        services.AddScoped<IErroServicoInterno, ErroServicoInterno>();
        services.AddScoped<IArtigoServicoInterno, ArtigoServicoInterno>();
        services.AddScoped<IIndiceBuscaReconstrucaoServico, IndiceBuscaReconstrucaoServico>();

        return services;
    }
}
