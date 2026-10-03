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
/// Demanda 008-campos-emissor-nacional (D1) ponta a ponta por HTTP real: <c>AtualizarVersaoDto.VersaoLayoutId</c>
/// é opcional — <c>null</c> mantém o layout atual (o frontend hoje não envia o campo), informado e
/// diferente troca a origem, e só quando o destino existe/está ativo e a versão não tem nenhum
/// <see cref="CampoVersao"/> cadastrado (cada um deles existe porque o campo canônico tinha origem
/// EXATAMENTE no layout atual — ver <c>CampoVersaoRepositorioInterno.CampoLayoutEstaAtivoNaVersaoDoProvedorAsync</c>).
///
/// <para>
/// Os desfechos são confirmados pelo GET seguinte (valor persistido), nunca só pelo status —
/// rules/api.md exige afirmar o VALOR resultante.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class TrocarLayoutDeVersaoHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita =
        "provedores-buscar,provedores-adicionar,"
        + "versoesprovedor-buscar,versoesprovedor-adicionar,versoesprovedor-atualizar,"
        + "camposversao-adicionar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public TrocarLayoutDeVersaoHttpTests(PostgresContainerFixture fixture)
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
    public async Task TrocarVersaoLayoutId_ParaLayoutValidoSemCampos_Atualiza()
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var layoutDestino = await CriarVersaoLayoutAsync();
        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = versao.Rotulo, versaoLayoutId = layoutDestino }, versao.Versao);

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("versaoLayoutId").GetInt32().Should().Be(layoutDestino);

        (await ObterVersaoLayoutIdAsync(versao.Id)).Should().Be(layoutDestino,
            because: "o valor persistido é o que importa, não só o 200 do envelope");
    }

    [Fact]
    public async Task AtualizarSemVersaoLayoutId_MantemOLayoutAtual()
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);
        var novoRotulo = $"Rotulo editado {NovoSufixo()}";

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}", new { rotulo = novoRotulo }, versao.Versao);

        status.Should().Be(HttpStatusCode.OK,
            because: "é exatamente o payload que o frontend atual envia — sem o campo, não pode quebrar");
        corpo.GetProperty("data").GetProperty("versaoLayoutId").GetInt32().Should().Be(layoutOrigem);

        (await ObterVersaoLayoutIdAsync(versao.Id)).Should().Be(layoutOrigem);
    }

    [Fact]
    public async Task TrocarVersaoLayoutId_ParaOMesmoLayoutAtual_NaoDispara422MesmoComCampos()
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);
        await SemearCampoVersaoAsync(versao.Id, layoutOrigem);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = versao.Rotulo, versaoLayoutId = layoutOrigem }, versao.Versao);

        status.Should().Be(HttpStatusCode.OK,
            because: "versaoLayoutId igual ao atual não é uma troca — a checagem de campos só vale quando é diferente");
        corpo.GetProperty("data").GetProperty("versaoLayoutId").GetInt32().Should().Be(layoutOrigem);
    }

    [Fact]
    public async Task TrocarVersaoLayoutId_ParaLayoutInexistente_Devolve422ComCodigoProprio()
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = versao.Rotulo, versaoLayoutId = 999999 }, versao.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        CodigoDe(corpo).Should().Be(CodigosErro.VersaoLayoutInvalida);
        MensagemDe(corpo).Should().NotContain("uq_codex_").And.NotContain("at Codex.",
            because: "nome de constraint e stack trace são detalhe interno do schema (CWE-209, rules/security.md)");

        (await ObterVersaoLayoutIdAsync(versao.Id)).Should().Be(layoutOrigem,
            because: "a recusa é anterior a qualquer escrita");
    }

    [Fact]
    public async Task TrocarVersaoLayoutId_ParaLayoutDesativado_Devolve422ComCodigoProprio()
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var layoutDesativado = await CriarVersaoLayoutAsync();
        await DesativarVersaoLayoutAsync(layoutDesativado);

        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = versao.Rotulo, versaoLayoutId = layoutDesativado }, versao.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "layout desativado não é um destino de origem válido, mesmo existindo a linha");
        CodigoDe(corpo).Should().Be(CodigosErro.VersaoLayoutInvalida);
        MensagemDe(corpo).Should().NotContain("uq_codex_").And.NotContain("at Codex.");
    }

    [Fact]
    public async Task TrocarVersaoLayoutId_QuandoVersaoJaTemCampos_Devolve422ComCodigoProprio()
    {
        var layoutOrigem = await CriarVersaoLayoutAsync();
        var layoutDestino = await CriarVersaoLayoutAsync();
        var provedorId = await CriarProvedorAsync();
        var versao = await CriarVersaoAsync(provedorId, layoutOrigem);
        await SemearCampoVersaoAsync(versao.Id, layoutOrigem);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versao.Id}",
            new { rotulo = versao.Rotulo, versaoLayoutId = layoutDestino }, versao.Versao);

        status.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "o campo da versão existe porque o campo canônico tinha origem no layout ATUAL — trocar quebraria o vínculo em silêncio");
        CodigoDe(corpo).Should().Be(CodigosErro.VersaoComCampos);
        MensagemDe(corpo).Should().NotContain("uq_codex_").And.NotContain("at Codex.");

        (await ObterVersaoLayoutIdAsync(versao.Id)).Should().Be(layoutOrigem);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record VersaoCriada(int Id, string Rotulo, uint Versao);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static string? CodigoDe(JsonElement corpo) => corpo.GetProperty("error").GetProperty("code").GetString();

    private static string? MensagemDe(JsonElement corpo) => corpo.GetProperty("error").GetProperty("message").GetString();

    private async Task<int> CriarVersaoLayoutAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout trocalayout {NovoSufixo()}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        return versaoLayout.Id;
    }

    private async Task DesativarVersaoLayoutAsync(int id)
    {
        await using var contexto = _fixture.CriarContexto();
        var versaoLayout = await contexto.VersoesLayout.SingleAsync(v => v.Id == id);
        versaoLayout.Desativar();
        await contexto.SaveChangesAsync();
    }

    private async Task<int> CriarProvedorAsync()
    {
        var sufixo = NovoSufixo();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome = $"Provedor trocalayout {sufixo}",
            identificadorInvoicy = $"INV-{sufixo}",
            categoria = nameof(CategoriaProvedor.Nacional),
            aliases = Array.Empty<string>(),
        });

        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<VersaoCriada> CriarVersaoAsync(int provedorId, int versaoLayoutId)
    {
        var rotulo = $"Versao trocalayout {NovoSufixo()}";

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

    /// <summary>
    /// Semeado direto no banco (não via API): o único objetivo é que <c>ContarPorVersaoAsync</c>
    /// enxergue >0 — a validação de origem do campo canônico (§5.2) não é o que este caso prova.
    /// </summary>
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
