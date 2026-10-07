using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Versões com verificação vencida por <b>HTTP real</b> contra Postgres real. A consulta
/// varre todas as versões do acervo, e o banco da <see cref="PostgresCollection"/> é compartilhado —
/// por isso roda num banco ADICIONAL, só com o conteúdo semeado aqui. "Hoje" é fixo em
/// 2026-10-05 12:00 (Brasília), e as datas dos casos são literais: 90 dias antes é 2026-07-07.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class VerificacoesVencidasHttpTests : IAsyncLifetime
{
    private const string Rota = "/api/v1/interno/versoes/verificacoes-vencidas";
    private const string RoleLeitura = "versoesprovedor-buscar";

    private static readonly SemaphoreSlim TravaDoBanco = new(1, 1);
    private static string? _connectionStringDoBanco;
    private static int _versaoLayoutId;

    private static readonly DateTimeOffset Agora = new(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

    private readonly PostgresContainerFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private CodexApiFactoryComBanco _fabricaBase = null!;
    private HttpClient _cliente = null!;
    private string _connectionString = null!;

    public VerificacoesVencidasHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await GarantirBancoAsync();
        await ExecutarSqlAsync("DELETE FROM codex.versoesprovedor; DELETE FROM codex.provedores");

        _fabricaBase = new CodexApiFactoryComBanco(_connectionString);
        _factory = ComRelogio(Agora);
        _cliente = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        _fabricaBase.Dispose();
        await ExecutarSqlAsync("DELETE FROM codex.versoesprovedor; DELETE FROM codex.provedores");
    }

    // ------------------------------------------------------------------ contrato

    [Fact]
    public async Task Listar_DevolveOContratoLiteral()
    {
        var provedorId = await CriarProvedorAsync("Provedor Contrato");
        var versaoId = await CriarVersaoAsync(provedorId, "1.0 Contrato", new DateOnly(2026, 7, 7), publicado: true);
        await CriarVersaoAsync(provedorId, "2.0 Rascunho", verificadoEm: null, publicado: false);

        var (status, corpo) = await ListarAsync("");

        status.Should().Be(HttpStatusCode.OK);
        corpo.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["items", "totalCount", "page", "pageSize", "totalPages"],
            because: "listagem é envelope cru, sem 'data'");
        corpo.GetProperty("totalCount").GetInt32().Should().Be(2);
        corpo.GetProperty("page").GetInt32().Should().Be(1);
        corpo.GetProperty("pageSize").GetInt32().Should().Be(25);

        var itens = corpo.GetProperty("items").EnumerateArray().ToList();
        itens.Should().HaveCount(2);

        var nuncaVerificada = itens[0];
        nuncaVerificada.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["versaoId", "rotulo", "slug", "provedor", "verificadoEm", "diasDesdeVerificacao", "publicado", "urlInterna"],
            because: "o frontend tipa exatamente estas propriedades");
        nuncaVerificada.GetProperty("rotulo").GetString().Should().Be("2.0 Rascunho");
        nuncaVerificada.GetProperty("verificadoEm").ValueKind.Should().Be(JsonValueKind.Null);
        nuncaVerificada.GetProperty("diasDesdeVerificacao").ValueKind.Should().Be(JsonValueKind.Null);
        nuncaVerificada.GetProperty("publicado").GetBoolean().Should().BeFalse(because: "rascunho entra, sinalizado");

        var verificada = itens[1];
        verificada.GetProperty("versaoId").GetInt32().Should().Be(versaoId);
        verificada.GetProperty("rotulo").GetString().Should().Be("1.0 Contrato");
        verificada.GetProperty("slug").GetString().Should().Be("1-0-contrato");
        verificada.GetProperty("verificadoEm").GetString().Should().Be("2026-07-07", because: "data sem horário");
        verificada.GetProperty("diasDesdeVerificacao").GetInt32().Should().Be(90);
        verificada.GetProperty("publicado").GetBoolean().Should().BeTrue();
        verificada.GetProperty("urlInterna").GetString().Should().Be($"/interno/provedores/{provedorId}/versoes/{versaoId}");

        var provedor = verificada.GetProperty("provedor");
        provedor.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["id", "nome", "slug"]);
        provedor.GetProperty("id").GetInt32().Should().Be(provedorId);
        provedor.GetProperty("nome").GetString().Should().Be("Provedor Contrato");
        provedor.GetProperty("slug").GetString().Should().Be("provedor-contrato");
    }

    [Fact]
    public async Task Listar_SemNenhumaVersao_Devolve200ComListaVazia()
    {
        var (status, corpo) = await ListarAsync("");

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("items").GetArrayLength().Should().Be(0);
        corpo.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    // ------------------------------------------------------------------ ordem

    [Fact]
    public async Task Listar_NuncaVerificadaPrimeiro_DepoisDaMaisAntigaParaAMaisNova()
    {
        var provedorId = await CriarProvedorAsync("Provedor Ordem");
        await CriarVersaoAsync(provedorId, "A", new DateOnly(2026, 1, 10));
        await CriarVersaoAsync(provedorId, "B", verificadoEm: null);
        await CriarVersaoAsync(provedorId, "C", new DateOnly(2025, 12, 1));
        await CriarVersaoAsync(provedorId, "D", verificadoEm: null);
        await CriarVersaoAsync(provedorId, "E", new DateOnly(2026, 3, 5));

        var (_, corpo) = await ListarAsync("");

        Rotulos(corpo).Should().Equal("B", "D", "C", "A", "E");
    }

    [Fact]
    public async Task Listar_OrdemAtravessaProvedores()
    {
        var primeiro = await CriarProvedorAsync("Provedor Um");
        var segundo = await CriarProvedorAsync("Provedor Dois");
        await CriarVersaoAsync(primeiro, "P1-recente", new DateOnly(2026, 5, 1));
        await CriarVersaoAsync(segundo, "P2-antiga", new DateOnly(2026, 2, 1));
        await CriarVersaoAsync(primeiro, "P1-media", new DateOnly(2026, 3, 1));

        var (_, corpo) = await ListarAsync("");

        Rotulos(corpo).Should().Equal("P2-antiga", "P1-media", "P1-recente");
    }

    // ------------------------------------------------------------------ corte de dias

    [Fact]
    public async Task Listar_FronteiraDeNoventaDias_89Fora_90E91Dentro()
    {
        var provedorId = await CriarProvedorAsync("Provedor Fronteira");
        await CriarVersaoAsync(provedorId, "89 dias", new DateOnly(2026, 7, 8));
        await CriarVersaoAsync(provedorId, "90 dias", new DateOnly(2026, 7, 7));
        await CriarVersaoAsync(provedorId, "91 dias", new DateOnly(2026, 7, 6));

        var (_, corpo) = await ListarAsync("?dias=90");

        Rotulos(corpo).Should().Equal("91 dias", "90 dias");
        Dias(corpo).Should().Equal(91, 90);
        corpo.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Listar_SemInformarDias_UsaNoventa()
    {
        var provedorId = await CriarProvedorAsync("Provedor Padrao");
        await CriarVersaoAsync(provedorId, "89 dias", new DateOnly(2026, 7, 8));
        await CriarVersaoAsync(provedorId, "90 dias", new DateOnly(2026, 7, 7));

        var (_, corpo) = await ListarAsync("");

        Rotulos(corpo).Should().Equal("90 dias");
    }

    [Fact]
    public async Task Listar_UmDia_VerificadaOntemEstaVencida_VerificadaHojeEOFuturoNao()
    {
        var provedorId = await CriarProvedorAsync("Provedor Um Dia");
        await CriarVersaoAsync(provedorId, "ontem", new DateOnly(2026, 10, 4));
        await CriarVersaoAsync(provedorId, "hoje", new DateOnly(2026, 10, 5));
        await CriarVersaoAsync(provedorId, "amanha", new DateOnly(2026, 10, 6));

        var (_, corpo) = await ListarAsync("?dias=1");

        Rotulos(corpo).Should().Equal("ontem");
        Dias(corpo).Should().Equal(1);
    }

    [Fact]
    public async Task Listar_HojeSegueOCalendarioDoBrasil_NaoOUtc()
    {
        // 01:30 UTC de 06/10 ainda é 22:30 de 05/10 em Brasília: "hoje" é 05/10.
        using var fabrica = ComRelogio(new DateTimeOffset(2026, 10, 6, 1, 30, 0, TimeSpan.Zero));
        using var cliente = fabrica.CreateClient();
        var provedorId = await CriarProvedorAsync("Provedor Fuso");
        await CriarVersaoAsync(provedorId, "07 de julho", new DateOnly(2026, 7, 7));

        var (statusForaDoCorte, foraDoCorte) = await ListarAsync("?dias=91", cliente);
        var (statusNoCorte, noCorte) = await ListarAsync("?dias=90", cliente);

        statusForaDoCorte.Should().Be(HttpStatusCode.OK);
        Rotulos(foraDoCorte).Should().BeEmpty(because: "no calendário do Brasil são 90 dias, não 91");
        statusNoCorte.Should().Be(HttpStatusCode.OK);
        Rotulos(noCorte).Should().Equal("07 de julho");
        Dias(noCorte).Should().Equal(90);
    }

    // ------------------------------------------------------------------ o que não entra

    [Fact]
    public async Task Listar_VersaoRemovida_NaoAparece()
    {
        var provedorId = await CriarProvedorAsync("Provedor Versao Removida");
        await CriarVersaoAsync(provedorId, "ativa", verificadoEm: null);
        await CriarVersaoAsync(provedorId, "removida", verificadoEm: null, removida: true);

        var (_, corpo) = await ListarAsync("");

        Rotulos(corpo).Should().Equal("ativa");
        corpo.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Listar_ProvedorRemovido_NaoMostraSuasVersoes()
    {
        var removido = await CriarProvedorAsync("Provedor Removido", removido: true);
        var ativo = await CriarProvedorAsync("Provedor Ativo");
        await CriarVersaoAsync(removido, "do removido", verificadoEm: null);
        await CriarVersaoAsync(ativo, "do ativo", verificadoEm: null);

        var (_, corpo) = await ListarAsync("");

        Rotulos(corpo).Should().Equal("do ativo");
        corpo.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Listar_ProvedorEVersaoNaoPublicados_Entram()
    {
        var rascunho = await CriarProvedorAsync("Provedor Rascunho", publicado: false);
        await CriarVersaoAsync(rascunho, "v rascunho", verificadoEm: null, publicado: false);

        var (_, corpo) = await ListarAsync("");

        Rotulos(corpo).Should().Equal("v rascunho");
    }

    // ------------------------------------------------------------------ paginação

    [Fact]
    public async Task Listar_Paginacao_EcoaOQueOServicoServiu_EPaginaAlemDoAcervoVem200Vazia()
    {
        var provedorId = await CriarProvedorAsync("Provedor Paginas");
        for (var i = 1; i <= 5; i++)
            await CriarVersaoAsync(provedorId, $"v{i}", new DateOnly(2026, 1, i));

        var (_, primeira) = await ListarAsync("?pagina=1&tamanhoPagina=2");
        var (_, ultima) = await ListarAsync("?pagina=3&tamanhoPagina=2");
        var (statusAlem, alem) = await ListarAsync("?pagina=2000000000&tamanhoPagina=200");
        var (_, limitada) = await ListarAsync("?tamanhoPagina=999999");

        Rotulos(primeira).Should().Equal("v1", "v2");
        primeira.GetProperty("totalCount").GetInt32().Should().Be(5);
        primeira.GetProperty("totalPages").GetInt32().Should().Be(3);
        Rotulos(ultima).Should().Equal("v5");
        statusAlem.Should().Be(HttpStatusCode.OK, because: "página além do acervo é resultado válido");
        Rotulos(alem).Should().BeEmpty();
        limitada.GetProperty("pageSize").GetInt32().Should().Be(200, because: "o teto de página é aplicado");
    }

    [Fact]
    public async Task Listar_ComEmpateNaOrdenacao_PaginasNaoRepetemNemPerdemLinhas_MesmoComOHeapMutado()
    {
        const int tamanhoPagina = 3;
        var provedorId = await CriarProvedorAsync("Provedor Empate");
        var mesmaData = new DateOnly(2026, 2, 2);
        var esperados = new List<string>();

        for (var i = 1; i <= 4; i++)
        {
            await CriarVersaoAsync(provedorId, $"nunca {i:00}", verificadoEm: null);
            esperados.Add($"nunca {i:00}");
        }

        for (var i = 1; i <= 20; i++)
        {
            await CriarVersaoAsync(provedorId, $"empatada {i:00}", mesmaData);
            esperados.Add($"empatada {i:00}");
        }

        var lidos = new List<string>();
        for (var pagina = 1; pagina <= 8; pagina++)
        {
            var (_, corpo) = await ListarAsync($"?pagina={pagina}&tamanhoPagina={tamanhoPagina}");
            lidos.AddRange(Rotulos(corpo));

            // O UPDATE reescreve as tuplas, da última para a primeira: a ordem física muda entre uma
            // página e a seguinte, e só uma chave única no ORDER BY mantém a ordem total.
            await ExecutarSqlAsync(
                "DO $$ DECLARE v int; BEGIN FOR v IN SELECT versaoprovedorid FROM codex.versoesprovedor ORDER BY versaoprovedorid DESC "
                + "LOOP UPDATE codex.versoesprovedor SET introducao = coalesce(introducao, '') || '.' WHERE versaoprovedorid = v; END LOOP; END $$");
        }

        lidos.Should().Equal(esperados, because: "sem desempate por chave única o OFFSET/LIMIT repete uma linha e some com outra");
    }

    // ------------------------------------------------------------------ validação

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("3651")]
    [InlineData("2147483647")]
    public async Task Listar_DiasForaDaFaixa_Devolve422(string dias)
    {
        var (status, corpo) = await ListarAsync($"?dias={dias}");

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        var erro = corpo.GetProperty("error");
        erro.GetProperty("code").GetString().Should().Be("VALIDACAO_NEGOCIO");
        erro.GetProperty("message").GetString().Should().Contain("dias").And.Contain("3650");
        corpo.GetRawText().Should().NotContain("at Codex.").And.NotContain("Npgsql").And.NotContain("_codex_");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("3650")]
    public async Task Listar_DiasNosLimitesDaFaixa_Devolve200(string dias)
    {
        var (status, _) = await ListarAsync($"?dias={dias}");

        status.Should().Be(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------ autenticação e autorização

    [Fact]
    public async Task Listar_SemToken_Devolve401()
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, Rota);

        using var resposta = await _cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("versoesprovedor-atualizar")]
    [InlineData("lacunasbusca-buscar")]
    [InlineData("erros-buscar")]
    public async Task Listar_ComRoleErrada_Devolve403(string role)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, Rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, role);

        using var resposta = await _cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ infraestrutura do teste

    private WebApplicationFactory<Program> ComRelogio(DateTimeOffset agora) =>
        _fabricaBase.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(new RelogioFixo(agora));
        }));

    private async Task GarantirBancoAsync()
    {
        await TravaDoBanco.WaitAsync();
        try
        {
            if (_connectionStringDoBanco is null)
            {
                _connectionStringDoBanco = await _fixture.CriarBancoVazioAsync($"codex_verif_venc_{Guid.NewGuid():N}"[..40]);

                await using var contexto = PostgresContainerFixture.CriarContexto(_connectionStringDoBanco);
                var layout = VersaoLayout.Criar("Layout verificacoes vencidas", 1);
                contexto.VersoesLayout.Add(layout);
                await contexto.SaveChangesAsync();
                _versaoLayoutId = layout.Id;
            }
        }
        finally
        {
            TravaDoBanco.Release();
        }

        _connectionString = _connectionStringDoBanco;
    }

    private async Task<int> CriarProvedorAsync(string nome, bool publicado = true, bool removido = false)
    {
        await using var contexto = PostgresContainerFixture.CriarContexto(_connectionString);
        var provedor = Provedor.Criar(nome, $"INV-{Guid.NewGuid():N}"[..20], CategoriaProvedor.Nacional);
        if (publicado)
            provedor.Publicar();
        if (removido)
            provedor.Desativar();

        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        return provedor.Id;
    }

    private async Task<int> CriarVersaoAsync(
        int provedorId, string rotulo, DateOnly? verificadoEm, bool publicado = true, bool removida = false)
    {
        await using var contexto = PostgresContainerFixture.CriarContexto(_connectionString);
        var versao = VersaoProvedor.Criar(provedorId, _versaoLayoutId, rotulo);
        if (publicado)
            versao.Publicar();
        if (verificadoEm is { } data)
            versao.RegistrarVerificacao(data);
        if (removida)
            versao.Desativar();

        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return versao.Id;
    }

    private Task<(HttpStatusCode Status, JsonElement Corpo)> ListarAsync(string consulta) => ListarAsync(consulta, _cliente);

    private static async Task<(HttpStatusCode Status, JsonElement Corpo)> ListarAsync(string consulta, HttpClient cliente)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, Rota + consulta);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RoleLeitura);

        using var resposta = await cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone());
    }

    private static List<string> Rotulos(JsonElement corpo) =>
        corpo.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("rotulo").GetString()!).ToList();

    private static List<int> Dias(JsonElement corpo) =>
        corpo.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("diasDesdeVerificacao").GetInt32()).ToList();

    private async Task ExecutarSqlAsync(string sql)
    {
        await using var conexao = new NpgsqlConnection(_connectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    private sealed class RelogioFixo : TimeProvider
    {
        private readonly DateTimeOffset _agora;

        public RelogioFixo(DateTimeOffset agora)
        {
            _agora = agora;
        }

        public override DateTimeOffset GetUtcNow() => _agora;
    }
}
