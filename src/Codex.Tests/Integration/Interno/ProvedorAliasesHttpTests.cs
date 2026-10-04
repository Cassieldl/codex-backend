using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// R6 (demanda 008) — aliases do provedor pela HTTP real. Dois defeitos juntos:
/// <c>GET /interno/provedores/{id}</c> devolvia <c>aliases: []</c> com aliases no banco (o
/// <c>ObterPorIdAsync</c> nunca os incluía), e o PUT inseria os novos sem apagar os antigos
/// (<c>SubstituirAliases</c> operava sobre uma coleção não carregada). Incluir só na leitura teria
/// trocado o acúmulo silencioso por <c>500</c>: <c>Clear()</c> numa coleção carregada com FK
/// obrigatória e <c>OnDelete(Restrict)</c> — o mesmo defeito do PUT de erro.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ProvedorAliasesHttpTests : IAsyncLifetime
{
    private const string Roles =
        "provedores-buscar,provedores-adicionar,provedores-atualizar,provedores-publicar,indicebusca-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ProvedorAliasesHttpTests(PostgresContainerFixture fixture)
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

    [Fact]
    public async Task GetPutComOCorpoDevolvido_TrocandoAliases_DeixaExatamenteOsNovosNoBanco()
    {
        var provedor = await CriarProvedorAsync(["ADN", "Sefin"]);

        // 1. O formulário abre: o GET tem de trazer os aliases que estão no banco.
        var (statusGet, corpoGet, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores/{provedor.Id}");
        statusGet.Should().Be(HttpStatusCode.OK);
        var detalhe = corpoGet.GetProperty("data");
        AliasesDe(detalhe).Should().BeEquivalentTo(["ADN", "Sefin"],
            because: "o GET devolvia `aliases: []` com aliases no banco");

        // 2. Salva com o corpo montado a partir do que o GET devolveu, trocando um alias (o "ADN"
        // se repete de propósito: a linha antiga sai e uma nova entra sob o mesmo UNIQUE).
        var (statusPut, corpoPut, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}",
            new
            {
                nome = detalhe.GetProperty("nome").GetString(),
                categoria = detalhe.GetProperty("categoria").GetString(),
                aliases = new[] { "ADN", "Nacional" },
            },
            detalhe.GetProperty("versaoConcorrencia").GetUInt32());

        statusPut.Should().Be(HttpStatusCode.OK, corpoPut.ToString());
        AliasesDe(corpoPut.GetProperty("data")).Should().BeEquivalentTo(["ADN", "Nacional"]);

        // 3. No BANCO — sem acúmulo (nenhuma linha antiga sobrando, ativa ou não).
        (await AliasesNoBancoAsync(provedor.Id)).Should().BeEquivalentTo(["ADN", "Nacional"],
            because: "o PUT inseria os novos e nunca apagava os antigos");

        var (_, corpoFinal, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores/{provedor.Id}");
        AliasesDe(corpoFinal.GetProperty("data")).Should().BeEquivalentTo(["ADN", "Nacional"]);
    }

    [Fact]
    public async Task Put_ComListaVaziaDeAliases_RemoveTodos()
    {
        var provedor = await CriarProvedorAsync(["ADN", "Sefin"]);

        var (statusPut, corpoPut, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}",
            new { nome = provedor.Nome, categoria = nameof(CategoriaProvedor.Nacional), aliases = Array.Empty<string>() },
            provedor.Versao);

        statusPut.Should().Be(HttpStatusCode.OK, corpoPut.ToString());
        (await AliasesNoBancoAsync(provedor.Id)).Should().BeEmpty(
            because: "aliases são opcionais — lista vazia é um conjunto válido e substitui o anterior");

        var (_, corpoGet, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores/{provedor.Id}");
        AliasesDe(corpoGet.GetProperty("data")).Should().BeEmpty();
    }

    [Fact]
    public async Task Put_TrocandoAlias_ReindexaEORemovidoDeixaDeAcharOProvedorNaBuscaInterna()
    {
        var sufixo = NovoSufixo();
        var aliasVelho = $"apelidovelho{sufixo}";
        var aliasNovo = $"apelidonovo{sufixo}";
        var provedor = await CriarProvedorAsync([aliasVelho]);

        (await TitulosNaBuscaInternaAsync(aliasVelho)).Should().Contain(provedor.Nome,
            because: "controle positivo: o alias alimenta o peso B do índice e acha o provedor");

        var (statusPut, corpoPut, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}",
            new { nome = provedor.Nome, categoria = nameof(CategoriaProvedor.Nacional), aliases = new[] { aliasNovo } },
            provedor.Versao);
        statusPut.Should().Be(HttpStatusCode.OK, corpoPut.ToString());

        (await TitulosNaBuscaInternaAsync(aliasNovo)).Should().Contain(provedor.Nome);
        (await TitulosNaBuscaInternaAsync(aliasVelho)).Should().NotContain(provedor.Nome,
            because: "o alias removido não pode continuar achando o provedor");
    }

    /// <summary>
    /// Publicar/remover carregam o provedor pelo mesmo <c>ObterPorIdAsync</c> que agora inclui os
    /// aliases: nenhum desses fluxos toca na coleção, então os aliases ficam intactos no banco e a
    /// resposta da publicação — que antes os devolvia vazios — passa a trazê-los.
    /// </summary>
    [Fact]
    public async Task Publicar_NaoAlteraOsAliasesEDevolveOsExistentes()
    {
        var provedor = await CriarProvedorAsync(["ADN", "Sefin"]);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{provedor.Id}/publicacao", new { publicado = true }, provedor.Versao);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        AliasesDe(corpo.GetProperty("data")).Should().BeEquivalentTo(["ADN", "Sefin"]);
        (await AliasesNoBancoAsync(provedor.Id)).Should().BeEquivalentTo(["ADN", "Sefin"]);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record ProvedorCriado(int Id, string Nome, uint Versao);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static string?[] AliasesDe(JsonElement dados) =>
        dados.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()).ToArray();

    private async Task<ProvedorCriado> CriarProvedorAsync(string[] aliases)
    {
        var sufixo = NovoSufixo();
        var nome = $"Provedor aliases {sufixo}";

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

    private async Task<string[]> AliasesNoBancoAsync(int provedorId)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.AliasesProvedor.AsNoTracking()
            .Where(a => a.ProvedorId == provedorId)
            .Select(a => a.Alias)
            .ToArrayAsync();
    }

    private async Task<string?[]> TitulosNaBuscaInternaAsync(string termo)
    {
        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Get, $"/api/v1/interno/busca?q={Uri.EscapeDataString(termo)}&tamanhoPagina=50");
        status.Should().Be(HttpStatusCode.OK);

        return corpo.GetProperty("data").GetProperty("grupos").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("itens").EnumerateArray())
            .Select(item => item.GetProperty("titulo").GetString())
            .ToArray();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
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
