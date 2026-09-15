using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// analise-backend.md §5.3: registrar validators na DI "não é suficiente" — prova (não
/// declaração, AP-SECURITY-003) de que o <c>422</c> realmente dispara no envelope padrão
/// (<c>{ error: { code, message, details[] } }</c>, rules/api.md) quando um DTO chega inválido.
///
/// <para>
/// Usa <c>POST /interno/layout/versoes</c> (I15b) porque <c>CriarVersaoLayoutDtoValidator</c> só
/// tem regras de FORMA (sem <c>MustAsync</c> contra repositório) — o único endpoint de escrita cujo
/// validator não precisa de Postgres real para reprovar. Os demais validators (com regra
/// cross-entity) são cobertos por teste unitário com repositório mockado
/// (<c>Unit/Validators/*Tests.cs</c>) — aqui a prova é do WIRING (registro + result factory), não
/// de regra de negócio específica.
/// </para>
/// </summary>
public sealed class FluentValidationWiringTests : IClassFixture<CodexApiFactory>
{
    private readonly CodexApiFactory _factory;

    public FluentValidationWiringTests(CodexApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_ComRotuloVazio_Retorna422NoEnvelopePadrao()
    {
        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interno/layout/versoes")
        {
            Content = JsonContent.Create(new { rotulo = "", descricao = (string?)null }),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "versoeslayout-adicionar");

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var corpo = await resposta.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(corpo);

        var erro = json.RootElement.GetProperty("error");
        erro.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        erro.GetProperty("details").EnumerateArray().Should().Contain(
            d => d.GetProperty("field").GetString() == "rotulo",
            because: "o wiring precisa expor QUAL campo falhou, não só que algo falhou");
    }

    [Fact]
    public async Task Post_ComCorpoValido_NaoRetorna422_PassaDoFiltroDeValidacao()
    {
        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interno/layout/versoes")
        {
            Content = JsonContent.Create(new { rotulo = "Layout de teste válido", descricao = (string?)null }),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "versoeslayout-adicionar");

        using var resposta = await cliente.SendAsync(requisicao);

        // Sem banco disponível neste teste, a criação em si falha adiante (500) — o que importa
        // aqui é que NÃO seja 422: a validação de forma deixou passar um corpo válido.
        resposta.StatusCode.Should().NotBe(HttpStatusCode.UnprocessableEntity);
    }
}
