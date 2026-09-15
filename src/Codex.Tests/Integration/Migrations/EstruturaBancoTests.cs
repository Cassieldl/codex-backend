using System.Text.RegularExpressions;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Codex.Tests.Integration.Migrations;

[Collection(PostgresCollection.Nome)]
public sealed class EstruturaBancoTests
{
    private readonly PostgresContainerFixture _fixture;

    public EstruturaBancoTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ItensBusca_TemAsTresColunasGeradasComoStored()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            """
            SELECT column_name, is_generated, generation_expression IS NOT NULL AS tem_expressao
            FROM information_schema.columns
            WHERE table_schema = 'codex' AND table_name = 'itensbusca'
              AND column_name IN ('documento', 'textobusca', 'visivelpublico')
            """;

        var colunasGeradas = new Dictionary<string, bool>();
        await using (var leitor = await comando.ExecuteReaderAsync())
        {
            while (await leitor.ReadAsync())
                colunasGeradas[leitor.GetString(0)] = leitor.GetString(1) == "ALWAYS" && leitor.GetBoolean(2);
        }

        colunasGeradas.Should().ContainKeys("documento", "textobusca", "visivelpublico");
        colunasGeradas.Values.Should().OnlyContain(gerada => gerada,
            because: "as três colunas de codex.itensbusca precisam ser GENERATED ALWAYS ... STORED (modelo-dados.md §5.6)");
    }

    [Fact]
    public async Task ItensBusca_TemOsDoisIndicesGinParciais()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            """
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE schemaname = 'codex' AND tablename = 'itensbusca'
              AND indexname IN ('idx_codex_itensbusca_documento_publico', 'idx_codex_itensbusca_documento_interno')
            """;

        var indices = new Dictionary<string, string>();
        await using (var leitor = await comando.ExecuteReaderAsync())
        {
            while (await leitor.ReadAsync())
                indices[leitor.GetString(0)] = leitor.GetString(1);
        }

        indices.Should().ContainKey("idx_codex_itensbusca_documento_publico");
        indices.Should().ContainKey("idx_codex_itensbusca_documento_interno");

        indices["idx_codex_itensbusca_documento_publico"].Should().Contain("gin").And.Contain("visivelpublico");
        indices["idx_codex_itensbusca_documento_interno"].Should().Contain("gin").And.Contain("desativadoem IS NULL");
    }

    [Theory]
    [InlineData("ck_codex_avisos_escopo_xor")]
    [InlineData("ck_codex_camposlayout_descontinuado_consistente")]
    public async Task ChecksNovosDaRodada3_Existem(string nomeConstraint)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM pg_constraint WHERE conname = @nome";
        comando.Parameters.AddWithValue("nome", nomeConstraint);

        var quantidade = (long)(await comando.ExecuteScalarAsync())!;

        quantidade.Should().Be(1, because: $"a constraint {nomeConstraint} precisa existir (Rodada 3 de decisoes.md)");
    }

    [Fact]
    public async Task TodasAsTabelasAuditadas_TemTriggerDeAtualizadoem()
    {
        string[] tabelasAuditadas =
        [
            "versoeslayout", "camposlayout", "provedores", "aliasesprovedor", "versoesprovedor",
            "camposversao", "avisos", "avisoscampos", "erros", "variantesmensagem",
            "errosversoes", "erroscampos", "itensbusca", "termosbusca", "redirecionamentos",
            "artigos", "comunicados", "artigoscamposlayout", "artigoserros",
        ];

        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            """
            SELECT c.relname, t.tgname
            FROM pg_trigger t
            JOIN pg_class c ON c.oid = t.tgrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE NOT t.tgisinternal AND n.nspname = 'codex' AND t.tgname LIKE 'trg_codex_%_atualizadoem'
            """;

        var triggersPorTabela = new HashSet<string>();
        await using (var leitor = await comando.ExecuteReaderAsync())
        {
            while (await leitor.ReadAsync())
                triggersPorTabela.Add(leitor.GetString(0));
        }

        triggersPorTabela.Should().BeEquivalentTo(tabelasAuditadas,
            because: "toda tabela auditada precisa do trigger BEFORE UPDATE codex.set_atualizadoem() (modelo-dados.md §3)");
    }

    [Theory]
    [InlineData("artigos")]
    [InlineData("comunicados")]
    [InlineData("artigoscamposlayout")]
    [InlineData("artigoserros")]
    public async Task AsQuatroTabelasEditoriais_SaemDeExcludeFromMigrations_ExistemNoBanco(string tabela)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'codex' AND table_name = @tabela";
        comando.Parameters.AddWithValue("tabela", tabela);

        var quantidade = (long)(await comando.ExecuteScalarAsync())!;

        quantidade.Should().Be(1,
            because: $"{tabela} sai de ExcludeFromMigrations() na 002 (decisoes.md U-1) — se a exclusão " +
                     "ainda estivesse ativa, a migration nunca teria emitido o CREATE TABLE e esta consulta " +
                     "devolveria zero, mesmo com a Configuration já sem a chamada");
    }

    /// <summary>
    /// PQ-03 — compara a STRING PERSISTIDA do CHECK real (via <c>pg_get_constraintdef</c>) com os
    /// valores do enum, nunca o nome do membro C#. Uma divergência de uma letra compila, passa em
    /// todo teste de unidade e só explode com 23514 na primeira publicação de artigo em produção.
    /// </summary>
    [Fact]
    public async Task CkArtigosTipo_TemExatamenteOsValoresDoEnumTipoArtigo()
    {
        var valoresNoCheck = await ValoresDoCheckAsync("ck_codex_artigos_tipo");
        var valoresDoEnum = Enum.GetValues<TipoArtigo>().Select(v => v.ToString());

        valoresNoCheck.Should().BeEquivalentTo(valoresDoEnum,
            because: "ck_codex_artigos_tipo e TipoArtigo precisam concordar caractere a caractere (U-3/M-5)");
    }

    /// <summary>PQ-03 — mesmo padrão para os 6 valores de TipoItemIndexado, incluindo o 'artigo' novo da 002.</summary>
    [Fact]
    public async Task CkItensBuscaTipoItem_TemExatamenteOsValoresDoEnumTipoItemIndexado()
    {
        var valoresNoCheck = await ValoresDoCheckAsync("ck_codex_itensbusca_tipoitem");
        var valoresDoEnum = Enum.GetValues<TipoItemIndexado>().Select(v => v.ToString());

        valoresNoCheck.Should().BeEquivalentTo(valoresDoEnum,
            because: "ck_codex_itensbusca_tipoitem e TipoItemIndexado precisam concordar caractere a caractere");
    }

    [Fact]
    public async Task IdxComunicadosVigentes_ExisteComOPredicadoExato()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT indexdef FROM pg_indexes WHERE indexname = 'idx_codex_comunicados_vigentes'";

        var indexdef = (string?)await comando.ExecuteScalarAsync();

        indexdef.Should().NotBeNull(because: "sustenta ComunicadoRepositorio.QueryVigentes nas duas direções (modelo-dados.md §2.1/§7)");
        indexdef!.Should().Contain("expiraem")
            .And.Contain("publicado")
            .And.Contain("desativadoem IS NULL");
    }

    /// <summary>Extrai os literais de string de um CHECK <c>... = ANY (ARRAY['a', 'b', ...])</c> via <c>pg_get_constraintdef</c>.</summary>
    private async Task<IReadOnlyList<string>> ValoresDoCheckAsync(string nomeConstraint)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = @nome";
        comando.Parameters.AddWithValue("nome", nomeConstraint);

        var definicao = (string?)await comando.ExecuteScalarAsync();
        definicao.Should().NotBeNull(because: $"a constraint {nomeConstraint} precisa existir");

        return [.. Regex.Matches(definicao!, "'([^']+)'::character varying").Select(m => m.Groups[1].Value)];
    }

    /// <summary>
    /// Concorrência otimista (modelo-dados.md §5, M-4) — prova via comportamento real, não via
    /// inspeção de schema: <c>xmin</c> é coluna de sistema, sempre existe; o que faltava era só a
    /// propriedade `Versao` e o bloco de mapeamento na Configuration. Sem eles, o SaveChanges não
    /// gera `WHERE xmin = @p` e dois PUTs concorrentes se sobrescreveriam em silêncio.
    /// </summary>
    [Fact]
    public async Task Artigo_ConflitoRealDeEdicao_LancaDbUpdateConcurrencyException()
    {
        int artigoId;
        await using (var contextoCriacao = _fixture.CriarContexto())
        {
            var artigo = Artigo.Criar(TipoArtigo.Conceito, $"Artigo xmin {Guid.NewGuid():N}", "corpo inicial");
            contextoCriacao.Artigos.Add(artigo);
            await contextoCriacao.SaveChangesAsync();
            artigoId = artigo.Id;
        }

        await using var contextoA = _fixture.CriarContexto();
        await using var contextoB = _fixture.CriarContexto();

        var artigoA = await contextoA.Artigos.FirstAsync(a => a.Id == artigoId);
        var artigoB = await contextoB.Artigos.FirstAsync(a => a.Id == artigoId);

        artigoA.AtualizarConteudo("Editado primeiro", "corpo editado");
        await contextoA.SaveChangesAsync(); // xmin avança no banco

        artigoB.AtualizarConteudo("Editado segundo — deveria falhar", "corpo editado 2");
        var acao = async () => await contextoB.SaveChangesAsync();

        await acao.Should().ThrowAsync<DbUpdateConcurrencyException>(
            because: "contextoB ainda carrega o xmin antigo — sem o mapeamento de Versao, a escrita sobrescreveria em silêncio");
    }

    /// <summary>Mesma prova de <see cref="Artigo_ConflitoRealDeEdicao_LancaDbUpdateConcurrencyException"/>, agora em Comunicado.</summary>
    [Fact]
    public async Task Comunicado_ConflitoRealDeEdicao_LancaDbUpdateConcurrencyException()
    {
        int comunicadoId;
        await using (var contextoCriacao = _fixture.CriarContexto())
        {
            var comunicado = Comunicado.CriarGlobal(
                $"Comunicado xmin {Guid.NewGuid():N}", "corpo inicial", Severidade.Informativo,
                DateTimeOffset.UtcNow.AddDays(1));
            contextoCriacao.Comunicados.Add(comunicado);
            await contextoCriacao.SaveChangesAsync();
            comunicadoId = comunicado.Id;
        }

        await using var contextoA = _fixture.CriarContexto();
        await using var contextoB = _fixture.CriarContexto();

        var comunicadoA = await contextoA.Comunicados.FirstAsync(c => c.Id == comunicadoId);
        var comunicadoB = await contextoB.Comunicados.FirstAsync(c => c.Id == comunicadoId);

        comunicadoA.Publicar();
        await contextoA.SaveChangesAsync(); // xmin avança no banco

        // Publicar() nos DOIS contextos (não Despublicar em B): B parte de Publicado=false na
        // própria leitura, então Publicar() é uma mudança de valor DETECTÁVEL pelo change tracker
        // — Despublicar() em cima de um Publicado ainda false (na cópia de B) não gera UPDATE
        // nenhum, e o teste passaria vazio sem provar nada.
        comunicadoB.Publicar();
        var acao = async () => await contextoB.SaveChangesAsync();

        await acao.Should().ThrowAsync<DbUpdateConcurrencyException>(
            because: "contextoB ainda carrega o xmin antigo — sem o mapeamento de Versao, a escrita sobrescreveria em silêncio");
    }
}
