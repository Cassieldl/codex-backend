using Codex.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Unit.Migrations;

public sealed class ModeloSemDerivaDasMigrationsTests
{
    // A comparação é entre o modelo e o snapshot em memória; a conexão nunca é aberta.
    private const string ConexaoNuncaAberta = "Host=localhost;Database=codex_db;Username=codex_user;Password=nao-usada";

    [Fact]
    public void ModeloEf_NaoTemMudancaSemMigration()
    {
        var opcoes = new DbContextOptionsBuilder<CodexDbContext>()
            .UseNpgsql(ConexaoNuncaAberta, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", CodexDbContext.Schema))
            .Options;

        using var contexto = new CodexDbContext(opcoes);

        contexto.Database.HasPendingModelChanges().Should().BeFalse(
            because: "toda mudança nas configurations precisa de migration; rode 'dotnet ef migrations add' para ver a deriva");
    }
}
