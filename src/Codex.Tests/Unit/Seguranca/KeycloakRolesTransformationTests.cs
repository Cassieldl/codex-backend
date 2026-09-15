using System.Security.Claims;
using Codex.API.Seguranca;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Unit.Seguranca;

/// <summary>
/// Prova (não só declara, AP-SECURITY-003) que <see cref="KeycloakRolesTransformation"/> achata
/// <c>resource_access.&lt;client&gt;.roles</c> em <see cref="ClaimTypes.Role"/> — token sintético,
/// sem depender de endpoint nenhum (analise-backend.md §9).
/// </summary>
public sealed class KeycloakRolesTransformationTests
{
    private const string ClienteRecurso = "codex-backend";

    private static KeycloakRolesTransformation CriarTransformacao(string clienteRecurso = ClienteRecurso)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Keycloak:resource"] = clienteRecurso })
            .Build();

        return new KeycloakRolesTransformation(configuracao);
    }

    private static ClaimsPrincipal CriarPrincipalAutenticado(string? resourceAccessJson)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        };

        if (resourceAccessJson is not null)
            claims.Add(new Claim("resource_access", resourceAccessJson));

        var identidade = new ClaimsIdentity(claims, authenticationType: "Bearer");
        return new ClaimsPrincipal(identidade);
    }

    [Fact]
    public async Task TransformAsync_ComRolesDoClienteRecurso_AdicionaClaimsDeRole()
    {
        var resourceAccess = """{"codex-backend":{"roles":["provedores-buscar","provedores-adicionar"]},"account":{"roles":["manage-account"]}}""";
        var principal = CriarPrincipalAutenticado(resourceAccess);
        var sut = CriarTransformacao();

        var resultado = await sut.TransformAsync(principal);

        resultado.FindAll(ClaimTypes.Role).Select(c => c.Value).Should()
            .BeEquivalentTo("provedores-buscar", "provedores-adicionar");
    }

    [Fact]
    public async Task TransformAsync_NuncaTrazRolesDeOutroClient()
    {
        var resourceAccess = """{"outro-client":{"roles":["algo-perigoso"]}}""";
        var principal = CriarPrincipalAutenticado(resourceAccess);
        var sut = CriarTransformacao();

        var resultado = await sut.TransformAsync(principal);

        resultado.FindAll(ClaimTypes.Role).Should().BeEmpty();
    }

    [Fact]
    public async Task TransformAsync_SemClaimResourceAccess_NaoLanca_NaoAdicionaRole()
    {
        var principal = CriarPrincipalAutenticado(resourceAccessJson: null);
        var sut = CriarTransformacao();

        var resultado = await sut.TransformAsync(principal);

        resultado.FindAll(ClaimTypes.Role).Should().BeEmpty();
    }

    [Fact]
    public async Task TransformAsync_ComJsonInvalido_NaoLanca_NaoAdicionaRole()
    {
        var principal = CriarPrincipalAutenticado("{ json inválido");
        var sut = CriarTransformacao();

        var act = async () => await sut.TransformAsync(principal);

        await act.Should().NotThrowAsync();
        principal.FindAll(ClaimTypes.Role).Should().BeEmpty();
    }

    [Fact]
    public async Task TransformAsync_ChamadoDuasVezes_EhIdempotente_NaoDuplicaClaims()
    {
        var resourceAccess = """{"codex-backend":{"roles":["provedores-buscar"]}}""";
        var principal = CriarPrincipalAutenticado(resourceAccess);
        var sut = CriarTransformacao();

        await sut.TransformAsync(principal);
        var resultado = await sut.TransformAsync(principal);

        resultado.FindAll(ClaimTypes.Role).Should().ContainSingle(c => c.Value == "provedores-buscar");
    }

    /// <summary>
    /// Os 10 formatos que a auditoria mediu (achado <c>BAIXO-B06</c>): em 7 deles a transformação
    /// lançava <see cref="InvalidOperationException"/> — que não é <c>JsonException</c> e escapava
    /// do <c>catch</c> —, derrubando toda a área interna com <c>500</c> em vez de <c>403</c>. Nome
    /// do caso vai no primeiro argumento só para o vermelho apontar qual formato quebrou.
    /// </summary>
    public static IEnumerable<object?[]> FormatosInesperadosDeResourceAccess()
    {
        yield return ["A_roles_com_numero", """{"codex-backend":{"roles":[1]}}""", Array.Empty<string>()];
        yield return ["B_roles_com_objeto", """{"codex-backend":{"roles":[{"nome":"x"}]}}""", Array.Empty<string>()];
        yield return ["C_roles_com_null", """{"codex-backend":{"roles":[null,"provedores-buscar"]}}""", new[] { "provedores-buscar" }];
        yield return ["D_roles_com_bool", """{"codex-backend":{"roles":[true]}}""", Array.Empty<string>()];
        yield return ["E_client_nao_objeto_string", """{"codex-backend":"provedores-buscar"}""", Array.Empty<string>()];
        yield return ["F_client_nao_objeto_numero", """{"codex-backend":7}""", Array.Empty<string>()];
        yield return ["G_client_array", """{"codex-backend":["provedores-buscar"]}""", Array.Empty<string>()];
        yield return ["H_raiz_array", """["codex-backend"]""", Array.Empty<string>()];
        yield return ["I_raiz_string", """ "codex-backend" """, Array.Empty<string>()];
        yield return ["J_ok_baseline", """{"codex-backend":{"roles":["provedores-buscar"]}}""", new[] { "provedores-buscar" }];
    }

    [Theory]
    [MemberData(nameof(FormatosInesperadosDeResourceAccess))]
    public async Task TransformAsync_ComFormatoInesperadoDeResourceAccess_NaoLanca_EExtraiSoOQueEhValido(
        string caso, string resourceAccessJson, string[] rolesEsperadas)
    {
        var principal = CriarPrincipalAutenticado(resourceAccessJson);
        var sut = CriarTransformacao();

        var act = async () => await sut.TransformAsync(principal);

        await act.Should().NotThrowAsync(because: $"o contrato documentado é 'nunca lança' ({caso})");
        principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Should()
            .BeEquivalentTo(rolesEsperadas, because: $"formato tolerado nunca pode INVENTAR role ({caso})");
    }

    [Fact]
    public async Task TransformAsync_ComRolesMisturandoStringERuido_AdicionaSomenteAsStrings()
    {
        // Tolerar formato malformado nunca pode virar "aceita token malformado como válido": o
        // ruído é descartado item a item, e a role legítima ao lado dele continua valendo.
        var resourceAccess = """{"codex-backend":{"roles":["provedores-buscar",1,null,{"x":1},"erros-buscar"]}}""";
        var principal = CriarPrincipalAutenticado(resourceAccess);
        var sut = CriarTransformacao();

        var resultado = await sut.TransformAsync(principal);

        resultado.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Should()
            .BeEquivalentTo("provedores-buscar", "erros-buscar");
    }

    [Fact]
    public async Task TransformAsync_PrincipalNaoAutenticado_NaoAdicionaRole()
    {
        var identidadeAnonima = new ClaimsIdentity(); // sem authenticationType => IsAuthenticated = false
        var principal = new ClaimsPrincipal(identidadeAnonima);
        var sut = CriarTransformacao();

        var resultado = await sut.TransformAsync(principal);

        resultado.FindAll(ClaimTypes.Role).Should().BeEmpty();
    }
}
