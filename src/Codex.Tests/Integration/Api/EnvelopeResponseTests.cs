using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.API.Common;
using FluentAssertions;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// rules/api.md (D9): shape exato do envelope por categoria — recurso único
/// (<c>{ data, message }</c>), coleção paginada CRUA (<c>{ items, totalCount, page, pageSize,
/// totalPages }</c>, sem <c>data</c>) e erro (<c>{ error: { code, message, details? } }</c>).
///
/// <para>
/// A prova do shape de SUCESSO (<c>ApiResponse&lt;T&gt;</c>/<c>PaginatedResponse&lt;T&gt;</c> com
/// dado real) é feita por serialização direta — sem depender de Postgres, já que nenhum 200 real
/// é alcançável neste ambiente sem banco (ver <c>ErrosHttpTests</c>). O shape de ERRO é provado
/// ponta-a-ponta via HTTP real (mesmo endpoint sem banco de <c>FluentValidationWiringTests</c>).
/// </para>
/// </summary>
public sealed class EnvelopeResponseTests : IClassFixture<CodexApiFactory>
{
    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private readonly CodexApiFactory _factory;

    public EnvelopeResponseTests(CodexApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ApiResponse_Serializa_ComDataEMessage_NuncaComCamposExtras()
    {
        var envelope = new ApiResponse<string> { Data = "valor", Message = "ok" };

        var json = JsonSerializer.Serialize(envelope, OpcoesJson);
        using var documento = JsonDocument.Parse(json);

        documento.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("data", "message");
        documento.RootElement.GetProperty("data").GetString().Should().Be("valor");
        documento.RootElement.GetProperty("message").GetString().Should().Be("ok");
    }

    [Fact]
    public void PaginatedResponse_Serializa_CruSemData_ComTotalPagesCalculado()
    {
        var envelope = new PaginatedResponse<int>
        {
            Items = [1, 2, 3],
            TotalCount = 53,
            Page = 2,
            PageSize = 25,
        };

        var json = JsonSerializer.Serialize(envelope, OpcoesJson);
        using var documento = JsonDocument.Parse(json);

        documento.RootElement.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo("items", "totalCount", "page", "pageSize", "totalPages");
        documento.RootElement.TryGetProperty("data", out _).Should().BeFalse(
            because: "coleção paginada é envelope CRU — nunca tem campo 'data' (rules/api.md)");
        documento.RootElement.GetProperty("totalPages").GetInt32().Should().Be(3); // ceil(53/25)
    }

    [Fact]
    public async Task ErroDeValidacao_TemShapeExato_ErrorCodeMessageDetails()
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
        using var documento = JsonDocument.Parse(corpo);

        documento.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("error");

        var erro = documento.RootElement.GetProperty("error");
        erro.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("code", "message", "details");
        erro.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        erro.GetProperty("details").ValueKind.Should().Be(JsonValueKind.Array);
    }
}
