using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Campos cujo validator não tinha teto, ou tinha teto maior que a coluna, passavam na validação e
/// morriam em 22001 no banco (500). Pela pipeline real: o limite cabe, limite+1 é 422. Os números
/// são literais, nunca a constante do código: o teste confere o contrato com a coluna.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class TetosDeColunaHttpTests : IAsyncLifetime
{
    private const string Roles =
        "versoeslayout-adicionar,versoeslayout-atualizar,erros-adicionar,erros-atualizar,"
        + "camposversao-adicionar,camposversao-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public TetosDeColunaHttpTests(PostgresContainerFixture fixture)
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

    // ------------------------------------------------------------------ versão de layout

    [Theory]
    [InlineData(80, HttpStatusCode.Created)]
    [InlineData(81, HttpStatusCode.UnprocessableEntity)]
    public async Task CriarVersaoLayout_RotuloNaFronteiraDaColuna(int tamanho, HttpStatusCode esperado)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/layout/versoes", new
        {
            rotulo = TextoUnico(tamanho),
        });

        status.Should().Be(esperado, because: corpo);
    }

    [Theory]
    [InlineData(80, HttpStatusCode.OK)]
    [InlineData(81, HttpStatusCode.UnprocessableEntity)]
    public async Task AtualizarVersaoLayout_RotuloNaFronteiraDaColuna(int tamanho, HttpStatusCode esperado)
    {
        var (id, versao) = await SemearVersaoLayoutAsync();

        var (status, corpo) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/layout/versoes/{id}", new { rotulo = TextoUnico(tamanho) }, versao);

        status.Should().Be(esperado, because: corpo);
    }

    // ------------------------------------------------------------------ erro

    [Theory]
    [InlineData(40, HttpStatusCode.Created)]
    [InlineData(41, HttpStatusCode.UnprocessableEntity)]
    public async Task CriarErro_CodigoNaFronteiraDaColuna(int tamanho, HttpStatusCode esperado)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros",
            CorpoDeErro(codigo: TextoUnico(tamanho), titulo: "Titulo curto"));

        status.Should().Be(esperado, because: corpo);
    }

    [Theory]
    [InlineData(200, HttpStatusCode.Created)]
    [InlineData(201, HttpStatusCode.UnprocessableEntity)]
    public async Task CriarErro_TituloNaFronteiraDaColuna(int tamanho, HttpStatusCode esperado)
    {
        // Separadores repetidos encolhem no slug: o slug cabe, e só o teto do título decide.
        var titulo = string.Concat(Enumerable.Repeat("ab - ", 50))[..tamanho];

        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros",
            CorpoDeErro(codigo: $"E{Guid.NewGuid():N}"[..12], titulo));

        status.Should().Be(esperado, because: corpo);
    }

    [Theory]
    [InlineData(200, HttpStatusCode.Created)]
    [InlineData(201, HttpStatusCode.UnprocessableEntity)]
    public async Task CriarErro_SlugDerivadoNaFronteiraDaColuna(int tamanhoDoSlug, HttpStatusCode esperado)
    {
        // Código e título cabem cada um na sua coluna; só o slug "{codigo}-{titulo}" passa de 200.
        var codigo = $"E{Guid.NewGuid():N}"[..12];
        var titulo = new string('t', tamanhoDoSlug - codigo.Length - 1);

        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", CorpoDeErro(codigo, titulo));

        status.Should().Be(esperado, because: corpo);
    }

    [Theory]
    [InlineData(200, HttpStatusCode.OK)]
    [InlineData(201, HttpStatusCode.UnprocessableEntity)]
    public async Task AtualizarErro_TituloNaFronteiraDaColuna(int tamanho, HttpStatusCode esperado)
    {
        var (id, versao) = await SemearErroAsync();

        var (status, corpo) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/erros/{id}", new
        {
            origem = nameof(OrigemErro.InvoiCy),
            titulo = TextoUnico(tamanho),
            variantes = new[] { "mensagem observada" },
        }, versao);

        status.Should().Be(esperado, because: corpo);
    }

    // ------------------------------------------------------------------ campo de versão

    [Theory]
    [InlineData("tipo", 60)]
    [InlineData("tamanho", 40)]
    [InlineData("mascara", 120)]
    [InlineData("destinoXml", 400)]
    public async Task CriarCampoVersao_CampoNaFronteiraDaColuna(string campo, int limite)
    {
        var cenario = await SemearVersaoComCampoLayoutAsync();
        var rota = $"/api/v1/interno/versoes/{cenario.VersaoProvedorId}/campos";

        var (statusAcima, corpoAcima) = await EnviarAsync(HttpMethod.Post, rota, CorpoDeCampoVersao(cenario.CampoLayoutId, campo, limite + 1));
        var (statusNoLimite, corpoNoLimite) = await EnviarAsync(HttpMethod.Post, rota, CorpoDeCampoVersao(cenario.CampoLayoutId, campo, limite));

        statusAcima.Should().Be(HttpStatusCode.UnprocessableEntity, because: corpoAcima);
        statusNoLimite.Should().Be(HttpStatusCode.Created, because: corpoNoLimite);
    }

    [Theory]
    [InlineData("tipo", 60)]
    [InlineData("tamanho", 40)]
    [InlineData("mascara", 120)]
    [InlineData("destinoXml", 400)]
    public async Task AtualizarCampoVersao_CampoNaFronteiraDaColuna(string campo, int limite)
    {
        var (id, versao) = await SemearCampoVersaoAsync();
        var rota = $"/api/v1/interno/campos-versao/{id}";

        var (statusAcima, corpoAcima) = await EnviarAsync(HttpMethod.Put, rota, CorpoDeCampoVersao(null, campo, limite + 1), versao);
        var (statusNoLimite, corpoNoLimite) = await EnviarAsync(HttpMethod.Put, rota, CorpoDeCampoVersao(null, campo, limite), versao);

        statusAcima.Should().Be(HttpStatusCode.UnprocessableEntity, because: corpoAcima);
        statusNoLimite.Should().Be(HttpStatusCode.OK, because: corpoNoLimite);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record CenarioCampoVersao(int VersaoProvedorId, int CampoLayoutId);

    /// <summary>Texto do tamanho pedido, com prefixo único para não colidir em slug ou código.</summary>
    private static string TextoUnico(int tamanho)
    {
        var prefixo = Guid.NewGuid().ToString("N")[..10];
        return (prefixo + new string('a', Math.Max(0, tamanho - prefixo.Length)))[..tamanho];
    }

    private static object CorpoDeErro(string codigo, string titulo) => new
    {
        codigo,
        origem = nameof(OrigemErro.InvoiCy),
        titulo,
        variantes = new[] { "mensagem observada" },
    };

    private static Dictionary<string, object?> CorpoDeCampoVersao(int? campoLayoutId, string campo, int tamanho)
    {
        var corpo = new Dictionary<string, object?>
        {
            ["tipo"] = "texto",
            ["estadoPreenchimento"] = nameof(EstadoPreenchimento.Obrigatorio),
        };

        if (campoLayoutId is not null)
            corpo["campoLayoutId"] = campoLayoutId;

        corpo[campo] = new string('x', tamanho);
        return corpo;
    }

    private async Task<(int Id, uint Versao)> SemearVersaoLayoutAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var versaoLayout = VersaoLayout.Criar(TextoUnico(30), await ProximaOrdemDeLayoutAsync(contexto));
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        return (versaoLayout.Id, versaoLayout.Versao);
    }

    private async Task<(int Id, uint Versao)> SemearErroAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var erro = Erro.Criar(TextoUnico(12), OrigemErro.InvoiCy, "Titulo original");
        erro.SubstituirVariantes(["mensagem observada"]);
        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        return (erro.Id, erro.Versao);
    }

    private async Task<CenarioCampoVersao> SemearVersaoComCampoLayoutAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var versaoLayout = VersaoLayout.Criar(TextoUnico(30), await ProximaOrdemDeLayoutAsync(contexto));
        var provedor = Provedor.Criar($"Provedor {TextoUnico(20)}", TextoUnico(20), CategoriaProvedor.Nacional);
        contexto.VersoesLayout.Add(versaoLayout);
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var sufixo = Guid.NewGuid().ToString("N")[..10];
        var campoLayout = CampoLayout.Criar(versaoLayout.Id, $"Campo {sufixo}", $"//Campo{sufixo}", "significado", ordem: 1);
        var versaoProvedor = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao {sufixo}");
        contexto.CamposLayout.Add(campoLayout);
        contexto.VersoesProvedor.Add(versaoProvedor);
        await contexto.SaveChangesAsync();

        return new CenarioCampoVersao(versaoProvedor.Id, campoLayout.Id);
    }

    private async Task<(int Id, uint Versao)> SemearCampoVersaoAsync()
    {
        var cenario = await SemearVersaoComCampoLayoutAsync();
        await using var contexto = _fixture.CriarContexto();

        var campoVersao = CampoVersao.Criar(cenario.VersaoProvedorId, cenario.CampoLayoutId, "texto", EstadoPreenchimento.Obrigatorio);
        contexto.CamposVersao.Add(campoVersao);
        await contexto.SaveChangesAsync();

        return (campoVersao.Id, campoVersao.Versao);
    }

    private static async Task<int> ProximaOrdemDeLayoutAsync(Codex.Infrastructure.Data.CodexDbContext contexto) =>
        (await contexto.VersoesLayout.AnyAsync() ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem) : 0) + 1;

    private async Task<(HttpStatusCode Status, string Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object corpoRequisicao, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota)
        {
            Content = JsonContent.Create(corpoRequisicao),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, Roles);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        using var resposta = await _cliente.SendAsync(requisicao);
        return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
    }
}
