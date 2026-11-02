using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Enumera TODAS as rotas registradas (analise-backend.md §7.4/§9): qualquer rota anônima fora da
/// allowlist de P1-P6, ou qualquer rota sem <c>[Authorize]</c> nem <c>[AllowAnonymous]</c>
/// explícito, reprova a entrega — rota anônima que toca o banco não pode passar despercebida
/// (rules/security.md, AP-SECURITY-003).
///
/// <para>
/// A chave da rota é o par <b>(verbo, template)</b> avaliado para CADA verbo da action, nunca para
/// o primeiro: uma action anônima com <c>[HttpGet, HttpPost]</c> passaria como "GET" e uma action
/// anônima <b>sem</b> atributo de verbo casa todos os métodos (inclusive escrita) e passaria como
/// "GET" também. Esse era o achado <c>MED-B01</c> — o detector, não a superfície.
/// </para>
///
/// <para>
/// O próprio detector tem aceite por mutação sintética (os <c>Detector_*</c> abaixo): descritores
/// fabricados com os casos que ele precisa pegar, para que o controle nunca volte a ser
/// verificado só por leitura (AP-SECURITY-003/006).
/// </para>
/// </summary>
public sealed class AllowlistRotasAnonimasTests : IClassFixture<CodexApiFactory>
{
    private static readonly HashSet<string> RotasPublicasPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET api/v1/busca",
        "GET api/v1/provedores",
        "GET api/v1/provedores/{provedorSlug}",
        "GET api/v1/provedores/{provedorSlug}/versoes/{versaoSlug}",
        // Envio modelo da versão publicada: recorte da estrutura do layout, só GET.
        "GET api/v1/provedores/{provedorSlug}/versoes/{versaoSlug}/envio-modelo",
        "GET api/v1/erros",
        "GET api/v1/erros/{erroSlug}",
        // Artigos — P7/P8 (BACK-B)
        "GET api/v1/artigos",
        "GET api/v1/artigos/{artigoSlug}",
        // Layout InvoiCy — P10/P11/P12 (003, BACK-B)
        "GET api/v1/layout",
        "GET api/v1/layout/versoes/{versaoSlug}",
        "GET api/v1/layout/campos/{campoSlug}",
        // Estrutura do documento de uma versão publicada do layout, só GET.
        "GET api/v1/layout/versoes/{versaoSlug}/estrutura",
        // Home — P9 (003, BACK-D). Agregado de leitura da tela inicial: só GET, nada de escrita.
        "GET api/v1/home",
        // Comunicados por contexto (provedor/versão) — leitura da mesma regra pública da Home, só GET.
        "GET api/v1/comunicados",
        // Sondas do orquestrador (004). Anônimas porque o probe não carrega token; são controller
        // justamente para caírem sob este detector — ver SaudeController.
        "GET api/v1/saude/vivo",
        "GET api/v1/saude/pronto",
    };

    private readonly CodexApiFactory _factory;

    public AllowlistRotasAnonimasTests(CodexApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void TodaRotaRegistrada_TemAuthorizeOuEstaNaAllowlistDeAnonimas()
    {
        var acoesRegistradas = AcoesRegistradas();

        acoesRegistradas.Should().NotBeEmpty("o provider de action descriptors precisa enxergar os controllers reais da API");

        var violacoes = DetectarViolacoes(acoesRegistradas, RotasPublicasPermitidas);

        violacoes.Should().BeEmpty(because: string.Join(" | ", violacoes));
    }

    [Fact]
    public void RotasPublicasPermitidas_TodasExistemDeFato_ESaoAnonimas()
    {
        var chavesAnonimasRegistradas = AcoesRegistradas()
            .Where(EhAnonima)
            .SelectMany(ChavesDaRota)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var rotaEsperada in RotasPublicasPermitidas)
        {
            chavesAnonimasRegistradas.Should().Contain(rotaEsperada,
                because: "a allowlist não pode citar uma rota que não existe mais, nem uma que deixou de ser anônima (allowlist morta)");
        }
    }

    // ------------------------------------------------------------------ aceite do próprio detector

    [Fact]
    public void Detector_PegaRotaAnonimaSemRestricaoDeVerbo()
    {
        // [Route("")] sem [HttpGet] casa POST/PUT/PATCH/DELETE anonimamente, e o template está
        // na allowlist — era exatamente a fuga do MED-B01.
        var acao = AcaoSintetica("api/v1/busca", verbos: null, new AllowAnonymousAttribute());

        var violacoes = DetectarViolacoes([acao], RotasPublicasPermitidas);

        violacoes.Should().ContainSingle()
            .Which.Should().Contain("SEM restrição de verbo");
    }

    [Fact]
    public void Detector_PegaVerboDeEscritaEmRotaAnonimaComTemplateDaAllowlist()
    {
        var acao = AcaoSintetica("api/v1/busca", verbos: ["GET", "POST"], new AllowAnonymousAttribute());

        var violacoes = DetectarViolacoes([acao], RotasPublicasPermitidas);

        violacoes.Should().ContainSingle()
            .Which.Should().Contain("POST api/v1/busca");
    }

    [Fact]
    public void Detector_PegaRotaAnonimaComTemplateForaDaAllowlist()
    {
        var acao = AcaoSintetica("api/v1/interno/provedores", verbos: ["GET"], new AllowAnonymousAttribute());

        var violacoes = DetectarViolacoes([acao], RotasPublicasPermitidas);

        violacoes.Should().ContainSingle()
            .Which.Should().Contain("GET api/v1/interno/provedores");
    }

    [Fact]
    public void Detector_PegaRotaSemAuthorizeNemAllowAnonymous()
    {
        var acao = AcaoSintetica("api/v1/interno/orfa", verbos: ["POST"]);

        var violacoes = DetectarViolacoes([acao], RotasPublicasPermitidas);

        violacoes.Should().ContainSingle()
            .Which.Should().Contain("desprotegida por omissão");
    }

    [Fact]
    public void Detector_NaoAcusaRotaLegitima_AnonimaNaAllowlistOuProtegidaPorAuthorize()
    {
        // Controle negativo: sem isto, um detector que acusa tudo passaria nos testes acima.
        ActionDescriptor[] acoes =
        [
            AcaoSintetica("api/v1/busca", verbos: ["GET"], new AllowAnonymousAttribute()),
            AcaoSintetica("api/v1/interno/provedores", verbos: ["POST"], new AuthorizeAttribute("provedores-adicionar")),
        ];

        DetectarViolacoes(acoes, RotasPublicasPermitidas).Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------ detector

    /// <summary>
    /// Núcleo do gabarito, isolado para poder ser exercitado com descritores sintéticos: uma
    /// violação por (verbo, template), mais a violação específica de "anônima sem restrição de
    /// verbo" — que não tem verbo para chavear e por isso escapava.
    /// </summary>
    private static IReadOnlyList<string> DetectarViolacoes(
        IEnumerable<ActionDescriptor> acoes, IReadOnlySet<string> allowlist)
    {
        var violacoes = new List<string>();

        foreach (var acao in acoes)
        {
            var template = acao.AttributeRouteInfo?.Template ?? string.Empty;
            var verbos = VerbosDaRota(acao);

            if (EhAnonima(acao))
            {
                // Nenhuma restrição de verbo = casa TODOS os métodos HTTP. Nunca pode ser
                // anônima, mesmo que o template esteja na allowlist (que é de GET).
                if (verbos.Count == 0)
                {
                    violacoes.Add($"[*] {template} é [AllowAnonymous] SEM restrição de verbo — casa POST/PUT/PATCH/DELETE anonimamente (achado de segurança).");
                    continue;
                }

                foreach (var verbo in verbos.Where(verbo => !allowlist.Contains($"{verbo} {template}")))
                    violacoes.Add($"{verbo} {template} é [AllowAnonymous] mas NÃO está na allowlist de P1-P6 (achado de segurança).");
            }
            else if (!acao.EndpointMetadata.OfType<IAuthorizeData>().Any())
            {
                var verbosDescritos = verbos.Count == 0 ? "[*]" : string.Join("/", verbos);
                violacoes.Add($"{verbosDescritos} {template} não tem [Authorize] nem [AllowAnonymous] — rota desprotegida por omissão.");
            }
        }

        return violacoes;
    }

    /// <summary>
    /// TODOS os verbos da rota — nunca o primeiro. Coleção vazia significa "sem restrição de
    /// verbo", que casa qualquer método HTTP.
    /// </summary>
    private static IReadOnlyList<string> VerbosDaRota(ActionDescriptor acao) =>
        acao.ActionConstraints?.OfType<HttpMethodActionConstraint>()
            .SelectMany(constraint => constraint.HttpMethods)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    internal static IEnumerable<string> ChavesDaRota(ActionDescriptor acao)
    {
        var template = acao.AttributeRouteInfo?.Template ?? string.Empty;

        return VerbosDaRota(acao).Select(verbo => $"{verbo} {template}");
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

    private static ActionDescriptor AcaoSintetica(string template, string[]? verbos, params object[] metadata) =>
        new ControllerActionDescriptor
        {
            AttributeRouteInfo = new AttributeRouteInfo { Template = template },
            EndpointMetadata = [.. metadata],
            ActionConstraints = verbos is null
                ? null
                : [new HttpMethodActionConstraint(verbos)],
        };
}
