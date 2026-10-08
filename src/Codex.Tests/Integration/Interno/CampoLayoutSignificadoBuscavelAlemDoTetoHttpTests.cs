using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// O truncamento de
/// <c>itensbusca.subtitulo</c> (<see cref="Codex.Infrastructure.Busca.TruncamentoExibicao"/>) é só de
/// EXIBIÇÃO — o texto indexado para busca (<c>pesoC</c>, derivado do <c>Significado</c> inteiro em
/// <c>PesosBusca.DoCampoLayout</c>) nunca é tocado. Este teste prova isso ponta a ponta: cria um campo
/// canônico cujo significado só contém um termo raro DEPOIS do caractere 300, e confirma que a busca
/// interna encontra o item por esse termo — se o truncamento alcançasse o texto indexado (ou se
/// <c>pesoC</c> fosse derivado do <c>Subtitulo</c> truncado em vez do <c>Significado</c> bruto), a
/// busca não encontraria nada.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CampoLayoutSignificadoBuscavelAlemDoTetoHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita =
        "versoeslayout-adicionar,campolayout-adicionar,indicebusca-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public CampoLayoutSignificadoBuscavelAlemDoTetoHttpTests(PostgresContainerFixture fixture)
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
    public async Task SignificadoComTermoRaroApos300Caracteres_BuscaInternaEncontraOCampo()
    {
        var sufixo = NovoSufixo();
        var termoRaro = $"palavrararaxyz{sufixo}";

        // Enchimento de exatamente 310 caracteres (acima do teto de 300 de itensbusca.subtitulo),
        // seguido do termo raro — se o truncamento alcançasse o texto indexado, o termo cairia fora.
        var enchimento = string.Join(' ', Enumerable.Range(0, 60).Select(i => $"enchimento{i:D3}"));
        enchimento = enchimento.Length >= 310 ? enchimento[..310] : enchimento.PadRight(310, 'x');
        var significado = $"{enchimento} {termoRaro}";
        significado.Length.Should().BeGreaterThan(300, because: "o termo tem de ficar além do teto de truncamento da coluna de exibição");

        var versaoLayoutId = await CriarVersaoLayoutAsync();
        var nomeCampo = $"Campo busca {sufixo}";

        var (statusCriar, corpoCriado, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/layout/versoes/{versaoLayoutId}/campos", new
            {
                nome = nomeCampo,
                caminhoXml = $"//Campo{sufixo}",
                significado,
                ordem = 1,
            });
        statusCriar.Should().Be(HttpStatusCode.Created,
            because: $"corpo: {(corpoCriado.ValueKind == JsonValueKind.Undefined ? "(vazio)" : corpoCriado.ToString())}");

        var (statusBusca, corpoBusca, _) = await EnviarAsync(
            HttpMethod.Get, $"/api/v1/interno/busca?q={termoRaro}&tamanhoPagina=50");

        statusBusca.Should().Be(HttpStatusCode.OK);

        var titulosEncontrados = corpoBusca.GetProperty("data").GetProperty("grupos").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("itens").EnumerateArray())
            .Select(item => item.GetProperty("titulo").GetString())
            .ToArray();

        titulosEncontrados.Should().Contain(nomeCampo,
            because: "o termo só existe depois do caractere 300 do significado — se a busca indexasse o " +
                     "subtitulo truncado (ou se o truncamento vazasse para o pesoC), este campo nunca apareceria");
    }

    // ------------------------------------------------------------------ apoio

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private async Task<int> CriarVersaoLayoutAsync()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/layout/versoes", new
        {
            rotulo = $"Layout busca {NovoSufixo()}",
            descricao = (string?)null,
        });

        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeEscrita);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }
}
