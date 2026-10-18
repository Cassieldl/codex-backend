using System.Net;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// Termos que o parser de texto preserva inteiros (host com porta, URL, e-mail, caminho) voltam
/// como lexema e entram na consulta do passo parcial. Lexema reaproveitado como sintaxe de
/// tsquery derrubava a busca com 500; aqui cada termo passa pelas duas superfícies, sozinho e ao
/// lado de uma âncora que só o passo parcial encontra.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class BuscaTermoHostilHttpTests : IAsyncLifetime
{
    private const string Ancora = "ancoratermohostil";
    private const string TituloDaAncora = "Item ancora da busca hostil";
    private const string RoleDeBusca = "indicebusca-buscar";

    private static readonly SemaphoreSlim TravaDoBanco = new(1, 1);
    private static string? _connectionStringDoBanco;

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public BuscaTermoHostilHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(await ObterBancoIsoladoAsync());
        _cliente = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    public static TheoryData<string> TermosHostis() => new()
    {
        "nfse.prefeitura.sp.gov.br:443/ws",
        "exemplo.com:8080",
        "http://exemplo.com:8080/caminho",
        "a@b.com",
        "a/b/c",
        "E0037: codigo",
        "cStat:100",
        "x:*",
        "o'reilly",
        "barra\\invertida",
        "(a | b) & !c",
    };

    [Theory]
    [MemberData(nameof(TermosHostis))]
    public async Task TermoHostilSozinho_Responde200NasDuasSuperficies(string termo)
    {
        var (statusPublico, corpoPublico) = await BuscarAsync(termo, interno: false);
        var (statusInterno, corpoInterno) = await BuscarAsync(termo, interno: true);

        statusPublico.Should().Be(HttpStatusCode.OK, because: $"busca pública por '{termo}' respondeu: {corpoPublico}");
        statusInterno.Should().Be(HttpStatusCode.OK, because: $"busca interna por '{termo}' respondeu: {corpoInterno}");
    }

    [Theory]
    [MemberData(nameof(TermosHostis))]
    public async Task TermoHostilAoLadoDaAncora_ChegaAoPassoParcialEAchaAAncora(string termo)
    {
        var consulta = $"{Ancora} {termo}";

        foreach (var interno in new[] { false, true })
        {
            var (status, corpo) = await BuscarAsync(consulta, interno);

            status.Should().Be(HttpStatusCode.OK, because: $"busca {(interno ? "interna" : "pública")} por '{consulta}' respondeu: {corpo}");

            var dados = JsonDocument.Parse(corpo).RootElement.GetProperty("data");
            dados.GetProperty("qualidade").GetString().Should().Be("parcial",
                because: "a âncora não contém o termo hostil, então só o passo parcial a encontra; é ele que monta a consulta com o lexema hostil");
            TitulosDe(dados).Should().Contain(TituloDaAncora);
        }
    }

    // ------------------------------------------------------------------ apoio

    private async Task<(HttpStatusCode Status, string Corpo)> BuscarAsync(string termo, bool interno)
    {
        var rota = interno ? "/api/v1/interno/busca" : "/api/v1/busca";
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"{rota}?q={Uri.EscapeDataString(termo)}&tamanhoPagina=50");

        if (interno)
            requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RoleDeBusca);

        using var resposta = await _cliente.SendAsync(requisicao);
        return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
    }

    private static IReadOnlyList<string> TitulosDe(JsonElement dados) =>
        dados.GetProperty("grupos").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("itens").EnumerateArray())
            .Select(item => item.GetProperty("titulo").GetString()!)
            .ToList();

    /// <summary>Banco próprio e com um único item: vocabulário de outras classes não interfere no passo parcial.</summary>
    private async Task<string> ObterBancoIsoladoAsync()
    {
        await TravaDoBanco.WaitAsync();
        try
        {
            if (_connectionStringDoBanco is not null)
                return _connectionStringDoBanco;

            var connectionString = await _fixture.CriarBancoVazioAsync($"codex_busca_hostil_{Guid.NewGuid():N}"[..40]);

            await using var conexao = new NpgsqlConnection(connectionString);
            await conexao.OpenAsync();

            await using var comando = conexao.CreateCommand();
            comando.CommandText = """
                INSERT INTO codex.itensbusca (tipoitem, itemid, titulo, url, publicado, pesoa)
                VALUES ('artigo', 1, @titulo, '/artigos/ancora', true, @pesoa)
                """;
            comando.Parameters.AddWithValue("titulo", TituloDaAncora);
            comando.Parameters.AddWithValue("pesoa", $"Documento {Ancora}");
            await comando.ExecuteNonQueryAsync();

            _connectionStringDoBanco = connectionString;
            return connectionString;
        }
        finally
        {
            TravaDoBanco.Release();
        }
    }
}
