using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// analise-backend.md §4.4/§8: mapeamento exceção → status HTTP do middleware global de erros.
///
/// <para>
/// Só o caso <c>400 IF_MATCH_OBRIGATORIO</c> é provável sem Postgres real: <see cref="Codex.API.Common.IfMatchExtensoes.ExigirIfMatch"/>
/// lança <see cref="Codex.API.Common.IfMatchObrigatorioException"/> ANTES de qualquer chamada ao
/// service/repositório (ver corpo das actions <c>Atualizar</c>/<c>Remover</c> dos controllers
/// internos) — nunca toca o banco. Os casos <c>410</c> (conteúdo despublicado, cascata
/// provedor→versão) e <c>409</c> (<c>If-Match</c> divergente de verdade / <c>DbUpdateConcurrencyException</c>)
/// exigem entidade real persistida e ficam bloqueados por FIX-SCHEMA (migration quebrada,
/// 42P17) — não são bug desta unidade; ver handoff.
/// </para>
/// </summary>
public sealed class ErrosHttpTests : IClassFixture<CodexApiFactory>
{
    private readonly CodexApiFactory _factory;

    public ErrosHttpTests(CodexApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Enums vão como NÚMERO (ordinal), não string — a API não registra <c>JsonStringEnumConverter</c>
    /// (achado registrado no handoff: response DTOs convertem enum para string manualmente via
    /// <c>.ToString()</c>, mas os DTOs de request esperam o ordinal cru do <see cref="System.Text.Json"/>
    /// padrão; comportamento atual, não o que necessariamente deveria ser).
    /// </summary>
    public static IEnumerable<object?[]> RotasComIfMatchObrigatorio()
    {
        yield return ["PUT", "/api/v1/interno/provedores/1", "provedores-atualizar",
            new { nome = "x", categoria = 0, aliases = Array.Empty<string>() }]; // CategoriaProvedor.Nacional
        yield return ["DELETE", "/api/v1/interno/provedores/1", "provedores-remover", null];
        yield return ["PUT", "/api/v1/interno/provedores/1/publicacao", "provedores-publicar", new { publicado = true }];
        yield return ["PUT", "/api/v1/interno/versoes/1", "versoesprovedor-atualizar", new { rotulo = "x" }];
        yield return ["PUT", "/api/v1/interno/avisos/1", "avisos-atualizar",
            new { severidade = 2, titulo = "x", corpo = "x" }]; // Severidade.Informativo
        yield return ["PUT", "/api/v1/interno/campos-layout/1", "campolayout-atualizar", new { nome = "x" }];
        yield return ["PUT", "/api/v1/interno/erros/1", "erros-atualizar",
            new { origem = 1, titulo = "x", variantes = new[] { "x" } }]; // OrigemErro.InvoiCy
    }

    [Theory]
    [MemberData(nameof(RotasComIfMatchObrigatorio))]
    public async Task PutOuDelete_SemIfMatch_Retorna400ComCodigoIfMatchObrigatorio(
        string metodoTexto, string rota, string role, object? corpo)
    {
        var metodo = new HttpMethod(metodoTexto);

        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, role);

        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: $"{metodo} {rota} exige If-Match para concorrência otimista — ausente deveria ser 400, nunca prosseguir para o service");

        var corpoResposta = await resposta.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(corpoResposta);
        json.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("IF_MATCH_OBRIGATORIO");
    }
}
