using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Codex.Infrastructure.Data;

/// <summary>
/// Factory de design-time para <c>dotnet ef</c> (migrations) — nunca usada em runtime da API
/// (lá o DbContext é registrado via <c>AddPersistencia</c>, com a connection string vinda de
/// configuração). Lê a connection string da variável de ambiente
/// <c>ConnectionStrings__Default</c> (mesmo padrão do restante do projeto — nunca lê <c>.env</c>
/// diretamente, ver README).
/// </summary>
public sealed class CodexDbContextFactory : IDesignTimeDbContextFactory<CodexDbContext>
{
    public CodexDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=codex_db;Username=codex_user;Password=troque-me";

        var optionsBuilder = new DbContextOptionsBuilder<CodexDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CodexDbContext.Schema));

        return new CodexDbContext(optionsBuilder.Options);
    }
}
