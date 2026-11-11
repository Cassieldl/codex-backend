using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// PROBE DE QA — fluxo interno de escrita (I3 → I3b → I4 → I7 → I9) exercitado por HTTP REAL
/// contra Postgres real, e o efeito de cada passo observado pela busca PÚBLICA (P1).
///
/// <para>A suíte entregue nunca leva um endpoint interno de escrita até o 200/201: os testes de
/// pipeline rodam sem banco (401/403/400) e os de cascata chamam o writer/serviço direto. Este
/// probe cobre o caminho completo, incluindo <c>409</c> real de concorrência otimista.</para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class FluxoInternoHttpTests : IAsyncLifetime
{
    private const string TodasAsRoles =
        "provedores-buscar,provedores-adicionar,provedores-atualizar,provedores-remover,provedores-publicar," +
        "versoesprovedor-buscar,versoesprovedor-adicionar,versoesprovedor-atualizar,versoesprovedor-remover,versoesprovedor-publicar," +
        "avisos-adicionar,avisos-atualizar,avisos-remover,avisos-publicar," +
        "versoeslayout-buscar,versoeslayout-adicionar,versoeslayout-atualizar,versoeslayout-publicar," +
        "campolayout-buscar,campolayout-adicionar,campolayout-atualizar,campolayout-remover,campolayout-publicar," +
        "camposversao-buscar,camposversao-adicionar,camposversao-atualizar,camposversao-remover," +
        "erros-buscar,erros-adicionar,erros-atualizar,erros-remover,erros-publicar," +
        "indicebusca-buscar,indicebusca-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public FluxoInternoHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, TodasAsRoles);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }

    private async Task<JsonElement> BuscarPublicoAsync(string termo)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/busca?q={Uri.EscapeDataString(termo)}&tamanhoPagina=50");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var texto = await resposta.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone().GetProperty("data");
    }

    /// <summary>
    /// Flag <c>publicado</c> da versão na busca interna. A pública não lista versão de provedor,
    /// então é por aqui que a cascata de visibilidade da versão aparece via HTTP.
    /// </summary>
    private async Task<bool> VersaoPublicadaNaBuscaInternaAsync(string termo)
    {
        var (status, corpo) = await EnviarAsync(
            HttpMethod.Get, $"/api/v1/interno/busca?q={Uri.EscapeDataString(termo)}&tipo=versaoprovedor&tamanhoPagina=50");
        status.Should().Be(HttpStatusCode.OK);

        return ItensDe(corpo.GetProperty("data")).Should().ContainSingle().Subject.GetProperty("publicado").GetBoolean();
    }

    private static IReadOnlyList<JsonElement> ItensDe(JsonElement resultadoBusca) => resultadoBusca
        .GetProperty("grupos").EnumerateArray()
        .SelectMany(g => g.GetProperty("itens").EnumerateArray())
        .ToList();

    private async Task<(int Id, uint Versao)> CriarProvedorAsync(string nome, string identificador)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome,
            identificadorInvoicy = identificador,
            categoria = "Nacional",
            aliases = Array.Empty<string>(),
        });

        status.Should().Be(HttpStatusCode.Created, because: "I3 devolve 201 + Location");
        var dados = corpo.GetProperty("data");
        return (dados.GetProperty("id").GetInt32(), dados.GetProperty("versaoConcorrencia").GetUInt32());
    }

    private async Task<uint> PublicarProvedorAsync(int id, uint versao, bool publicado)
    {
        var (status, corpo) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}/publicacao", new { publicado }, versao);

        status.Should().Be(HttpStatusCode.OK);
        return corpo.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();
    }

    // ---------------------------------------------------------------- ciclo de publicação

    [Fact]
    public async Task CicloCompleto_CriarPublicarDespublicarRepublicar_RefleteNaBuscaPublicaACadaPasso()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var termo = $"fluxointerno{sufixo}";

        var (id, versao) = await CriarProvedorAsync($"{termo} Provedor", $"FLX-{sufixo}");

        ItensDe(await BuscarPublicoAsync(termo)).Should().BeEmpty(
            because: "I3 nunca publica — recém-criado não pode aparecer na busca pública");

        versao = await PublicarProvedorAsync(id, versao, publicado: true);
        ItensDe(await BuscarPublicoAsync(termo)).Should().HaveCount(1, because: "I3b publica e a cascata roda na mesma transação");

        versao = await PublicarProvedorAsync(id, versao, publicado: false);
        ItensDe(await BuscarPublicoAsync(termo)).Should().BeEmpty();

        await PublicarProvedorAsync(id, versao, publicado: true);
        ItensDe(await BuscarPublicoAsync(termo)).Should().HaveCount(1, because: "republicar repovoa a busca sem reconstrução manual do índice");
    }

    [Fact]
    public async Task DespublicarProvedor_ViaHttp_EscondeTambemAVersaoPublicada_ERepublicarADevolve()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var termo = $"cascatahttp{sufixo}";

        var (idProvedor, versaoProvedor) = await CriarProvedorAsync($"{termo} Provedor", $"CSC-{sufixo}");

        var (statusLayout, corpoLayout) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/layout/versoes", new
        {
            rotulo = $"Layout {sufixo}",
            descricao = (string?)null,
        });
        statusLayout.Should().Be(HttpStatusCode.Created);
        var idLayout = corpoLayout.GetProperty("data").GetProperty("id").GetInt32();

        var (statusVersao, corpoVersao) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{idProvedor}/versoes", new
        {
            versaoLayoutId = idLayout,
            rotulo = $"{termo} Versao",
            introducao = (string?)null,
        });
        statusVersao.Should().Be(HttpStatusCode.Created);
        var versaoDto = corpoVersao.GetProperty("data");
        var idVersao = versaoDto.GetProperty("id").GetInt32();
        var concorrenciaVersao = versaoDto.GetProperty("versaoConcorrencia").GetUInt32();

        versaoProvedor = await PublicarProvedorAsync(idProvedor, versaoProvedor, publicado: true);

        var (statusPubVersao, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{idVersao}/publicacao", new { publicado = true }, concorrenciaVersao);
        statusPubVersao.Should().Be(HttpStatusCode.OK);

        ItensDe(await BuscarPublicoAsync(termo)).Should().ContainSingle(because: "a busca pública não lista versão de provedor")
            .Which.GetProperty("tipo").GetString().Should().Be("provedor");
        (await VersaoPublicadaNaBuscaInternaAsync(termo)).Should().BeTrue();

        versaoProvedor = await PublicarProvedorAsync(idProvedor, versaoProvedor, publicado: false);
        ItensDe(await BuscarPublicoAsync(termo)).Should().BeEmpty();
        (await VersaoPublicadaNaBuscaInternaAsync(termo)).Should().BeFalse(
            because: "M19/M28: despublicar o provedor esconde a versão dependente mesmo ela continuando publicada");

        await PublicarProvedorAsync(idProvedor, versaoProvedor, publicado: true);
        ItensDe(await BuscarPublicoAsync(termo)).Should().ContainSingle();
        (await VersaoPublicadaNaBuscaInternaAsync(termo)).Should().BeTrue(
            because: "republicar o provedor devolve a versão que continuava publicada");
    }

    [Fact]
    public async Task RepublicarProvedor_NaoRessuscitaVersaoQueFoiDespublicadaIndividualmente()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var termo = $"naoressuscita{sufixo}";

        var (idProvedor, versaoProvedor) = await CriarProvedorAsync($"{termo} Provedor", $"NRS-{sufixo}");

        var (_, corpoLayout) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/layout/versoes", new { rotulo = $"Layout {sufixo}", descricao = (string?)null });
        var idLayout = corpoLayout.GetProperty("data").GetProperty("id").GetInt32();

        var (_, corpoVersao) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{idProvedor}/versoes", new
        {
            versaoLayoutId = idLayout,
            rotulo = $"{termo} Versao",
            introducao = (string?)null,
        });
        var idVersao = corpoVersao.GetProperty("data").GetProperty("id").GetInt32();
        var concorrenciaVersao = corpoVersao.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

        versaoProvedor = await PublicarProvedorAsync(idProvedor, versaoProvedor, publicado: true);

        var (_, corpoPub) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/versoes/{idVersao}/publicacao", new { publicado = true }, concorrenciaVersao);
        concorrenciaVersao = corpoPub.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

        // Despublica SÓ a versão, depois despublica e republica o provedor.
        await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/versoes/{idVersao}/publicacao", new { publicado = false }, concorrenciaVersao);

        versaoProvedor = await PublicarProvedorAsync(idProvedor, versaoProvedor, publicado: false);
        await PublicarProvedorAsync(idProvedor, versaoProvedor, publicado: true);

        var itens = ItensDe(await BuscarPublicoAsync(termo));
        itens.Should().HaveCount(1);
        itens[0].GetProperty("tipo").GetString().Should().Be("provedor");
        (await VersaoPublicadaNaBuscaInternaAsync(termo)).Should().BeFalse(
            because: "republicar o provedor não pode ressuscitar uma versão despublicada individualmente");
    }

    // ---------------------------------------------------------------- concorrência otimista

    [Fact]
    public async Task Publicacao_ComIfMatchDesatualizado_Retorna409ConflitoConcorrencia()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var (id, versaoInicial) = await CriarProvedorAsync($"conflito {sufixo}", $"CNF-{sufixo}");

        await PublicarProvedorAsync(id, versaoInicial, publicado: true); // consome a versão

        var (status, corpo) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}/publicacao", new { publicado = false }, versaoInicial);

        status.Should().Be(HttpStatusCode.Conflict);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("CONFLITO_CONCORRENCIA");
        corpo.GetRawText().Should().NotContain("uq_codex_").And.NotContain("at Codex.");
    }

    [Fact]
    public async Task Atualizacao_ComIfMatchDesatualizado_Retorna409_EComOAtualPassa()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var (id, versaoInicial) = await CriarProvedorAsync($"editar {sufixo}", $"EDT-{sufixo}");

        var novaVersao = await PublicarProvedorAsync(id, versaoInicial, publicado: true);

        var (statusVelho, corpoVelho) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/provedores/{id}", new
        {
            nome = $"editar renomeado {sufixo}",
            categoria = "Nacional",
            aliases = Array.Empty<string>(),
        }, versaoInicial);
        statusVelho.Should().Be(HttpStatusCode.Conflict);
        corpoVelho.GetRawText().Should().NotContain("uq_codex_").And.NotContain("at Codex.");

        var (statusNovo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/provedores/{id}", new
        {
            nome = $"editar renomeado {sufixo}",
            categoria = "Nacional",
            aliases = Array.Empty<string>(),
        }, novaVersao);
        statusNovo.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListaInterna_TrazVersaoConcorrenciaUtilizavelNoItemDeLista_AP_API_002()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var (id, _) = await CriarProvedorAsync($"lista {sufixo}", $"LST-{sufixo}");

        var (status, corpo) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores?q=lista {sufixo}&tamanhoPagina=25");
        status.Should().Be(HttpStatusCode.OK);

        var item = corpo.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == id);

        var versaoDoItemDeLista = item.GetProperty("versaoConcorrencia").GetUInt32();
        versaoDoItemDeLista.Should().NotBe(0, because: "AP-API-002: o item de lista precisa carregar o identificador de concorrência utilizável");

        // Prova de que o valor do item de lista serve mesmo para um PUT — não é enfeite.
        var (statusPut, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}/publicacao", new { publicado = true }, versaoDoItemDeLista);
        statusPut.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------- índice × edição de conteúdo

    [Fact]
    public async Task RenomearProvedorPublicado_AtualizaOTituloExibidoNoResultadoDeBusca()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var termoOriginal = $"nomeantigo{sufixo}";
        var termoNovo = $"nomenovo{sufixo}";

        var (id, versao) = await CriarProvedorAsync($"{termoOriginal} Provedor", $"RNM-{sufixo}");
        versao = await PublicarProvedorAsync(id, versao, publicado: true);

        var (statusRename, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/provedores/{id}", new
        {
            nome = $"{termoNovo} Provedor",
            categoria = "Nacional",
            aliases = Array.Empty<string>(),
        }, versao);
        statusRename.Should().Be(HttpStatusCode.OK);

        var itens = ItensDe(await BuscarPublicoAsync(termoNovo));
        itens.Should().HaveCount(1, because: "a busca precisa achar o provedor pelo nome NOVO");

        itens[0].GetProperty("titulo").GetString().Should()
            .Contain(termoNovo, because: "o título exibido no resultado de busca é o nome atual do provedor, não o nome com que ele foi criado");
    }
}
