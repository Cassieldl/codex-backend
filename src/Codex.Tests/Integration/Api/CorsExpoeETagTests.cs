using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Achado <c>AP-SECURITY-012</c> — terceira camada do <c>If-Match</c>: o backend passou a
/// EMITIR <c>ETag</c> nos GETs de detalhe internos e nas respostas de mutação, mas <c>ETag</c>
/// <b>não</b> é header de resposta CORS-safelisted. Sem
/// <c>Access-Control-Expose-Headers: ETag</c>, <c>response.headers.etag</c> volta
/// <c>undefined</c> no navegador em requisição cross-origin, e o cliente continua sem ter o
/// validador que os writes internos exigem: emitir resolveu o protocolo e não resolveu o
/// cliente.
///
/// <para>
/// Precisões que estes testes preservam de propósito, porque a correção é estreita:
/// o <c>304</c> de P4 <b>não</b> depende disto (a revalidação por <c>If-None-Match</c> é do
/// próprio navegador e não passa por <c>Access-Control-Expose-Headers</c>), e o <c>If-Match</c>
/// de <b>requisição</b> já era coberto por <c>AllowAnyHeader()</c>. Por isso os casos abaixo
/// afirmam também o que <b>não</b> mudou: origem única (nunca <c>*</c>), sem
/// <c>Allow-Credentials</c>, e a lista de expostos com <c>ETag</c> e nada mais.
/// </para>
/// </summary>
public sealed class CorsExpoeETagTests
{
    private const string OrigemPermitida = "https://codex-frontend.teste";
    private const string OrigemEstranha = "https://atacante.teste";

    // 401 sem token, não toca o banco e não gasta balde de rate limit — o que interessa aqui são
    // os headers de CORS, que saem antes da autenticação (UseCors precede UseAuthentication).
    private const string RotaQualquer = "/api/v1/interno/provedores";

    [Fact]
    public async Task RequisicaoCrossOriginDaOrigemPermitida_ExpoeETagAoJavaScript()
    {
        using var resposta = await RequisitarComOrigemAsync(OrigemPermitida);

        resposta.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal([OrigemPermitida],
            because: "guarda de vacuidade: sem isto a resposta não é sequer uma resposta CORS, e qualquer "
                     + "afirmação sobre os headers de CORS seria vazia");

        resposta.Headers.GetValues("Access-Control-Expose-Headers").Should().Equal(["ETag"],
            because: "ETag não é CORS-safelisted: sem o expose o navegador esconde o validador do JavaScript, "
                     + "e nenhum PUT/DELETE interno consegue montar o If-Match que o backend exige");
    }

    [Fact]
    public async Task PoliticaContinuaRestritiva_SemCoringaSemCredenciaisSemExtraExposto()
    {
        using var resposta = await RequisitarComOrigemAsync(OrigemPermitida);

        var origemDevolvida = resposta.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Subject;

        origemDevolvida.Should().Be(OrigemPermitida);
        origemDevolvida.Should().NotBe("*", because: "rules/security.md A05 — nunca coringa de origem");

        resposta.Headers.Should().NotContainKey("Access-Control-Allow-Credentials",
            because: "o token vai no header Authorization, nunca em cookie — o expose de ETag não muda isso");

        resposta.Headers.GetValues("Access-Control-Expose-Headers").Should().Equal(["ETag"],
            because: "a correção é o expose de ETag e nada mais — nenhum header interno a mais fica legível");
    }

    [Fact]
    public async Task RequisicaoDeOrigemNaoPermitida_NaoRecebeNenhumHeaderDeCors()
    {
        using var resposta = await RequisitarComOrigemAsync(OrigemEstranha);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "guarda de vacuidade: a rota respondeu de fato, então a ausência dos headers é significativa");

        resposta.Headers.Should().NotContainKey("Access-Control-Allow-Origin");
        resposta.Headers.Should().NotContainKey("Access-Control-Expose-Headers");
    }

    [Theory]
    [InlineData("OPTIONS")]
    [InlineData("GET")]
    public async Task SoAOrigemPermitida_RecebeAllowOrigin(string metodo)
    {
        using var fabrica = new FabricaComOrigemDeclarada();
        using var cliente = fabrica.CreateClient();

        using var requisicaoPermitida = RequisicaoCors(metodo, OrigemPermitida);
        using var requisicaoEstranha = RequisicaoCors(metodo, OrigemEstranha);

        using var respostaPermitida = await cliente.SendAsync(requisicaoPermitida);
        using var respostaEstranha = await cliente.SendAsync(requisicaoEstranha);

        respostaPermitida.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal([OrigemPermitida],
            because: "controle positivo: sem ele a ausência do header na origem estranha não prova nada");

        respostaEstranha.Headers.Should().NotContainKey("Access-Control-Allow-Origin",
            because: "origem fora da allowlist não pode ler a resposta nem passar do preflight");
    }

    [Fact]
    public async Task PreflightDeEscritaInterna_JaAceitavaIfMatchNaRequisicao()
    {
        // A metade de REQUISIÇÃO do contrato nunca esteve quebrada — AllowAnyHeader() cobre
        // If-Match. Fixado em teste para que a correção do expose não seja confundida com um
        // alargamento da política de headers de requisição.
        using var fabrica = new FabricaComOrigemDeclarada();
        using var cliente = fabrica.CreateClient();

        using var requisicao = new HttpRequestMessage(HttpMethod.Options, RotaQualquer);
        requisicao.Headers.Add("Origin", OrigemPermitida);
        requisicao.Headers.Add("Access-Control-Request-Method", "PUT");
        requisicao.Headers.Add("Access-Control-Request-Headers", "If-Match");

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal([OrigemPermitida]);
        resposta.Headers.GetValues("Access-Control-Allow-Headers").Should().Contain("If-Match");
    }

    private static HttpRequestMessage RequisicaoCors(string metodo, string origem)
    {
        var requisicao = new HttpRequestMessage(new HttpMethod(metodo), RotaQualquer);
        requisicao.Headers.Add("Origin", origem);

        if (metodo == "OPTIONS")
            requisicao.Headers.Add("Access-Control-Request-Method", "GET");

        return requisicao;
    }

    private static async Task<HttpResponseMessage> RequisitarComOrigemAsync(string origem)
    {
        using var fabrica = new FabricaComOrigemDeclarada();
        var cliente = fabrica.CreateClient();

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaQualquer);
        requisicao.Headers.Add("Origin", origem);

        return await cliente.SendAsync(requisicao);
    }

    /// <summary>
    /// Declara <c>Cors:OrigemPermitida</c> por configuração (valor de teste, diferente do
    /// <c>appsettings.Development.json</c>): o que se prova é que a política lê a origem
    /// configurada e expõe o <c>ETag</c> para ela — nunca que uma origem específica está
    /// embutida no código.
    /// </summary>
    private sealed class FabricaComOrigemDeclarada : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            // UseSetting, não ConfigureAppConfiguration: a política de CORS é montada durante o
            // builder do Program (leitura direta de builder.Configuration), antes de qualquer
            // fonte adicionada por ConfigureAppConfiguration entrar em jogo — medido: com
            // ConfigureAppConfiguration a origem de teste não chega na política e nenhum header
            // de CORS sai.
            builder.UseSetting("Cors:OrigemPermitida", OrigemPermitida);
            builder.UseSetting(
                "ConnectionStrings:Default",
                "Host=127.0.0.1;Port=1;Database=inalcancavel;Username=x;Password=x;Timeout=1;Command Timeout=1");
        }
    }
}
