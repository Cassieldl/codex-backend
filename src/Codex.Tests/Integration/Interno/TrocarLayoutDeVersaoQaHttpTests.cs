using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Casos complementares a
/// <see cref="TrocarLayoutDeVersaoHttpTests"/>: o token de concorrência muda depois da troca (não
/// fica "a troca funcionou, mas o cliente não consegue encadear outra escrita sem reler"), a troca
/// respeita o protocolo de <c>If-Match</c> desatualizado (409 antes de qualquer validação de
/// layout), e as mensagens dos dois 422 próprios de D1 não vazam detalhe interno.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class TrocarLayoutDeVersaoQaHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita =
        "provedores-buscar,provedores-adicionar,"
        + "versoesprovedor-buscar,versoesprovedor-adicionar,versoesprovedor-atualizar,"
        + "camposversao-adicionar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public TrocarLayoutDeVersaoQaHttpTests(PostgresContainerFixture fixture)
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
    public async Task TrocarVersaoLayoutId_ParaLayoutValido_MudaOTokenDeConcorrencia()
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var layoutDestino = await CriarVersaoLayoutAsync();
        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = versao.Rotulo, versaoLayoutId = layoutDestino }, versao.Versao);

        status.Should().Be(HttpStatusCode.OK);
        var versaoConcorrenciaNova = corpo.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

        versaoConcorrenciaNova.Should().NotBe(versao.Versao,
            because: "a linha mudou no banco (xmin novo); devolver o token antigo tornaria a próxima escrita um 409 falso");

        // E o token novo serve de fato para encadear a próxima escrita, sem precisar reler.
        var (statusEncadeada, _, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = $"{versao.Rotulo} editado" }, versaoConcorrenciaNova);

        statusEncadeada.Should().Be(HttpStatusCode.OK,
            because: "o token devolvido pela troca de layout precisa ser o corrente para a escrita seguinte");
    }

    [Fact]
    public async Task TrocarVersaoLayoutId_ComIfMatchDesatualizado_Devolve409AntesDeValidarOLayout()
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var layoutDestino = await CriarVersaoLayoutAsync();
        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);

        // Uma edição concorrente consome o token que o teste tinha em mãos.
        var (statusPrimeira, _, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = $"{versao.Rotulo} concorrente" }, versao.Versao);
        statusPrimeira.Should().Be(HttpStatusCode.OK);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = versao.Rotulo, versaoLayoutId = layoutDestino }, versao.Versao);

        status.Should().Be(HttpStatusCode.Conflict,
            because: "o If-Match é do estado anterior à edição concorrente — a troca de layout não pode contornar o protocolo de concorrência");
        CodigoDe(corpo).Should().Be("CONFLITO_CONCORRENCIA");
        corpo.GetRawText().Should().NotContain("uq_codex_").And.NotContain("at Codex.");

        (await ObterVersaoLayoutIdAsync(versao.Id)).Should().Be(layoutOrigem,
            because: "a recusa por conflito é anterior a qualquer troca de layout");
    }

    [Theory]
    [InlineData(999999)]
    public async Task TrocarVersaoLayoutId_ParaInexistente_AMensagemNaoVazaDetalheInterno(int layoutInexistente)
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = versao.Rotulo, versaoLayoutId = layoutInexistente }, versao.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        var mensagem = corpo.GetProperty("error").GetProperty("message").GetString();

        mensagem.Should().NotBeNullOrWhiteSpace();
        mensagem.Should().NotContainAny(
            new[] { "Exception", "at Codex.", "SqlState", "Npgsql", "StackTrace" },
            "422 de negócio nunca pode carregar detalhe de implementação — só o que o usuário precisa saber");
    }

    [Fact]
    public async Task TrocarVersaoLayoutId_QuandoVersaoJaTemCampos_AMensagemNaoVazaDetalheInterno()
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var layoutDestino = await CriarVersaoLayoutAsync();
        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);
        await SemearCampoVersaoAsync(versao.Id, layoutOrigem);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = versao.Rotulo, versaoLayoutId = layoutDestino }, versao.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        var mensagem = corpo.GetProperty("error").GetProperty("message").GetString();

        mensagem.Should().NotBeNullOrWhiteSpace();
        mensagem.Should().NotContainAny(new[] { "Exception", "at Codex.", "SqlState", "Npgsql", "StackTrace" });
    }

    // ------------------------------------------------------------------ apoio

    private sealed record VersaoCriada(int Id, string Rotulo, uint Versao);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static string? CodigoDe(JsonElement corpo) => corpo.GetProperty("error").GetProperty("code").GetString();

    private async Task<int> CriarVersaoLayoutAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout trocalayoutqa {NovoSufixo()}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        return versaoLayout.Id;
    }

    private async Task<int> CriarProvedorAsync()
    {
        var sufixo = NovoSufixo();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome = $"Provedor trocalayoutqa {sufixo}",
            identificadorInvoicy = $"INVQA-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases = Array.Empty<string>(),
        });

        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<VersaoCriada> CriarVersaoAsync(int provedorId, int versaoLayoutId)
    {
        var rotulo = $"Versao trocalayoutqa {NovoSufixo()}";

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{provedorId}/versoes", new
        {
            versaoLayoutId,
            rotulo,
        });

        status.Should().Be(HttpStatusCode.Created);
        var criada = corpo.GetProperty("data");

        return new VersaoCriada(
            criada.GetProperty("id").GetInt32(), rotulo, criada.GetProperty("versaoConcorrencia").GetUInt32());
    }

    private async Task SemearCampoVersaoAsync(int versaoProvedorId, int versaoLayoutId)
    {
        await using var contexto = _fixture.CriarContexto();

        var sufixo = NovoSufixo();
        var campoLayout = CampoLayout.Criar(versaoLayoutId, $"Campo {sufixo}", $"//Campo{sufixo}", "significado", ordem: 1);
        contexto.CamposLayout.Add(campoLayout);
        await contexto.SaveChangesAsync();

        var campoVersao = CampoVersao.Criar(versaoProvedorId, campoLayout.Id, "texto", EstadoPreenchimento.Obrigatorio);
        contexto.CamposVersao.Add(campoVersao);
        await contexto.SaveChangesAsync();
    }

    private async Task<int> ObterVersaoLayoutIdAsync(int versaoId)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.VersoesProvedor.AsNoTracking()
            .Where(v => v.Id == versaoId)
            .Select(v => v.VersaoLayoutId)
            .SingleAsync();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeEscrita);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }
}
