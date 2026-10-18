using System.Net;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// Itens com a mesma pontuação saem sempre na mesma ordem, pela chave única do índice. As linhas
/// são gravadas com ids fora da ordem física, para que nem a ordem de inserção nem o inverso dela
/// coincidam com a esperada: sem desempate, o Postgres devolve a ordem física ou a do heapsort.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class BuscaRankingDesempateHttpTests : IAsyncLifetime
{
    private const string TokenPequeno = "empatepequenodesempate";
    private const string TokenGrande = "empategrandedesempate";
    private const string TermoAusente = "inexistentenoacervo";
    private const string RoleDeBusca = "indicebusca-buscar";

    private const int IdBaseGrande = 1000;
    private const int TotalGrande = 302;
    private const int TamanhoPagina = 50;

    private static readonly SemaphoreSlim TravaDoBanco = new(1, 1);
    private static string? _connectionStringDoBanco;

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public BuscaRankingDesempateHttpTests(PostgresContainerFixture fixture)
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

    public static TheoryData<bool, bool> Superficies() => new()
    {
        { false, false },
        { false, true },
        { true, false },
        { true, true },
    };

    [Theory]
    [MemberData(nameof(Superficies))]
    public async Task ItensEmpatados_SaemSempreNaOrdemDoId(bool interno, bool passoParcial)
    {
        var consulta = passoParcial ? $"{TokenPequeno} {TermoAusente}" : TokenPequeno;

        // Inseridos como C, A, D, B (ids 103, 101, 104, 102): a ordem esperada não é a de inserção nem o inverso.
        string[] esperada = ["Empate A", "Empate B", "Empate C", "Empate D"];

        for (var repeticao = 0; repeticao < 3; repeticao++)
        {
            var (qualidade, titulos) = await BuscarAsync(consulta, interno);

            qualidade.Should().Be(passoParcial ? "parcial" : "exata");
            titulos.Should().Equal(esperada, because: $"a repetição {repeticao + 1} da mesma consulta tem de devolver a mesma ordem");
        }
    }

    [Theory]
    [MemberData(nameof(Superficies))]
    public async Task CorteDoRanking_ManteveOsMenoresIds_MesmoGravadosPorUltimo(bool interno, bool passoParcial)
    {
        var consulta = passoParcial ? $"{TokenGrande} {TermoAusente}" : TokenGrande;

        var (qualidade, titulos) = await BuscarAsync(consulta, interno);

        qualidade.Should().Be(passoParcial ? "parcial" : "exata");

        // Os ids 1000 e 1001 são as duas últimas linhas físicas: sem desempate no SQL, o corte de 300 os descarta.
        titulos.Should().Equal(
            Enumerable.Range(IdBaseGrande, TamanhoPagina).Select(id => $"Grande {id}"),
            because: "o corte do ranking entre empatados é decidido pela chave única, não pela posição física da linha");
    }

    // ------------------------------------------------------------------ apoio

    private async Task<(string Qualidade, IReadOnlyList<string> Titulos)> BuscarAsync(string termo, bool interno)
    {
        var rota = interno ? "/api/v1/interno/busca" : "/api/v1/busca";
        using var requisicao = new HttpRequestMessage(
            HttpMethod.Get, $"{rota}?q={Uri.EscapeDataString(termo)}&tamanhoPagina={TamanhoPagina}");

        if (interno)
            requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RoleDeBusca);

        using var resposta = await _cliente.SendAsync(requisicao);
        var corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: corpo);

        var dados = JsonDocument.Parse(corpo).RootElement.GetProperty("data");
        var titulos = dados.GetProperty("grupos").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("itens").EnumerateArray())
            .Select(item => item.GetProperty("titulo").GetString()!)
            .ToList();

        return (dados.GetProperty("qualidade").GetString()!, titulos);
    }

    /// <summary>Banco próprio: a posição física das linhas só é previsível numa tabela que ninguém mais escreve.</summary>
    private async Task<string> ObterBancoIsoladoAsync()
    {
        await TravaDoBanco.WaitAsync();
        try
        {
            if (_connectionStringDoBanco is not null)
                return _connectionStringDoBanco;

            var connectionString = await _fixture.CriarBancoVazioAsync($"codex_busca_empate_{Guid.NewGuid():N}"[..40]);

            await using var conexao = new NpgsqlConnection(connectionString);
            await conexao.OpenAsync();

            await InserirAsync(conexao, [(103, "Empate C"), (101, "Empate A"), (104, "Empate D"), (102, "Empate B")], TokenPequeno);

            var grande = Enumerable.Range(0, TotalGrande - 2)
                .Select(posicao => (IdBaseGrande + 2 + posicao, $"Grande {IdBaseGrande + 2 + posicao}"))
                .Concat([(IdBaseGrande, $"Grande {IdBaseGrande}"), (IdBaseGrande + 1, $"Grande {IdBaseGrande + 1}")])
                .ToList();
            await InserirAsync(conexao, grande, TokenGrande);

            _connectionStringDoBanco = connectionString;
            return connectionString;
        }
        finally
        {
            TravaDoBanco.Release();
        }
    }

    /// <summary>Um INSERT só, na ordem da lista: numa tabela vazia a ordem física é a da lista.</summary>
    private static async Task InserirAsync(NpgsqlConnection conexao, IReadOnlyList<(int Id, string Titulo)> linhas, string token)
    {
        await using var comando = conexao.CreateCommand();
        var valores = new List<string>();

        for (var indice = 0; indice < linhas.Count; indice++)
        {
            valores.Add($"(@id{indice}, 'artigo', @id{indice}, @titulo{indice}, '/artigos/empate', true, @pesoa)");
            comando.Parameters.AddWithValue($"id{indice}", linhas[indice].Id);
            comando.Parameters.AddWithValue($"titulo{indice}", linhas[indice].Titulo);
        }

        comando.Parameters.AddWithValue("pesoa", $"Documento {token}");
        comando.CommandText = $"""
            INSERT INTO codex.itensbusca (itembuscaid, tipoitem, itemid, titulo, url, publicado, pesoa)
            OVERRIDING SYSTEM VALUE
            VALUES {string.Join(", ", valores)}
            """;

        await comando.ExecuteNonQueryAsync();
    }
}
