using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Migrations;

/// <summary>
/// A tabela da fila editorial existe no banco migrado com as garantias que a gravação pressupõe.
/// Consulta o catálogo do Postgres (o build passa mesmo com a migration vazia — gotcha de
/// <c>ExcludeFromMigrations</c>). A tabela não tem FK por desenho: o registro guarda o
/// identificador e o nome do token como texto, para sobreviver à troca de provedor de identidade.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class LacunasBuscaEstruturaTests
{
    private readonly PostgresContainerFixture _fixture;

    public LacunasBuscaEstruturaTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ChavePrimaria_EhOIdentificadorDaLacuna()
    {
        var definicao = await LerDefinicaoDeConstraintAsync("pk_codex_lacunasbusca");

        definicao.Should().Contain("PRIMARY KEY (lacunabuscaid)");
    }

    [Theory]
    [InlineData("ck_codex_lacunasbusca_estado", "Aberta")]
    [InlineData("ck_codex_lacunasbusca_estado", "Resolvida")]
    [InlineData("ck_codex_lacunasbusca_estado", "Descartada")]
    [InlineData("ck_codex_lacunasbusca_resolucao_consistente", "resolvidaem IS NOT NULL")]
    [InlineData("ck_codex_lacunasbusca_termo_minimo", "length(btrim(")]
    public async Task ChecksDeIntegridade_Existem(string nomeConstraint, string trechoEsperado)
    {
        var definicao = await LerDefinicaoDeConstraintAsync(nomeConstraint);

        definicao.Should().Contain(trechoEsperado);
    }

    [Fact]
    public async Task NaoHaChaveEstrangeira()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            """
            SELECT count(*) FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'codex' AND t.relname = 'lacunasbusca' AND c.contype = 'f'
            """;

        ((long)(await comando.ExecuteScalarAsync())!).Should().Be(0);
    }

    [Theory]
    [InlineData("idx_codex_lacunasbusca_estado_criadoem", "(estado, criadoem)")]
    [InlineData("idx_codex_lacunasbusca_termonormalizado_estado", "(termonormalizado, estado)")]
    public async Task Indices_Existem(string nomeIndice, string colunasEsperadas)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'codex' AND tablename = 'lacunasbusca' AND indexname = @nome";
        comando.Parameters.AddWithValue("nome", nomeIndice);

        var indexdef = (string?)await comando.ExecuteScalarAsync();

        indexdef.Should().NotBeNull().And.Subject.Should().Contain(colunasEsperadas);
    }

    [Theory]
    [InlineData("termo", 300)]
    [InlineData("termonormalizado", 300)]
    [InlineData("parceiro", 160)]
    [InlineData("numerochamado", 60)]
    [InlineData("nota", 500)]
    [InlineData("registradoporid", 100)]
    [InlineData("registradopornome", 160)]
    [InlineData("resolvidoporid", 100)]
    [InlineData("resolvidopornome", 160)]
    public async Task TetosDasColunas_SaoOsDoContrato(string coluna, int tetoEsperado)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "SELECT character_maximum_length FROM information_schema.columns "
            + "WHERE table_schema = 'codex' AND table_name = 'lacunasbusca' AND column_name = @coluna";
        comando.Parameters.AddWithValue("coluna", coluna);

        ((int?)await comando.ExecuteScalarAsync()).Should().Be(tetoEsperado);
    }

    /// <summary>
    /// O trigger não é gerado pelo EF e a ausência dele é silenciosa. Prova por comportamento:
    /// UPDATE em SQL puro e comparação do valor antes/depois (leitura por reader tipado — o
    /// caminho genérico de <c>ExecuteScalarAsync</c> devolve <c>DateTime</c> e o cast daria null).
    /// </summary>
    [Fact]
    public async Task UpdateEmSqlPuro_TemAtualizadoemCarimbadoPeloTrigger()
    {
        var id = await InserirLacunaAsync();
        var antes = await LerAtualizadoemAsync(id);

        await using (var conexao = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = "UPDATE codex.lacunasbusca SET nota = 'tocada pelo teste do trigger' WHERE lacunabuscaid = @id";
            comando.Parameters.AddWithValue("id", id);
            (await comando.ExecuteNonQueryAsync()).Should().Be(1);
        }

        var depois = await LerAtualizadoemAsync(id);

        depois.Should().BeAfter(antes, because: "trg_codex_lacunasbusca_atualizadoem precisa carimbar atualizadoem em todo UPDATE");
    }

    [Fact]
    public async Task Check_RecusaLacunaResolvidaSemQuemEQuando()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            """
            INSERT INTO codex.lacunasbusca (termo, termonormalizado, estado, registradoporid)
            VALUES ('termo', 'termo', 'Resolvida', 'sub-1')
            """;

        var acao = async () => await comando.ExecuteNonQueryAsync();

        (await acao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23514");
    }

    private async Task<int> InserirLacunaAsync()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            """
            INSERT INTO codex.lacunasbusca (termo, termonormalizado, estado, registradoporid)
            VALUES ('termo do trigger', 'termo do trigger', 'Aberta', 'sub-trigger')
            RETURNING lacunabuscaid
            """;

        return (int)(await comando.ExecuteScalarAsync())!;
    }

    private async Task<DateTimeOffset> LerAtualizadoemAsync(int id)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT atualizadoem FROM codex.lacunasbusca WHERE lacunabuscaid = @id";
        comando.Parameters.AddWithValue("id", id);

        await using var leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue();

        return leitor.GetFieldValue<DateTimeOffset>(0);
    }

    private async Task<string> LerDefinicaoDeConstraintAsync(string nomeConstraint)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            """
            SELECT pg_get_constraintdef(c.oid)
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'codex' AND t.relname = 'lacunasbusca' AND c.conname = @nome
            """;
        comando.Parameters.AddWithValue("nome", nomeConstraint);

        var definicao = (string?)await comando.ExecuteScalarAsync();

        definicao.Should().NotBeNull(because: $"{nomeConstraint} faz parte da migration da fila editorial");
        return definicao!;
    }
}
