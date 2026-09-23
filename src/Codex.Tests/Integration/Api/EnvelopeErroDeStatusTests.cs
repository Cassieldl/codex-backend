using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Achado <c>BAIXO-B09</c>: dois status saíam fora do envelope único de <c>rules/api.md</c> —
/// o <c>429</c> sem corpo nenhum e sem <c>Retry-After</c> (o cliente não sabe por que foi barrado
/// nem quando pode voltar) e o <c>415</c> no <c>ProblemDetails</c> do framework
/// (<c>type</c>/<c>title</c>/<c>status</c>/<c>traceId</c>).
///
/// <para>
/// Precisão preservada da auditoria: o <c>415</c> <b>não</b> é superfície pré-autenticação — foi
/// medido que sem token dá <c>401</c> e com role errada dá <c>403</c>, e o <c>415</c> só aparece
/// depois da autorização. O que se corrige aqui é consistência de contrato, não brecha.
/// </para>
/// </summary>
public sealed class EnvelopeErroDeStatusTests
{
    [Fact]
    public async Task ContentTypeNaoSuportado_ComRoleCorreta_Retorna415NoEnvelopeUnico()
    {
        using var fabrica = new FabricaSemBanco();
        using var cliente = fabrica.CreateClient();

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interno/layout/versoes")
        {
            Content = new StringContent("rotulo=x", Encoding.UTF8, "text/plain"),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "versoeslayout-adicionar");

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        await AfirmarEnvelopeAsync(resposta, "MEDIA_TYPE_NAO_SUPORTADO");
    }

    [Fact]
    public async Task VerboNaoPermitidoNaRota_Retorna405NoEnvelopeUnico()
    {
        using var fabrica = new FabricaSemBanco();
        using var cliente = fabrica.CreateClient();

        using var requisicao = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/busca");

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        await AfirmarEnvelopeAsync(resposta, "METODO_NAO_PERMITIDO");
    }

    [Fact]
    public async Task LimiteDeRequisicoesExcedido_Retorna429ComEnvelopeERetryAfter()
    {
        using var fabrica = new FabricaSemBanco();
        using var cliente = fabrica.CreateClient();

        // Termo curto de propósito: 400 antes de qualquer acesso a banco. O rate limiter conta a
        // requisição de todo jeito, porque roda antes da action.
        HttpResponseMessage? respostaLimitada = null;

        for (var tentativa = 1; tentativa <= 20 && respostaLimitada is null; tentativa++)
        {
            var resposta = await cliente.GetAsync("/api/v1/busca?q=a");

            if (resposta.StatusCode == HttpStatusCode.TooManyRequests)
                respostaLimitada = resposta;
            else
                resposta.Dispose();
        }

        respostaLimitada.Should().NotBeNull(because: "a política \"busca\" precisa barrar o excesso com 429");

        using var resposta429 = respostaLimitada!;

        resposta429.Headers.RetryAfter.Should().NotBeNull(
            because: "sem Retry-After o cliente não sabe quando pode voltar e tende a insistir");
        await AfirmarEnvelopeAsync(resposta429, "LIMITE_REQUISICOES");
    }

    private static async Task AfirmarEnvelopeAsync(HttpResponseMessage resposta, string codigoEsperado)
    {
        var corpo = await resposta.Content.ReadAsStringAsync();

        corpo.Should().NotBeNullOrWhiteSpace(because: "erro sem corpo quebra o envelope único de rules/api.md");

        using var documento = JsonDocument.Parse(corpo);

        documento.RootElement.EnumerateObject().Select(propriedade => propriedade.Name).Should()
            .Equal(["error"], because: "nada além de 'error' na raiz — nunca o ProblemDetails do framework");

        var erro = documento.RootElement.GetProperty("error");

        erro.GetProperty("code").GetString().Should().Be(codigoEsperado);
        erro.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();

        // Sem o `NotBeNull` antes, o `?.` curto-circuitaria a cadeia inteira quando o header
        // faltasse — justamente o caso a pegar — e a asserção do tipo não chegaria a rodar.
        resposta.Content.Headers.ContentType.Should().NotBeNull();
        resposta.Content.Headers.ContentType!.MediaType.Should()
            .Be(MediaTypeHeaderValue.Parse("application/json").MediaType);
    }

    /// <summary>
    /// Factory própria: o balde de rate limit é estado por host, e gastá-lo na factory
    /// compartilhada contaminaria qualquer outro teste que toque <c>/busca</c>.
    /// </summary>
    private sealed class FabricaSemBanco : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configuracao) =>
            {
                configuracao.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] =
                        "Host=127.0.0.1;Port=1;Database=inalcancavel;Username=x;Password=x;Timeout=1;Command Timeout=1",
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                });
            });
        }
    }
}
