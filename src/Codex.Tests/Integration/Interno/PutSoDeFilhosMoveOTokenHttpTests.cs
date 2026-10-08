using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Pela classe: quando um PUT muda SÓ filhos ou vínculos do agregado, o EF
/// não emite <c>UPDATE</c> na linha do pai — o <c>xmin</c> não anda, o <c>WHERE xmin</c> não é
/// conferido e um segundo PUT com o token antigo responde 200, perdendo a edição anterior sem
/// aviso. Um teste por agregado exposto: o PUT só da coleção muda o <c>versaoConcorrencia</c>, e o
/// PUT seguinte com o token antigo dá <c>409 CONFLITO_CONCORRENCIA</c>.
///
/// <para>
/// Agregados expostos: provedor (aliases), erro (variantes e junções com versão e com campo) e
/// artigo (junções com campo de layout e com erro). O aviso tem a classe própria
/// <c>AvisoCamposAfetadosHttpTests</c> (campos afetados em <c>avisoscampos</c>).
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class PutSoDeFilhosMoveOTokenHttpTests : IAsyncLifetime
{
    private const string Roles =
        "provedores-buscar,provedores-adicionar,provedores-atualizar,"
        + "versoeslayout-adicionar,versoesprovedor-adicionar,"
        + "erros-buscar,erros-adicionar,erros-atualizar,"
        + "artigos-buscar,artigos-adicionar,artigos-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public PutSoDeFilhosMoveOTokenHttpTests(PostgresContainerFixture fixture)
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
    public async Task Provedor_PutSoDosAliases_MoveOTokenEUmPutComTokenAntigoDevolve409()
    {
        var (id, nome, token) = await CriarProvedorAsync(["ADN"]);

        object Corpo(string alias) => new { nome, categoria = nameof(CategoriaProvedor.Nacional), aliases = new[] { alias } };

        var (statusPrimeiro, corpoPrimeiro) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/provedores/{id}", Corpo("Primeiro"), token);
        statusPrimeiro.Should().Be(HttpStatusCode.OK, corpoPrimeiro.ToString());
        TokenDe(corpoPrimeiro).Should().NotBe(token);

        var (statusSegundo, corpoSegundo) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/provedores/{id}", Corpo("Segundo"), token);
        ConflitoDe(statusSegundo, corpoSegundo);

        await using var contexto = _fixture.CriarContexto();
        (await contexto.AliasesProvedor.AsNoTracking().Where(a => a.ProvedorId == id).Select(a => a.Alias).ToArrayAsync())
            .Should().BeEquivalentTo(["Primeiro"], because: "o PUT recusado não pode ter tocado nos aliases");
    }

    [Fact]
    public async Task Erro_PutSoDasVariantes_MoveOTokenEUmPutComTokenAntigoDevolve409()
    {
        var erro = await CriarErroAsync(["mensagem original"]);

        object Corpo(string variante) => new { origem = nameof(OrigemErro.InvoiCy), titulo = erro.Titulo, variantes = new[] { variante } };

        var (statusPrimeiro, corpoPrimeiro) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/erros/{erro.Id}", Corpo("primeira"), erro.Token);
        statusPrimeiro.Should().Be(HttpStatusCode.OK, corpoPrimeiro.ToString());
        TokenDe(corpoPrimeiro).Should().NotBe(erro.Token);

        var (statusSegundo, corpoSegundo) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/erros/{erro.Id}", Corpo("segunda"), erro.Token);
        ConflitoDe(statusSegundo, corpoSegundo);

        await using var contexto = _fixture.CriarContexto();
        (await contexto.VariantesMensagem.AsNoTracking().Where(v => v.ErroId == erro.Id).Select(v => v.Texto).ToArrayAsync())
            .Should().BeEquivalentTo(["primeira"]);
    }

    [Fact]
    public async Task Erro_PutSoDoVinculoComVersao_MoveOTokenEUmPutComTokenAntigoDevolve409()
    {
        var erro = await CriarErroAsync(["mensagem fixa"]);
        var versaoProvedorId = await CriarVersaoProvedorAsync();

        object Corpo(int[] versoes) => new
        {
            origem = nameof(OrigemErro.InvoiCy), titulo = erro.Titulo, variantes = new[] { "mensagem fixa" }, versoesProvedorIds = versoes,
        };

        var (statusPrimeiro, corpoPrimeiro) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/erros/{erro.Id}", Corpo([versaoProvedorId]), erro.Token);
        statusPrimeiro.Should().Be(HttpStatusCode.OK, corpoPrimeiro.ToString());
        TokenDe(corpoPrimeiro).Should().NotBe(erro.Token);

        var (statusSegundo, corpoSegundo) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/erros/{erro.Id}", Corpo([]), erro.Token);
        ConflitoDe(statusSegundo, corpoSegundo);

        await using var contexto = _fixture.CriarContexto();
        (await contexto.ErrosVersoes.AsNoTracking().Where(v => v.ErroId == erro.Id).Select(v => v.VersaoProvedorId).ToArrayAsync())
            .Should().BeEquivalentTo([versaoProvedorId]);
    }

    [Fact]
    public async Task Artigo_PutSoDosErrosRelacionados_MoveOTokenEUmPutComTokenAntigoDevolve409()
    {
        var erro = await CriarErroAsync(["mensagem do vinculo"]);
        var (artigoId, titulo, token) = await CriarArtigoAsync();

        object Corpo(int[] erros) => new
        {
            tipo = nameof(TipoArtigo.Conceito), titulo, corpo = "Corpo fixo do artigo.", erroIds = erros,
        };

        var (statusPrimeiro, corpoPrimeiro) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/artigos/{artigoId}", Corpo([erro.Id]), token);
        statusPrimeiro.Should().Be(HttpStatusCode.OK, corpoPrimeiro.ToString());
        TokenDe(corpoPrimeiro).Should().NotBe(token);

        var (statusSegundo, corpoSegundo) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/artigos/{artigoId}", Corpo([]), token);
        ConflitoDe(statusSegundo, corpoSegundo);

        await using var contexto = _fixture.CriarContexto();
        (await contexto.ArtigosErros.AsNoTracking().Where(a => a.ArtigoId == artigoId).Select(a => a.ErroId).ToArrayAsync())
            .Should().BeEquivalentTo([erro.Id]);
    }

    [Fact]
    public async Task Artigo_PutSoDosCamposDeLayoutRelacionados_MoveOToken()
    {
        var campoLayoutId = await CriarCampoLayoutAsync();
        var (artigoId, titulo, token) = await CriarArtigoAsync();

        var (status, corpo) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/artigos/{artigoId}", new
        {
            tipo = nameof(TipoArtigo.Conceito), titulo, corpo = "Corpo fixo do artigo.", campoLayoutIds = new[] { campoLayoutId },
        }, token);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        TokenDe(corpo).Should().NotBe(token);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record ErroCriado(int Id, string Titulo, uint Token);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static uint TokenDe(JsonElement corpo) => corpo.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

    private static void ConflitoDe(HttpStatusCode status, JsonElement corpo)
    {
        status.Should().Be(HttpStatusCode.Conflict,
            because: "o token do primeiro PUT andou — o token antigo é de uma versão que já não existe");
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("CONFLITO_CONCORRENCIA");
    }

    private async Task<(int Id, string Nome, uint Token)> CriarProvedorAsync(string[] aliases)
    {
        var sufixo = NovoSufixo();
        var nome = $"Provedor token {sufixo}";

        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome,
            identificadorInvoicy = $"INV-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases,
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return (corpo.GetProperty("data").GetProperty("id").GetInt32(), nome, TokenDe(corpo));
    }

    private async Task<ErroCriado> CriarErroAsync(string[] variantes)
    {
        var sufixo = NovoSufixo();
        var titulo = $"Erro token {sufixo}";

        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", new
        {
            codigo = $"E-{sufixo}",
            origem = nameof(OrigemErro.InvoiCy),
            titulo,
            variantes,
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return new ErroCriado(corpo.GetProperty("data").GetProperty("id").GetInt32(), titulo, TokenDe(corpo));
    }

    private async Task<(int Id, string Titulo, uint Token)> CriarArtigoAsync()
    {
        var titulo = $"Artigo token {NovoSufixo()}";

        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/artigos", new
        {
            tipo = nameof(TipoArtigo.Conceito),
            titulo,
            corpo = "Corpo fixo do artigo.",
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return (corpo.GetProperty("data").GetProperty("id").GetInt32(), titulo, TokenDe(corpo));
    }

    private async Task<int> CriarVersaoLayoutAsync()
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/layout/versoes", new
        {
            rotulo = $"Layout token {NovoSufixo()}",
            descricao = (string?)null,
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<int> CriarVersaoProvedorAsync()
    {
        var (provedorId, _, _) = await CriarProvedorAsync([]);
        var versaoLayoutId = await CriarVersaoLayoutAsync();

        var (status, corpo) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{provedorId}/versoes", new
        {
            versaoLayoutId,
            rotulo = $"Versao token {NovoSufixo()}",
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    /// <summary>Semeado direto no banco: o que interessa é só haver um campo de layout para vincular ao artigo.</summary>
    private async Task<int> CriarCampoLayoutAsync()
    {
        var versaoLayoutId = await CriarVersaoLayoutAsync();
        var sufixo = NovoSufixo();

        await using var contexto = _fixture.CriarContexto();
        var campo = Codex.Domain.Entities.CampoLayout.Criar(versaoLayoutId, $"Campo {sufixo}", $"//Campo{sufixo}", "significado", ordem: 1);
        contexto.CamposLayout.Add(campo);
        await contexto.SaveChangesAsync();

        return campo.Id;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, Roles);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }
}
