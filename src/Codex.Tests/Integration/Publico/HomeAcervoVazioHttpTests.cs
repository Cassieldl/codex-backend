using System.Net;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// P9 no estado inicial do produto: banco migrado e <b>sem uma única linha</b>. É o caso que a
/// suíte normal não consegue provar — o Postgres da <see cref="PostgresCollection"/> é
/// compartilhado e nunca está vazio —, e é justamente onde moram os erros de agregação que
/// estouram: <c>Single</c> sem elemento, <c>Max</c> sobre conjunto vazio, <c>First</c> de grupo
/// inexistente. Por isso roda contra um banco ADICIONAL, criado no mesmo container
/// (<see cref="PostgresContainerFixture.CriarBancoVazioAsync"/>).
///
/// <para>
/// A exigência é dura: <c>200</c> com coleções vazias. Nunca <c>404</c> ("ainda não há conteúdo"
/// não é recurso ausente) e nunca <c>500</c>.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class HomeAcervoVazioHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public HomeAcervoVazioHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var connectionString = await _fixture.CriarBancoVazioAsync($"codex_home_vazia_{Guid.NewGuid():N}"[..40]);

        _factory = new CodexApiFactoryComBanco(connectionString);
        _cliente = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Home_ComAcervoTotalmenteVazio_Responde200ComColecoesVaziasELayoutNulo()
    {
        using var resposta = await _cliente.GetAsync("/api/v1/home");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"acervo vazio é o estado inicial do produto, nunca 404/500 — corpo recebido: {texto}");

        var dados = JsonDocument.Parse(texto).RootElement.GetProperty("data");

        dados.GetProperty("comunicados").GetArrayLength().Should().Be(0);
        dados.GetProperty("provedoresPorCategoria").GetArrayLength().Should().Be(0);
        dados.GetProperty("artigosRecentes").GetArrayLength().Should().Be(0);
        dados.GetProperty("layout").ValueKind.Should().Be(JsonValueKind.Null,
            because: "`null` distingue 'não há versão de layout publicada' de 'há uma versão sem campos'");
    }

    [Fact]
    public async Task Home_ComAcervoVazio_MantemOCacheControl()
    {
        using var resposta = await _cliente.GetAsync("/api/v1/home");

        resposta.Headers.CacheControl.Should().NotBeNull();
        resposta.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
    }
}
