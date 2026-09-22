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
            "exemplos", // 003/M-a — a tabela nasce COM o trigger, então entra na lista esperada.
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

    /// <summary>
    /// M-a (003) — <c>codex.exemplos</c> sai de <c>ExcludeFromMigrations()</c>. Mesma prova do
    /// bloco editorial da 002: se a exclusão ainda estivesse ativa, a migration nunca teria
    /// emitido o <c>CREATE TABLE</c> e esta consulta devolveria zero mesmo com a
    /// <c>Configuration</c> já sem a chamada (o build passa nos dois casos).
    /// </summary>
    [Fact]
    public async Task Exemplos_SaiDeExcludeFromMigrations_ExisteNoBanco()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'codex' AND table_name = 'exemplos'";

        var quantidade = (long)(await comando.ExecuteScalarAsync())!;

        quantidade.Should().Be(1, because: "a migration da 003 escreve o CreateTable de codex.exemplos à mão (modelo-dados.md §1.6)");
    }

    /// <summary>
    /// Os dois <c>UNIQUE</c> de <c>exemplos</c> chegam ao banco como ÍNDICES únicos (o
    /// <c>HasIndex().IsUnique()</c> da Configuration), não como entradas de <c>pg_constraint</c> —
    /// mesma forma já usada por <c>uq_codex_versoesprovedor_provedorid_slug</c> e companhia. A
    /// garantia de unicidade é idêntica; a consulta é que muda, e procurar no lugar errado dá
    /// falso vermelho (ou, invertido, falso verde).
    /// </summary>
    [Theory]
    [InlineData("uq_codex_exemplos_versaoprovedorid_nome", "versaoprovedorid, nome")]
    [InlineData("uq_codex_exemplos_versaoprovedorid_slug", "versaoprovedorid, slug")]
    public async Task Exemplos_TemOsDoisIndicesUnicos(string nomeIndice, string colunasEsperadas)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'codex' AND tablename = 'exemplos' AND indexname = @nome";
        comando.Parameters.AddWithValue("nome", nomeIndice);

        var indexdef = (string?)await comando.ExecuteScalarAsync();

        indexdef.Should().NotBeNull(because: $"{nomeIndice} está no DDL de modelo-dados.md §1.1");
        indexdef!.Should().Contain("UNIQUE").And.Contain($"({colunasEsperadas})");
    }

    [Theory]
    [InlineData("pk_codex_exemplos")]
    [InlineData("fk_codex_exemplos_versaoprovedorid")]
    [InlineData("ck_codex_exemplos_xml_tamanho")]
    public async Task Exemplos_TemAsConstraintsDoModeloDeDados(string nomeConstraint)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            """
            SELECT COUNT(*)
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'codex' AND t.relname = 'exemplos' AND c.conname = @nome
            """;
        comando.Parameters.AddWithValue("nome", nomeConstraint);

        var quantidade = (long)(await comando.ExecuteScalarAsync())!;

        quantidade.Should().Be(1, because: $"{nomeConstraint} está no DDL de modelo-dados.md §1.1");
    }

    /// <summary>
    /// <c>RESTRICT</c> é a única política de FK de toda a base (18 FKs da migration inicial,
    /// nenhuma em CASCADE) — apagar uma versão com exemplos não pode levar os cenários junto em
    /// silêncio.
    /// </summary>
    [Fact]
    public async Task FkExemplosVersaoProvedor_EhOnDeleteRestrict()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'fk_codex_exemplos_versaoprovedorid'";

        var definicao = (string?)await comando.ExecuteScalarAsync();

        definicao.Should().NotBeNull();
        definicao!.Should().Contain("REFERENCES codex.versoesprovedor").And.Contain("ON DELETE RESTRICT");
    }

    /// <summary>
    /// O CHECK do XML é a guarda de catástrofe em BYTES (256 KiB), deliberadamente diferente do
    /// teto de negócio em CARACTERES (64 KiB, FluentValidation) — modelo-dados.md §1.5. Se alguém
    /// "corrigir a inconsistência" igualando os dois, este teste cai.
    /// </summary>
    [Fact]
    public async Task CkExemplosXmlTamanho_Mede256KiBEmBytes_NaoOs64KiBDeNegocio()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_codex_exemplos_xml_tamanho'";

        var definicao = (string?)await comando.ExecuteScalarAsync();

        definicao.Should().NotBeNull();
        definicao!.Should().Contain("octet_length").And.Contain("262144");
    }

    /// <summary>F4/M-b — o CHECK precisa dos 5 valores; sem 'artigo', o redirect de artigo da 002 morre com 23514.</summary>
    [Fact]
    public async Task CkRedirecionamentosTipoItem_TemExatamenteOsCincoValores()
    {
        var valoresNoCheck = await ValoresDoCheckAsync("ck_codex_redirecionamentos_tipoitem");

        valoresNoCheck.Should().BeEquivalentTo(
            ["provedor", "versaoprovedor", "erro", "campolayout", "artigo"],
            because: "a 003 recria o CHECK incluindo 'artigo' (analise-backend.md §1)");
    }

    /// <summary>
    /// PQ-03 — o trigger não é gerado pelo EF e sua ausência é SILENCIOSA: sem ele, a coluna
    /// simplesmente para de ser atualizada. Prova por comportamento: <c>UPDATE</c> em SQL puro
    /// (nunca pelo EF, que poderia carimbar a coluna sozinho) e comparação do valor antes/depois.
    /// Leitura por <c>ExecuteReaderAsync</c> + <c>GetFieldValue&lt;DateTimeOffset&gt;</c>: o
    /// caminho genérico de <c>ExecuteScalarAsync</c> materializa <c>timestamptz</c> como
    /// <c>DateTime</c>, e o <c>as DateTimeOffset?</c> devolveria <c>null</c> sempre.
    /// </summary>
    [Fact]
    public async Task Exemplo_UpdateEmSqlPuro_TemAtualizadoemCarimbadoPeloTrigger()
    {
        var exemploId = await SemearExemploAsync();

        var antes = await LerAtualizadoemAsync(exemploId);

        await using (var conexao = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = "UPDATE codex.exemplos SET descricao = 'tocado pelo teste do trigger' WHERE exemploid = @id";
            comando.Parameters.AddWithValue("id", exemploId);
            (await comando.ExecuteNonQueryAsync()).Should().Be(1);
        }

        var depois = await LerAtualizadoemAsync(exemploId);

        depois.Should().BeAfter(antes,
            because: "trg_codex_exemplos_atualizadoem precisa carimbar atualizadoem em todo UPDATE (rules/database.md)");
    }

    /// <summary>
    /// Concorrência otimista de <c>Exemplo</c> (decisão C2) — mesma prova por comportamento usada
    /// em Artigo/Comunicado: sem o mapeamento de <c>Versao</c> como <c>xmin</c>, dois PUTs
    /// concorrentes se sobrescreveriam em silêncio e o <c>If-Match</c> de I41/I42 seria decorativo.
    /// </summary>
    [Fact]
    public async Task Exemplo_ConflitoRealDeEdicao_LancaDbUpdateConcurrencyException()
    {
        var exemploId = await SemearExemploAsync();

        await using var contextoA = _fixture.CriarContexto();
        await using var contextoB = _fixture.CriarContexto();

        var exemploA = await contextoA.Exemplos.FirstAsync(e => e.Id == exemploId);
        var exemploB = await contextoB.Exemplos.FirstAsync(e => e.Id == exemploId);

        exemploA.AtualizarConteudo("Editado primeiro", "<xml>1</xml>", null);
        await contextoA.SaveChangesAsync(); // xmin avança no banco

        exemploB.AtualizarConteudo("Editado segundo — deveria falhar", "<xml>2</xml>", null);
        var acao = async () => await contextoB.SaveChangesAsync();

        await acao.Should().ThrowAsync<DbUpdateConcurrencyException>(
            because: "contextoB ainda carrega o xmin antigo");
    }

    private async Task<int> SemearExemploAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdemAtual = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout exemplo {sufixo}", maiorOrdemAtual + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor Exemplo {sufixo}", $"EXM-{sufixo}", CategoriaProvedor.Nacional);
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versão exemplo {sufixo}");
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        var exemplo = Exemplo.Criar(versao.Id, $"Cenário {sufixo}", "<xml>0</xml>", "descrição inicial");
        contexto.Exemplos.Add(exemplo);
        await contexto.SaveChangesAsync();

        return exemplo.Id;
    }

    private async Task<DateTimeOffset> LerAtualizadoemAsync(int exemploId)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT atualizadoem FROM codex.exemplos WHERE exemploid = @id";
        comando.Parameters.AddWithValue("id", exemploId);

        await using var leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue();

        return leitor.GetFieldValue<DateTimeOffset>(0);
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
