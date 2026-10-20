using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Codex.Tests.Integration.Api;

/// <summary>Toda rota anônima que toca o banco precisa de teto de taxa, com a política certa para ela.</summary>
public sealed class RateLimitEmRotasAnonimasTests : IClassFixture<CodexApiFactory>
{
    // Só as duas sondas do orquestrador ficam fora do teto, por verbo; rota ou verbo novo de saúde cai na guarda.
    private static readonly HashSet<string> SondasSemTeto = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET api/v1/saude/vivo",
        "GET api/v1/saude/pronto",
    };

    private const string PoliticaBusca = "busca";
    private const string PoliticaVitrine = "vitrine";

    // Busca, Provedores (3), Erros (2), Artigos (2), Layout (3), Home e Comunicados.
    private const int MinimoDeAcoesAnonimasComBanco = 13;

    private readonly CodexApiFactory _factory;

    public RateLimitEmRotasAnonimasTests(CodexApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void TodaAcaoAnonimaForaDaSaude_TemRateLimitComAPoliticaEsperada()
    {
        var acoesAnonimasComBanco = AcoesSobAGuarda(AcoesRegistradas());

        acoesAnonimasComBanco.Should().HaveCountGreaterThanOrEqualTo(MinimoDeAcoesAnonimasComBanco,
            "o universo encolher em silêncio esvazia a guarda");

        var violacoes = DetectarViolacoes(acoesAnonimasComBanco);

        violacoes.Should().BeEmpty(because: string.Join(" | ", violacoes));
    }

    [Fact]
    public void RotaNovaDeSaude_SemRateLimit_EntraNaGuardaEReprova()
    {
        ActionDescriptor[] acoes =
        [
            AcaoSinteticaComVerbo("GET", "api/v1/saude/vivo", new AllowAnonymousAttribute()),
            AcaoSinteticaComVerbo("GET", "api/v1/saude/pronto", new AllowAnonymousAttribute()),
            AcaoSinteticaComVerbo("GET", "api/v1/saude/diagnostico", new AllowAnonymousAttribute()),
            AcaoSinteticaComVerbo("POST", "api/v1/saude/vivo", new AllowAnonymousAttribute()),
        ];

        var violacoes = DetectarViolacoes(AcoesSobAGuarda(acoes));

        violacoes.Should().HaveCount(2);
        violacoes.Should().ContainSingle(violacao => violacao.Contains("api/v1/saude/diagnostico"))
            .Which.Should().Contain("sem rate limit");
        violacoes.Should().ContainSingle(violacao => violacao.Contains("api/v1/saude/vivo"))
            .Which.Should().Contain("sem rate limit");
    }

    [Fact]
    public void Detector_PegaAcaoAnonimaSemRateLimit()
    {
        var acao = AcaoSintetica("api/v1/home", new AllowAnonymousAttribute());

        DetectarViolacoes([acao]).Should().ContainSingle()
            .Which.Should().Contain("api/v1/home").And.Contain("sem rate limit");
    }

    [Fact]
    public void Detector_PegaAcaoAnonimaComPoliticaDeOutraRota()
    {
        var acao = AcaoSintetica(
            "api/v1/busca", new AllowAnonymousAttribute(), new EnableRateLimitingAttribute(PoliticaVitrine));

        DetectarViolacoes([acao]).Should().ContainSingle()
            .Which.Should().Contain("api/v1/busca").And.Contain("\"vitrine\"");
    }

    [Fact]
    public void Detector_PegaRateLimitDesligadoNaAcao_MesmoComOControllerLigado()
    {
        // O middleware pula a requisição quando acha DisableRateLimiting, não importa a ordem.
        var acao = AcaoSintetica(
            "api/v1/erros/{erroSlug}",
            new AllowAnonymousAttribute(),
            new EnableRateLimitingAttribute(PoliticaVitrine),
            new DisableRateLimitingAttribute());

        DetectarViolacoes([acao]).Should().ContainSingle()
            .Which.Should().Contain("api/v1/erros/{erroSlug}").And.Contain("desligado");
    }

    [Fact]
    public void Detector_NaoAcusaAcaoComAPoliticaCerta()
    {
        // Controle negativo: sem isto, um detector que acusa tudo passaria nos testes acima.
        ActionDescriptor[] acoes =
        [
            AcaoSintetica("api/v1/busca", new AllowAnonymousAttribute(), new EnableRateLimitingAttribute(PoliticaBusca)),
            AcaoSintetica("api/v1/home", new AllowAnonymousAttribute(), new EnableRateLimitingAttribute(PoliticaVitrine)),
        ];

        DetectarViolacoes(acoes).Should().BeEmpty();
    }

    private static IReadOnlyList<string> DetectarViolacoes(IEnumerable<ActionDescriptor> acoes)
    {
        var violacoes = new List<string>();

        foreach (var acao in acoes)
        {
            var template = Template(acao);
            var politicaEsperada = PoliticaEsperada(template);

            if (acao.EndpointMetadata.OfType<DisableRateLimitingAttribute>().Any())
            {
                violacoes.Add($"{template} é anônima com rate limit desligado.");
                continue;
            }

            // O middleware usa a última ocorrência, então a da ação vence a do controller.
            var politicaAplicada = acao.EndpointMetadata.OfType<EnableRateLimitingAttribute>().LastOrDefault();

            if (politicaAplicada is null)
            {
                violacoes.Add($"{template} é anônima e está sem rate limit (esperado \"{politicaEsperada}\").");
                continue;
            }

            if (!string.Equals(politicaAplicada.PolicyName, politicaEsperada, StringComparison.Ordinal))
                violacoes.Add($"{template} usa a política \"{politicaAplicada.PolicyName}\", esperado \"{politicaEsperada}\".");
        }

        return violacoes;
    }

    private static string PoliticaEsperada(string template) =>
        template.StartsWith("api/v1/busca", StringComparison.OrdinalIgnoreCase) ? PoliticaBusca : PoliticaVitrine;

    private static string Template(ActionDescriptor acao) => acao.AttributeRouteInfo?.Template ?? string.Empty;

    private static List<ActionDescriptor> AcoesSobAGuarda(IEnumerable<ActionDescriptor> acoes) =>
        acoes.Where(EhAnonima).Where(acao => !EhSondaSemTeto(acao)).ToList();

    // Ação sem verbo casa qualquer método, então nunca é sonda isenta.
    private static bool EhSondaSemTeto(ActionDescriptor acao)
    {
        var chaves = AllowlistRotasAnonimasTests.ChavesDaRota(acao).ToList();

        return chaves.Count > 0 && chaves.All(SondasSemTeto.Contains);
    }

    private static bool EhAnonima(ActionDescriptor acao) =>
        acao.EndpointMetadata.OfType<IAllowAnonymous>().Any();

    private List<ControllerActionDescriptor> AcoesRegistradas()
    {
        using var escopo = _factory.Services.CreateScope();
        var provedorDeAcoes = escopo.ServiceProvider.GetRequiredService<IActionDescriptorCollectionProvider>();

        return provedorDeAcoes.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .ToList();
    }

    private static ActionDescriptor AcaoSintetica(string template, params object[] metadata) =>
        new ControllerActionDescriptor
        {
            AttributeRouteInfo = new AttributeRouteInfo { Template = template },
            EndpointMetadata = [.. metadata],
        };

    private static ActionDescriptor AcaoSinteticaComVerbo(string verbo, string template, params object[] metadata) =>
        new ControllerActionDescriptor
        {
            AttributeRouteInfo = new AttributeRouteInfo { Template = template },
            EndpointMetadata = [.. metadata],
            ActionConstraints = [new HttpMethodActionConstraint([verbo])],
        };
}
