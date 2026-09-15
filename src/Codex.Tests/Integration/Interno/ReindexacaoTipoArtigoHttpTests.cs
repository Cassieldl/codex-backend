using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Decisão B-2: o <c>tipo</c> de um artigo <b>é</b> editável, e trocá-lo <b>reindexa</b> o item.
/// Exercitado por HTTP real (I26) contra Postgres real.
///
/// <para>
/// Por que existe: "campo aceito no DTO mas ignorado no service" não é contrato, é armadilha — e
/// aqui o campo carrega um efeito colateral invisível na resposta (o peso D do índice de busca).
/// Sem estes casos, tirar o <c>AtualizarTipo</c> ou o <c>AtualizarPesosAsync</c> do fluxo de
/// atualização não derrubava nada.
/// </para>
///
/// <para>O motivo de B-2 ter rejeitado tipo imutável era estabilidade de URL — por isso a URL
/// indexada é afirmada explicitamente depois da troca.</para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ReindexacaoTipoArtigoHttpTests : IAsyncLifetime
{
    private const string RolesDeArtigo =
        "artigos-buscar,artigos-adicionar,artigos-atualizar,artigos-publicar,artigos-remover";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ReindexacaoTipoArtigoHttpTests(PostgresContainerFixture fixture)
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
    public async Task TrocarTipo_ViaPut_PersisteONovoTipoNoRecursoInterno()
    {
        var artigo = await CriarArtigoAsync("Conceito");

        var atualizado = await TrocarTipoAsync(artigo, "Procedimento");

        atualizado.GetProperty("tipo").GetString().Should().Be("Procedimento",
            because: "campo aceito no DTO e ignorado pelo service não é contrato — é armadilha (B-2)");
    }

    [Fact]
    public async Task TrocarTipo_ReindexaOItem_PesoDPassaASerONovoTipo()
    {
        var artigo = await CriarArtigoAsync("Conceito");

        (await PesoDIndexadoAsync(artigo.Id)).Should().Be("Conceito",
            because: "o peso D do artigo é o tipo (PesosBusca.DoArtigo) — a criação já o indexa");

        await TrocarTipoAsync(artigo, "Mudanca");

        (await PesoDIndexadoAsync(artigo.Id)).Should().Be("Mudanca",
            because: "trocar o tipo sem reindexar deixa a busca respondendo pelo tipo antigo — o efeito não aparece na resposta do PUT, só no índice");
    }

    [Fact]
    public async Task TrocarTipo_NaoMexeNaUrlIndexada_QueEOMotivoDeBDoisTerRejeitadoTipoImutavel()
    {
        var artigo = await CriarArtigoAsync("Conceito");
        var urlAntes = await UrlIndexadaAsync(artigo.Id);

        await TrocarTipoAsync(artigo, "Procedimento");

        (await UrlIndexadaAsync(artigo.Id)).Should().Be(urlAntes,
            because: "corrigir a classificação não pode custar a URL viva — era exatamente o custo de apagar-e-recriar que B-2 recusou");
    }

    [Fact]
    public async Task TrocarTipo_DeArtigoPublicado_RefleteNoDetalhePublico()
    {
        var artigo = await CriarArtigoAsync("Conceito");
        var versaoPublicado = await PublicarAsync(artigo.Id, artigo.Versao);

        await TrocarTipoAsync(artigo with { Versao = versaoPublicado }, "Procedimento");

        using var resposta = await _cliente.GetAsync($"/api/v1/artigos/{artigo.Slug}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;

        corpo.GetProperty("data").GetProperty("tipo").GetString().Should().Be("Procedimento",
            because: "a correção de classificação só serve se chegar à superfície que o leitor vê");
    }

    // ------------------------------------------------------------------ apoio

    private sealed record ArtigoCriado(int Id, string Slug, uint Versao);

    private async Task<ArtigoCriado> CriarArtigoAsync(string tipo)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        // `tipo` vai como string literal, como o cliente manda — enum C# serializado pelo .NET
        // pode passar por um contrato que o JSON real não atravessa.
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/artigos", new
        {
            tipo,
            titulo = $"Artigo reindexacao {sufixo}",
            corpo = $"Corpo do artigo de reindexacao {sufixo}.",
        });

        status.Should().Be(HttpStatusCode.Created, because: "I25 devolve 201");
        var dados = corpo.GetProperty("data");

        return new ArtigoCriado(
            dados.GetProperty("id").GetInt32(),
            dados.GetProperty("slug").GetString()!,
            dados.GetProperty("versaoConcorrencia").GetUInt32());
    }

    private async Task<JsonElement> TrocarTipoAsync(ArtigoCriado artigo, string novoTipo)
    {
        var atual = await ObterAdminAsync(artigo.Id);

        var (status, corpo) = await EnviarAsync(
            HttpMethod.Put,
            $"/api/v1/interno/artigos/{artigo.Id}",
            new
            {
                tipo = novoTipo,
                titulo = atual.GetProperty("titulo").GetString(),
                corpo = atual.GetProperty("corpo").GetString(),
            },
            ifMatch: atual.GetProperty("versaoConcorrencia").GetUInt32());

        status.Should().Be(HttpStatusCode.OK, because: "I26 devolve 200 com o recurso atualizado");
        return corpo.GetProperty("data");
    }

    private async Task<uint> PublicarAsync(int id, uint versao)
    {
        var (status, corpo) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/artigos/{id}/publicacao", new { publicado = true }, versao);

        status.Should().Be(HttpStatusCode.OK);
        return corpo.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();
    }

    private async Task<JsonElement> ObterAdminAsync(int id)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/artigos/{id}");
        status.Should().Be(HttpStatusCode.OK);
        return corpo.GetProperty("data");
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeArtigo);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }

    private Task<string?> PesoDIndexadoAsync(int artigoId) => LerColunaIndexadaAsync("pesod", artigoId);

    private Task<string?> UrlIndexadaAsync(int artigoId) => LerColunaIndexadaAsync("url", artigoId);

    private async Task<string?> LerColunaIndexadaAsync(string coluna, int artigoId)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = $"SELECT {coluna} FROM codex.itensbusca WHERE tipoitem = 'artigo' AND itemid = @id";
        comando.Parameters.AddWithValue("id", artigoId);

        await using var leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue(because: $"o artigo {artigoId} tem de estar indexado");

        return leitor.IsDBNull(0) ? null : leitor.GetString(0);
    }
}
