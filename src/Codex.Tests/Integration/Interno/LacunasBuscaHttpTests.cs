using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Fila editorial por <b>HTTP real</b> contra Postgres real. As asserções leem o JSON do
/// wire — enum como nome, datas, nomes de propriedade — e as de identidade conferem a LINHA gravada,
/// porque "registrado por" que só aparece na resposta não prova de onde o valor veio.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class LacunasBuscaHttpTests : IAsyncLifetime
{
    private const string Rota = "/api/v1/interno/lacunas-busca";
    private const string TodasAsRoles = "lacunasbusca-buscar,lacunasbusca-adicionar,lacunasbusca-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public LacunasBuscaHttpTests(PostgresContainerFixture fixture)
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

    // ------------------------------------------------------------------ registrar

    [Fact]
    public async Task Registrar_Devolve201ComOContratoLiteral()
    {
        var (status, corpo, resposta) = await EnviarAsync(HttpMethod.Post, Rota, new
        {
            termo = "Alíquota do ISS",
            parceiro = "Prefeitura de Exemplo",
            numeroChamado = "CH-2026-001",
        });

        status.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Location.Should().NotBeNull();
        resposta.Headers.ETag.Should().NotBeNull(because: "sem o ETag o cliente não tem de onde tirar o If-Match");

        corpo.GetProperty("message").GetString().Should().Be("Lacuna registrada.");

        var dados = corpo.GetProperty("data");
        dados.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["id", "termo", "parceiro", "numeroChamado", "estado", "nota", "registradoPorNome", "registradoEm",
             "resolvidaEm", "resolvidoPorNome", "ocorrenciasAbertas", "versaoConcorrencia"],
            because: "o frontend tipa exatamente estas propriedades");
        dados.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
        dados.GetProperty("termo").GetString().Should().Be("Alíquota do ISS");
        dados.GetProperty("parceiro").GetString().Should().Be("Prefeitura de Exemplo");
        dados.GetProperty("numeroChamado").GetString().Should().Be("CH-2026-001");
        dados.GetProperty("estado").GetString().Should().Be("Aberta", because: "enum sai como nome, nunca ordinal");
        dados.GetProperty("nota").ValueKind.Should().Be(JsonValueKind.Null);
        dados.GetProperty("registradoPorNome").GetString().Should().Be("Maria Souza");
        dados.GetProperty("registradoEm").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2));
        dados.GetProperty("resolvidaEm").ValueKind.Should().Be(JsonValueKind.Null);
        dados.GetProperty("resolvidoPorNome").ValueKind.Should().Be(JsonValueKind.Null);
        dados.GetProperty("ocorrenciasAbertas").GetInt32().Should().Be(1);
        dados.GetProperty("versaoConcorrencia").GetUInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Registrar_QuemRegistrouVemDoToken_ECamposExtrasNoCorpoSaoIgnorados()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, Rota, new
        {
            termo = "campo extra",
            registradoPorId = "sub-forjado",
            registradoPorNome = "Usuário Forjado",
            registradoEm = "2001-01-01T00:00:00Z",
            estado = "Resolvida",
            organizacaoId = 99,
        });

        status.Should().Be(HttpStatusCode.Created);
        var id = corpo.GetProperty("data").GetProperty("id").GetInt32();

        var linha = await LerLinhaAsync(id);
        linha.RegistradoPorId.Should().Be("sub-maria", because: "o sub vem do token (X-Test-Sub), nunca do corpo");
        linha.RegistradoPorNome.Should().Be("Maria Souza");
        linha.Estado.Should().Be("Aberta", because: "a lacuna sempre nasce aberta");
        linha.CriadoEm.Year.Should().Be(DateTimeOffset.UtcNow.Year, because: "a data é a do servidor");
        corpo.GetProperty("data").GetProperty("registradoPorNome").GetString().Should().Be("Maria Souza");
    }

    [Fact]
    public async Task Registrar_ParceiroEChamadoOpcionais_FicamNulos()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, Rota, new { termo = "só o termo", parceiro = "  ", numeroChamado = "" });

        status.Should().Be(HttpStatusCode.Created);
        var dados = corpo.GetProperty("data");
        dados.GetProperty("parceiro").ValueKind.Should().Be(JsonValueKind.Null);
        dados.GetProperty("numeroChamado").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Registrar_TokenSemNome_GravaNomeNulo()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, Rota, new { termo = "sem nome" }, nome: "");

        status.Should().Be(HttpStatusCode.Created);
        corpo.GetProperty("data").GetProperty("registradoPorNome").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    public async Task Registrar_TermoVazioOuCurto_Devolve422ComOCampo(string termo)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, Rota, new { termo });

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        corpo.GetProperty("error").GetProperty("details").EnumerateArray()
            .Select(d => d.GetProperty("field").GetString()).Should().Contain(campo => campo!.Equals("Termo", StringComparison.OrdinalIgnoreCase));
        (await ContarLinhasAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Registrar_SemATermoNoCorpo_NaoViraErroInterno()
    {
        var (status, _, _) = await EnviarAsync(HttpMethod.Post, Rota, new { parceiro = "x" });

        ((int)status).Should().BeInRange(400, 422, because: "corpo sem o campo obrigatório é erro do cliente, nunca 500");
        (await ContarLinhasAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("termo", 300, HttpStatusCode.Created)]
    [InlineData("termo", 301, HttpStatusCode.UnprocessableEntity)]
    [InlineData("parceiro", 160, HttpStatusCode.Created)]
    [InlineData("parceiro", 161, HttpStatusCode.UnprocessableEntity)]
    [InlineData("numeroChamado", 60, HttpStatusCode.Created)]
    [InlineData("numeroChamado", 61, HttpStatusCode.UnprocessableEntity)]
    public async Task Registrar_FronteirasDeTamanho_NuncaViram500(string campo, int tamanho, HttpStatusCode esperado)
    {
        var corpoRequisicao = new Dictionary<string, string> { ["termo"] = "termo base", [campo] = new string('x', tamanho) };

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, Rota, corpoRequisicao);

        status.Should().Be(esperado);
        if (esperado == HttpStatusCode.UnprocessableEntity)
            SemVazamentoDeSchema(corpo);
    }

    [Fact]
    public async Task Registrar_TermoQueEstouraOTetoSoEmUnidadesUtf16ComEmoji_Devolve422()
    {
        // 299 caracteres + emoji (2 unidades UTF-16) = 301 unidades, mas 300 caracteres no banco.
        var termo = new string('a', 299) + "😀";

        var (status, _, _) = await EnviarAsync(HttpMethod.Post, Rota, new { termo });

        // A contagem do validator é em unidades UTF-16 (301): recusa antes de chegar ao banco.
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Registrar_NomeLongoNoToken_ETruncadoNaGravacao()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, Rota, new { termo = "nome longo" }, nome: new string('n', 400));

        status.Should().Be(HttpStatusCode.Created);
        corpo.GetProperty("data").GetProperty("registradoPorNome").GetString().Should().HaveLength(160);
    }

    // ------------------------------------------------------------------ recorrência

    [Fact]
    public async Task Registrar_MesmoTermoComAcentoCaixaEEspacos_ContaComoRecorrencia()
    {
        var primeira = await RegistrarAsync("Alíquota ISS");
        var segunda = await RegistrarAsync("  aliquota   iss ");
        var terceira = await RegistrarAsync("ALÍQUOTA ISS");
        var outro = await RegistrarAsync("inscrição municipal");

        primeira.GetProperty("ocorrenciasAbertas").GetInt32().Should().Be(1);
        segunda.GetProperty("ocorrenciasAbertas").GetInt32().Should().Be(2);
        terceira.GetProperty("ocorrenciasAbertas").GetInt32().Should().Be(3);
        outro.GetProperty("ocorrenciasAbertas").GetInt32().Should().Be(1, because: "outro termo não entra na conta");
    }

    // ------------------------------------------------------------------ listar

    [Fact]
    public async Task Listar_PadraoMaisRecentesPrimeiro_ComEnvelopeCruDePaginacao()
    {
        var a = await RegistrarAsync("termo a");
        var b = await RegistrarAsync("termo b");
        var c = await RegistrarAsync("termo c");

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, Rota);

        status.Should().Be(HttpStatusCode.OK);
        corpo.TryGetProperty("data", out _).Should().BeFalse(because: "listagem paginada é envelope cru, sem 'data'");
        corpo.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).Should().Equal(
            c.GetProperty("id").GetInt32(), b.GetProperty("id").GetInt32(), a.GetProperty("id").GetInt32());
        corpo.GetProperty("totalCount").GetInt32().Should().Be(3);
        corpo.GetProperty("page").GetInt32().Should().Be(1);
        corpo.GetProperty("pageSize").GetInt32().Should().Be(25);
        corpo.GetProperty("totalPages").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Listar_FiltraPorEstado()
    {
        var aberta = await RegistrarAsync("aberta");
        var resolvida = await RegistrarAsync("resolvida");
        var descartada = await RegistrarAsync("descartada");
        await AlterarEstadoAsync(resolvida, "Resolvida");
        await AlterarEstadoAsync(descartada, "Descartada");

        (await IdsDaListaAsync("?estado=Aberta")).Should().Equal(aberta.GetProperty("id").GetInt32());
        (await IdsDaListaAsync("?estado=Resolvida")).Should().Equal(resolvida.GetProperty("id").GetInt32());
        (await IdsDaListaAsync("?estado=Descartada")).Should().Equal(descartada.GetProperty("id").GetInt32());
        (await IdsDaListaAsync(string.Empty)).Should().HaveCount(3, because: "sem filtro vêm todos os estados");
    }

    [Fact]
    public async Task Listar_EstadoInvalido_Devolve400()
    {
        var (status, _, _) = await EnviarAsync(HttpMethod.Get, $"{Rota}?estado=Inexistente");

        status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Listar_OrdenaPorOcorrencias_AMaisRecorrenteNoTopo()
    {
        await RegistrarAsync("raro");
        await RegistrarAsync("recorrente");
        await RegistrarAsync("médio");
        await RegistrarAsync("Recorrente");
        await RegistrarAsync("medio");
        var ultima = await RegistrarAsync("RECORRENTE");

        var (_, corpo, _) = await EnviarAsync(HttpMethod.Get, $"{Rota}?sortBy=ocorrencias");

        var itens = corpo.GetProperty("items").EnumerateArray().ToList();
        itens.Select(i => i.GetProperty("ocorrenciasAbertas").GetInt32()).Should().Equal(3, 3, 3, 2, 2, 1);
        itens[0].GetProperty("id").GetInt32().Should().Be(ultima.GetProperty("id").GetInt32(),
            because: "no empate de recorrência, a mais recente vem primeiro");
    }

    [Fact]
    public async Task Listar_SortByDesconhecido_CaiNoPadraoENaoViraErro()
    {
        await RegistrarAsync("um termo");

        var (status, _, _) = await EnviarAsync(HttpMethod.Get, $"{Rota}?sortBy=passwordhash");

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Listar_PaginaComEmpateNoCriterio_NaoRepeteNemPerdeItemMesmoComHeapMutado()
    {
        // Cinco linhas com o MESMO criadoem: sem desempate por chave única, o OFFSET/LIMIT não
        // tem ordem total e a ordem física do heap decide — e ela muda a cada UPDATE.
        await ExecutarSqlAsync(
            """
            INSERT INTO codex.lacunasbusca (termo, termonormalizado, estado, registradoporid, criadoem)
            SELECT 'termo ' || n, 'termo ' || n, 'Aberta', 'sub-lote', timestamptz '2026-01-01 12:00:00+00'
            FROM generate_series(1, 5) AS n
            """);

        var pagina1 = await IdsDaListaAsync("?pagina=1&tamanhoPagina=2");

        // Muta o heap entre as páginas: mexer numa linha da página 1 move a tupla de lugar.
        await ExecutarSqlAsync(
            $"UPDATE codex.lacunasbusca SET nota = 'mexida' WHERE lacunabuscaid = {pagina1[0]}");

        var pagina2 = await IdsDaListaAsync("?pagina=2&tamanhoPagina=2");
        var pagina3 = await IdsDaListaAsync("?pagina=3&tamanhoPagina=2");

        var todos = pagina1.Concat(pagina2).Concat(pagina3).ToList();
        todos.Should().OnlyHaveUniqueItems(because: "item repetido entre páginas é o sintoma de ordem parcial");
        todos.Should().HaveCount(5);
        todos.Should().BeInDescendingOrder(because: "empate de data desempata pelo id, decrescente como o padrão");
    }

    [Fact]
    public async Task Listar_TamanhoDePaginaAcimaDoTeto_EPaginaGigante_NaoViram500()
    {
        await RegistrarAsync("qualquer");

        var (statusTamanho, corpoTamanho, _) = await EnviarAsync(HttpMethod.Get, $"{Rota}?tamanhoPagina=99999999");
        var (statusPagina, corpoPagina, _) = await EnviarAsync(HttpMethod.Get, $"{Rota}?pagina=2147483647&tamanhoPagina=100");

        statusTamanho.Should().Be(HttpStatusCode.OK);
        corpoTamanho.GetProperty("pageSize").GetInt32().Should().Be(200);
        statusPagina.Should().Be(HttpStatusCode.OK, because: "offset negativo por estouro de int era o 500 das listagens");
        corpoPagina.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Obter_Existente_Devolve200ComETag_EInexistenteDevolve404()
    {
        var criada = await RegistrarAsync("detalhe");
        var id = criada.GetProperty("id").GetInt32();

        var (status, corpo, resposta) = await EnviarAsync(HttpMethod.Get, $"{Rota}/{id}");
        var (statusInexistente, corpoInexistente, _) = await EnviarAsync(HttpMethod.Get, $"{Rota}/999999");

        status.Should().Be(HttpStatusCode.OK);
        resposta.Headers.ETag.Should().NotBeNull();
        corpo.GetProperty("data").GetProperty("termo").GetString().Should().Be("detalhe");
        statusInexistente.Should().Be(HttpStatusCode.NotFound);
        SemVazamentoDeSchema(corpoInexistente);
    }

    // ------------------------------------------------------------------ alterar estado

    [Fact]
    public async Task AlterarEstado_Resolver_GravaQuemQuandoENota_EQuemResolveVemDoToken()
    {
        var criada = await RegistrarAsync("resolver isto");
        var id = criada.GetProperty("id").GetInt32();

        var (status, corpo, resposta) = await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{id}/estado",
            new { estado = "Resolvida", nota = "Artigo 'Alíquota do ISS' publicado.", resolvidoPorNome = "Forjado" },
            ifMatch: criada.GetProperty("versaoConcorrencia").GetUInt32(),
            sub: "sub-joao", nome: "João Lima");

        status.Should().Be(HttpStatusCode.OK);
        resposta.Headers.ETag.Should().NotBeNull();

        var dados = corpo.GetProperty("data");
        dados.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["lacuna", "mesmoTermoAlteradas"]);
        dados.GetProperty("mesmoTermoAlteradas").GetInt32().Should().Be(0);

        var lacuna = dados.GetProperty("lacuna");
        lacuna.GetProperty("estado").GetString().Should().Be("Resolvida");
        lacuna.GetProperty("nota").GetString().Should().Be("Artigo 'Alíquota do ISS' publicado.");
        lacuna.GetProperty("resolvidoPorNome").GetString().Should().Be("João Lima");
        lacuna.GetProperty("resolvidaEm").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2));
        lacuna.GetProperty("registradoPorNome").GetString().Should().Be("Maria Souza", because: "quem registrou não muda ao resolver");
        lacuna.GetProperty("ocorrenciasAbertas").GetInt32().Should().Be(0, because: "resolvida deixa de contar na fila");
        lacuna.GetProperty("versaoConcorrencia").GetUInt32().Should().NotBe(criada.GetProperty("versaoConcorrencia").GetUInt32());

        var linha = await LerLinhaAsync(id);
        linha.ResolvidoPorId.Should().Be("sub-joao");
        linha.RegistradoPorId.Should().Be("sub-maria");
    }

    [Fact]
    public async Task AlterarEstado_TokenAntigo_Devolve409SemVazarSchema()
    {
        var criada = await RegistrarAsync("conflito");
        var id = criada.GetProperty("id").GetInt32();
        var tokenOriginal = criada.GetProperty("versaoConcorrencia").GetUInt32();

        var (primeiro, _, _) = await EnviarAsync(HttpMethod.Put, $"{Rota}/{id}/estado", new { estado = "Descartada" }, ifMatch: tokenOriginal);
        var (segundo, corpo, _) = await EnviarAsync(HttpMethod.Put, $"{Rota}/{id}/estado", new { estado = "Aberta" }, ifMatch: tokenOriginal);

        primeiro.Should().Be(HttpStatusCode.OK);
        segundo.Should().Be(HttpStatusCode.Conflict);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("CONFLITO_CONCORRENCIA");
        SemVazamentoDeSchema(corpo);
        (await LerLinhaAsync(id)).Estado.Should().Be("Descartada", because: "a segunda escrita foi recusada, nada mudou");
    }

    [Fact]
    public async Task AlterarEstado_SemIfMatch_Devolve400_EInexistente404()
    {
        var criada = await RegistrarAsync("sem if-match");
        var id = criada.GetProperty("id").GetInt32();

        var (semHeader, corpoSemHeader, _) = await EnviarAsync(HttpMethod.Put, $"{Rota}/{id}/estado", new { estado = "Resolvida" });
        var (inexistente, _, _) = await EnviarAsync(HttpMethod.Put, $"{Rota}/999999/estado", new { estado = "Resolvida" }, ifMatch: 1);

        semHeader.Should().Be(HttpStatusCode.BadRequest);
        corpoSemHeader.GetProperty("error").GetProperty("code").GetString().Should().Be("IF_MATCH_OBRIGATORIO");
        inexistente.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AlterarEstado_ParaOMesmoEstado_Devolve422()
    {
        var criada = await RegistrarAsync("mesmo estado");

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{criada.GetProperty("id").GetInt32()}/estado", new { estado = "Aberta" },
            ifMatch: criada.GetProperty("versaoConcorrencia").GetUInt32());

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        SemVazamentoDeSchema(corpo);
    }

    [Theory]
    [InlineData(500, HttpStatusCode.OK)]
    [InlineData(501, HttpStatusCode.UnprocessableEntity)]
    public async Task AlterarEstado_NotaNaFronteira_NuncaVira500(int tamanho, HttpStatusCode esperado)
    {
        var criada = await RegistrarAsync("nota longa");

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{criada.GetProperty("id").GetInt32()}/estado",
            new { estado = "Resolvida", nota = new string('n', tamanho) },
            ifMatch: criada.GetProperty("versaoConcorrencia").GetUInt32());

        status.Should().Be(esperado);
        if (esperado == HttpStatusCode.UnprocessableEntity)
            SemVazamentoDeSchema(corpo);
    }

    [Fact]
    public async Task AlterarEstado_EstadoForaDoEnum_Devolve422()
    {
        var criada = await RegistrarAsync("estado inválido");

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{criada.GetProperty("id").GetInt32()}/estado", new { estado = 99 },
            ifMatch: criada.GetProperty("versaoConcorrencia").GetUInt32());

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AlterarEstado_Reabrir_LimpaAResolucao()
    {
        var criada = await RegistrarAsync("reabrir");
        var resolvida = await AlterarEstadoAsync(criada, "Resolvida", nota: "feito");

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{criada.GetProperty("id").GetInt32()}/estado", new { estado = "Aberta" },
            ifMatch: resolvida.GetProperty("versaoConcorrencia").GetUInt32());

        status.Should().Be(HttpStatusCode.OK);
        var lacuna = corpo.GetProperty("data").GetProperty("lacuna");
        lacuna.GetProperty("estado").GetString().Should().Be("Aberta");
        lacuna.GetProperty("nota").ValueKind.Should().Be(JsonValueKind.Null);
        lacuna.GetProperty("resolvidaEm").ValueKind.Should().Be(JsonValueKind.Null);
        lacuna.GetProperty("resolvidoPorNome").ValueKind.Should().Be(JsonValueKind.Null);
        lacuna.GetProperty("ocorrenciasAbertas").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task AlterarEstado_IncluirMesmoTermo_ResolveAsIrmasAbertasEDeixaOResto()
    {
        var primeira = await RegistrarAsync("Alíquota ISS");
        var segunda = await RegistrarAsync("aliquota iss");
        var terceira = await RegistrarAsync("ALÍQUOTA  ISS");
        var jaDescartada = await RegistrarAsync("aliquota iss");
        var outroTermo = await RegistrarAsync("inscrição municipal");
        await AlterarEstadoAsync(jaDescartada, "Descartada", nota: "duplicado");

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{primeira.GetProperty("id").GetInt32()}/estado",
            new { estado = "Resolvida", nota = "Artigo publicado", incluirMesmoTermo = true },
            ifMatch: primeira.GetProperty("versaoConcorrencia").GetUInt32(),
            sub: "sub-joao", nome: "João Lima");

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("mesmoTermoAlteradas").GetInt32().Should().Be(2,
            because: "as duas irmãs ainda abertas; a descartada e a de outro termo ficam de fora");

        var estados = await LerEstadosAsync();
        estados[primeira.GetProperty("id").GetInt32()].Should().Be("Resolvida");
        estados[segunda.GetProperty("id").GetInt32()].Should().Be("Resolvida");
        estados[terceira.GetProperty("id").GetInt32()].Should().Be("Resolvida");
        estados[jaDescartada.GetProperty("id").GetInt32()].Should().Be("Descartada", because: "quem já foi tratada não é sobrescrita");
        estados[outroTermo.GetProperty("id").GetInt32()].Should().Be("Aberta");

        var irma = await LerLinhaAsync(segunda.GetProperty("id").GetInt32());
        irma.ResolvidoPorId.Should().Be("sub-joao");
        irma.ResolvidoPorNome.Should().Be("João Lima");
        irma.Nota.Should().Be("Artigo publicado");
        irma.ResolvidaEm.Should().NotBeNull();
        irma.RegistradoPorId.Should().Be("sub-maria", because: "quem registrou a irmã não muda");
    }

    [Fact]
    public async Task AlterarEstado_IncluirMesmoTermoSemIrmas_Devolve0Alteradas()
    {
        var unica = await RegistrarAsync("termo único");

        var (_, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{unica.GetProperty("id").GetInt32()}/estado",
            new { estado = "Descartada", incluirMesmoTermo = true },
            ifMatch: unica.GetProperty("versaoConcorrencia").GetUInt32());

        corpo.GetProperty("data").GetProperty("mesmoTermoAlteradas").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task AlterarEstado_IncluirMesmoTermoAoReabrir_Devolve422_ENadaMuda()
    {
        var criada = await RegistrarAsync("reabrir em lote");
        var resolvida = await AlterarEstadoAsync(criada, "Resolvida");

        var (status, _, _) = await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{criada.GetProperty("id").GetInt32()}/estado",
            new { estado = "Aberta", incluirMesmoTermo = true },
            ifMatch: resolvida.GetProperty("versaoConcorrencia").GetUInt32());

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerLinhaAsync(criada.GetProperty("id").GetInt32())).Estado.Should().Be("Resolvida");
    }

    [Fact]
    public async Task AlterarEstado_AtualizadoemDasIrmasAvancaPeloTrigger()
    {
        var primeira = await RegistrarAsync("trigger em lote");
        var irma = await RegistrarAsync("trigger em lote");
        var antes = await LerLinhaAsync(irma.GetProperty("id").GetInt32());

        await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{primeira.GetProperty("id").GetInt32()}/estado",
            new { estado = "Resolvida", incluirMesmoTermo = true },
            ifMatch: primeira.GetProperty("versaoConcorrencia").GetUInt32());

        var depois = await LerLinhaAsync(irma.GetProperty("id").GetInt32());
        depois.AtualizadoEm.Should().BeAfter(antes.AtualizadoEm);
    }

    // ------------------------------------------------------------------ 401 / 403 em cada endpoint

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/1")]
    [InlineData("POST", "")]
    [InlineData("PUT", "/1/estado")]
    public async Task SemToken_Devolve401(string verbo, string sufixo)
    {
        using var requisicao = new HttpRequestMessage(new HttpMethod(verbo), Rota + sufixo);
        if (verbo is "POST" or "PUT")
            requisicao.Content = JsonContent.Create(new { termo = "x", estado = "Resolvida" });

        using var resposta = await _cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ContarLinhasAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("GET", "", "lacunasbusca-adicionar,lacunasbusca-atualizar")]
    [InlineData("GET", "/1", "lacunasbusca-adicionar,lacunasbusca-atualizar")]
    [InlineData("POST", "", "lacunasbusca-buscar,lacunasbusca-atualizar")]
    [InlineData("PUT", "/1/estado", "lacunasbusca-buscar,lacunasbusca-adicionar")]
    public async Task ComRoleDeOutraAcao_Devolve403_ENaoGrava(string verbo, string sufixo, string roles)
    {
        using var requisicao = new HttpRequestMessage(new HttpMethod(verbo), Rota + sufixo);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, roles);
        if (verbo is "POST" or "PUT")
            requisicao.Content = JsonContent.Create(new { termo = "x", estado = "Resolvida" });

        using var resposta = await _cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ContarLinhasAsync()).Should().Be(0);
    }

    // ------------------------------------------------------------------ apoio

    private async Task<JsonElement> RegistrarAsync(string termo)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, Rota, new { termo });
        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data");
    }

    private async Task<JsonElement> AlterarEstadoAsync(JsonElement lacuna, string estado, string? nota = null)
    {
        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"{Rota}/{lacuna.GetProperty("id").GetInt32()}/estado", new { estado, nota },
            ifMatch: lacuna.GetProperty("versaoConcorrencia").GetUInt32());
        status.Should().Be(HttpStatusCode.OK);
        return corpo.GetProperty("data").GetProperty("lacuna");
    }

    private async Task<List<int>> IdsDaListaAsync(string consulta)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, Rota + consulta);
        status.Should().Be(HttpStatusCode.OK);
        return corpo.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
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

    /// <summary>Asserção de ausência do padrão em TODO erro 4xx: nome de constraint, coluna ou stack nunca saem na resposta.</summary>
    private static void SemVazamentoDeSchema(JsonElement corpo)
    {
        var texto = corpo.GetRawText();
        texto.Should().NotContain("_codex_").And.NotContain("ck_codex_").And.NotContain("lacunasbusca")
            .And.NotContain("at Codex.").And.NotContain("Npgsql");
    }

    private async Task<int> ContarLinhasAsync()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT count(*) FROM codex.lacunasbusca";
        return (int)(long)(await comando.ExecuteScalarAsync())!;
    }

    private async Task ExecutarSqlAsync(string sql)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    private async Task<Dictionary<int, string>> LerEstadosAsync()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT lacunabuscaid, estado FROM codex.lacunasbusca";
        var estados = new Dictionary<int, string>();
        await using var leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
            estados[leitor.GetInt32(0)] = leitor.GetString(1);
        return estados;
    }

    private async Task<LinhaGravada> LerLinhaAsync(int id)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "SELECT registradoporid, registradopornome, resolvidoporid, resolvidopornome, estado, nota, resolvidaem, criadoem, atualizadoem "
            + "FROM codex.lacunasbusca WHERE lacunabuscaid = @id";
        comando.Parameters.AddWithValue("id", id);

        await using var leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue();

        return new LinhaGravada(
            leitor.GetString(0),
            leitor.IsDBNull(1) ? null : leitor.GetString(1),
            leitor.IsDBNull(2) ? null : leitor.GetString(2),
            leitor.IsDBNull(3) ? null : leitor.GetString(3),
            leitor.GetString(4),
            leitor.IsDBNull(5) ? null : leitor.GetString(5),
            leitor.IsDBNull(6) ? null : leitor.GetFieldValue<DateTimeOffset>(6),
            leitor.GetFieldValue<DateTimeOffset>(7),
            leitor.GetFieldValue<DateTimeOffset>(8));
    }

    private sealed record LinhaGravada(
        string RegistradoPorId,
        string? RegistradoPorNome,
        string? ResolvidoPorId,
        string? ResolvidoPorNome,
        string Estado,
        string? Nota,
        DateTimeOffset? ResolvidaEm,
        DateTimeOffset CriadoEm,
        DateTimeOffset AtualizadoEm);
}
