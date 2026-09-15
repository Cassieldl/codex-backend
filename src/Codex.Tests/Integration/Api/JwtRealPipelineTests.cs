using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// A validação de JWT exercitada de verdade — o ponto cego que a demanda inteira carregou.
///
/// <para>
/// Todo teste de 401/403 do repositório usa <see cref="TestAuthHandler"/>, que substitui o
/// esquema: prova que a rota tem <c>[Authorize]</c> com a role certa, e nada sobre a validação
/// do token. Aqui o token atravessa o <c>JwtBearer</c> configurado por <c>SegurancaExtensoes</c>,
/// passa pelo <c>KeycloakRolesTransformation</c> e chega ao <c>[Authorize(Roles = ...)]</c> — a
/// cadeia completa, uma vez.
/// </para>
/// </summary>
public sealed class JwtRealPipelineTests : IClassFixture<CodexApiFactoryJwtReal>
{
    /// <summary>Rota de I1 — 401/403 são decididos antes do banco, então nenhum recurso precisa existir.</summary>
    private const string RotaProtegida = "/api/v1/interno/provedores";
    private const string RoleExigida = "provedores-buscar";

    private readonly CodexApiFactoryJwtReal _factory;

    public JwtRealPipelineTests(CodexApiFactoryJwtReal factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TokenRs256Valido_ComRoleNoResourceAccess_AtravessaAutenticacaoEAutorizacao()
    {
        using var resposta = await ChamarComToken(CriarToken());

        // Não asserir 200: sem banco o controller falha depois da autorização. O que este teste
        // prova é que a cadeia token -> validação -> transformation -> [Authorize] deixa passar.
        resposta.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        resposta.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SemTokenAlgum_Retorna401()
    {
        using var cliente = _factory.CreateClient();

        using var resposta = await cliente.GetAsync(RotaProtegida);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenExpirado_Retorna401()
    {
        // Duas horas atrás: bem além dos 5 minutos de tolerância de relógio padrão.
        var expirado = CriarToken(emitidoEm: DateTime.UtcNow.AddHours(-2), expiraEm: DateTime.UtcNow.AddHours(-1));

        using var resposta = await ChamarComToken(expirado);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "ValidateLifetime = true precisa recusar token fora da validade");
    }

    [Fact]
    public async Task TokenDeOutraAudience_Retorna401()
    {
        using var resposta = await ChamarComToken(CriarToken(audience: "outro-servico"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "sem ValidateAudience, token emitido para outro client do mesmo realm entraria aqui");
    }

    [Fact]
    public async Task TokenDeOutroIssuer_Retorna401()
    {
        using var resposta = await ChamarComToken(CriarToken(issuer: "https://idp-do-atacante/realms/codex"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenAssinadoPorOutraChaveRsa_Retorna401()
    {
        var chaveDoAtacante = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "chave-do-atacante" };
        var forjado = CriarToken(credenciais: new SigningCredentials(chaveDoAtacante, SecurityAlgorithms.RsaSha256));

        using var resposta = await ChamarComToken(forjado);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "ValidateIssuerSigningKey = true precisa recusar assinatura de chave desconhecida");
    }

    [Fact]
    public async Task TokenSemAssinatura_AlgNone_Retorna401()
    {
        using var resposta = await ChamarComToken(CriarToken(semCredenciais: true));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "token sem assinatura (alg none) é o bypass clássico de A07 e nunca pode autenticar");
    }

    /// <summary>
    /// Confusão de algoritmo isolada: o servidor recebe a chave simétrica do atacante como
    /// emissora confiável, então assinatura e chave conferem. O único obstáculo restante é
    /// <c>ValidAlgorithms = ["RS256"]</c> — se ele cair, o token passa.
    /// </summary>
    [Fact]
    public async Task TokenHs256_MesmoComAChaveAceitaComoEmissora_Retorna401()
    {
        var segredo = new byte[32];
        RandomNumberGenerator.Fill(segredo);
        var chaveSimetrica = new SymmetricSecurityKey(segredo) { KeyId = "chave-simetrica-do-atacante" };

        using var factoryQueConfiaNaChaveSimetrica = new CodexApiFactoryJwtReal { ChaveExtraConfiavel = chaveSimetrica };
        using var cliente = factoryQueConfiaNaChaveSimetrica.CreateClient();

        var tokenHs256 = CriarToken(credenciais: new SigningCredentials(chaveSimetrica, SecurityAlgorithms.HmacSha256));

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaProtegida);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenHs256);
        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenValidoSemRolesParaEsteClient_Retorna403()
    {
        using var resposta = await ChamarComToken(CriarToken(roles: []));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "autenticado sem permissão é 403, nunca 401 nem passagem livre");
    }

    /// <summary>
    /// Role no lugar errado do token (<c>realm_access</c> em vez de
    /// <c>resource_access.codex-backend</c>) não vale: o resource server só honra as roles do
    /// próprio client.
    /// </summary>
    [Fact]
    public async Task RoleCertaEmRealmAccess_NaoValeComoRoleDoClient_Retorna403()
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "codex.dev",
            ["realm_access"] = new Dictionary<string, object> { ["roles"] = new[] { RoleExigida } },
        };

