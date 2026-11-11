using System.Net;
using System.Text.Json;
using Codex.Application.Services;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// <c>GET /api/v1/comunicados</c> medido pelo JSON literal da resposta HTTP contra um Postgres
/// real. O relógio do host é um <see cref="TimeProvider"/> controlado: o que decide se um
/// comunicado vence é o tempo que passa, não o job de expiração (que nenhum host de teste liga).
///
/// <para>
/// O banco é compartilhado pela <see cref="PostgresCollection"/>: toda asserção de conjunto é
/// restrita ao cenário (sufixo único nos títulos), nunca a contagens globais. Limpeza é hard
/// delete.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ComunicadosPublicosHttpTests : IAsyncLifetime
{
    private const string Rota = "/api/v1/comunicados";

    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];
    // Truncado ao milissegundo: o Postgres guarda microssegundos, e o caso "no exato instante da
    // expiração" compara o relógio com o valor lido de volta do banco.
    private readonly RelogioControlado _relogio = new(TruncarAoMilissegundo(DateTimeOffset.UtcNow));

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _cliente = null!;
    private Cenario _cenario = null!;

    public ComunicadosPublicosHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_relogio);
            }));
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _cenario = await SemearAsync();
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        await LimparAsync();
    }

    // ------------------------------------------------ combinações de abrangência (o que vale onde)

    [Fact]
    public async Task SemParametros_TrazSoOsGlobaisNoAr()
    {
        var titulos = await TitulosDoCenarioAsync(Rota);

        titulos.Should().Equal(T("G-crit"), T("G-info"));
    }

    [Fact]
    public async Task ComProvedor_TrazGlobaisMaisProvedorMaisVersoesDele_NaOrdemDeSeveridadeERecencia()
    {
        var titulos = await TitulosDoCenarioAsync(Consulta(("provedor", _cenario.SlugP)));

        // Seis elementos cuja ordem esperada não coincide com a de inserção, com o inverso dela
        // nem com a alfabética da severidade (Alerta, Critico, Informativo):
        //   Critico: P-crit (mais recente) antes de G-crit; Alerta: V1-alerta e P-alerta nasceram
        //   na MESMA transação (criadoem empatado) e o id maior vem primeiro; Informativo: V2-info
        //   antes de G-info.
        titulos.Should().Equal(
            T("P-crit"), T("G-crit"), T("V1-alerta"), T("P-alerta"), T("V2-info"), T("G-info"));
    }

    [Fact]
    public async Task ComProvedorEVersao_TrazSoAVersaoPedida_NaoAsIrmas()
    {
        var titulos = await TitulosDoCenarioAsync(Consulta(("provedor", _cenario.SlugP), ("versao", _cenario.SlugV1)));

        titulos.Should().Equal(T("P-crit"), T("G-crit"), T("V1-alerta"), T("P-alerta"), T("G-info"));
        titulos.Should().NotContain(T("V2-info"), because: "a versão pedida é a V1 — comunicado da V2 não é dela");
    }

    [Fact]
    public async Task VersaoComOMesmoSlugEmOutroProvedor_NaoVazaParaOProvedorPedido()
    {
        // O slug de versão é único por provedor, não no acervo: P e Q têm cada um uma versão com
        // o mesmo slug. Casar só pelo slug da versão traria o comunicado da versão do OUTRO provedor.
        _cenario.SlugV1.Should().Be(_cenario.SlugW1, because: "o cenário depende de dois provedores com versões homônimas");

        var doP = await TitulosDoCenarioAsync(Consulta(("provedor", _cenario.SlugP), ("versao", _cenario.SlugV1)));
        var doQ = await TitulosDoCenarioAsync(Consulta(("provedor", _cenario.SlugQ), ("versao", _cenario.SlugW1)));

        doP.Should().Contain(T("V1-alerta")).And.NotContain(T("Q-versao"));
        doQ.Should().Contain(T("Q-versao")).And.NotContain(T("V1-alerta"));
    }

    [Fact]
    public async Task ProvedorRepetido_SomaOsComunicadosDosDois()
    {
        var titulos = await TitulosDoCenarioAsync(Consulta(("provedor", _cenario.SlugP), ("provedor", _cenario.SlugQ)));

        titulos.Should().BeEquivalentTo(new[]
        {
            T("G-crit"), T("G-info"), T("P-crit"), T("P-alerta"), T("V1-alerta"), T("V2-info"), T("Q-alerta"), T("Q-versao"),
        });
    }

    [Fact]
    public async Task ProvedorRepetidoComOMesmoSlug_NaoDuplicaComunicado()
    {
        var titulos = await TitulosDoCenarioAsync(Consulta(("provedor", _cenario.SlugP), ("provedor", _cenario.SlugP)));

        titulos.Should().OnlyHaveUniqueItems();
        titulos.Should().HaveCount(6);
    }

    [Fact]
    public async Task ProvedorDeOutroContexto_NaoTrazComunicadoDoProvedorPedido()
    {
        var titulos = await TitulosDoCenarioAsync(Consulta(("provedor", _cenario.SlugQ)));

        titulos.Should().BeEquivalentTo(new[] { T("G-crit"), T("G-info"), T("Q-alerta"), T("Q-versao") });
    }

    // ------------------------------------ slug desconhecido / não público: 200 e nada que vaze

    [Fact]
    public async Task SlugInexistente_Responde200_ComSoOsGlobais()
    {
        var titulos = await TitulosDoCenarioAsync(Consulta(("provedor", "nao-existe-" + _sufixo)));

        titulos.Should().Equal(T("G-crit"), T("G-info"));
    }

    [Fact]
    public async Task ProvedorEmRascunho_NaoTrazOComunicadoDele_NemOcultaOsGlobais()
    {
        var consulta = Consulta(("provedor", _cenario.SlugRascunho));

        using var resposta = await _cliente.GetAsync(consulta);
        var corpo = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TitulosDoCenarioAsync(consulta)).Should().Equal(T("G-crit"), T("G-info"));
        corpo.Should().NotContain(T("Rascunho-provedor"), because: "PQ-04 — vazaria o provedor pré-lançamento");
        corpo.Should().NotContain(_cenario.NomeRascunho);
    }

    [Fact]
    public async Task ProvedorRemovido_NaoTrazOComunicadoDele()
    {
        var titulos = await TitulosDoCenarioAsync(Consulta(("provedor", _cenario.SlugRemovido)));

        titulos.Should().Equal(T("G-crit"), T("G-info"));
    }

    [Fact]
    public async Task VersaoEmRascunhoOuRemovida_NaoTrazOComunicadoDela_MasMantemOsDoProvedor()
    {
        var doRascunho = await TitulosDoCenarioAsync(
            Consulta(("provedor", _cenario.SlugP), ("versao", _cenario.SlugVersaoRascunho)));
        var daRemovida = await TitulosDoCenarioAsync(
            Consulta(("provedor", _cenario.SlugP), ("versao", _cenario.SlugVersaoRemovida)));
        var semFiltroDeVersao = await TitulosDoCenarioAsync(Consulta(("provedor", _cenario.SlugP)));

        var soDoProvedor = new[] { T("P-crit"), T("G-crit"), T("P-alerta"), T("G-info") };
        doRascunho.Should().Equal(soDoProvedor);
        daRemovida.Should().Equal(soDoProvedor);
        semFiltroDeVersao.Should().NotContain(T("V3-rascunho")).And.NotContain(T("V4-removida"));
    }

    [Fact]
    public async Task VersaoInexistente_TrazOsGlobaisEOsDoProvedor()
    {
        var titulos = await TitulosDoCenarioAsync(
            Consulta(("provedor", _cenario.SlugP), ("versao", "nao-existe-" + _sufixo)));

        titulos.Should().Equal(T("P-crit"), T("G-crit"), T("P-alerta"), T("G-info"));
    }

    // -------------------------------------------------------- vigência: publicado, ativo, não vencido

    [Fact]
    public async Task NaoPublicadoRemovidoEVencido_NaoAparecem_MesmoComOJobDesligado()
    {
        var titulos = await TitulosDoCenarioAsync(Consulta(("provedor", _cenario.SlugP)));

        // Controle positivo: sem ele, um filtro que esconde TUDO passaria neste teste.
        titulos.Should().Contain(T("G-crit"));

        titulos.Should().NotContain(T("G-vencido"), because: "vencido, ainda publicado=true — a leitura filtra por conta própria");
        titulos.Should().NotContain(T("P-vencido"));
        titulos.Should().NotContain(T("G-rascunho"), because: "não publicado");
        titulos.Should().NotContain(T("G-removido"), because: "removido (soft delete)");
    }

    [Fact]
    public async Task ComunicadoQueVencePelaPassagemDoTempo_SaiSemNenhumaEscrita()
    {
        var consulta = Consulta(("provedor", _cenario.SlugP));
        await SemearComunicadoQueVenceEmUmaHoraAsync();

        (await TitulosDoCenarioAsync(consulta)).Should().Contain(T("G-curto"));

        // Nenhuma linha muda: só o relógio anda. É o caso que a ETag derivada de xmin não vê.
        _relogio.Agora = _cenario.Referencia.AddHours(2);
        var depoisDeVencer = await TitulosDoCenarioAsync(consulta);

        depoisDeVencer.Should().NotContain(T("G-curto"), because: "expiraEm ficou para trás do relógio");
        depoisDeVencer.Should().Contain(T("G-crit"), because: "o que vence daqui a dias continua no ar");

        _relogio.Agora = _cenario.Referencia;
        (await TitulosDoCenarioAsync(consulta)).Should().Contain(T("G-curto"),
            because: "a decisão é função do relógio, não de estado gravado");
    }

    [Fact]
    public async Task ComunicadoNoExatoInstanteDaExpiracao_JaNaoAparece()
    {
        var consulta = Consulta(("provedor", _cenario.SlugP));
        var expiraEm = await SemearComunicadoQueVenceEmUmaHoraAsync();

        _relogio.Agora = expiraEm;

        (await TitulosDoCenarioAsync(consulta)).Should().NotContain(T("G-curto"),
            because: "o predicado é ExpiraEm > agora — no instante exato ele já venceu");
    }

    [Fact]
    public async Task ExcessoDeComunicados_CortaNoTeto_PreservandoOsMaisGraves()
    {
        await using (var contexto = _fixture.CriarContexto())
        {
            var excesso = Enumerable.Range(0, ComunicadoServicoPublico.MaximoComunicados + 1)
                .Select(i => Comunicado.CriarGlobal(T($"G-excesso-{i}"), "Corpo.", Severidade.Informativo, _cenario.Referencia.AddDays(5)))
                .ToList();
            excesso.ForEach(c => c.Publicar());
            contexto.Comunicados.AddRange(excesso);
            await contexto.SaveChangesAsync();
        }

        var dados = (await ObterDadosAsync(Rota)).EnumerateArray().ToList();

        dados.Should().HaveCount(ComunicadoServicoPublico.MaximoComunicados);
        dados.Select(c => c.GetProperty("titulo").GetString()).Should().Contain(T("G-crit"),
            because: "o corte é depois da ordenação por severidade: o crítico nunca fica de fora para caber informativo");
    }

    // ------------------------------------------------------------------------- contrato literal

    [Fact]
    public async Task Resposta_TemEnvelopeApiResponse_ComAColecaoEmData()
    {
        using var resposta = await _cliente.GetAsync(Rota);
        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        corpo.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["data", "message"]);
        corpo.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task ComunicadoGlobal_NaoTemAlvoNemUrl()
    {
        var global = await LocalizarAsync(Consulta(), "G-crit");

        global.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["id", "titulo", "corpo", "severidade", "abrangencia", "expiraEm", "provedor", "versao", "url"]);
        global.GetProperty("id").ValueKind.Should().Be(JsonValueKind.Number);
        global.GetProperty("severidade").GetString().Should().Be("Critico", because: "enum sai como nome, nunca como número");
        global.GetProperty("abrangencia").GetString().Should().Be("Global");
        global.GetProperty("provedor").ValueKind.Should().Be(JsonValueKind.Null);
        global.GetProperty("versao").ValueKind.Should().Be(JsonValueKind.Null);
        global.GetProperty("url").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ComunicadoDeProvedor_TrazProvedorEUrlDaPaginaDele_SemVersao()
    {
        var doProvedor = await LocalizarAsync(Consulta(("provedor", _cenario.SlugP)), "P-alerta");

        doProvedor.GetProperty("abrangencia").GetString().Should().Be("Provedor");
        doProvedor.GetProperty("severidade").GetString().Should().Be("Alerta");
        doProvedor.GetProperty("provedor").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["nome", "slug"]);
        doProvedor.GetProperty("provedor").GetProperty("slug").GetString().Should().Be(_cenario.SlugP);
        doProvedor.GetProperty("provedor").GetProperty("nome").GetString().Should().Be(_cenario.NomeP);
        doProvedor.GetProperty("versao").ValueKind.Should().Be(JsonValueKind.Null);
        doProvedor.GetProperty("url").GetString().Should().Be($"/provedores/{_cenario.SlugP}");
    }

    [Fact]
    public async Task ComunicadoDeVersao_TrazProvedorVersaoEUrlDaPaginaDoProvedor()
    {
        var daVersao = await LocalizarAsync(Consulta(("provedor", _cenario.SlugP)), "V1-alerta");

        daVersao.GetProperty("abrangencia").GetString().Should().Be("ProvedorVersao");
        daVersao.GetProperty("provedor").GetProperty("slug").GetString().Should().Be(_cenario.SlugP);
        daVersao.GetProperty("versao").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["rotulo", "slug"]);
        daVersao.GetProperty("versao").GetProperty("slug").GetString().Should().Be(_cenario.SlugV1);
        daVersao.GetProperty("versao").GetProperty("rotulo").GetString().Should().Be(_cenario.RotuloV1);
        daVersao.GetProperty("url").GetString().Should().Be($"/provedores/{_cenario.SlugP}");
    }

    // --------------------------------------------------------------- cache, anonimato, sem ETag

    [Fact]
    public async Task Resposta_TemCacheControlCurto_ESemEtagDeConteudo()
    {
        using var resposta = await _cliente.GetAsync(Consulta(("provedor", _cenario.SlugP)));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Headers.CacheControl.Should().NotBeNull();
        resposta.Headers.CacheControl!.Public.Should().BeTrue();
        resposta.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
        resposta.Headers.CacheControl.ToString().Should().Contain("stale-while-revalidate=300");

        resposta.Headers.ETag.Should().BeNull(
            because: "a resposta muda só pela passagem do tempo — ETag de conteúdo serviria corpo vencido por 304");
    }

    [Fact]
    public async Task RevalidarComIfNoneMatch_NuncaDevolve304()
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, Rota);
        requisicao.Headers.TryAddWithoutValidation("If-None-Match", "*");

        using var resposta = await _cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RespondeSemAuthorization()
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, Rota);
        using var resposta = await _cliente.SendAsync(requisicao);

        requisicao.Headers.Authorization.Should().BeNull();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task EscritaNaRotaPublica_NaoExiste(string verbo)
    {
        using var requisicao = new HttpRequestMessage(new HttpMethod(verbo), Rota);
        using var resposta = await _cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed, because: "a rota é só GET (U2)");
    }

    // ----------------------------------------------------------------- parâmetros inválidos → 400

    [Fact]
    public async Task VersaoSemProvedor_Devolve400()
    {
        await AfirmarParametroInvalidoAsync(Consulta(("versao", _cenario.SlugV1)));
    }

    [Fact]
    public async Task VersaoComDoisProvedores_Devolve400()
    {
        await AfirmarParametroInvalidoAsync(
            Consulta(("provedor", _cenario.SlugP), ("provedor", _cenario.SlugQ), ("versao", _cenario.SlugV1)));
    }

    [Fact]
    public async Task VersaoComOMesmoProvedorRepetido_ContaComoUmSo_E200()
    {
        using var resposta = await _cliente.GetAsync(
            Consulta(("provedor", _cenario.SlugP), ("provedor", _cenario.SlugP), ("versao", _cenario.SlugV1)));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VersaoEmBranco_ChegaComoAusente_ETrazOsComunicadosDoProvedor()
    {
        // O model binding converte texto em branco para null: é um filtro que não foi informado.
        var titulos = await TitulosDoCenarioAsync(Consulta(("provedor", _cenario.SlugP), ("versao", "   ")));

        titulos.Should().Contain(T("V2-info")).And.Contain(T("V1-alerta"));
    }

    [Fact]
    public async Task ProvedorEmBranco_Devolve400()
    {
        await AfirmarParametroInvalidoAsync(Consulta(("provedor", "   ")));
    }

    [Fact]
    public async Task SlugDeProvedorAcimaDoTeto_Devolve400_ENoTetoPassa()
    {
        await AfirmarParametroInvalidoAsync(
            Consulta(("provedor", new string('a', ComunicadoServicoPublico.MaximoSlug + 1))));

        using var noTeto = await _cliente.GetAsync(
            Consulta(("provedor", new string('a', ComunicadoServicoPublico.MaximoSlug))));
        noTeto.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SlugDeVersaoAcimaDoTeto_Devolve400_ENoTetoPassa()
    {
        await AfirmarParametroInvalidoAsync(Consulta(
            ("provedor", _cenario.SlugP), ("versao", new string('a', ComunicadoServicoPublico.MaximoSlug + 1))));

        using var noTeto = await _cliente.GetAsync(Consulta(
            ("provedor", _cenario.SlugP), ("versao", new string('a', ComunicadoServicoPublico.MaximoSlug))));
        noTeto.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProvedoresAcimaDoTeto_Devolve400_ENoTetoPassa()
    {
        var onze = Enumerable.Range(0, ComunicadoServicoPublico.MaximoProvedores + 1)
            .Select(i => ("provedor", $"p{i}-{_sufixo}")).ToArray();
        var dez = onze.Take(ComunicadoServicoPublico.MaximoProvedores).ToArray();

        await AfirmarParametroInvalidoAsync(Consulta(onze));

        using var noTeto = await _cliente.GetAsync(Consulta(dez));
        noTeto.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ValorHostil_ViraSlugInexistente_ENaoDerrubaNada()
    {
        foreach (var hostil in new[] { "'; DROP TABLE codex.comunicados; --", "%' OR '1'='1", "<script>alert(1)</script>", "../../etc/passwd" })
        {
            using var resposta = await _cliente.GetAsync(Consulta(("provedor", hostil), ("versao", hostil)));

            resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: "valor hostil é só um slug que não existe");
        }

        (await TitulosDoCenarioAsync(Consulta())).Should().Contain(T("G-crit"), because: "a tabela continua de pé");
    }

    // ------------------------------------------------------------------------------------ apoio

    private static DateTimeOffset TruncarAoMilissegundo(DateTimeOffset instante) =>
        new(instante.Ticks - instante.Ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

    private string T(string chave) => $"{chave} {_sufixo}";

    private static string Consulta(params (string Nome, string Valor)[] parametros) =>
        parametros.Length == 0
            ? Rota
            : $"{Rota}?{string.Join('&', parametros.Select(p => $"{p.Nome}={Uri.EscapeDataString(p.Valor)}"))}";

    private async Task<JsonElement> ObterDadosAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: $"GET {rota} deveria responder 200: {texto}");

        return JsonDocument.Parse(texto).RootElement.GetProperty("data").Clone();
    }

    /// <summary>Títulos do cenário, na ordem da resposta — comunicados de outras classes ficam de fora.</summary>
    private async Task<List<string>> TitulosDoCenarioAsync(string rota) =>
        (await ObterDadosAsync(rota)).EnumerateArray()
            .Select(c => c.GetProperty("titulo").GetString()!)
            .Where(titulo => titulo.EndsWith(_sufixo, StringComparison.Ordinal))
            .ToList();

    private async Task<JsonElement> LocalizarAsync(string rota, string chave)
    {
        var item = (await ObterDadosAsync(rota)).EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("titulo").GetString() == T(chave));

        item.ValueKind.Should().Be(JsonValueKind.Object, because: $"o comunicado '{chave}' deveria estar na resposta");
        return item;
    }

    private async Task AfirmarParametroInvalidoAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: $"GET {rota}: {texto}");

        var erro = JsonDocument.Parse(texto).RootElement.GetProperty("error");
        erro.GetProperty("code").GetString().Should().Be("PARAMETRO_INVALIDO");
        erro.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();

        AfirmarSemDetalheInterno(texto);
        texto.Should().NotContain(_cenario.SlugP, because: "a mensagem é texto nosso, nunca eco do que o cliente mandou");
    }

    private static void AfirmarSemDetalheInterno(string corpo)
    {
        corpo.Should().NotContain("Exception");
        corpo.Should().NotContain("   at ");
        corpo.Should().NotContain("Codex.");
        corpo.Should().NotContain("uq_codex_");
        corpo.Should().NotContain("Npgsql");
        corpo.Should().NotContain("ArgumentException");
    }

    private sealed class RelogioControlado : TimeProvider
    {
        public RelogioControlado(DateTimeOffset agora)
        {
            Agora = agora;
        }

        public DateTimeOffset Agora { get; set; }

        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private sealed record Cenario
    {
        public required DateTimeOffset Referencia { get; init; }
        public required string SlugP { get; init; }
        public required string NomeP { get; init; }
        public required string SlugQ { get; init; }
        public required string SlugRascunho { get; init; }
        public required string NomeRascunho { get; init; }
        public required string SlugRemovido { get; init; }
        public required string SlugV1 { get; init; }
        public required string RotuloV1 { get; init; }
        public required string SlugW1 { get; init; }
        public required string SlugVersaoRascunho { get; init; }
        public required string SlugVersaoRemovida { get; init; }
    }

    private async Task<Cenario> SemearAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => (int?)v.Ordem) ?? 0;
        var layout = VersaoLayout.Criar($"Com L {_sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(layout);
        await contexto.SaveChangesAsync();

        var provedorP = Provedor.Criar($"Com Provedor P {_sufixo}", $"COM-P-{_sufixo}", CategoriaProvedor.Nacional);
        var provedorQ = Provedor.Criar($"Com Provedor Q {_sufixo}", $"COM-Q-{_sufixo}", CategoriaProvedor.Nacional);
        var provedorRascunho = Provedor.Criar($"Com Provedor Rascunho {_sufixo}", $"COM-R-{_sufixo}", CategoriaProvedor.Nacional);
        var provedorRemovido = Provedor.Criar($"Com Provedor Removido {_sufixo}", $"COM-S-{_sufixo}", CategoriaProvedor.Nacional);
        provedorP.Publicar();
        provedorQ.Publicar();
        provedorRemovido.Publicar();
        provedorRemovido.Desativar();
        contexto.Provedores.AddRange(provedorP, provedorQ, provedorRascunho, provedorRemovido);
        await contexto.SaveChangesAsync();

        // V1 de P e W1 de Q têm o MESMO rótulo, logo o mesmo slug: único por provedor, não global.
        var versaoV1 = VersaoProvedor.Criar(provedorP.Id, layout.Id, $"Com Versao Um {_sufixo}");
        var versaoV2 = VersaoProvedor.Criar(provedorP.Id, layout.Id, $"Com Versao Dois {_sufixo}");
        var versaoRascunho = VersaoProvedor.Criar(provedorP.Id, layout.Id, $"Com Versao Rascunho {_sufixo}");
        var versaoRemovida = VersaoProvedor.Criar(provedorP.Id, layout.Id, $"Com Versao Removida {_sufixo}");
        var versaoW1 = VersaoProvedor.Criar(provedorQ.Id, layout.Id, $"Com Versao Um {_sufixo}");
        versaoV1.Publicar();
        versaoV2.Publicar();
        versaoW1.Publicar();
        versaoRemovida.Publicar();
        versaoRemovida.Desativar();
        contexto.VersoesProvedor.AddRange(versaoV1, versaoV2, versaoRascunho, versaoRemovida, versaoW1);
        await contexto.SaveChangesAsync();

        var referencia = _relogio.Agora;
        var longe = referencia.AddDays(5);
        var passado = referencia.AddHours(-1);

        // Cada SaveChanges é uma transação (criadoem = now() dela): a ordem de criação abaixo é a
        // ordem de recência, exceto o par que nasce junto de propósito.
        async Task Criar(bool publicar, params Comunicado[] comunicados)
        {
            foreach (var comunicado in comunicados)
            {
                if (publicar)
                    comunicado.Publicar();
            }

            contexto.Comunicados.AddRange(comunicados);
            await contexto.SaveChangesAsync();
        }

        Comunicado Montar(string chave, Func<string, Comunicado> fabrica) => fabrica(T(chave));

        await Criar(true, Montar("G-info", t => Comunicado.CriarGlobal(t, "Corpo.", Severidade.Informativo, longe)));
        await Criar(true, Montar("G-crit", t => Comunicado.CriarGlobal(t, "Corpo.", Severidade.Critico, longe)));
        await Criar(
            true,
            Montar("P-alerta", t => Comunicado.CriarDeProvedor(provedorP.Id, t, "Corpo.", Severidade.Alerta, longe)),
            Montar("V1-alerta", t => Comunicado.CriarDeVersaoProvedor(provedorP.Id, versaoV1.Id, t, "Corpo.", Severidade.Alerta, longe)));
        await Criar(true, Montar("P-crit", t => Comunicado.CriarDeProvedor(provedorP.Id, t, "Corpo.", Severidade.Critico, longe)));
        await Criar(true, Montar("V2-info", t => Comunicado.CriarDeVersaoProvedor(provedorP.Id, versaoV2.Id, t, "Corpo.", Severidade.Informativo, longe)));

        await Criar(true, Montar("Q-alerta", t => Comunicado.CriarDeProvedor(provedorQ.Id, t, "Corpo.", Severidade.Alerta, longe)));
        await Criar(true, Montar("Q-versao", t => Comunicado.CriarDeVersaoProvedor(provedorQ.Id, versaoW1.Id, t, "Corpo.", Severidade.Alerta, longe)));

        // Fora do ar por motivos diferentes.
        await Criar(true, Montar("G-vencido", t => Comunicado.CriarGlobal(t, "Corpo.", Severidade.Critico, passado)));
        await Criar(true, Montar("P-vencido", t => Comunicado.CriarDeProvedor(provedorP.Id, t, "Corpo.", Severidade.Critico, passado)));
        await Criar(false, Montar("G-rascunho", t => Comunicado.CriarGlobal(t, "Corpo.", Severidade.Critico, longe)));

        var removido = Montar("G-removido", t => Comunicado.CriarGlobal(t, "Corpo.", Severidade.Critico, longe));
        removido.Desativar();
        await Criar(true, removido);

        await Criar(true, Montar("Rascunho-provedor", t => Comunicado.CriarDeProvedor(provedorRascunho.Id, t, "Corpo.", Severidade.Critico, longe)));
        await Criar(true, Montar("Removido-provedor", t => Comunicado.CriarDeProvedor(provedorRemovido.Id, t, "Corpo.", Severidade.Critico, longe)));
        await Criar(true, Montar("V3-rascunho", t => Comunicado.CriarDeVersaoProvedor(provedorP.Id, versaoRascunho.Id, t, "Corpo.", Severidade.Critico, longe)));
        await Criar(true, Montar("V4-removida", t => Comunicado.CriarDeVersaoProvedor(provedorP.Id, versaoRemovida.Id, t, "Corpo.", Severidade.Critico, longe)));

        return new Cenario
        {
            Referencia = referencia,
            SlugP = provedorP.Slug,
            NomeP = provedorP.Nome,
            SlugQ = provedorQ.Slug,
            SlugRascunho = provedorRascunho.Slug,
            NomeRascunho = provedorRascunho.Nome,
            SlugRemovido = provedorRemovido.Slug,
            SlugV1 = versaoV1.Slug,
            RotuloV1 = versaoV1.Rotulo,
            SlugW1 = versaoW1.Slug,
            SlugVersaoRascunho = versaoRascunho.Slug,
            SlugVersaoRemovida = versaoRemovida.Slug,
        };
    }

    /// <summary>Global, vivo na referência do cenário e vencido duas horas depois dela.</summary>
    private async Task<DateTimeOffset> SemearComunicadoQueVenceEmUmaHoraAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var expiraEm = _cenario.Referencia.AddHours(1);
        var comunicado = Comunicado.CriarGlobal(T("G-curto"), "Corpo.", Severidade.Informativo, expiraEm);
        comunicado.Publicar();
        contexto.Comunicados.Add(comunicado);
        await contexto.SaveChangesAsync();

        return expiraEm;
    }

    private async Task LimparAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var comunicados = await contexto.Comunicados.Where(c => c.Titulo.EndsWith(_sufixo)).ToListAsync();
        var provedores = await contexto.Provedores.Where(p => p.Nome.EndsWith(_sufixo)).ToListAsync();
        var idsProvedor = provedores.Select(p => p.Id).ToList();
        var versoesProvedor = await contexto.VersoesProvedor.Where(v => idsProvedor.Contains(v.ProvedorId)).ToListAsync();
        var versoesLayout = await contexto.VersoesLayout.Where(v => v.Rotulo.EndsWith(_sufixo)).ToListAsync();

        contexto.Comunicados.RemoveRange(comunicados);
        contexto.VersoesProvedor.RemoveRange(versoesProvedor);
        contexto.Provedores.RemoveRange(provedores);
        contexto.VersoesLayout.RemoveRange(versoesLayout);

        await contexto.SaveChangesAsync();
    }
}
