using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// PROBE DE QA — complemento adversarial ao <see cref="PipelineAutorizacaoTests"/>.
///
/// <para>
/// O teste existente prova 401 (sem token) e 403 (com uma role sintética inexistente). Nenhum dos
/// dois lê a role declarada na tabela: se uma linha do <c>MemberData</c> declarar a role errada,
/// os dois casos continuam verdes e a linha não protege nada. Este probe fecha a lacuna pelo lado
/// positivo: manda a role EXATA declarada na tabela e exige que o pipeline NÃO devolva 401/403.
/// Se devolver 403, a role da tabela diverge da que o <c>[Authorize]</c> do controller exige.
/// </para>
/// </summary>
public sealed class RoleCorretaPassaTests : IClassFixture<CodexApiFactory>
{
    private readonly CodexApiFactory _factory;

    public RoleCorretaPassaTests(CodexApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [MemberData(nameof(PipelineAutorizacaoTests.EndpointsInternos), MemberType = typeof(PipelineAutorizacaoTests))]
    public async Task Endpoint_ComRoleDeclaradaNaTabela_NaoRetorna401Nem403(HttpMethod metodo, string rota, string roleExigida)
    {
        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(metodo, rota) { Content = ConteudoParaMetodo(metodo) };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, roleExigida);

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            because: $"{metodo} {rota} com token autenticado nunca deveria devolver 401");
        resposta.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            because: $"a tabela de §7.4 declara que {metodo} {rota} exige '{roleExigida}' — 403 aqui significa que a role declarada diverge da exigida pelo [Authorize] do controller");
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
