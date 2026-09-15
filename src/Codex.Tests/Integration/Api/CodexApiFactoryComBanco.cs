using Codex.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Codex.Tests.Integration;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Variante de <see cref="CodexApiFactory"/> que aponta o <see cref="CodexDbContext"/> para um
/// Postgres real via Testcontainers (<see cref="PostgresContainerFixture"/>) — usada apenas pelos
/// poucos testes de pipeline que precisam de uma entidade REAL persistida para provar uma regra
/// de negócio ponta-a-ponta (ex.: escopo de aviso carregado do banco pelo validator). A imensa
/// maioria dos testes de pipeline (401/403, If-Match, wiring de validação) não precisa disto —
/// ver <see cref="CodexApiFactory"/>.
/// </summary>
public sealed class CodexApiFactoryComBanco : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CodexApiFactoryComBanco(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });

            // Nenhum job de segundo plano roda sob teste: ele bateria no banco por baixo dos casos,
            // e timing de hosted service é fonte clássica de intermitência. Feito aqui, na
            // ServiceCollection já montada, e não por configuração — callback de
            // ConfigureAppConfiguration só vale depois que o Program.cs já decidiu o que registrar.
            services.RemoveAll<IHostedService>();

            // Program.cs já registrou CodexDbContext apontando para o appsettings.Development.json
            // original — substitui pela connection string do container de teste.
            services.RemoveAll<DbContextOptions<CodexDbContext>>();
            services.AddDbContext<CodexDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", CodexDbContext.Schema)));
        });
    }
}
