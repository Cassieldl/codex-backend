using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Os tetos do campo canônico são os das colunas: <c>nome varchar(120)</c>,
/// <c>grupo varchar(120)</c> e <c>slug varchar(160)</c>, que nasce do caminho XML. Sem regra no
/// validator, o valor acima do teto passava e o Postgres recusava com <c>22001</c>: <c>500</c> no
/// lugar do <c>422</c>.
///
/// <para>
/// Os números são literais de propósito: o teste existe para travar o contrato com o banco, e uma
/// constante importada subiria junto com o produto.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CampoLayoutTetosHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita =
        "versoeslayout-adicionar,campolayout-adicionar,campolayout-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public CampoLayoutTetosHttpTests(PostgresContainerFixture fixture)
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

    [Theory]
    [InlineData(120, HttpStatusCode.Created)]
    [InlineData(121, HttpStatusCode.UnprocessableEntity)]
    public async Task CriarCampoLayout_RespeitaOTetoDoNome(int tamanho, HttpStatusCode esperado)
    {
        var versaoLayoutId = await CriarVersaoLayoutAsync();

        var (status, corpo) = await CriarCampoAsync(versaoLayoutId, nome: new string('n', tamanho));

        status.Should().Be(esperado, because: DescreverCorpo(corpo));
    }

    [Theory]
    [InlineData(120, HttpStatusCode.Created)]
    [InlineData(121, HttpStatusCode.UnprocessableEntity)]
    public async Task CriarCampoLayout_RespeitaOTetoDoGrupo(int tamanho, HttpStatusCode esperado)
    {
        var versaoLayoutId = await CriarVersaoLayoutAsync();

        var (status, corpo) = await CriarCampoAsync(versaoLayoutId, grupo: new string('g', tamanho));

        status.Should().Be(esperado, because: DescreverCorpo(corpo));
    }

    /// <summary>
    /// O caminho é que tem teto de 160, não o slug: a normalização só encurta, então um caminho de
    /// até 160 caracteres sempre gera slug que cabe na coluna.
    /// </summary>
    [Theory]
    [InlineData(160, HttpStatusCode.Created)]
    [InlineData(161, HttpStatusCode.UnprocessableEntity)]
    public async Task CriarCampoLayout_RespeitaOTetoDoCaminho(int tamanho, HttpStatusCode esperado)
    {
        var versaoLayoutId = await CriarVersaoLayoutAsync();
        var caminhoXml = $"//{NovoSufixo()}".PadRight(tamanho, 'c');

        var (status, corpo) = await CriarCampoAsync(versaoLayoutId, caminhoXml: caminhoXml);

        status.Should().Be(esperado, because: DescreverCorpo(corpo));
    }

    [Theory]
    [InlineData("nome")]
    [InlineData("grupo")]
    public async Task AtualizarCampoLayout_AcimaDoTeto_Responde422(string propriedade)
    {
        var versaoLayoutId = await CriarVersaoLayoutAsync();
        var (statusCriar, corpoCriado) = await CriarCampoAsync(versaoLayoutId);
        statusCriar.Should().Be(HttpStatusCode.Created, because: DescreverCorpo(corpoCriado));

        var campoLayoutId = corpoCriado.GetProperty("data").GetProperty("id").GetInt32();
        var versaoConcorrencia = corpoCriado.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();
        var corpoAtualizacao = new Dictionary<string, string> { [propriedade] = new string('x', 121) };

        var (status, corpo) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/campos-layout/{campoLayoutId}", corpoAtualizacao, versaoConcorrencia);

        status.Should().Be(HttpStatusCode.UnprocessableEntity, because: DescreverCorpo(corpo));
    }

    // ------------------------------------------------------------------ apoio

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static string DescreverCorpo(JsonElement corpo) =>
        $"corpo: {(corpo.ValueKind == JsonValueKind.Undefined ? "(vazio)" : corpo.ToString())}";

    private Task<(HttpStatusCode Status, JsonElement Corpo)> CriarCampoAsync(
        int versaoLayoutId, string? nome = null, string? grupo = null, string? caminhoXml = null) =>
        EnviarAsync(HttpMethod.Post, $"/api/v1/interno/layout/versoes/{versaoLayoutId}/campos", new
        {
            nome = nome ?? $"Campo {NovoSufixo()}",
            caminhoXml = caminhoXml ?? $"//Campo{NovoSufixo()}",
            significado = "Significado do campo.",
            grupo,
            ordem = 1,
        });

    private async Task<int> CriarVersaoLayoutAsync()
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/layout/versoes", new
        {
            rotulo = $"Layout tetos {NovoSufixo()}",
            descricao = (string?)null,
        });

        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object corpoRequisicao, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeEscrita);
        requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }
}
