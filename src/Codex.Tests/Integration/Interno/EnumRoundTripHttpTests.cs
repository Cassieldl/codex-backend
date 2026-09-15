using System.Net;
using System.Text;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Round-trip de ENUM ponta a ponta contra Postgres real: o valor que o frontend ESCREVE em JSON
/// é exatamente o valor que ele LÊ de volta.
///
/// <para>
/// É a prova que fecha o buraco de BUG-01. O contrato do frontend não é só "a escrita aceita
/// string" — é que a grafia é a MESMA nos dois sentidos, porque ele tipa esses campos como união
/// de literais (<c>'Prefeitura' | 'InvoiCy' | 'Schema'</c>). Qualquer divergência de caixa entre
/// entrada e saída quebraria o cliente em silêncio: a resposta não casaria com nenhum literal do
/// tipo e o valor atravessaria a UI como texto desconhecido.
/// </para>
///
/// <para>
/// <c>InvoiCy</c> é o caso deliberadamente escolhido: é o único nome de enum do domínio com caixa
/// interna irregular (maiúscula no meio). Um <c>JsonNamingPolicy</c> qualquer no converter, ou um
/// <c>ToUpper</c>/<c>ToLower</c> perdido no caminho de leitura, quebraria justamente aqui e
/// passaria batido em <c>Nacional</c> ou <c>Schema</c>.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class EnumRoundTripHttpTests : IAsyncLifetime
{
    private const string Roles = "erros-adicionar,erros-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public EnumRoundTripHttpTests(PostgresContainerFixture fixture)
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
    [InlineData("InvoiCy")]
    [InlineData("Prefeitura")]
    [InlineData("Schema")]
    public async Task OrigemEscritaComoString_VoltaComAMesmaGrafiaNaCriacaoENoDetalhe(string origem)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        // Origem "Prefeitura" exige ao menos uma versão de provedor aplicável (§5.1d); as outras
        // aceitam lista vazia. Como aqui o objeto do teste é a GRAFIA do enum e não essa regra,
        // "Prefeitura" é exercitada por um caminho que a satisfaz.
        var versoesProvedorIds = origem == "Prefeitura"
            ? await CriarVersaoProvedorPublicavelAsync(sufixo)
            : [];

        var idsEmJson = string.Join(", ", versoesProvedorIds);

        var corpo = $$"""
            {
              "codigo": "E-RT-{{sufixo}}",
              "origem": "{{origem}}",
              "titulo": "Erro de round-trip {{sufixo}}",
              "causaProvavel": null,
              "solucaoSugerida": null,
              "variantes": ["mensagem observada {{sufixo}}"],
              "versoesProvedorIds": [{{idsEmJson}}],
              "camposLayoutIds": []
            }
            """;

        var (statusCriacao, corpoCriacao) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", corpo);

        statusCriacao.Should().Be(HttpStatusCode.Created,
            because: "o frontend manda origem como string \"{0}\"", origem);

        var criado = corpoCriacao.GetProperty("data");
        criado.GetProperty("origem").GetString().Should().Be(origem,
            because: "a resposta da criação precisa devolver a MESMA grafia que entrou");

        var id = criado.GetProperty("id").GetInt32();

        var (statusDetalhe, corpoDetalhe) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/erros/{id}");

        statusDetalhe.Should().Be(HttpStatusCode.OK);
        corpoDetalhe.GetProperty("data").GetProperty("origem").GetString().Should().Be(origem,
            because: "a grafia precisa sobreviver à ida ao banco e à releitura, não só ao eco da criação");
    }

    /// <summary>
    /// Aceitar o ordinal na entrada é compatibilidade (o converter foi registrado com
    /// <c>allowIntegerValues: true</c>), mas a SAÍDA é sempre o nome — nunca o número. Sem isto, um
    /// cliente que ainda mandasse número receberia de volta algo que seu tipo não reconhece.
    /// </summary>
    [Fact]
    public async Task OrigemEscritaComoOrdinal_VoltaComoNome_NuncaComoNumero()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        var corpo = $$"""
            {
              "codigo": "E-ORD-{{sufixo}}",
              "origem": 2,
              "titulo": "Erro ordinal {{sufixo}}",
              "variantes": ["mensagem observada {{sufixo}}"],
              "versoesProvedorIds": [],
              "camposLayoutIds": []
            }
            """;

        var (status, corpoResposta) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", corpo);

        status.Should().Be(HttpStatusCode.Created);

        var origem = corpoResposta.GetProperty("data").GetProperty("origem");
        origem.ValueKind.Should().Be(JsonValueKind.String, because: "enum sai da API como nome, nunca como ordinal");
        origem.GetString().Should().Be("Schema", because: "OrigemErro.Schema é o ordinal 2");
    }

    // ---------------------------------------------------------------- apoio

    /// <summary>Provedor + versão de layout + versão de provedor, o mínimo para satisfazer §5.1d.</summary>
    private async Task<int[]> CriarVersaoProvedorPublicavelAsync(string sufixo)
    {
        using var clienteAmplo = _factory.CreateClient();

        const string RolesEscrita = "provedores-adicionar,versoeslayout-adicionar,versoesprovedor-adicionar";

        var (_, corpoProvedor) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", $$"""
            {
              "nome": "Provedor RT {{sufixo}}",
              "identificadorInvoicy": "RT-{{sufixo}}",
              "categoria": "Nacional",
              "aliases": []
            }
            """, RolesEscrita, clienteAmplo);

        var idProvedor = corpoProvedor.GetProperty("data").GetProperty("id").GetInt32();

        var (_, corpoLayout) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/layout/versoes", $$"""
            {
              "rotulo": "Layout RT {{sufixo}}",
              "descricao": null
            }
            """, RolesEscrita, clienteAmplo);

        var idLayout = corpoLayout.GetProperty("data").GetProperty("id").GetInt32();

        var (_, corpoVersao) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/provedores/{idProvedor}/versoes", $$"""
            {
              "versaoLayoutId": {{idLayout}},
              "rotulo": "Versao RT {{sufixo}}",
              "introducao": null
            }
            """, RolesEscrita, clienteAmplo);

        return [corpoVersao.GetProperty("data").GetProperty("id").GetInt32()];
    }

    private Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, string? corpoJson = null) =>
        EnviarAsync(metodo, rota, corpoJson, Roles, _cliente);

    private static async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, string? corpoJson, string roles, HttpClient cliente)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, roles);

        if (corpoJson is not null)
            requisicao.Content = new StringContent(corpoJson, Encoding.UTF8, "application/json");

        using var resposta = await cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }
}
