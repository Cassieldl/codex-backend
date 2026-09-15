using System.Net;
using System.Text.Json;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Achado <c>BAIXO-B08</c>: o <c>400 PARAMETRO_INVALIDO</c> do <c>/busca</c> devolvia
/// <c>"... caracteres. (Parameter 'entrada')"</c> a usuário anônimo — o service lançava
/// <c>ArgumentException(mensagem, nameof(entrada))</c> e o controller repassava <c>ex.Message</c>
/// cru. Mesma família do vazamento que a FIX-JSON fechou (tipo interno e offset de byte da
/// <c>JsonException</c>).
///
/// <para>
/// Duas metades são provadas aqui, porque a correção tem duas: (1) a mensagem devolvida é texto
/// nosso, sem nome de parâmetro nem nome de tipo; (2) o <c>catch</c> deixou de ser
/// <c>catch (ArgumentException)</c> — argumento inválido inesperado nascido abaixo do service não
/// vira mais um <c>400</c> carregando a mensagem dele, e sim o <c>500</c> genérico do middleware.
/// Sem a segunda metade, qualquer <c>ArgumentNullException</c> futura na pilha reabriria o mesmo
/// vazamento por outra porta.
/// </para>
///
/// <para>
/// Toda asserção negativa deste arquivo é precedida por uma asserção de igualdade sobre a
/// mensagem esperada: corpo vazio (ou 404 de rota trocada) satisfaria "não contém Parameter" sem
/// provar nada.
/// </para>
/// </summary>
public sealed class MensagemPublicaSemNomeDeParametroTests
{
    private const string MensagemEsperada = "O termo de busca precisa ter entre 2 e 300 caracteres.";

    /// <summary>Fragmentos de implementação que nunca podem alcançar o corpo da resposta.</summary>
    public static TheoryData<string> DetalhesInternos() =>
        new() { "Parameter", "entrada", "ArgumentException", "BuscaService", "Codex.", "nameof" };

    [Fact]
    public async Task P1Anonimo_TermoCurto_DevolveMensagemLimpaSemNomeDeParametro()
    {
        var corpo = await CorpoDoErroDeTermoCurtoAsync(comRoleInterna: false);

        corpo.Mensagem.Should().Be(MensagemEsperada,
            because: "guarda de vacuidade: a igualdade prova que HÁ mensagem antes de afirmar o que ela não contém");
        corpo.Codigo.Should().Be("PARAMETRO_INVALIDO");
        corpo.Bruto.Should().NotContain("(Parameter", "era exatamente o sufixo que ArgumentException anexava");
    }

    [Theory]
    [MemberData(nameof(DetalhesInternos))]
    public async Task P1Anonimo_TermoCurto_NaoVazaNenhumDetalheDeImplementacao(string fragmento)
    {
        var corpo = await CorpoDoErroDeTermoCurtoAsync(comRoleInterna: false);

        corpo.Mensagem.Should().Be(MensagemEsperada, because: "guarda de vacuidade da asserção negativa abaixo");
        corpo.Bruto.Should().NotContain(fragmento);
    }

    [Fact]
    public async Task I21Autenticado_TermoCurto_DevolveAMesmaMensagemLimpa()
    {
        // A superfície interna tinha o mesmo catch e o mesmo ex.Message. Corrigir só a pública
        // deixaria metade do vazamento vivo — e é a metade que a auditoria não mediu.
        var corpo = await CorpoDoErroDeTermoCurtoAsync(comRoleInterna: true);

        corpo.Mensagem.Should().Be(MensagemEsperada);
        corpo.Codigo.Should().Be("PARAMETRO_INVALIDO");
        corpo.Bruto.Should().NotContain("(Parameter");
    }

    [Fact]
    public async Task ArgumentExceptionInesperadaAbaixoDoService_NaoViraParametroInvalidoNemVazaAMensagemDela()
    {
        const string segredoDePilha = "conexaoInternaSecreta";

        using var fabrica = new FabricaComConsultaQueLancaArgumento(segredoDePilha);
        using var cliente = fabrica.CreateClient();

        // Termo VÁLIDO: passa a validação e chega no IConsultaBusca, que lança
        // ArgumentNullException com paramName secreto — o que o catch antigo capturaria.
        using var resposta = await cliente.GetAsync("/api/v1/busca?q=nota%20fiscal");

        var bruto = await resposta.Content.ReadAsStringAsync();
        bruto.Should().NotBeNullOrWhiteSpace(because: "guarda de vacuidade");

        using var documento = JsonDocument.Parse(bruto);
        var erro = documento.RootElement.GetProperty("error");

        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError,
            because: "argumento inválido inesperado é defeito nosso, nunca parâmetro inválido do leitor");
        erro.GetProperty("code").GetString().Should().Be("ERRO_INTERNO");
        erro.GetProperty("message").GetString().Should().Be("Ocorreu um erro inesperado.");
        bruto.Should().NotContain(segredoDePilha);
        bruto.Should().NotContain("(Parameter");
    }

    private static async Task<CorpoErro> CorpoDoErroDeTermoCurtoAsync(bool comRoleInterna)
    {
        using var fabrica = new FabricaDeBusca();
        using var cliente = fabrica.CreateClient();

        var rota = comRoleInterna ? "/api/v1/interno/busca?q=a" : "/api/v1/busca?q=a";

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);

        if (comRoleInterna)
            requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "indicebusca-buscar");

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var bruto = await resposta.Content.ReadAsStringAsync();
        bruto.Should().NotBeNullOrWhiteSpace(because: "guarda de vacuidade: corpo vazio passaria em qualquer NotContain");

        using var documento = JsonDocument.Parse(bruto);
        var erro = documento.RootElement.GetProperty("error");

        return new CorpoErro(
            erro.GetProperty("code").GetString(),
            erro.GetProperty("message").GetString(),
            bruto);
    }

    private sealed record CorpoErro(string? Codigo, string? Mensagem, string Bruto);

    /// <summary>
    /// Factory própria por teste: o balde de rate limit da política "busca" é estado por host, e
    /// gastá-lo numa factory compartilhada contaminaria os outros testes que tocam <c>/busca</c>.
    /// A connection string aponta para host inalcançável de propósito — nenhum caso deste arquivo
    /// chega ao banco.
    /// </summary>
    private class FabricaDeBusca : WebApplicationFactory<Program>
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

                ConfigurarConsultaBusca(services);
            });
        }

        protected virtual void ConfigurarConsultaBusca(IServiceCollection services)
        {
        }
    }

    private sealed class FabricaComConsultaQueLancaArgumento : FabricaDeBusca
    {
        private readonly string _paramNameSecreto;

        public FabricaComConsultaQueLancaArgumento(string paramNameSecreto)
        {
            _paramNameSecreto = paramNameSecreto;
        }

        protected override void ConfigurarConsultaBusca(IServiceCollection services)
        {
            services.RemoveAll<IConsultaBusca>();
            services.AddScoped<IConsultaBusca>(_ => new ConsultaQueLancaArgumento(_paramNameSecreto));
        }
    }

    private sealed class ConsultaQueLancaArgumento : IConsultaBusca
    {
        private readonly string _paramNameSecreto;

        public ConsultaQueLancaArgumento(string paramNameSecreto)
        {
            _paramNameSecreto = paramNameSecreto;
        }

        public Task<ResultadoBuscaDto> BuscarAsync(
            string termo,
            string? provedorSlug,
            IReadOnlyCollection<string>? tipos,
            int pagina,
            int tamanhoPagina,
            bool incluirRascunho,
            CancellationToken ct) =>
            throw new ArgumentNullException(_paramNameSecreto);
    }
}
