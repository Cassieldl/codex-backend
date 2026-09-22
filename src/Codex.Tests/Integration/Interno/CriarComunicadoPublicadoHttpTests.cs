using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Application.Interfaces;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// D-1 (decisoes.md, U-5) — I32 passa a aceitar <c>publicar: true</c> e criar já no ar, em UMA
/// transação: o fluxo antigo (I32 + I35) deixava um instante de rascunho visível na listagem.
/// Medido por HTTP real contra Postgres real, porque a regra é metade controller (a role
/// condicional), metade service (a publicação dentro do mesmo <c>SaveChanges</c>).
///
/// <para>
/// Cobre também a regressão de M-1 na porta nova: nenhuma transição de comunicado — inclusive a
/// que agora publica na criação — pode tocar <c>IIndiceBuscaWriter</c> nem gerar linha em
/// <c>codex.itensbusca</c>.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CriarComunicadoPublicadoHttpTests : IAsyncLifetime
{
    private const string Rota = "/api/v1/interno/comunicados";
    private const string RolesComPublicar = "comunicados-adicionar,comunicados-buscar,comunicados-publicar";
    private const string RolesSemPublicar = "comunicados-adicionar,comunicados-buscar";

    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public CriarComunicadoPublicadoHttpTests(PostgresContainerFixture fixture)
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
        await LimparAsync();
    }

    // -------------------------------------------------------------- os 3 desfechos (§6.5)

    [Fact]
    public async Task Criar_ComPublicarTrueEComARole_NasceNoAr_ComMensagemDePublicado()
    {
        var titulo = $"D1 publicado {_sufixo}";

        var (status, corpo) = await CriarAsync(titulo, publicar: true, RolesComPublicar);

        status.Should().Be(HttpStatusCode.Created);
        corpo.GetProperty("data").GetProperty("publicado").GetBoolean().Should().BeTrue();
        corpo.GetProperty("message").GetString().Should().Be("Comunicado publicado.",
            because: "a mensagem descreve o que ACONTECEU, lida de criado.Publicado (PQ-16)");

        (await ContarPublicadosAsync(titulo)).Should().Be(1,
            because: "D-1 é atômico — a linha nasce publicada, nunca passa por rascunho");
    }

    [Fact]
    public async Task Criar_ComPublicarFalse_NasceEmRascunho_ComMensagemDeRascunho()
    {
        var titulo = $"D1 rascunho explicito {_sufixo}";

        var (status, corpo) = await CriarAsync(titulo, publicar: false, RolesComPublicar);

        status.Should().Be(HttpStatusCode.Created);
        corpo.GetProperty("data").GetProperty("publicado").GetBoolean().Should().BeFalse();
        corpo.GetProperty("message").GetString().Should().Be("Comunicado criado como rascunho.");
    }

    [Fact]
    public async Task Criar_SemOCampoPublicar_MantemOComportamentoAnterior_Rascunho()
    {
        var titulo = $"D1 sem campo {_sufixo}";

        var (status, corpo) = await EnviarAsync(RolesComPublicar, new
        {
            titulo,
            corpo = "Campo publicar omitido de propósito.",
            severidade = "Alerta",
            abrangencia = "Global",
            expiraEm = DateTimeOffset.UtcNow.AddDays(2),
        });

        status.Should().Be(HttpStatusCode.Created);
        corpo.GetProperty("data").GetProperty("publicado").GetBoolean().Should().BeFalse(
            because: "o default de `publicar` é false — omitir o campo não pode publicar nada");
        corpo.GetProperty("message").GetString().Should().Be("Comunicado criado como rascunho.");
    }

    [Fact]
    public async Task Criar_ComPublicarTrueSemARoleDePublicar_Responde403_ENaoPersisteNada()
    {
        var titulo = $"D1 sem role {_sufixo}";

        var (status, _) = await CriarAsync(titulo, publicar: true, RolesSemPublicar);

        status.Should().Be(HttpStatusCode.Forbidden,
            because: "nunca aceitar publicar=true e criar em rascunho em silêncio — quem pediu para publicar precisa saber que não publicou");

        (await ContarPorTituloAsync(titulo)).Should().Be(0,
            because: "403 é recusa da operação inteira: não pode sobrar nem um rascunho no banco");
    }

    [Fact]
    public async Task Criar_SemPublicar_ComARoleDeAdicionarApenas_ContinuaFuncionando()
    {
        // Controle negativo do 403 acima: sem este caso, um `Forbid()` incondicional passaria.
        var titulo = $"D1 rascunho sem role {_sufixo}";

        var (status, corpo) = await CriarAsync(titulo, publicar: false, RolesSemPublicar);

        status.Should().Be(HttpStatusCode.Created,
            because: "a role comunicados-publicar só é exigida quando se pede para publicar");
        corpo.GetProperty("data").GetProperty("publicado").GetBoolean().Should().BeFalse();
    }

    // ------------------------------------------------------------- expiração no passado

    [Fact]
    public async Task Criar_ComPublicarTrueEExpiracaoNoPassado_Responde422()
    {
        var titulo = $"D1 expirado {_sufixo}";

        var (status, corpo) = await EnviarAsync(RolesComPublicar, new
        {
            titulo,
            corpo = "Expiração já vencida.",
            severidade = "Critico",
            abrangencia = "Global",
            expiraEm = DateTimeOffset.UtcNow.AddDays(-1),
            publicar = true,
        });

        status.Should().Be(HttpStatusCode.UnprocessableEntity);

        // O código é VALIDATION_ERROR, não COMUNICADO_EXPIRADO: CriarComunicadoDtoValidator já
        // exige ExpiraEm no futuro em I32 (desde a 002, independente de `publicar`) e responde
        // antes do service. A guarda COMUNICADO_EXPIRADO de ComunicadoServicoInterno.CriarAsync
        // é defesa do service para chamadas fora do pipeline MVC — divergência registrada no
        // handoff da unidade BACK-D.
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");

        (await ContarPorTituloAsync(titulo)).Should().Be(0);
    }

    // ------------------------------------------------------------------- regressão de M-1

    [Fact]
    public async Task CriarPublicando_NaoTocaOIndiceDeBusca_EOEspiaoProvaQueEstaLigado()
    {
        var linhasAntes = await ContarLinhasDeItensBuscaAsync();

        var espiao = new Mock<IIndiceBuscaWriter>();

        using var fabricaComEspiao = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(servicos =>
            {
                servicos.RemoveAll<IIndiceBuscaWriter>();
                servicos.AddScoped(_ => espiao.Object);
            }));

        using var cliente = fabricaComEspiao.CreateClient();

        var respostaComunicado = await PostarAsync(cliente, Rota, RolesComPublicar, new
        {
            titulo = $"D1 sem indice {_sufixo}",
            corpo = "Publicado na criação.",
            severidade = "Critico",
            abrangencia = "Global",
            expiraEm = DateTimeOffset.UtcNow.AddDays(2),
            publicar = true,
        });

        respostaComunicado.Should().Be(HttpStatusCode.Created);

        espiao.Invocations.Should().BeEmpty(
            because: "M-1 — nenhum fluxo de comunicado, inclusive a publicação na criação (D-1), toca IIndiceBuscaWriter");

        (await ContarLinhasDeItensBuscaAsync()).Should().Be(linhasAntes,
            because: "M-1 — comunicado é transitório e nunca gera linha em codex.itensbusca");

        // Prova de que o espião está de fato no lugar do writer real: um fluxo que DEVE indexar
        // (artigo) o aciona. Sem esta metade, a asserção de ausência acima passaria mesmo com a
        // substituição de DI não tendo efeito nenhum.
        var respostaArtigo = await PostarAsync(cliente, "/api/v1/interno/artigos", "artigos-adicionar", new
        {
            tipo = "Conceito",
            titulo = $"D1 artigo sonda {_sufixo}",
            corpo = "Artigo é indexado — comunicado não.",
        });

        respostaArtigo.Should().Be(HttpStatusCode.Created);
        espiao.Invocations.Should().NotBeEmpty(because: "artigo é indexado — é o controle que prova o detector vivo");
    }

    // ------------------------------------------------------------------------------ apoio

    private Task<(HttpStatusCode Status, JsonElement Corpo)> CriarAsync(string titulo, bool publicar, string roles) =>
        EnviarAsync(roles, new
        {
            titulo,
            corpo = "Corpo do comunicado de teste.",
            severidade = "Alerta",
            abrangencia = "Global",
            expiraEm = DateTimeOffset.UtcNow.AddDays(2),
            publicar,
        });

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(string roles, object corpoRequisicao)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, Rota)
        {
            Content = JsonContent.Create(corpoRequisicao),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, roles);

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }

    private static async Task<HttpStatusCode> PostarAsync(HttpClient cliente, string rota, string roles, object corpo)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, rota) { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, roles);

        using var resposta = await cliente.SendAsync(requisicao);
        return resposta.StatusCode;
    }

    private async Task<int> ContarPorTituloAsync(string titulo)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.Comunicados.CountAsync(c => c.Titulo == titulo);
    }

    private async Task<int> ContarPublicadosAsync(string titulo)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.Comunicados.CountAsync(c => c.Titulo == titulo && c.Publicado);
    }

    private async Task<long> ContarLinhasDeItensBuscaAsync()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM codex.itensbusca";

        return (long)(await comando.ExecuteScalarAsync())!;
    }

    private async Task LimparAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var comunicados = await contexto.Comunicados.Where(c => c.Titulo.Contains(_sufixo)).ToListAsync();
        var artigos = await contexto.Artigos.Where(a => a.Titulo.Contains(_sufixo)).ToListAsync();

        contexto.Comunicados.RemoveRange(comunicados);
        contexto.Artigos.RemoveRange(artigos);

        await contexto.SaveChangesAsync();
    }
}
