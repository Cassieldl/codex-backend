using System.Net;
using System.Text;
using System.Text.Json;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// PQ-17 (analise-backend.md §4.6) — a CORRIDA de verdade, não o caminho feliz: a segunda versão
/// de layout promovida a <c>Vigente</c> tem de ser recusada pelo banco
/// (<c>uq_codex_versoeslayout_estado_vigente</c>) e chegar ao editor como <c>409</c> com código e
/// mensagem de NEGÓCIO. Antes desta unidade o mesmo <c>23505</c> virava o genérico
/// <c>REGISTRO_DUPLICADO</c> com o nome cru da constraint no corpo da resposta.
///
/// <para>
/// O corpo da requisição é JSON literal: <c>estado</c> é um enum no DTO, e enum serializado pelo
/// próprio .NET binda de volta mesmo quando o contrato publicado está errado — só o literal mede
/// o que o cliente realmente envia.
/// </para>
///
/// <para>
/// Promover a primeira versão é obrigatório para que o caso exista, e isso ocupa o slot GLOBAL de
/// <c>Vigente</c> — por isso o <see cref="DisposeAsync"/> apaga o que foi semeado (hard delete:
/// soft delete deixaria o índice único parcial ainda ocupado para as outras classes).
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class PromocaoVigenteVersaoLayoutHttpTests : IAsyncLifetime
{
    private const string RoleAtualizar = "versoeslayout-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public PromocaoVigenteVersaoLayoutHttpTests(PostgresContainerFixture fixture)
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

    [Fact]
    public async Task SegundaVersaoPromovidaAVigente_Recebe409DeNegocio_ESemNenhumaMudancaDeEstado()
    {
        var versaoA = await CriarVersaoAsync("A");
        var versaoB = await CriarVersaoAsync("B");
        var (idA, idB) = (versaoA.Id, versaoB.Id);

        var promocaoA = await EnviarAtualizacaoAsync(versaoA, """{"estado": "Vigente"}""");
        promocaoA.Status.Should().Be(HttpStatusCode.OK, because: "a primeira promoção é o caminho feliz");

        var promocaoB = await EnviarAtualizacaoAsync(versaoB, """{"estado": "Vigente"}""");

        promocaoB.Status.Should().Be(HttpStatusCode.Conflict);

        var erro = promocaoB.Corpo.GetProperty("error");
        erro.GetProperty("code").GetString().Should().Be("VERSAO_LAYOUT_VIGENTE_JA_EXISTE");

        var mensagem = erro.GetProperty("message").GetString()!;
        mensagem.Should().NotContain("uq_codex_",
            because: "nome de constraint é detalhe interno de banco — o editor recebe orientação, não DDL");
        mensagem.Should().Contain("Vigente");

        // A constraint recusa a escrita inteira: nada de estado intermediário.
        await using var contexto = _fixture.CriarContexto();
        (await contexto.VersoesLayout.AsNoTracking().FirstAsync(v => v.Id == idA)).Estado
            .Should().Be(EstadoVersaoLayout.Vigente);
        (await contexto.VersoesLayout.AsNoTracking().FirstAsync(v => v.Id == idB)).Estado
            .Should().Be(EstadoVersaoLayout.Prevista);
    }

    [Fact]
    public async Task ColisaoDeOrdem_ContinuaNoCodigoGenerico_NaoFoiCapturadaPelaRegraNova()
    {
        // Controle negativo: a interceptação por ConstraintName não pode engolir os outros 23505
        // da mesma entidade.
        var versaoA = await CriarVersaoAsync("A");
        var versaoB = await CriarVersaoAsync("B");

        var resposta = await EnviarAtualizacaoAsync(versaoB, $$"""{"ordem": {{versaoA.Ordem}}}""");

        resposta.Status.Should().Be(HttpStatusCode.Conflict);
        resposta.Corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("REGISTRO_DUPLICADO");
        resposta.Corpo.GetProperty("error").GetProperty("message").GetString()
            .Should().NotContain("uq_codex_",
                because: "o caminho genérico da classe base também não pode devolver o nome da constraint");
    }

    // ---------------------------------------------------------------------------------- apoio

    private sealed record Resposta(HttpStatusCode Status, JsonElement Corpo);

    /// <summary>Versão criada pela própria API: o <c>ETag</c> vem da resposta real, nunca montado pelo teste.</summary>
    private sealed record VersaoCriada(int Id, int Ordem, string ETag);

    private async Task<VersaoCriada> CriarVersaoAsync(string rotulo)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interno/layout/versoes")
        {
            Content = new StringContent(
                $$"""{"rotulo": "Promocao {{rotulo}} {{_sufixo}}"}""", Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "versoeslayout-adicionar");

        using var resposta = await _cliente.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);

        var etag = resposta.Headers.ETag?.ToString();
        etag.Should().NotBeNullOrWhiteSpace(because: "a criação publica o validador de concorrência no header");

        var dados = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("data");

        return new VersaoCriada(dados.GetProperty("id").GetInt32(), dados.GetProperty("ordem").GetInt32(), etag!);
    }

    private async Task<Resposta> EnviarAtualizacaoAsync(VersaoCriada versao, string corpoJson)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/layout/versoes/{versao.Id}")
        {
            Content = new StringContent(corpoJson, Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RoleAtualizar);
        requisicao.Headers.TryAddWithoutValidation("If-Match", versao.ETag);

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();

        return new Resposta(
            resposta.StatusCode,
            string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    private async Task LimparAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var versoes = await contexto.VersoesLayout.Where(v => v.Rotulo.Contains(_sufixo)).ToListAsync();
        contexto.VersoesLayout.RemoveRange(versoes);

        await contexto.SaveChangesAsync();
    }
}