        using var resposta = await ChamarComToken(CriarTokenComClaims(claims));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RoleDeOutroClientNoResourceAccess_NaoValeParaEsteClient_Retorna403()
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "codex.dev",
            ["resource_access"] = new Dictionary<string, object>
            {
                ["outro-client"] = new Dictionary<string, object> { ["roles"] = new[] { RoleExigida } },
            },
        };

        using var resposta = await ChamarComToken(CriarTokenComClaims(claims));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <c>resource_access</c> malformado atravessando o pipeline real — o vetor que
    /// <c>BAIXO-B06</c> descreve e que só tinha prova unitária.
    ///
    /// <para>
    /// A tolerância a formato inesperado é deliberada e falha <b>fechada</b>: sem roles extraídas
    /// não há <c>ClaimTypes.Role</c>, e todo <c>[Authorize(Roles = …)]</c> reprova. O que este
    /// caso impede é a regressão que importa — um formato exótico virando <c>500</c> (o token é
    /// controlado por quem chama, então seria negação de serviço barata) ou, pior, passando.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("raiz string")]
    [InlineData("client nao e objeto")]
    [InlineData("roles nao e array")]
    [InlineData("roles com elemento numerico")]
    [InlineData("client ausente")]
    public async Task ResourceAccessMalformado_NaoQuebraOPipeline_ETermaEm403(string caso)
    {
        var resourceAccess = MontarResourceAccessMalformado(caso);
        var claims = new Dictionary<string, object> { ["sub"] = "codex.dev", ["resource_access"] = resourceAccess };

        using var resposta = await ChamarComToken(CriarTokenComClaims(claims));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: $"'{caso}' precisa falhar fechado — nem 500 (DoS via token) nem passagem livre");
    }

    private static object MontarResourceAccessMalformado(string caso) => caso switch
    {
        "raiz string" => "nao-sou-objeto",
        "client nao e objeto" => new Dictionary<string, object> { [CodexApiFactoryJwtReal.AudienceDeTeste] = "nao-sou-objeto" },
        "roles nao e array" => new Dictionary<string, object>
        {
            [CodexApiFactoryJwtReal.AudienceDeTeste] = new Dictionary<string, object> { ["roles"] = "provedores-buscar" },
        },
        "roles com elemento numerico" => new Dictionary<string, object>
        {
            [CodexApiFactoryJwtReal.AudienceDeTeste] = new Dictionary<string, object> { ["roles"] = new object[] { 42 } },
        },
        "client ausente" => new Dictionary<string, object>
        {
            ["account"] = new Dictionary<string, object> { ["roles"] = new[] { "manage-account" } },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(caso), caso, "Caso de malformação não previsto."),
    };

    /// <summary>
    /// Guarda do próprio seam de teste: a factory troca só a fonte da chave. Se o wiring de
    /// produção afrouxar uma das flags de A07, o pipeline em execução deixa de provar o que os
    /// casos acima afirmam provar — e isso passaria calado.
    /// </summary>
    [Fact]
    public void ParametrosDeValidacaoEmExecucao_SaoOsDeProducao()
    {
        var parametros = _factory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .TokenValidationParameters;

        parametros.ValidateIssuer.Should().BeTrue();
        parametros.ValidateAudience.Should().BeTrue();
        parametros.ValidateLifetime.Should().BeTrue();
        parametros.ValidateIssuerSigningKey.Should().BeTrue();
        parametros.ValidAlgorithms.Should().BeEquivalentTo([SecurityAlgorithms.RsaSha256]);
        parametros.ValidAudiences.Should().Contain(CodexApiFactoryJwtReal.AudienceDeTeste);
    }

    private async Task<HttpResponseMessage> ChamarComToken(string token)
    {
        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaProtegida);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await cliente.SendAsync(requisicao);
    }

    private static string CriarToken(
        string? issuer = null,
        string? audience = null,
        DateTime? emitidoEm = null,
        DateTime? expiraEm = null,
        string[]? roles = null,
        SigningCredentials? credenciais = null,
        bool semCredenciais = false)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "codex.dev",
            ["resource_access"] = new Dictionary<string, object>
            {
                [CodexApiFactoryJwtReal.AudienceDeTeste] = new Dictionary<string, object>
                {
                    ["roles"] = roles ?? [RoleExigida],
                },
            },
        };

        return CriarTokenComClaims(claims, issuer, audience, emitidoEm, expiraEm, credenciais, semCredenciais);
    }

    private static string CriarTokenComClaims(
        Dictionary<string, object> claims,
        string? issuer = null,
        string? audience = null,
        DateTime? emitidoEm = null,
        DateTime? expiraEm = null,
        SigningCredentials? credenciais = null,
        bool semCredenciais = false)
    {
        var agora = emitidoEm ?? DateTime.UtcNow;
        var descritor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? CodexApiFactoryJwtReal.IssuerDeTeste,
            Audience = audience ?? CodexApiFactoryJwtReal.AudienceDeTeste,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expiraEm ?? agora.AddMinutes(5),
            Claims = claims,
            SigningCredentials = semCredenciais
                ? null
                : credenciais ?? new SigningCredentials(CodexApiFactoryJwtReal.ChaveDoIdp, SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descritor);
    }
}
