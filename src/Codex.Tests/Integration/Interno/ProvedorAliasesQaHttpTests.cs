using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Casos complementares a <c>ProvedorAliasesHttpTests</c> e
/// <c>ProvedorAliasesValidacaoHttpTests</c>: a sequência real do defeito (GET → PUT com o corpo
/// devolvido → banco) para conjunto igual/trocado/repetido em série, a conferência das LINHAS do banco
/// (identidade e <c>desativadoem</c>, não só o texto), o efeito na busca PÚBLICA, a troca de slug
/// preservando os aliases e os 422 de alias nulo/em branco.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ProvedorAliasesQaHttpTests : IAsyncLifetime
{
    private const string Roles =
        "provedores-buscar,provedores-adicionar,provedores-atualizar,provedores-publicar,indicebusca-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ProvedorAliasesQaHttpTests(PostgresContainerFixture fixture)
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

    // ------------------------------------------------------------------ round-trip: banco

    [Fact]
    public async Task GetPutComOMesmoConjunto_NaoDuplicaENaoDeixaLinhaAntigaViva()
    {
        var provedor = await CriarProvedorAsync(["ADN", "Sefin"]);
        var linhasAntes = await LinhasNoBancoAsync(provedor.Id);

        var (_, corpoGet, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores/{provedor.Id}");
        var detalhe = corpoGet.GetProperty("data");

        var (statusPut, corpoPut, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}",
            new
            {
                nome = detalhe.GetProperty("nome").GetString(),
                categoria = detalhe.GetProperty("categoria").GetString(),
                aliases = AliasesDe(detalhe),
            },
            detalhe.GetProperty("versaoConcorrencia").GetUInt32());

        statusPut.Should().Be(HttpStatusCode.OK, corpoPut.ToString());

        var linhasDepois = await LinhasNoBancoAsync(provedor.Id);
        linhasDepois.Select(l => l.Alias).Should().BeEquivalentTo(["ADN", "Sefin"],
            because: "salvar o formulário sem mexer nos aliases não pode duplicá-los");
        linhasDepois.Should().OnlyContain(l => l.DesativadoEm == null,
            because: "o PUT remove de verdade — linha desativada ainda reservaria o UNIQUE e acumularia");
        linhasDepois.Select(l => l.Id).Should().NotIntersectWith(linhasAntes.Select(l => l.Id),
            because: "as linhas antigas saíram e as novas entraram; nenhuma linha antiga sobrou");
    }

    [Fact]
    public async Task PutsSucessivosComConjuntosDiferentes_SempreDeixamSoOUltimoConjunto()
    {
        var provedor = await CriarProvedorAsync(["A1", "A2", "A3"]);
        var versao = provedor.Versao;

        foreach (var conjunto in new[] { new[] { "B1" }, new[] { "C1", "C2", "C3", "C4" }, new[] { "A1", "C1" } })
        {
            var (status, corpo, _) = await AtualizarAsync(provedor.Id, provedor.Nome, conjunto, versao);
            status.Should().Be(HttpStatusCode.OK, corpo.ToString());
            versao = corpo.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

            (await LinhasNoBancoAsync(provedor.Id)).Select(l => l.Alias).Should().BeEquivalentTo(conjunto,
                because: "o defeito original era o acúmulo: cada PUT somava aos anteriores");
        }
    }

    /// <summary>
    /// QA-R6-01: editar SÓ os aliases não toca a linha do provedor, então o <c>xmin</c> (token de
    /// concorrência) não muda e um segundo editor com o token antigo sobrescreve o primeiro em
    /// silêncio. Mesma família de "inserir filho não altera a versão do pai".
    /// </summary>
    [Fact]
    public async Task PutSoDeAliases_MudaOTokenDeConcorrencia_EUmPutComTokenAntigoDevolve409()
    {
        var provedor = await CriarProvedorAsync(["ADN"]);

        var (statusPrimeiro, corpoPrimeiro, _) = await AtualizarAsync(provedor.Id, provedor.Nome, ["Primeiro"], provedor.Versao);
        statusPrimeiro.Should().Be(HttpStatusCode.OK);
        corpoPrimeiro.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32().Should().NotBe(provedor.Versao,
            because: "os aliases mudaram — o token que o cliente reenvia no próximo PUT tem de mudar junto");

        var (status, corpo, _) = await AtualizarAsync(provedor.Id, provedor.Nome, ["Segundo"], provedor.Versao);

        status.Should().Be(HttpStatusCode.Conflict, corpo.ToString());
        (await LinhasNoBancoAsync(provedor.Id)).Select(l => l.Alias).Should().BeEquivalentTo(["Primeiro"],
            because: "a recusa por conflito é anterior a qualquer remoção de alias");
    }

    // ------------------------------------------------------------------ validação extra

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Atualizar_ComAliasNuloOuEmBranco_Devolve422EMantemOsAliasesAtuais(string? aliasInvalido)
    {
        var provedor = await CriarProvedorAsync(["ADN"]);

        var (status, corpo, _) = await AtualizarAsync(provedor.Id, provedor.Nome, ["Valido", aliasInvalido], provedor.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity, corpo.ToString());
        (await LinhasNoBancoAsync(provedor.Id)).Select(l => l.Alias).Should().BeEquivalentTo(["ADN"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Criar_ComAliasNuloOuEmBranco_Devolve422(string? aliasInvalido)
    {
        var sufixo = NovoSufixo();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome = $"Provedor alias invalido {sufixo}",
            identificadorInvoicy = $"INV-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases = new string?[] { "Valido", aliasInvalido },
        });

        status.Should().Be(HttpStatusCode.UnprocessableEntity, corpo.ToString());
    }

    // ------------------------------------------------------------------ busca

    [Fact]
    public async Task Put_TrocandoAlias_NaBuscaPublica_OAliasRemovidoNaoAchaMaisEONovoAcha()
    {
        var sufixo = NovoSufixo();
        var aliasVelho = $"apelidopubvelho{sufixo}";
        var aliasNovo = $"apelidopubnovo{sufixo}";
        var provedor = await CriarProvedorAsync([aliasVelho]);
        var versao = await PublicarAsync(provedor.Id, provedor.Versao);

        (await TitulosNaBuscaPublicaAsync(aliasVelho)).Should().Contain(provedor.Nome,
            because: "controle positivo: provedor publicado é achado pelo alias na busca pública");

        var (status, corpo, _) = await AtualizarAsync(provedor.Id, provedor.Nome, [aliasNovo], versao);
        status.Should().Be(HttpStatusCode.OK, corpo.ToString());

        (await TitulosNaBuscaPublicaAsync(aliasNovo)).Should().Contain(provedor.Nome,
            because: "a edição não pode tirar o provedor publicado da busca pública");
        (await TitulosNaBuscaPublicaAsync(aliasVelho)).Should().NotContain(provedor.Nome,
            because: "o alias removido alimentava o peso B — continuar achando é o defeito original");
    }

    [Fact]
    public async Task Put_RemovendoTodosOsAliases_OProvedorDeixaDeSerAchadoPorEles()
    {
        var sufixo = NovoSufixo();
        var alias = $"apelidounico{sufixo}";
        var provedor = await CriarProvedorAsync([alias]);

        (await TitulosNaBuscaInternaAsync(alias)).Should().Contain(provedor.Nome);

        var (status, corpo, _) = await AtualizarAsync(provedor.Id, provedor.Nome, [], provedor.Versao);
        status.Should().Be(HttpStatusCode.OK, corpo.ToString());

        (await TitulosNaBuscaInternaAsync(alias)).Should().NotContain(provedor.Nome);
    }

    // ------------------------------------------------------------------ troca de slug

    [Fact]
    public async Task TrocarSlug_PreservaAsLinhasDeAliasEDevolveOsAliasesNaResposta()
    {
        var sufixo = NovoSufixo();
        var alias = $"apelidoslug{sufixo}";
        var provedor = await CriarProvedorAsync([alias, "Segundo"]);
        var linhasAntes = await LinhasNoBancoAsync(provedor.Id);

        var slugNovo = $"slug-aliases-{sufixo}";
        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}/slug", new { slug = slugNovo }, provedor.Versao);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        AliasesDe(corpo.GetProperty("data")).Should().BeEquivalentTo([alias, "Segundo"]);

        var linhasDepois = await LinhasNoBancoAsync(provedor.Id);
        linhasDepois.Select(l => l.Id).Should().BeEquivalentTo(linhasAntes.Select(l => l.Id),
            because: "trocar o slug não toca nos aliases — as MESMAS linhas continuam lá");

        var (_, corpoGet, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores/{provedor.Id}");
        AliasesDe(corpoGet.GetProperty("data")).Should().BeEquivalentTo([alias, "Segundo"]);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record ProvedorCriado(int Id, string Nome, uint Versao);

    private sealed record LinhaDeAlias(int Id, string Alias, DateTimeOffset? DesativadoEm);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static string[] AliasesDe(JsonElement dados) =>
        dados.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()!).ToArray();

    private async Task<ProvedorCriado> CriarProvedorAsync(string[] aliases)
    {
        var sufixo = NovoSufixo();
        var nome = $"Provedor qa alias {sufixo}";

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome,
            identificadorInvoicy = $"INV-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases,
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        var criado = corpo.GetProperty("data");

        return new ProvedorCriado(criado.GetProperty("id").GetInt32(), nome, criado.GetProperty("versaoConcorrencia").GetUInt32());
    }

    private async Task<uint> PublicarAsync(int id, uint versao)
    {
        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}/publicacao", new { publicado = true }, versao);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        return corpo.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();
    }

    private Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> AtualizarAsync(
        int id, string nome, string?[] aliases, uint versao) =>
        EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}",
            new { nome, categoria = nameof(CategoriaProvedor.Nacional), aliases },
            versao);

    /// <summary>Todas as linhas, SEM filtro de <c>desativadoem</c> — é o que o UNIQUE enxerga.</summary>
    private async Task<LinhaDeAlias[]> LinhasNoBancoAsync(int provedorId)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.AliasesProvedor.AsNoTracking()
            .Where(a => a.ProvedorId == provedorId)
            .Select(a => new LinhaDeAlias(a.Id, a.Alias, a.DesativadoEm))
            .ToArrayAsync();
    }

    private Task<string?[]> TitulosNaBuscaInternaAsync(string termo) =>
        TitulosDeBuscaAsync($"/api/v1/interno/busca?q={Uri.EscapeDataString(termo)}&tamanhoPagina=50", autenticado: true);

    private Task<string?[]> TitulosNaBuscaPublicaAsync(string termo) =>
        TitulosDeBuscaAsync($"/api/v1/busca?q={Uri.EscapeDataString(termo)}&tamanhoPagina=50", autenticado: false);

    private async Task<string?[]> TitulosDeBuscaAsync(string rota, bool autenticado)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, rota, autenticado: autenticado);
        status.Should().Be(HttpStatusCode.OK, corpo.ToString());

        return corpo.GetProperty("data").GetProperty("grupos").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("itens").EnumerateArray())
            .Select(item => item.GetProperty("titulo").GetString())
            .ToArray();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null, bool autenticado = true)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);

        if (autenticado)
            requisicao.Headers.Add(TestAuthHandler.HeaderRoles, Roles);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }
}
