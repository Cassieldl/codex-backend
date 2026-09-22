using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// P4 + decisão C5 (003/§5.5) medidos pela FRONTEIRA que o leitor da página enxerga.
///
/// <para>
/// A parte que só o HTTP prova: a <c>ETag</c> de P4 é derivada do <c>xmin</c> da
/// <c>VersaoProvedor</c>, e exemplo é FILHO dela. Sem o "toque" no pai a cada escrita de exemplo,
/// a página pública continuaria respondendo <c>304</c> com o conteúdo velho — o cadastro parece
/// ter funcionado na área interna e nunca aparece para o leitor. Por isso cada caso lê a
/// <c>ETag</c> real da resposta anterior e devolve em <c>If-None-Match</c>, em vez de conferir
/// campo de objeto.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ExemplosNaPaginaVersaoHttpTests : IAsyncLifetime
{
    private const string RolesDeExemplo =
        "exemplos-buscar,exemplos-adicionar,exemplos-atualizar,exemplos-remover";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ExemplosNaPaginaVersaoHttpTests(PostgresContainerFixture fixture)
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

    private sealed record Cenario(int VersaoId, string RotaPublica);

    [Fact]
    public async Task P4_VersaoSemExemplos_TrazListaVaziaNuncaAusente()
    {
        var cenario = await SemearVersaoPublicadaAsync();

        var (status, corpo, _) = await ObterPaginaAsync(cenario.RotaPublica);

        status.Should().Be(HttpStatusCode.OK);
        var exemplos = corpo.GetProperty("data").GetProperty("exemplos");
        exemplos.ValueKind.Should().Be(JsonValueKind.Array, because: "U9 — sempre presente, [] quando não há");
        exemplos.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task P4_ExemploCriado_ApareceComSlugNomeDescricaoEXml()
    {
        var cenario = await SemearVersaoPublicadaAsync();
        await CriarExemploAsync(cenario.VersaoId, "NF-e de Serviço Padrão", "<nfe>ok</nfe>", "Cenário feliz.");

        var (_, corpo, _) = await ObterPaginaAsync(cenario.RotaPublica);

        var exemplo = corpo.GetProperty("data").GetProperty("exemplos").EnumerateArray().Single();
        exemplo.GetProperty("slug").GetString().Should().Be("nf-e-de-servico-padrao");
        exemplo.GetProperty("nome").GetString().Should().Be("NF-e de Serviço Padrão");
        exemplo.GetProperty("descricao").GetString().Should().Be("Cenário feliz.");
        exemplo.GetProperty("xml").GetString().Should().Be("<nfe>ok</nfe>");
    }

    /// <summary>
    /// Ordem estável entre requisições — a âncora <c>#exemplo-{slug}</c> é copiada para um chamado
    /// e a página não pode reembaralhar os cenários a cada <c>UPDATE</c> no heap. Três itens, com
    /// a ordem esperada distinta da alfabética.
    /// </summary>
    [Fact]
    public async Task P4_Exemplos_SaemNaOrdemDeCriacao()
    {
        var cenario = await SemearVersaoPublicadaAsync();
        await CriarExemploAsync(cenario.VersaoId, "Zebra", "<a/>");
        await CriarExemploAsync(cenario.VersaoId, "Alfa", "<b/>");
        await CriarExemploAsync(cenario.VersaoId, "Meio", "<c/>");

        var (_, corpo, _) = await ObterPaginaAsync(cenario.RotaPublica);

        corpo.GetProperty("data").GetProperty("exemplos").EnumerateArray()
            .Select(e => e.GetProperty("nome").GetString())
            .Should().ContainInOrder("Zebra", "Alfa", "Meio");
    }

    [Fact]
    public async Task P4_ExemploRemovido_SomeDaPaginaPublica()
    {
        var cenario = await SemearVersaoPublicadaAsync();
        var exemplo = await CriarExemploAsync(cenario.VersaoId, "Some depois", "<x/>");

        var (statusRemocao, _, _) = await EnviarInternoAsync(
            HttpMethod.Delete, $"/api/v1/interno/exemplos/{exemplo.GetProperty("id").GetInt32()}",
            ifMatch: exemplo.GetProperty("versaoConcorrencia").GetUInt32());
        statusRemocao.Should().Be(HttpStatusCode.NoContent);

        var (_, corpo, _) = await ObterPaginaAsync(cenario.RotaPublica);
        corpo.GetProperty("data").GetProperty("exemplos").EnumerateArray().Should().BeEmpty();
    }

    // ------------------------------------------------------------------ C5 — invalidação da ETag

    [Fact]
    public async Task C5_CriarExemplo_InvalidaAETagDaPaginaDaVersao()
    {
        var cenario = await SemearVersaoPublicadaAsync();

        var etagInicial = await LerETagAsync(cenario.RotaPublica);
        (await ObterPaginaAsync(cenario.RotaPublica, etagInicial)).Status.Should().Be(HttpStatusCode.NotModified,
            because: "sem nenhuma escrita, a mesma ETag ainda vale — é o controle positivo do caso");

        await CriarExemploAsync(cenario.VersaoId, "Novo cenário", "<x/>");

        var (status, corpo, _) = await ObterPaginaAsync(cenario.RotaPublica, etagInicial);

        status.Should().Be(HttpStatusCode.OK,
            because: "criar exemplo toca o xmin da VersaoProvedor pai (C5) — a ETag antiga precisa deixar de valer");
        corpo.GetProperty("data").GetProperty("exemplos").EnumerateArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task C5_EditarExemplo_InvalidaAETagDaPaginaDaVersao()
    {
        var cenario = await SemearVersaoPublicadaAsync();
        var exemplo = await CriarExemploAsync(cenario.VersaoId, "Antes da edição", "<antes/>");

        var etagAntes = await LerETagAsync(cenario.RotaPublica);

        var (statusEdicao, _, _) = await EnviarInternoAsync(
            HttpMethod.Put, $"/api/v1/interno/exemplos/{exemplo.GetProperty("id").GetInt32()}",
            new { nome = "Depois da edição", descricao = (string?)null, xml = "<depois/>" },
            exemplo.GetProperty("versaoConcorrencia").GetUInt32());
        statusEdicao.Should().Be(HttpStatusCode.OK);

        var (status, corpo, _) = await ObterPaginaAsync(cenario.RotaPublica, etagAntes);

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("exemplos").EnumerateArray().Single()
            .GetProperty("xml").GetString().Should().Be("<depois/>");
    }

    [Fact]
    public async Task C5_RemoverExemplo_InvalidaAETagDaPaginaDaVersao()
    {
        var cenario = await SemearVersaoPublicadaAsync();
        var exemplo = await CriarExemploAsync(cenario.VersaoId, "Some e invalida", "<x/>");

        var etagAntes = await LerETagAsync(cenario.RotaPublica);

        var (statusRemocao, _, _) = await EnviarInternoAsync(
            HttpMethod.Delete, $"/api/v1/interno/exemplos/{exemplo.GetProperty("id").GetInt32()}",
            ifMatch: exemplo.GetProperty("versaoConcorrencia").GetUInt32());
        statusRemocao.Should().Be(HttpStatusCode.NoContent);

        var (status, corpo, _) = await ObterPaginaAsync(cenario.RotaPublica, etagAntes);

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("exemplos").EnumerateArray().Should().BeEmpty();
    }

    // ------------------------------------------------------------------ helpers

    private async Task<string> LerETagAsync(string rotaPublica)
    {
        var (status, _, resposta) = await ObterPaginaAsync(rotaPublica);

        status.Should().Be(HttpStatusCode.OK);
        var etag = resposta.Headers.ETag?.ToString();
        etag.Should().NotBeNullOrEmpty(because: "P4 publica ETag derivada do xmin da versão");

        return etag!;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> ObterPaginaAsync(
        string rotaPublica, string? ifNoneMatch = null)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rotaPublica);

        // Verbatim: o valor vai exatamente como o cliente o leu, sem o HttpClient reformatar.
        if (ifNoneMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }

    private async Task<JsonElement> CriarExemploAsync(int versaoId, string nome, string xml, string? descricao = null)
    {
        var (status, corpo, _) = await EnviarInternoAsync(
            HttpMethod.Post, $"/api/v1/interno/versoes/{versaoId}/exemplos", new { nome, descricao, xml });

        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data").Clone();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarInternoAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeExemplo);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }

    private async Task<Cenario> SemearVersaoPublicadaAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdemAtual = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout p4ex {sufixo}", maiorOrdemAtual + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor p4ex {sufixo}", $"P4E-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versão p4ex {sufixo}");
        versao.Publicar();
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return new Cenario(versao.Id, $"/api/v1/provedores/{provedor.Slug}/versoes/{versao.Slug}");
    }
}
