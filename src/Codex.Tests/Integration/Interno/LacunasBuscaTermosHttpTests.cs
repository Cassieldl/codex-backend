using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Fila editorial agrupada por termo por <b>HTTP real</b> contra Postgres real. As
/// asserções leem o JSON do wire; a ação sobre o grupo usa só o que a própria resposta expõe
/// (<c>lacunaReferenciaId</c> e <c>versaoConcorrencia</c>) no <c>PUT</c> que a tela de fato chama.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class LacunasBuscaTermosHttpTests : IAsyncLifetime
{
    private const string Rota = "/api/v1/interno/lacunas-busca";
    private const string RotaTermos = Rota + "/termos";
    private const string TodasAsRoles = "lacunasbusca-buscar,lacunasbusca-adicionar,lacunasbusca-atualizar";

    private static readonly DateTimeOffset Base = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public LacunasBuscaTermosHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        await ExecutarSqlAsync("DELETE FROM codex.lacunasbusca");
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        await ExecutarSqlAsync("DELETE FROM codex.lacunasbusca");
    }

    // ------------------------------------------------------------------ agrupamento e contrato

    [Fact]
    public async Task Termos_TresVariacoesDoMesmoTermoEUmOutro_ViramDoisGruposOrdenadosPorOcorrencia()
    {
        await RegistrarAsync("inscrição municipal");
        await RegistrarAsync("Alíquota ISS");
        await RegistrarAsync("  aliquota   iss ");
        await RegistrarAsync("ALÍQUOTA ISS");

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, RotaTermos);

        status.Should().Be(HttpStatusCode.OK);
        corpo.TryGetProperty("data", out _).Should().BeFalse(because: "listagem paginada é envelope cru, sem 'data'");
        var itens = corpo.GetProperty("items").EnumerateArray().ToList();
        itens.Should().HaveCount(2);

        itens[0].GetProperty("termoNormalizado").GetString().Should().Be("aliquota iss");
        itens[0].GetProperty("ocorrencias").GetInt32().Should().Be(3);
        itens[1].GetProperty("termoNormalizado").GetString().Should().Be("inscricao municipal");
        itens[1].GetProperty("ocorrencias").GetInt32().Should().Be(1);

        corpo.GetProperty("totalCount").GetInt32().Should().Be(2, because: "a página é de grupos, não de linhas");
        corpo.GetProperty("page").GetInt32().Should().Be(1);
        corpo.GetProperty("pageSize").GetInt32().Should().Be(25);
        corpo.GetProperty("totalPages").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Termos_ContratoLiteralDoItem()
    {
        await InserirAsync("Alíquota ISS", "aliquota iss", "Prefeitura A", "CH-1", criadoEm: Base, nome: "Ana");
        var maisRecente = await InserirAsync(
            "aliquota   ISS", "aliquota iss", "Prefeitura B", "CH-2", criadoEm: Base.AddDays(2), nome: "Bruno");

        var (_, corpo, _) = await EnviarAsync(HttpMethod.Get, RotaTermos);

        var item = corpo.GetProperty("items").EnumerateArray().Single();
        item.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["termoNormalizado", "termo", "ocorrencias", "primeiraEm", "ultimaEm", "parceiros", "totalParceiros",
             "chamados", "totalChamados", "ultimoRegistradoPorNome", "lacunaReferenciaId", "versaoConcorrencia"],
            because: "o frontend tipa exatamente estas propriedades");

        item.GetProperty("termo").GetString().Should().Be("aliquota   ISS", because: "a forma exibível é a da ocorrência mais recente");
        item.GetProperty("ocorrencias").GetInt32().Should().Be(2);
        item.GetProperty("primeiraEm").GetDateTimeOffset().Should().Be(Base);
        item.GetProperty("ultimaEm").GetDateTimeOffset().Should().Be(Base.AddDays(2));
        item.GetProperty("parceiros").EnumerateArray().Select(p => p.GetString()).Should().Equal("Prefeitura B", "Prefeitura A");
        item.GetProperty("totalParceiros").GetInt32().Should().Be(2);
        item.GetProperty("chamados").EnumerateArray().Select(p => p.GetString()).Should().Equal("CH-2", "CH-1");
        item.GetProperty("totalChamados").GetInt32().Should().Be(2);
        item.GetProperty("ultimoRegistradoPorNome").GetString().Should().Be("Bruno");
        item.GetProperty("lacunaReferenciaId").GetInt32().Should().Be(maisRecente, because: "a referência é a ocorrência mais recente");
        item.GetProperty("versaoConcorrencia").GetUInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Termos_UltimoRegistradoPorSemNome_VemNulo()
    {
        await InserirAsync("sem nome", "sem nome", criadoEm: Base, nome: null);

        var (_, corpo, _) = await EnviarAsync(HttpMethod.Get, RotaTermos);

        corpo.GetProperty("items")[0].GetProperty("ultimoRegistradoPorNome").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Termos_SemDadosPessoaisAlemDoQueJaEstaNaLinha_NaoExpoeIdentificadorDoUsuario()
    {
        await RegistrarAsync("termo qualquer");

        var (_, corpo, _) = await EnviarAsync(HttpMethod.Get, RotaTermos);

        corpo.GetRawText().Should().NotContain("sub-maria", because: "o sub do token nunca sai na fila");
    }

    // ------------------------------------------------------------------ contextos

    [Fact]
    public async Task Termos_ContextosDistintosSaoAgregadosComTetoDeDezEOTotalCompleto()
    {
        // 12 parceiros e 12 chamados distintos, um por dia; o mais antigo é o 1.
        for (var dia = 1; dia <= 12; dia++)
            await InserirAsync("termo recorrente", "termo recorrente", $"Parceiro {dia:00}", $"CH-{dia:00}", criadoEm: Base.AddDays(dia));

        var (_, corpo, _) = await EnviarAsync(HttpMethod.Get, RotaTermos);

        var item = corpo.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("ocorrencias").GetInt32().Should().Be(12);
        item.GetProperty("totalParceiros").GetInt32().Should().Be(12);
        item.GetProperty("totalChamados").GetInt32().Should().Be(12);

        var parceiros = item.GetProperty("parceiros").EnumerateArray().Select(p => p.GetString()).ToList();
        parceiros.Should().HaveCount(10, because: "o teto é de dez, os mais recentes");
        parceiros.First().Should().Be("Parceiro 12");
        parceiros.Last().Should().Be("Parceiro 03");

        var chamados = item.GetProperty("chamados").EnumerateArray().Select(p => p.GetString()).ToList();
        chamados.Should().HaveCount(10);
        chamados.First().Should().Be("CH-12");
        chamados.Last().Should().Be("CH-03");
    }

    [Fact]
    public async Task Termos_ContextoRepetidoSemDiferencaDeCaixa_ContaUmaVezComAGrafiaMaisRecente_ENulosSaoOmitidos()
    {
        await InserirAsync("repetido", "repetido", "prefeitura x", "ch-9", criadoEm: Base);
        await InserirAsync("repetido", "repetido", "PREFEITURA X", "CH-9", criadoEm: Base.AddDays(1));
        await InserirAsync("repetido", "repetido", "Prefeitura X", null, criadoEm: Base.AddDays(2));
        await InserirAsync("repetido", "repetido", null, null, criadoEm: Base.AddDays(3));
        await InserirAsync("repetido", "repetido", null, "CH-10", criadoEm: Base.AddDays(4));

        var (_, corpo, _) = await EnviarAsync(HttpMethod.Get, RotaTermos);

        var item = corpo.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("ocorrencias").GetInt32().Should().Be(5);
        item.GetProperty("parceiros").EnumerateArray().Select(p => p.GetString()).Should().Equal("Prefeitura X");
        item.GetProperty("totalParceiros").GetInt32().Should().Be(1);
        item.GetProperty("chamados").EnumerateArray().Select(p => p.GetString()).Should().Equal("CH-10", "CH-9");
        item.GetProperty("totalChamados").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Termos_ContextosDeUmGrupoNaoVazamParaOutro()
    {
        await InserirAsync("grupo a", "grupo a", "Parceiro A", "CH-A", criadoEm: Base);
        await InserirAsync("grupo b", "grupo b", "Parceiro B", "CH-B", criadoEm: Base.AddDays(1));
        await InserirAsync("grupo b", "grupo b", null, null, criadoEm: Base.AddDays(2));

        var (_, corpo, _) = await EnviarAsync(HttpMethod.Get, $"{RotaTermos}?sortBy=termo&sortDirection=asc");

        var itens = corpo.GetProperty("items").EnumerateArray().ToList();
        itens.Select(i => i.GetProperty("termoNormalizado").GetString()).Should().Equal("grupo a", "grupo b");
        itens[0].GetProperty("parceiros").EnumerateArray().Select(p => p.GetString()).Should().Equal("Parceiro A");
        itens[1].GetProperty("parceiros").EnumerateArray().Select(p => p.GetString()).Should().Equal("Parceiro B");
        itens[1].GetProperty("chamados").EnumerateArray().Select(p => p.GetString()).Should().Equal("CH-B");
    }

    // ------------------------------------------------------------------ estado

    [Fact]
    public async Task Termos_PadraoEAberta_EFiltraPorEstadoContandoSoOsDoEstado()
    {
        await InserirAsync("misto", "misto", criadoEm: Base);
        await InserirAsync("misto", "misto", criadoEm: Base.AddDays(1));
        await InserirAsync("misto", "misto", estado: "Resolvida", criadoEm: Base.AddDays(2));
        await InserirAsync("só resolvido", "so resolvido", estado: "Resolvida", criadoEm: Base.AddDays(3));
        await InserirAsync("só descartado", "so descartado", estado: "Descartada", criadoEm: Base.AddDays(4));

        var padrao = await TermosAsync(string.Empty);
        var abertas = await TermosAsync("?estado=Aberta");
        var resolvidas = await TermosAsync("?estado=Resolvida&sortBy=termo&sortDirection=asc");
        var descartadas = await TermosAsync("?estado=Descartada");

        padrao.Select(t => (t.Termo, t.Ocorrencias)).Should().Equal([("misto", 2)], because: "sem estado, a fila é a das abertas");
        abertas.Select(t => (t.Termo, t.Ocorrencias)).Should().Equal([("misto", 2)]);
        resolvidas.Select(t => (t.Termo, t.Ocorrencias)).Should().Equal([("misto", 1), ("só resolvido", 1)]);
        descartadas.Select(t => (t.Termo, t.Ocorrencias)).Should().Equal([("só descartado", 1)]);
    }

    [Fact]
    public async Task Termos_LinhaRemovida_NaoEntraNoGrupoNemNaContagem()
    {
        await InserirAsync("ativo", "ativo", criadoEm: Base);
        await InserirAsync("ativo", "ativo", criadoEm: Base.AddDays(1), removida: true);
        await InserirAsync("só removido", "so removido", criadoEm: Base.AddDays(2), removida: true);

        var (_, corpo, _) = await EnviarAsync(HttpMethod.Get, RotaTermos);

        corpo.GetProperty("totalCount").GetInt32().Should().Be(1);
        var item = corpo.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("ocorrencias").GetInt32().Should().Be(1);
        item.GetProperty("ultimaEm").GetDateTimeOffset().Should().Be(Base);
    }

    [Fact]
    public async Task Termos_EstadoInvalido_Devolve400SemVazarSchema()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, $"{RotaTermos}?estado=Inexistente");

        status.Should().Be(HttpStatusCode.BadRequest);
        SemVazamentoDeSchema(corpo);
    }

    // ------------------------------------------------------------------ ordenação e paginação

    [Fact]
    public async Task Termos_Ordenacoes_ComWhitelistEPadrao()
    {
        // bb: 3 ocorrências, última no dia 9 · aa: 2, última no dia 5 · cc: 1, última no dia 12.
        await InserirAsync("bb", "bb", criadoEm: Base);
        await InserirAsync("bb", "bb", criadoEm: Base.AddDays(1));
        await InserirAsync("bb", "bb", criadoEm: Base.AddDays(9));
        await InserirAsync("aa", "aa", criadoEm: Base.AddDays(2));
        await InserirAsync("aa", "aa", criadoEm: Base.AddDays(5));
        await InserirAsync("cc", "cc", criadoEm: Base.AddDays(12));

        (await NormalizadosAsync(string.Empty)).Should().Equal(new[] { "bb", "aa", "cc" }, "padrão: mais recorrente primeiro");
        (await NormalizadosAsync("?sortBy=ocorrencias&sortDirection=asc")).Should().Equal("cc", "aa", "bb");
        (await NormalizadosAsync("?sortBy=recente")).Should().Equal("cc", "bb", "aa");
        (await NormalizadosAsync("?sortBy=recente&sortDirection=asc")).Should().Equal("aa", "bb", "cc");
        (await NormalizadosAsync("?sortBy=termo&sortDirection=asc")).Should().Equal("aa", "bb", "cc");
        (await NormalizadosAsync("?sortBy=termo")).Should().Equal("cc", "bb", "aa");
        (await NormalizadosAsync("?sortBy=passwordhash")).Should().Equal(new[] { "bb", "aa", "cc" }, "coluna fora da whitelist cai no padrão");
    }

    [Fact]
    public async Task Termos_EmpateDeOcorrenciasEmpateDeData_DesempataPeloTermoNormalizado()
    {
        // Três grupos com o mesmo número de ocorrências e a mesma última data.
        foreach (var termo in new[] { "zeta", "alfa", "meio" })
        {
            await InserirAsync(termo, termo, criadoEm: Base);
            await InserirAsync(termo, termo, criadoEm: Base);
        }

        (await NormalizadosAsync(string.Empty)).Should().Equal("alfa", "meio", "zeta");
        (await NormalizadosAsync("?sortBy=recente")).Should().Equal("alfa", "meio", "zeta");
    }

    [Theory]
    [InlineData("")]
    [InlineData("&sortBy=recente")]
    [InlineData("&sortBy=ocorrencias&sortDirection=asc")]
    [InlineData("&sortBy=recente&sortDirection=asc")]
    public async Task Termos_PaginaDeGrupos_NaoRepeteNemPerdeGrupoMesmoComHeapMutadoEntreAsPaginas(string ordenacao)
    {
        // 12 grupos de 3 linhas, TODOS empatados em ocorrências e em data: só o termo normalizado
        // desempata. 36 linhas para 12 grupos separam "página de grupos" de "página de linhas".
        var termos = Enumerable.Range(1, 12).Select(n => $"termo {n:00}").ToList();
        foreach (var termo in termos)
            for (var i = 0; i < 3; i++)
                await InserirAsync(termo, termo, criadoEm: Base);

        var coletados = new List<string>();
        for (var pagina = 1; pagina <= 4; pagina++)
        {
            var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, $"{RotaTermos}?pagina={pagina}&tamanhoPagina=3{ordenacao}");
            status.Should().Be(HttpStatusCode.OK);

            corpo.GetProperty("totalCount").GetInt32().Should().Be(12, because: "conta grupos, não as 36 linhas");
            corpo.GetProperty("totalPages").GetInt32().Should().Be(4);
            var daPagina = corpo.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("termoNormalizado").GetString()!).ToList();
            daPagina.Should().HaveCount(3);
            coletados.AddRange(daPagina);

            // Muta o heap entre as páginas: reescrever as linhas já vistas move as tuplas de lugar.
            foreach (var visto in coletados)
                await ExecutarSqlAsync($"UPDATE codex.lacunasbusca SET nota = nota WHERE termonormalizado = '{visto}'");
        }

        coletados.Should().OnlyHaveUniqueItems(because: "grupo repetido entre páginas é o sintoma de ordem parcial");
        coletados.Should().Equal(termos, because: "o empate total se resolve pelo termo normalizado, em ordem crescente");
    }

    [Fact]
    public async Task Termos_TamanhoDePaginaAcimaDoTetoEPaginaGigante_NaoViram500()
    {
        await RegistrarAsync("qualquer");

        var (statusTamanho, corpoTamanho, _) = await EnviarAsync(HttpMethod.Get, $"{RotaTermos}?tamanhoPagina=99999999");
        var (statusPagina, corpoPagina, _) = await EnviarAsync(HttpMethod.Get, $"{RotaTermos}?pagina=2147483647&tamanhoPagina=100");

        statusTamanho.Should().Be(HttpStatusCode.OK);
        corpoTamanho.GetProperty("pageSize").GetInt32().Should().Be(200);
        statusPagina.Should().Be(HttpStatusCode.OK, because: "offset negativo por estouro de int era o 500 das listagens");
        corpoPagina.GetProperty("items").GetArrayLength().Should().Be(0);
        corpoPagina.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Termos_FilaVazia_Devolve200ComListaVazia()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, RotaTermos);

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("items").GetArrayLength().Should().Be(0);
        corpo.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    // ------------------------------------------------------------------ tratar o grupo pelo PUT existente

    [Fact]
    public async Task ResolverOGrupo_ComIdETokenExpostosEIncluirMesmoTermo_TiraOGrupoDaFilaAberta()
    {
        await RegistrarAsync("Alíquota ISS");
        await RegistrarAsync("aliquota iss");
        await RegistrarAsync("ALÍQUOTA  ISS");
        await RegistrarAsync("inscrição municipal");

        var antes = await TermosAsync(string.Empty);
        var grupo = antes.Single(t => t.TermoNormalizado == "aliquota iss");
        grupo.Ocorrencias.Should().Be(3);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{grupo.LacunaReferenciaId}/estado",
            new { estado = "Resolvida", nota = "Artigo publicado", incluirMesmoTermo = true },
            ifMatch: grupo.VersaoConcorrencia, sub: "sub-joao", nome: "João Lima");

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("mesmoTermoAlteradas").GetInt32().Should().Be(2);

        var abertas = await TermosAsync(string.Empty);
        abertas.Select(t => t.TermoNormalizado).Should().Equal(new[] { "inscricao municipal" }, "o grupo inteiro saiu da fila aberta");

        var resolvidas = await TermosAsync("?estado=Resolvida");
        var resolvido = resolvidas.Single();
        resolvido.TermoNormalizado.Should().Be("aliquota iss");
        resolvido.Ocorrencias.Should().Be(3, because: "as três ocorrências foram resolvidas juntas");
    }

    [Fact]
    public async Task ResolverOGrupo_OcorrenciasComOMesmoInstante_AReferenciaEhSempreAdeMaiorId()
    {
        // Lote importado: mesma data nas duas linhas. Sem desempate, a referência dependeria da ordem física.
        await InserirAsync("lote", "lote", criadoEm: Base, nome: "Primeira");
        var maiorId = await InserirAsync("LOTE", "lote", criadoEm: Base, nome: "Segunda");

        var grupo = (await TermosAsync(string.Empty)).Single();

        grupo.LacunaReferenciaId.Should().Be(maiorId);
        grupo.Termo.Should().Be("LOTE");
    }

    [Fact]
    public async Task ResolverOGrupo_ReferenciaEhAMaisRecenteEntreAsAbertas_EMesmoTokenValido()
    {
        var antiga = await RegistrarAsync("fila");
        var recente = await RegistrarAsync("FILA");

        var grupo = (await TermosAsync(string.Empty)).Single();

        grupo.LacunaReferenciaId.Should().Be(recente.GetProperty("id").GetInt32());
        grupo.LacunaReferenciaId.Should().NotBe(antiga.GetProperty("id").GetInt32());
        grupo.VersaoConcorrencia.Should().Be(recente.GetProperty("versaoConcorrencia").GetUInt32(),
            because: "o token exposto é o da própria linha de referência");
    }

    [Fact]
    public async Task ResolverOGrupo_OutraOcorrenciaChegouDepoisDeCarregarAFila_TokenAntigoAindaTrataTodasAsAbertas()
    {
        await RegistrarAsync("novo termo");
        var grupo = (await TermosAsync(string.Empty)).Single();
        await RegistrarAsync("Novo Termo"); // chega depois de a tela carregar a fila

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{grupo.LacunaReferenciaId}/estado",
            new { estado = "Descartada", incluirMesmoTermo = true }, ifMatch: grupo.VersaoConcorrencia);

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("mesmoTermoAlteradas").GetInt32().Should().Be(1,
            because: "a ocorrência nova também é do mesmo termo normalizado");
        (await TermosAsync(string.Empty)).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ 401 / 403

    [Fact]
    public async Task SemToken_Devolve401()
    {
        using var resposta = await _cliente.GetAsync(RotaTermos);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("lacunasbusca-adicionar,lacunasbusca-atualizar")]
    [InlineData("versoesprovedor-buscar")]
    public async Task ComRoleDeOutraAcao_Devolve403(string roles)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaTermos);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, roles);

        using var resposta = await _cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record TermoDaFila(
        string TermoNormalizado, string Termo, int Ocorrencias, int LacunaReferenciaId, uint VersaoConcorrencia);

    private async Task<List<TermoDaFila>> TermosAsync(string consulta)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, RotaTermos + consulta);
        status.Should().Be(HttpStatusCode.OK);

        return corpo.GetProperty("items").EnumerateArray().Select(i => new TermoDaFila(
            i.GetProperty("termoNormalizado").GetString()!,
            i.GetProperty("termo").GetString()!,
            i.GetProperty("ocorrencias").GetInt32(),
            i.GetProperty("lacunaReferenciaId").GetInt32(),
            i.GetProperty("versaoConcorrencia").GetUInt32())).ToList();
    }

    private async Task<List<string>> NormalizadosAsync(string consulta) =>
        (await TermosAsync(consulta)).Select(t => t.TermoNormalizado).ToList();

    private async Task<JsonElement> RegistrarAsync(string termo)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, Rota, new { termo });
        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data");
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo,
        string rota,
        object? corpoRequisicao = null,
        uint? ifMatch = null,
        string sub = "sub-maria",
        string nome = "Maria Souza")
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, TodasAsRoles);
        requisicao.Headers.Add(TestAuthHandler.HeaderSub, sub);
        requisicao.Headers.Add(TestAuthHandler.HeaderNome, nome);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }

    /// <summary>Asserção de ausência do padrão em todo erro 4xx: nome de constraint, coluna ou stack nunca saem na resposta.</summary>
    private static void SemVazamentoDeSchema(JsonElement corpo)
    {
        var texto = corpo.ValueKind == JsonValueKind.Undefined ? string.Empty : corpo.GetRawText();
        texto.Should().NotContain("_codex_").And.NotContain("ck_codex_").And.NotContain("lacunasbusca")
            .And.NotContain("at Codex.").And.NotContain("Npgsql");
    }

    /// <summary>Insere uma linha com data e estado controlados — o POST não deixa escolher o instante do registro.</summary>
    private async Task<int> InserirAsync(
        string termo,
        string termoNormalizado,
        string? parceiro = null,
        string? numeroChamado = null,
        string estado = "Aberta",
        DateTimeOffset? criadoEm = null,
        string? nome = "Maria Souza",
        bool removida = false)
    {
        var instante = criadoEm ?? Base;
        var resolvida = estado != "Aberta";

        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            """
            INSERT INTO codex.lacunasbusca
                (termo, termonormalizado, parceiro, numerochamado, estado, registradoporid, registradopornome,
                 resolvidaem, resolvidoporid, criadoem, desativadoem)
            VALUES
                (@termo, @normalizado, @parceiro, @chamado, @estado, 'sub-lote', @nome,
                 @resolvidaEm, @resolvidoPor, @criadoEm, @desativadoEm)
            RETURNING lacunabuscaid
            """;
        comando.Parameters.AddWithValue("termo", termo);
        comando.Parameters.AddWithValue("normalizado", termoNormalizado);
        comando.Parameters.AddWithValue("parceiro", (object?)parceiro ?? DBNull.Value);
        comando.Parameters.AddWithValue("chamado", (object?)numeroChamado ?? DBNull.Value);
        comando.Parameters.AddWithValue("estado", estado);
        comando.Parameters.AddWithValue("nome", (object?)nome ?? DBNull.Value);
        comando.Parameters.AddWithValue("resolvidaEm", resolvida ? (object)instante : DBNull.Value);
        comando.Parameters.AddWithValue("resolvidoPor", resolvida ? (object)"sub-lote" : DBNull.Value);
        comando.Parameters.AddWithValue("criadoEm", instante);
        comando.Parameters.AddWithValue("desativadoEm", removida ? (object)instante : DBNull.Value);

        return (int)(await comando.ExecuteScalarAsync())!;
    }

    private async Task ExecutarSqlAsync(string sql)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }
}
