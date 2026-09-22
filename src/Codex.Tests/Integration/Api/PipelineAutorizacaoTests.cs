using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Gabarito eliminatório de QA/Segurança (analise-backend.md §7.4, AP-SECURITY-003): PROVA, não
/// declaração — para cada endpoint interno de I1 a I22 (com sufixos a/b/c/d), um request real via
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> sem token
/// devolve <c>401</c>, e com token válido mas role errada devolve <c>403</c>. Teste de service NÃO
/// conta — contorna o middleware. Nenhum destes casos toca o banco (AP-BACKEND-011): 401/403 são
/// decisões do pipeline de autenticação/autorização, antes de qualquer acesso a dados.
/// </summary>
public sealed class PipelineAutorizacaoTests : IClassFixture<CodexApiFactory>
{
    private readonly CodexApiFactory _factory;

    public PipelineAutorizacaoTests(CodexApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// (Método HTTP, rota, role exigida pelo endpoint) — uma linha por endpoint de I1 a I22.
    /// Rotas usam id literal "1"; 401/403 nunca chegam a resolver o recurso, então o id não
    /// precisa existir no banco.
    /// </summary>
    public static IEnumerable<object[]> EndpointsInternos()
    {
        (HttpMethod Metodo, string Rota, string Role)[] linhas =
        [
            // Provedores — I1-I5, I3b, I6, I7, I14a2
            (HttpMethod.Get, "/api/v1/interno/provedores", "provedores-buscar"),
            (HttpMethod.Get, "/api/v1/interno/provedores/1", "provedores-buscar"),
            (HttpMethod.Post, "/api/v1/interno/provedores", "provedores-adicionar"),
            (HttpMethod.Put, "/api/v1/interno/provedores/1/publicacao", "provedores-publicar"),
            (HttpMethod.Put, "/api/v1/interno/provedores/1", "provedores-atualizar"),
            (HttpMethod.Put, "/api/v1/interno/provedores/1/slug", "provedores-atualizar"), // I37 (003, P-10)
            (HttpMethod.Delete, "/api/v1/interno/provedores/1", "provedores-remover"),
            (HttpMethod.Get, "/api/v1/interno/provedores/1/versoes", "versoesprovedor-buscar"),
            (HttpMethod.Post, "/api/v1/interno/provedores/1/versoes", "versoesprovedor-adicionar"),
            (HttpMethod.Post, "/api/v1/interno/provedores/1/avisos", "avisos-adicionar"),

            // Versões de provedor — I8a-I10, I11, I12, I14a
            (HttpMethod.Get, "/api/v1/interno/versoes/1", "versoesprovedor-buscar"),
            (HttpMethod.Put, "/api/v1/interno/versoes/1", "versoesprovedor-atualizar"),
            (HttpMethod.Delete, "/api/v1/interno/versoes/1", "versoesprovedor-remover"),
            (HttpMethod.Put, "/api/v1/interno/versoes/1/publicacao", "versoesprovedor-publicar"),
            (HttpMethod.Put, "/api/v1/interno/versoes/1/verificacao", "versoesprovedor-atualizar"),
            (HttpMethod.Get, "/api/v1/interno/versoes/1/campos", "camposversao-buscar"),
            (HttpMethod.Post, "/api/v1/interno/versoes/1/campos", "camposversao-adicionar"),
            (HttpMethod.Post, "/api/v1/interno/versoes/1/avisos", "avisos-adicionar"),

            // Campos de versão — I13a/I13b
            (HttpMethod.Put, "/api/v1/interno/campos-versao/1", "camposversao-atualizar"),
            (HttpMethod.Delete, "/api/v1/interno/campos-versao/1", "camposversao-remover"),

            // Avisos — I14b/I14c/I14d
            (HttpMethod.Put, "/api/v1/interno/avisos/1", "avisos-atualizar"),
            (HttpMethod.Delete, "/api/v1/interno/avisos/1", "avisos-remover"),
            (HttpMethod.Put, "/api/v1/interno/avisos/1/publicacao", "avisos-publicar"),

            // Versões de layout — I15a-I15d, I16
            (HttpMethod.Get, "/api/v1/interno/layout/versoes", "versoeslayout-buscar"),
            (HttpMethod.Post, "/api/v1/interno/layout/versoes", "versoeslayout-adicionar"),
            (HttpMethod.Put, "/api/v1/interno/layout/versoes/1", "versoeslayout-atualizar"),
            (HttpMethod.Put, "/api/v1/interno/layout/versoes/1/publicacao", "versoeslayout-publicar"),
            (HttpMethod.Get, "/api/v1/interno/layout/versoes/1/campos", "campolayout-buscar"),
            (HttpMethod.Post, "/api/v1/interno/layout/versoes/1/campos", "campolayout-adicionar"),

            // Campos de layout — I17a-I17c
            (HttpMethod.Put, "/api/v1/interno/campos-layout/1", "campolayout-atualizar"),
            (HttpMethod.Delete, "/api/v1/interno/campos-layout/1", "campolayout-remover"),
            (HttpMethod.Put, "/api/v1/interno/campos-layout/1/publicacao", "campolayout-publicar"),

            // Erros — I18-I20c (+ detalhe por id, adição de REST não numerada no contrato, mesma role de I18)
            (HttpMethod.Get, "/api/v1/interno/erros", "erros-buscar"),
            (HttpMethod.Get, "/api/v1/interno/erros/1", "erros-buscar"),
            (HttpMethod.Post, "/api/v1/interno/erros", "erros-adicionar"),
            (HttpMethod.Put, "/api/v1/interno/erros/1", "erros-atualizar"),
            (HttpMethod.Put, "/api/v1/interno/erros/1/slug", "erros-atualizar"), // I43 (003, P-10)
            (HttpMethod.Delete, "/api/v1/interno/erros/1", "erros-remover"),
            (HttpMethod.Put, "/api/v1/interno/erros/1/publicacao", "erros-publicar"),

            // Artigos — I23-I29, I36 (BACK-B)
            (HttpMethod.Get, "/api/v1/interno/artigos", "artigos-buscar"),
            (HttpMethod.Get, "/api/v1/interno/artigos/existe?slug=x", "artigos-buscar"),
            (HttpMethod.Get, "/api/v1/interno/artigos/1", "artigos-buscar"),
            (HttpMethod.Post, "/api/v1/interno/artigos", "artigos-adicionar"),
            (HttpMethod.Put, "/api/v1/interno/artigos/1", "artigos-atualizar"),
            (HttpMethod.Delete, "/api/v1/interno/artigos/1", "artigos-remover"),
            (HttpMethod.Put, "/api/v1/interno/artigos/1/publicacao", "artigos-publicar"),
            (HttpMethod.Put, "/api/v1/interno/artigos/1/verificacao", "artigos-atualizar"),
            (HttpMethod.Put, "/api/v1/interno/artigos/1/slug", "artigos-atualizar"), // I44 (003, P-10)

            // Comunicados — I30-I35 (BACK-C)
            (HttpMethod.Get, "/api/v1/interno/comunicados", "comunicados-buscar"),
            (HttpMethod.Get, "/api/v1/interno/comunicados/1", "comunicados-buscar"),
            (HttpMethod.Post, "/api/v1/interno/comunicados", "comunicados-adicionar"),
            (HttpMethod.Put, "/api/v1/interno/comunicados/1", "comunicados-atualizar"),
            (HttpMethod.Delete, "/api/v1/interno/comunicados/1", "comunicados-remover"),
            (HttpMethod.Put, "/api/v1/interno/comunicados/1/publicacao", "comunicados-publicar"),

            // Exemplos — I38-I42 (003, BACK-C). Sem linha de publicação: exemplo não tem
            // publicação própria (decisão C1), logo não existe role exemplos-publicar.
            (HttpMethod.Get, "/api/v1/interno/versoes/1/exemplos", "exemplos-buscar"),
            (HttpMethod.Post, "/api/v1/interno/versoes/1/exemplos", "exemplos-adicionar"),
            (HttpMethod.Get, "/api/v1/interno/exemplos/1", "exemplos-buscar"),
            (HttpMethod.Put, "/api/v1/interno/exemplos/1", "exemplos-atualizar"),
            (HttpMethod.Delete, "/api/v1/interno/exemplos/1", "exemplos-remover"),

            // Busca interna — I21, I22
            (HttpMethod.Get, "/api/v1/interno/busca?q=teste", "indicebusca-buscar"),
            (HttpMethod.Post, "/api/v1/interno/indice-busca/reconstrucao", "indicebusca-atualizar"),
        ];

        foreach (var linha in linhas)
            yield return [linha.Metodo, linha.Rota, linha.Role];
    }

    [Theory]
    [MemberData(nameof(EndpointsInternos))]
    public async Task Endpoint_SemToken_Retorna401(HttpMethod metodo, string rota, string roleExigida)
    {
        _ = roleExigida; // não usado neste caso — mantido na assinatura para MemberData único.
        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(metodo, rota) { Content = ConteudoParaMetodo(metodo) };

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: $"{metodo} {rota} deveria exigir autenticação (401 sem token)");
    }

    [Theory]
    [MemberData(nameof(EndpointsInternos))]
    public async Task Endpoint_ComRoleErrada_Retorna403(HttpMethod metodo, string rota, string roleExigida)
    {
        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(metodo, rota) { Content = ConteudoParaMetodo(metodo) };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, $"role-que-nao-existe-{roleExigida}");

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: $"{metodo} {rota} exige a role '{roleExigida}' — token autenticado sem ela deveria devolver 403");
    }

    private static HttpContent? ConteudoParaMetodo(HttpMethod metodo)
    {
        if (metodo == HttpMethod.Get || metodo == HttpMethod.Delete)
            return null;

        var conteudo = new StringContent("{}", Encoding.UTF8, "application/json");
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return conteudo;
    }
}
