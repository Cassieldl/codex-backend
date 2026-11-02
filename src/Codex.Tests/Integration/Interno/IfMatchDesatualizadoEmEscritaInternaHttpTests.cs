using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>PUT/DELETE interno com If-Match velho responde 409 e não toca a linha.</summary>
[Collection(PostgresCollection.Nome)]
public sealed class IfMatchDesatualizadoEmEscritaInternaHttpTests : IAsyncLifetime
{
    private const int QuantidadeDeEscritasComIfMatch = 34;

    private static readonly string[] EscritasDeLayoutECamposDeVersao =
    [
        "PUT /api/v1/interno/campos-layout/1",
        "DELETE /api/v1/interno/campos-layout/1",
        "PUT /api/v1/interno/campos-layout/1/publicacao",
        "PUT /api/v1/interno/campos-versao/1",
        "DELETE /api/v1/interno/campos-versao/1",
        "PUT /api/v1/interno/layout/versoes/1",
        "PUT /api/v1/interno/layout/versoes/1/publicacao",
        "PUT /api/v1/interno/layout/versoes/1/estrutura",
    ];

    private static readonly Regex SegmentoDeId = new("/1(?=/|$)", RegexOptions.CultureInvariant);

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private SemeadorInternoHttp _semeador = null!;

    public IfMatchDesatualizadoEmEscritaInternaHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        _semeador = new SemeadorInternoHttp(_cliente);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private enum Recurso
    {
        Provedor,
        VersaoProvedor,
        CampoVersao,
        Aviso,
        VersaoLayout,
        CampoLayout,
        Erro,
        Artigo,
        Comunicado,
        Exemplo,
        Lacuna,
    }

    private sealed record Cenario(Recurso Recurso, Func<string, object>? Corpo);

    private static readonly Dictionary<string, Cenario> Cenarios = new(StringComparer.Ordinal)
    {
        ["PUT /api/v1/interno/provedores/1/publicacao"] = new(Recurso.Provedor, _ => new { publicado = true }),
        ["PUT /api/v1/interno/provedores/1"] = new(Recurso.Provedor, s => new { nome = $"Provedor editado {s}", categoria = "Nacional", aliases = Array.Empty<string>() }),
        ["PUT /api/v1/interno/provedores/1/slug"] = new(Recurso.Provedor, s => new { slug = $"provedor-novo-{s}" }),
        ["DELETE /api/v1/interno/provedores/1"] = new(Recurso.Provedor, null),

        ["PUT /api/v1/interno/versoes/1"] = new(Recurso.VersaoProvedor, s => new { rotulo = $"Versao editada {s}" }),
        ["DELETE /api/v1/interno/versoes/1"] = new(Recurso.VersaoProvedor, null),
        ["PUT /api/v1/interno/versoes/1/publicacao"] = new(Recurso.VersaoProvedor, _ => new { publicado = true }),
        ["PUT /api/v1/interno/versoes/1/verificacao"] = new(Recurso.VersaoProvedor, _ => new { verificadoEm = Ontem() }),

        ["PUT /api/v1/interno/campos-versao/1"] = new(Recurso.CampoVersao, _ => new { tipo = "numero", estadoPreenchimento = "Obrigatorio", ordem = 1 }),
        ["DELETE /api/v1/interno/campos-versao/1"] = new(Recurso.CampoVersao, null),

        ["PUT /api/v1/interno/avisos/1"] = new(Recurso.Aviso, s => new { severidade = "Alerta", titulo = $"Aviso editado {s}", corpo = "Corpo editado." }),
        ["DELETE /api/v1/interno/avisos/1"] = new(Recurso.Aviso, null),
        ["PUT /api/v1/interno/avisos/1/publicacao"] = new(Recurso.Aviso, _ => new { publicado = true }),

        ["PUT /api/v1/interno/layout/versoes/1"] = new(Recurso.VersaoLayout, s => new { rotulo = $"Layout editado {s}" }),
        ["PUT /api/v1/interno/layout/versoes/1/publicacao"] = new(Recurso.VersaoLayout, _ => new { publicado = true }),
        ["PUT /api/v1/interno/layout/versoes/1/estrutura"] = new(Recurso.VersaoLayout, _ => CorpoDeEstrutura()),

        ["PUT /api/v1/interno/campos-layout/1"] = new(Recurso.CampoLayout, s => new { nome = $"Campo editado {s}" }),
        ["DELETE /api/v1/interno/campos-layout/1"] = new(Recurso.CampoLayout, null),
        ["PUT /api/v1/interno/campos-layout/1/publicacao"] = new(Recurso.CampoLayout, _ => new { publicado = true }),

        ["PUT /api/v1/interno/erros/1"] = new(Recurso.Erro, s => new { origem = "InvoiCy", titulo = $"Erro editado {s}", variantes = new[] { "Mensagem editada" } }),
        ["PUT /api/v1/interno/erros/1/slug"] = new(Recurso.Erro, s => new { slug = $"erro-novo-{s}" }),
        ["DELETE /api/v1/interno/erros/1"] = new(Recurso.Erro, null),
        ["PUT /api/v1/interno/erros/1/publicacao"] = new(Recurso.Erro, _ => new { publicado = true }),

        ["PUT /api/v1/interno/artigos/1"] = new(Recurso.Artigo, s => SemeadorInternoHttp.CorpoDeArtigo($"Artigo editado {s}")),
        ["DELETE /api/v1/interno/artigos/1"] = new(Recurso.Artigo, null),
        ["PUT /api/v1/interno/artigos/1/publicacao"] = new(Recurso.Artigo, _ => new { publicado = true }),
        ["PUT /api/v1/interno/artigos/1/verificacao"] = new(Recurso.Artigo, _ => new { verificadoEm = Ontem() }),
        ["PUT /api/v1/interno/artigos/1/slug"] = new(Recurso.Artigo, s => new { slug = $"artigo-novo-{s}" }),

        ["PUT /api/v1/interno/comunicados/1"] = new(Recurso.Comunicado, s => CorpoDeComunicado($"Comunicado editado {s}")),
        ["DELETE /api/v1/interno/comunicados/1"] = new(Recurso.Comunicado, null),
        ["PUT /api/v1/interno/comunicados/1/publicacao"] = new(Recurso.Comunicado, _ => new { publicado = true }),

        ["PUT /api/v1/interno/exemplos/1"] = new(Recurso.Exemplo, s => new { nome = $"Exemplo editado {s}", xml = "<nfe>editado</nfe>" }),
        ["DELETE /api/v1/interno/exemplos/1"] = new(Recurso.Exemplo, null),

        ["PUT /api/v1/interno/lacunas-busca/1/estado"] = new(Recurso.Lacuna, _ => new { estado = "Resolvida" }),
    };

