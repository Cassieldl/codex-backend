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
            (HttpMethod.Put, "/api/v1/interno/layout/versoes/1/estrutura", "versoeslayout-atualizar"),
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

            // Fila editorial — lacunas de busca
            (HttpMethod.Get, "/api/v1/interno/lacunas-busca", "lacunasbusca-buscar"),
            (HttpMethod.Get, "/api/v1/interno/lacunas-busca/1", "lacunasbusca-buscar"),
            (HttpMethod.Get, "/api/v1/interno/lacunas-busca/termos", "lacunasbusca-buscar"),
            (HttpMethod.Post, "/api/v1/interno/lacunas-busca", "lacunasbusca-adicionar"),
            (HttpMethod.Put, "/api/v1/interno/lacunas-busca/1/estado", "lacunasbusca-atualizar"),

            // Versões com verificação vencida
            (HttpMethod.Get, "/api/v1/interno/versoes/verificacoes-vencidas", "versoesprovedor-buscar"),

            // Busca interna — I21, I22
            (HttpMethod.Get, "/api/v1/interno/busca?q=teste", "indicebusca-buscar"),
            (HttpMethod.Post, "/api/v1/interno/indice-busca/reconstrucao", "indicebusca-atualizar"),
        ];

        foreach (var linha in linhas)
            yield return [linha.Metodo, linha.Rota, linha.Role];
    }

    private const int QuantidadeMinimaDeRotasDeEscrita = 46;

    private const int QuantidadeDeEscritasSemRoleDeBusca = 5;

    private static readonly string[] EntidadesSemRoleDeBusca = ["avisos"];

    public static IEnumerable<object[]> EscritasInternas() => EndpointsInternos()
        .Where(linha => (HttpMethod)linha[0] != HttpMethod.Get);

    public static IEnumerable<object[]> EscritasInternasComRoleDeBusca() => EscritasInternas()
        .Where(linha => RolesDoMapa().Contains(RoleDeBuscaDaEntidade((string)linha[2])))
        .Select(linha => new[] { linha[0], linha[1], linha[2], RoleDeBuscaDaEntidade((string)linha[2]) });

    [Fact]
    public void UniversoDeEscritas_NaoEncolhe()
    {
        EscritasInternas().Should().HaveCountGreaterThanOrEqualTo(QuantidadeMinimaDeRotasDeEscrita,
            because: "uma rota de escrita que sai da tabela deixa de ser provada contra escalada de role");
    }

    [Fact]
    public void EscritasSemRoleDeBusca_SaoSoAsDasEntidadesConhecidas()
    {
        var puladas = EscritasInternas()
            .Where(linha => !RolesDoMapa().Contains(RoleDeBuscaDaEntidade((string)linha[2])))
            .Select(linha => $"{linha[0]} {linha[1]}")
            .ToList();

        var entidadesPuladas = EscritasInternas()
            .Select(linha => EntidadeDaRole((string)linha[2]))
            .Where(entidade => !RolesDoMapa().Contains($"{entidade}-buscar"))
            .Distinct()
            .ToList();

        entidadesPuladas.Should().BeEquivalentTo(EntidadesSemRoleDeBusca,
            because: $"o caso 'só a role de busca' fica fora destas rotas: {string.Join(" | ", puladas)}");
        puladas.Should().HaveCount(QuantidadeDeEscritasSemRoleDeBusca);
    }

    [Theory]
    [MemberData(nameof(EscritasInternasComRoleDeBusca))]
    public async Task Escrita_ComSoARoleDeBuscaDaEntidade_Retorna403(HttpMethod metodo, string rota, string roleExigida, string roleDeBusca)
    {
        using var resposta = await EnviarComRoles(metodo, rota, roleDeBusca);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: $"{metodo} {rota} exige '{roleExigida}'; quem só lê ('{roleDeBusca}') não pode escrever");
    }

    [Theory]
    [MemberData(nameof(EscritasInternas))]
    public async Task Escrita_ComTodasAsOutrasRolesDoMapa_Retorna403(HttpMethod metodo, string rota, string roleExigida)
    {
        var outrasRoles = RolesDoMapa().Where(role => role != roleExigida);

        using var resposta = await EnviarComRoles(metodo, rota, string.Join(',', outrasRoles));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: $"{metodo} {rota} exige '{roleExigida}'; nenhuma outra role do mapa pode substituí-la");
    }

    private async Task<HttpResponseMessage> EnviarComRoles(HttpMethod metodo, string rota, string roles)
    {
        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(metodo, rota) { Content = ConteudoParaMetodo(metodo) };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, roles);

        return await cliente.SendAsync(requisicao);
    }

    private static HashSet<string> RolesDoMapa() => EndpointsInternos()
        .Select(linha => (string)linha[2])
        .ToHashSet(StringComparer.Ordinal);

    private static string EntidadeDaRole(string role) => role[..role.LastIndexOf('-')];

    private static string RoleDeBuscaDaEntidade(string role) => $"{EntidadeDaRole(role)}-buscar";

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
