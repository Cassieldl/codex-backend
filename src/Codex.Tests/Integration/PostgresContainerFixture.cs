using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Codex.Tests.Integration;

/// <summary>
/// Sobe um Postgres 18 real via Testcontainers (nunca InMemory — AP-BACKEND-005: já
/// aconteceu de 389 testes InMemory passarem enquanto a API quebrava em produção). Replica, em
/// SQL puro (sem <c>\getenv</c>, que é meta-comando de <c>psql</c> e não roda via ADO.NET), a
/// parte de <c>database/init.sql</c> que as migrations do EF assumem já existir (extensões,
/// <c>codex.f_unaccent</c>, <c>codex.codex_pt</c>, <c>codex.normalizar_busca</c>) — depois
/// aplica as migrations reais do projeto. Conecta como superuser do container em todos os
/// passos (simplificação de teste: não reproduz o isolamento de privilégio de
/// <c>codex_user</c>, que não faz parte do que os 9 testes obrigatórios desta unidade provam).
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("codex_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await CriarInfraestruturaDeBuscaAsync(ConnectionString);
        await AplicarMigrationsAsync(ConnectionString);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public CodexDbContext CriarContexto() => CriarContexto(ConnectionString);

    public static CodexDbContext CriarContexto(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CodexDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", CodexDbContext.Schema))
            .Options;

        return new CodexDbContext(options);
    }

    /// <summary>
    /// Cria um banco ADICIONAL no MESMO container, migrado e sem uma única linha de conteúdo, e
    /// devolve a connection string dele. É o que permite provar "acervo vazio responde 200" de
    /// verdade: o banco principal é compartilhado pela <see cref="PostgresCollection"/> e nunca
    /// está vazio. Barato — reaproveita o container, só paga o CREATE DATABASE + migrations.
    /// </summary>
    public async Task<string> CriarBancoVazioAsync(string nomeDoBanco)
    {
        await using (var conexao = new NpgsqlConnection(ConnectionString))
        {
            await conexao.OpenAsync();

            await using var comando = conexao.CreateCommand();
            // Identificador não pode ser parametrizado; o nome vem sempre de constante/sufixo de
            // teste, e as aspas duplas impedem que ele seja interpretado como mais de um token.
            comando.CommandText = $"""CREATE DATABASE "{nomeDoBanco.Replace("\"", string.Empty, StringComparison.Ordinal)}" """;
            await comando.ExecuteNonQueryAsync();
        }

        var connectionStringDoBancoVazio = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = nomeDoBanco,
        }.ConnectionString;

        await CriarInfraestruturaDeBuscaAsync(connectionStringDoBancoVazio);
        await AplicarMigrationsAsync(connectionStringDoBancoVazio);

        return connectionStringDoBancoVazio;
    }

    private static async Task CriarInfraestruturaDeBuscaAsync(string connectionString)
    {
        await using var conexao = new NpgsqlConnection(connectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            """
            CREATE EXTENSION IF NOT EXISTS unaccent WITH SCHEMA public;
            CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;
            CREATE EXTENSION IF NOT EXISTS fuzzystrmatch WITH SCHEMA public;

            CREATE SCHEMA IF NOT EXISTS codex;

            CREATE OR REPLACE FUNCTION codex.f_unaccent(text)
            RETURNS text
            LANGUAGE sql
            IMMUTABLE
            PARALLEL SAFE
            STRICT
            AS $$
                SELECT public.unaccent('public.unaccent'::regdictionary, $1)
            $$;

            DO $do$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_catalog.pg_ts_config c
                    JOIN pg_catalog.pg_namespace n ON n.oid = c.cfgnamespace
                    WHERE c.cfgname = 'codex_pt' AND n.nspname = 'codex'
                ) THEN
                    EXECUTE 'CREATE TEXT SEARCH CONFIGURATION codex.codex_pt (COPY = pg_catalog.portuguese)';
                    EXECUTE 'ALTER TEXT SEARCH CONFIGURATION codex.codex_pt ALTER MAPPING FOR hword, hword_part, word WITH unaccent, portuguese_stem';
                END IF;
            END
            $do$;

            CREATE OR REPLACE FUNCTION codex.normalizar_busca(entrada text)
            RETURNS text
            LANGUAGE sql
            IMMUTABLE
            PARALLEL SAFE
            AS $$
                SELECT
                    trim(regexp_replace(lower(codex.f_unaccent(coalesce(entrada, ''))), '\s+', ' ', 'g'))
                    || ' ' ||
                    trim(regexp_replace(
                        regexp_replace(
                            lower(codex.f_unaccent(coalesce(entrada, ''))),
                            '[]/\_.:;,|()<>[-]', ' ', 'g'
                        ),
                        '\s+', ' ', 'g'
                    ))
                    || ' ' ||
                    regexp_replace(lower(codex.f_unaccent(coalesce(entrada, ''))), '[^a-z0-9]', '', 'g')
            $$;
            """;

        await comando.ExecuteNonQueryAsync();
    }

    private static async Task AplicarMigrationsAsync(string connectionString)
    {
        await using var contexto = CriarContexto(connectionString);
        await contexto.Database.MigrateAsync();
    }
}