    public static IEnumerable<object[]> EscritasComIfMatch() => PipelineAutorizacaoTests.EndpointsInternos()
        .Where(linha => (HttpMethod)linha[0] == HttpMethod.Put || (HttpMethod)linha[0] == HttpMethod.Delete)
        .Select(linha => new object[] { ((HttpMethod)linha[0]).Method, (string)linha[1] });

    [Fact]
    public void Universo_TemTodasAsEscritasComIfMatch()
    {
        var universo = EscritasComIfMatch().Select(Chave).ToList();

        universo.Should().HaveCount(QuantidadeDeEscritasComIfMatch,
            because: "cada PUT/DELETE interno chama ExigirIfMatch; rota que entra ou sai da tabela muda este número");
        universo.Should().Contain(EscritasDeLayoutECamposDeVersao);
    }

    [Fact]
    public void TodaEscritaDoUniverso_TemCenario()
    {
        Cenarios.Keys.Should().BeEquivalentTo(EscritasComIfMatch().Select(Chave),
            because: "rota de escrita nova na tabela de autorização precisa de recurso e corpo para provar o 409");
    }

    [Theory]
    [MemberData(nameof(EscritasComIfMatch))]
    public async Task Escrita_ComIfMatchDesatualizado_Devolve409SemTocarALinha(string metodo, string rotaModelo)
    {
        var cenario = Cenarios[$"{metodo} {rotaModelo}"];
        var sufixo = Guid.NewGuid().ToString("N")[..10];
        var id = await CriarAsync(cenario.Recurso, sufixo);
        var versaoAntes = await LerVersaoAsync(cenario.Recurso, id);
        var rota = SegmentoDeId.Replace(rotaModelo, $"/{id}");

        var (status, texto) = await _semeador.EnviarAsync(
            new HttpMethod(metodo), rota, cenario.Corpo?.Invoke(sufixo), ifMatch: $"\"{versaoAntes - 1}\"");

        status.Should().Be(HttpStatusCode.Conflict, because: $"{metodo} {rota} com If-Match velho respondeu: {texto}");
        JsonDocument.Parse(texto).RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("CONFLITO_CONCORRENCIA");
        texto.Should().NotContain("uq_codex_").And.NotContain("at Codex.");
        (await LerVersaoAsync(cenario.Recurso, id)).Should().Be(versaoAntes,
            because: "a escrita recusada não pode ter gravado nada na linha");
    }

    private static string Chave(object[] linha) => $"{linha[0]} {linha[1]}";

    private static object CorpoDeEstrutura()
    {
        var raiz = new { nome = "Envio", caminho = "Envio", formato = "ambos", filhos = Array.Empty<object>() };
        return new { xml = raiz, json = raiz };
    }

    private static DateOnly Ontem() => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

    private static object CorpoDeComunicado(string titulo) => new
    {
        titulo,
        corpo = "Corpo semeado para teste.",
        severidade = "Informativo",
        abrangencia = "Global",
        expiraEm = DateTimeOffset.UtcNow.AddDays(9),
    };

