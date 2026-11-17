using System.Net;
using System.Text;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// Mudanças estruturadas da versão do layout, gravadas pela edição interna e lidas nas rotas
/// públicas, sempre em JSON literal: enum serializado pelo próprio .NET binda mesmo com o contrato
/// errado, e só o texto cru da resposta prova que <c>tipo</c> sai como texto camelCase.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class MudancasVersaoLayoutHttpTests : IAsyncLifetime
{
    private const string MudancasJson = """
        [
          {"tipo": "grupoNovo", "depois": "//Condominio", "descricao": "Grupo do condomínio."},
          {"tipo": "renomeado", "antes": "//Servico/Valores/nDI", "depois": "//Servico/Valores/nroDUIMP", "descricao": "DI vira DUIMP."},
          {"tipo": "saiu", "antes": "//Servico/Valores/ValServEst", "descricao": "Saiu do layout."}
        ]
        """;

    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public MudancasVersaoLayoutHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();

        await using var contexto = _fixture.CriarContexto();
        contexto.VersoesLayout.RemoveRange(await contexto.VersoesLayout.Where(v => v.Rotulo.Contains(_sufixo)).ToListAsync());
        await contexto.SaveChangesAsync();
    }

    [Fact]
    public async Task LeituraPublica_TrazMudancasComTipoEmTextoCamelCase_EListaVaziaSemMudancas()
    {
        var comMudancas = await CriarAsync($$"""{"rotulo": "Com mudancas {{_sufixo}}", "mudancas": {{MudancasJson}}}""");
        var semMudancas = await CriarAsync($$"""{"rotulo": "Sem mudancas {{_sufixo}}"}""");
        await PublicarAsync(comMudancas);
        await PublicarAsync(semMudancas);

        var pagina = await LerTextoAsync($"/api/v1/layout/versoes/{comMudancas.Slug}");
        pagina.Should().Contain("\"tipo\":\"grupoNovo\"").And.Contain("\"tipo\":\"renomeado\"").And.Contain("\"tipo\":\"saiu\"");

        var mudancas = JsonDocument.Parse(pagina).RootElement.GetProperty("data").GetProperty("versao").GetProperty("mudancas");
        mudancas.EnumerateArray().Select(m => m.GetProperty("tipo").ValueKind).Should().AllBeEquivalentTo(JsonValueKind.String);
        mudancas[1].GetRawText().Should().Be(
            """{"tipo":"renomeado","antes":"//Servico/Valores/nDI","depois":"//Servico/Valores/nroDUIMP","descricao":"DI vira DUIMP."}""");
        mudancas[0].GetProperty("antes").ValueKind.Should().Be(JsonValueKind.Null);
        mudancas[2].GetProperty("depois").ValueKind.Should().Be(JsonValueKind.Null);

        var paginaSem = await LerTextoAsync($"/api/v1/layout/versoes/{semMudancas.Slug}");
        JsonDocument.Parse(paginaSem).RootElement.GetProperty("data").GetProperty("versao").GetProperty("mudancas")
            .GetRawText().Should().Be("[]");

        var indice = JsonDocument.Parse(await LerTextoAsync("/api/v1/layout")).RootElement.GetProperty("data");
        var noIndice = indice.GetProperty("versoes").EnumerateArray().Single(v => v.GetProperty("slug").GetString() == comMudancas.Slug);
        noIndice.GetProperty("mudancas").EnumerateArray().Select(m => m.GetProperty("tipo").GetString())
            .Should().Equal("grupoNovo", "renomeado", "saiu");
    }

    [Fact]
    public async Task Put_SubstituiAListaInteira_ESemOCampoMantem()
    {
        var versao = await CriarAsync($$"""{"rotulo": "Substituir {{_sufixo}}", "mudancas": {{MudancasJson}}}""");

        var (status, corpo, etag) = await AtualizarAsync(versao.Id, versao.ETag,
            """{"mudancas": [{"tipo": "alterado", "antes": "//Tomador/TomaCEP", "depois": "//Tomador/TomaCEP", "descricao": "CEP vira texto."}]}""");
        status.Should().Be(HttpStatusCode.OK, because: corpo.ToString());

        var (statusSemCampo, corpoSemCampo, _) = await AtualizarAsync(versao.Id, etag, """{"descricao": "Nota nova."}""");
        statusSemCampo.Should().Be(HttpStatusCode.OK, because: corpoSemCampo.ToString());

        foreach (var resposta in new[] { corpo, corpoSemCampo })
            resposta.GetProperty("data").GetProperty("mudancas").GetRawText().Should().Be(
                """[{"tipo":"alterado","antes":"//Tomador/TomaCEP","depois":"//Tomador/TomaCEP","descricao":"CEP vira texto."}]""");
    }

    [Theory]
    [InlineData("""{"tipo": 1, "depois": "//A", "descricao": "Número no tipo."}""")]
    [InlineData("""{"tipo": "inventado", "depois": "//A", "descricao": "Tipo que não existe."}""")]
    public async Task Put_TipoForaDoContrato_Responde400(string mudanca)
    {
        var versao = await CriarAsync($$"""{"rotulo": "Tipo invalido {{_sufixo}}"}""");

        var (status, corpo, _) = await AtualizarAsync(versao.Id, versao.ETag, $$"""{"mudancas": [{{mudanca}}]}""");

        status.Should().Be(HttpStatusCode.BadRequest, because: corpo.ToString());
    }

    [Theory]
    [InlineData("""{"tipo": "renomeado", "depois": "//B", "descricao": "Sem o caminho de antes."}""", "mudancas[0].antes")]
    [InlineData("""{"tipo": "saiu", "antes": "//A", "descricao": ""}""", "mudancas[0].descricao")]
    public async Task Put_MudancaIncompleta_Responde422NoCampo(string mudanca, string campo)
    {
        var versao = await CriarAsync($$"""{"rotulo": "Incompleta {{_sufixo}}"}""");

        var (status, corpo, _) = await AtualizarAsync(versao.Id, versao.ETag, $$"""{"mudancas": [{{mudanca}}]}""");

        status.Should().Be(HttpStatusCode.UnprocessableEntity, because: corpo.ToString());
        corpo.ToString().Should().Contain(campo);
    }

    [Theory]
    [InlineData("""{"tipo": "grupoNovo", "antes": "//Velho", "depois": "//A", "descricao": "Grupo novo com antes."}""", "mudancas[0].antes")]
    [InlineData("""{"tipo": "campoNovo", "antes": "//Velho", "depois": "//A/B", "descricao": "Campo novo com antes."}""", "mudancas[0].antes")]
    [InlineData("""{"tipo": "saiu", "antes": "//A", "depois": "//B", "descricao": "Saiu com depois."}""", "mudancas[0].depois")]
    public async Task Put_CaminhoQueOTipoNaoTem_Responde422NoCampo(string mudanca, string campo)
    {
        var versao = await CriarAsync($$"""{"rotulo": "Caminho a mais {{_sufixo}}"}""");

        var (status, corpo, _) = await AtualizarAsync(versao.Id, versao.ETag, $$"""{"mudancas": [{{mudanca}}]}""");

        status.Should().Be(HttpStatusCode.UnprocessableEntity, because: corpo.ToString());
        corpo.ToString().Should().Contain(campo);
    }

    // ---------------------------------------------------------------------------------- apoio

    private sealed record VersaoCriada(int Id, string Slug, string ETag);

    private async Task<VersaoCriada> CriarAsync(string corpoJson)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interno/layout/versoes")
        {
            Content = new StringContent(corpoJson, Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "versoeslayout-adicionar");

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, because: texto);

        var dados = JsonDocument.Parse(texto).RootElement.GetProperty("data");
        return new VersaoCriada(dados.GetProperty("id").GetInt32(), dados.GetProperty("slug").GetString()!, resposta.Headers.ETag!.ToString());
    }

    private async Task PublicarAsync(VersaoCriada versao)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/layout/versoes/{versao.Id}/publicacao")
        {
            Content = new StringContent("""{"publicado": true}""", Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "versoeslayout-publicar");
        requisicao.Headers.TryAddWithoutValidation("If-Match", versao.ETag);

        using var resposta = await _cliente.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: await resposta.Content.ReadAsStringAsync());
    }

    private async Task<string> LerTextoAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: texto);
        return texto;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, string ETag)> AtualizarAsync(int id, string etag, string corpoJson)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/layout/versoes/{id}")
        {
            Content = new StringContent(corpoJson, Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "versoeslayout-atualizar");
        requisicao.Headers.TryAddWithoutValidation("If-Match", etag);

        using var resposta = await _cliente.SendAsync(requisicao);
        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta.Headers.ETag?.ToString() ?? string.Empty);
    }
}
