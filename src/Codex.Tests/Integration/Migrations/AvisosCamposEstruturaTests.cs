using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Migrations;

/// <summary>
/// A junção aviso × campo afetado existe no banco migrado com as garantias que a gravação do
/// vínculo pressupõe: chave composta (sem repetido), FKs sem cascata (o aviso e o campo são
/// soft-deleted, nunca apagados em silêncio) e o índice de busca reversa por campo. Consulta o
/// catálogo do Postgres — o build passa mesmo com a tabela ausente.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class AvisosCamposEstruturaTests
{
    private readonly PostgresContainerFixture _fixture;

    public AvisosCamposEstruturaTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ChavePrimaria_EhCompostaPorAvisoECampo()
    {
        var definicao = await LerDefinicaoDeConstraintAsync("pk_codex_avisoscampos");

        definicao.Should().Contain("PRIMARY KEY (avisoid, campoversaoid)");
    }

    [Theory]
    [InlineData("fk_codex_avisoscampos_avisoid", "REFERENCES codex.avisos")]
    [InlineData("fk_codex_avisoscampos_campoversaoid", "REFERENCES codex.camposversao")]
    public async Task ChavesEstrangeiras_ApontamParaATabelaCertaESemCascata(string nomeConstraint, string referenciaEsperada)
    {
        var definicao = await LerDefinicaoDeConstraintAsync(nomeConstraint);

        definicao.Should().Contain(referenciaEsperada).And.Contain("ON DELETE RESTRICT");
    }

    [Fact]
    public async Task IndiceDeBuscaReversaPorCampo_Existe()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'codex' AND tablename = 'avisoscampos' "
            + "AND indexname = 'idx_codex_avisoscampos_campoversaoid'";

        var indexdef = (string?)await comando.ExecuteScalarAsync();

        indexdef.Should().NotBeNull().And.Subject.Should().Contain("(campoversaoid)");
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
            WHERE n.nspname = 'codex' AND t.relname = 'avisoscampos' AND c.conname = @nome
            """;
        comando.Parameters.AddWithValue("nome", nomeConstraint);

        var definicao = (string?)await comando.ExecuteScalarAsync();

        definicao.Should().NotBeNull(because: $"{nomeConstraint} está no DDL de modelo-dados.md §4.8");
        return definicao!;
    }
}