    private async Task<int> CriarAsync(Recurso recurso, string sufixo)
    {
        switch (recurso)
        {
            case Recurso.Provedor:
                return await _semeador.CriarProvedorAsync(sufixo);

            case Recurso.VersaoLayout:
                return await _semeador.CriarVersaoLayoutAsync(sufixo);

            case Recurso.CampoLayout:
                return await _semeador.CriarCampoLayoutAsync(await _semeador.CriarVersaoLayoutAsync(sufixo), sufixo);

            case Recurso.VersaoProvedor:
                return await CriarVersaoProvedorAsync(sufixo);

            case Recurso.CampoVersao:
            {
                var versaoLayoutId = await _semeador.CriarVersaoLayoutAsync(sufixo);
                var campoLayoutId = await _semeador.CriarCampoLayoutAsync(versaoLayoutId, sufixo);
                var provedorId = await _semeador.CriarProvedorAsync(sufixo);
                var versaoProvedorId = await _semeador.CriarVersaoProvedorAsync(provedorId, versaoLayoutId, sufixo);
                return await _semeador.CriarCampoVersaoAsync(versaoProvedorId, campoLayoutId);
            }

            case Recurso.Aviso:
                return await _semeador.CriarAsync($"/api/v1/interno/provedores/{await _semeador.CriarProvedorAsync(sufixo)}/avisos",
                    new { severidade = "Informativo", titulo = $"Aviso semeado {sufixo}", corpo = "Corpo semeado." });

            case Recurso.Erro:
                return await _semeador.CriarAsync("/api/v1/interno/erros",
                    SemeadorInternoHttp.CorpoDeErro($"E{sufixo}", $"Erro semeado {sufixo}"));

            case Recurso.Artigo:
                return await _semeador.CriarAsync("/api/v1/interno/artigos",
                    SemeadorInternoHttp.CorpoDeArtigo($"Artigo semeado {sufixo}"));

            case Recurso.Comunicado:
                return await _semeador.CriarAsync("/api/v1/interno/comunicados", CorpoDeComunicado($"Comunicado semeado {sufixo}"));

            case Recurso.Exemplo:
                return await _semeador.CriarAsync($"/api/v1/interno/versoes/{await CriarVersaoProvedorAsync(sufixo)}/exemplos",
                    new { nome = $"Exemplo semeado {sufixo}", xml = "<nfe>ok</nfe>" });

            case Recurso.Lacuna:
                return await _semeador.CriarAsync("/api/v1/interno/lacunas-busca", new { termo = $"lacuna semeada {sufixo}" });

            default:
                throw new ArgumentOutOfRangeException(nameof(recurso), recurso, null);
        }
    }

    private async Task<int> CriarVersaoProvedorAsync(string sufixo)
    {
        var versaoLayoutId = await _semeador.CriarVersaoLayoutAsync(sufixo);
        var provedorId = await _semeador.CriarProvedorAsync(sufixo);
        return await _semeador.CriarVersaoProvedorAsync(provedorId, versaoLayoutId, sufixo);
    }

    private async Task<uint> LerVersaoAsync(Recurso recurso, int id)
    {
        await using var contexto = _fixture.CriarContexto();
        return recurso switch
        {
            Recurso.Provedor => await Versao(contexto.Provedores.Where(x => x.Id == id).Select(x => x.Versao)),
            Recurso.VersaoProvedor => await Versao(contexto.VersoesProvedor.Where(x => x.Id == id).Select(x => x.Versao)),
            Recurso.CampoVersao => await Versao(contexto.CamposVersao.Where(x => x.Id == id).Select(x => x.Versao)),
            Recurso.Aviso => await Versao(contexto.Avisos.Where(x => x.Id == id).Select(x => x.Versao)),
            Recurso.VersaoLayout => await Versao(contexto.VersoesLayout.Where(x => x.Id == id).Select(x => x.Versao)),
            Recurso.CampoLayout => await Versao(contexto.CamposLayout.Where(x => x.Id == id).Select(x => x.Versao)),
            Recurso.Erro => await Versao(contexto.Erros.Where(x => x.Id == id).Select(x => x.Versao)),
            Recurso.Artigo => await Versao(contexto.Artigos.Where(x => x.Id == id).Select(x => x.Versao)),
            Recurso.Comunicado => await Versao(contexto.Comunicados.Where(x => x.Id == id).Select(x => x.Versao)),
            Recurso.Exemplo => await Versao(contexto.Exemplos.Where(x => x.Id == id).Select(x => x.Versao)),
            Recurso.Lacuna => await Versao(contexto.LacunasBusca.Where(x => x.Id == id).Select(x => x.Versao)),
            _ => throw new ArgumentOutOfRangeException(nameof(recurso), recurso, null),
        };
    }

    private static Task<uint> Versao(IQueryable<uint> consulta) => consulta.SingleAsync();
}
