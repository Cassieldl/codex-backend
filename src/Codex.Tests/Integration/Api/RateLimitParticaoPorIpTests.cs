using System.Net;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Prova que o balde da política "busca" é <b>por IP</b>, e não um balde global.
///
/// <para>
/// O 429 de P1 já tinha teste (<see cref="EnvelopeErroDeStatusTests"/>: envelope e
/// <c>Retry-After</c>), mas ele estoura a partição <c>"desconhecido"</c> — o
/// <c>TestServer</c> não preenche <c>RemoteIpAddress</c>, então toda requisição de todo teste
/// cai no mesmo balde de 5. Com isso, a correção que virou <c>AP-BACKEND-010</c> (o balde era
/// global único e um cliente abusivo derrubava a busca para todo mundo — auto-DoS) nunca foi
/// exercitada: o caminho de 30 permissões por IP identificado não era executado uma vez sequer.
/// </para>
///
/// <para>
/// O seam é mínimo e explícito: um <see cref="IStartupFilter"/> no início do pipeline copia o
/// header <see cref="HeaderIpDeTeste"/> para <c>Connection.RemoteIpAddress</c>, que é
/// exatamente de onde a política lê a chave de partição. Nada da política é alterado.
/// </para>
/// </summary>
public sealed class RateLimitParticaoPorIpTests
{
    private const string HeaderIpDeTeste = "X-Test-Remote-Ip";
    private const string RotaBusca = "/api/v1/busca?q=a";
    /// <summary>
    /// Qualquer rota da política serve; o índice do layout é a mais barata do grupo. A Home custa
    /// cinco consultas por requisição, e com o banco inalcançável da factory cada uma espera o
    /// timeout — a classe inteira passava de dez minutos só pela escolha da rota.
    /// </summary>
    private const string RotaVitrine = "/api/v1/layout";

    /// <summary>Espelha <c>RateLimitingExtensoes.LimitePorIp</c> (privado por ser detalhe da política).</summary>
    private const int LimitePorIp = 30;

    /// <summary>Espelha <c>RateLimitingExtensoes.LimiteSemIp</c> — orçamento reduzido da partição compartilhada.</summary>
    private const int LimiteSemIp = 5;

    /// <summary>Espelha <c>RateLimitingExtensoes.LimiteVitrinePorIp</c>.</summary>
    private const int LimiteVitrinePorIp = 120;

    /// <summary>Espelha <c>RateLimitingExtensoes.LimiteVitrineSemIp</c> — partição compartilhada.</summary>
    private const int LimiteVitrineSemIp = 20;

    [Fact]
    public async Task IpQueEstourouOLimite_NaoBloqueiaOutroIp()
    {
        using var fabrica = new FabricaComIpControlado();
        using var cliente = fabrica.CreateClient();

        var respostasDoAbusivo = await Disparar(cliente, "203.0.113.10", LimitePorIp + 1);

        respostasDoAbusivo[^1].Should().Be(HttpStatusCode.TooManyRequests,
            because: $"a {LimitePorIp + 1}a requisicao do mesmo IP estoura a janela");

        var respostaDoOutroCliente = (await Disparar(cliente, "198.51.100.20", 1))[0];

        respostaDoOutroCliente.Should().NotBe(HttpStatusCode.TooManyRequests,
            because: "com balde global, o abuso de um IP derrubaria a busca para todos — o auto-DoS de AP-BACKEND-010");
    }

    [Fact]
    public async Task IpIdentificado_TemOrcamentoMaiorQueAParticaoCompartilhada()
    {
        using var fabrica = new FabricaComIpControlado();
        using var cliente = fabrica.CreateClient();

        var comIp = await Disparar(cliente, "203.0.113.30", LimitePorIp);
        var semIp = await Disparar(cliente, ip: null, LimiteSemIp + 1);

        comIp.Should().NotContain(HttpStatusCode.TooManyRequests,
            because: $"um IP identificado tem {LimitePorIp} permissoes na janela");
        semIp[^1].Should().Be(HttpStatusCode.TooManyRequests,
            because: $"a particao de IP desconhecido e compartilhada e recebe so {LimiteSemIp} permissoes");
    }

    /// <summary>
    /// A vitrine anônima (Home, layout, provedores, erros, artigos) não tinha teto de taxa: com o
    /// <c>Cache-Control</c> de 60 s ignorado, a taxa de requisições virava carga direta no banco.
    /// Ela tem política PRÓPRIA — orçamento maior que o da busca, porque é a porta de entrada do
    /// portal e um escritório atrás de NAT chega como um IP só.
    /// </summary>
    [Fact]
    public async Task Vitrine_TemOrcamentoProprio_MaiorQueODaBusca()
    {
        using var fabrica = new FabricaComIpControlado();
        using var cliente = fabrica.CreateClient();

        var naFaixaDaBusca = await Disparar(cliente, "203.0.113.40", LimitePorIp + 1, RotaVitrine);

        naFaixaDaBusca.Should().NotContain(HttpStatusCode.TooManyRequests,
            because: $"a vitrine tem {LimiteVitrinePorIp} permissoes por IP — aplicar a politica da busca aqui barraria tráfego legítimo");
    }

    /// <summary>
    /// O teto é exercitado pela partição compartilhada (20), não pela de IP identificado (120):
    /// prova a mesma coisa — existe teto — a um sexto das requisições.
    /// </summary>
    [Fact]
    public async Task Vitrine_AcimaDoProprioTeto_Devolve429()
    {
        using var fabrica = new FabricaComIpControlado();
        using var cliente = fabrica.CreateClient();

        var respostas = await Disparar(cliente, ip: null, LimiteVitrineSemIp + 1, RotaVitrine);

        respostas[^1].Should().Be(HttpStatusCode.TooManyRequests,
            because: "sem teto nenhum, quem ignora o cache controla a carga do banco");
    }

    /// <summary>Baldes por política: estourar a busca não pode derrubar a Home do mesmo cliente.</summary>
    [Fact]
    public async Task BuscaEstourada_NaoDerrubaAVitrineDoMesmoIp()
    {
        using var fabrica = new FabricaComIpControlado();
        using var cliente = fabrica.CreateClient();

        const string ip = "203.0.113.60";
        var naBusca = await Disparar(cliente, ip, LimitePorIp + 1, RotaBusca);
        naBusca[^1].Should().Be(HttpStatusCode.TooManyRequests);

        var naVitrine = (await Disparar(cliente, ip, 1, RotaVitrine))[0];

        naVitrine.Should().NotBe(HttpStatusCode.TooManyRequests,
            because: "as duas politicas mantem baldes independentes, ainda que particionem pela mesma chave");
    }

    private static async Task<List<HttpStatusCode>> Disparar(
        HttpClient cliente,
        string? ip,
        int quantidade,
        string rota = RotaBusca)
    {
        var status = new List<HttpStatusCode>(quantidade);

        for (var i = 0; i < quantidade; i++)
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
            if (ip is not null)
                requisicao.Headers.Add(HeaderIpDeTeste, ip);

            using var resposta = await cliente.SendAsync(requisicao);
            status.Add(resposta.StatusCode);
        }

        return status;
    }

    /// <summary>
    /// Factory sem banco alcançável — a busca responde erro antes de tocar o Postgres, e o rate
    /// limiter conta a requisição de todo jeito por rodar antes da action. Estado do limitador
    /// vive no host, então cada teste cria a sua (AP-BACKEND-011).
    /// </summary>
    private sealed class FabricaComIpControlado : WebApplicationFactory<Program>
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
                services.AddSingleton<IStartupFilter, FiltroIpDeTeste>();

                // O índice do layout responde sem tocar o banco: uma rota que espera o timeout da
                // conexão a cada chamada faz dezenas de requisições ultrapassarem a janela de um
                // minuto, e o balde reseta no meio do teste — o 429 nunca chega e a asserção de
                // teto passa a medir a lentidão, não o limite.
                services.RemoveAll<ILayoutServicoPublico>();
                services.AddSingleton<ILayoutServicoPublico, LayoutServicoInstantaneo>();
            });
        }
    }

    private sealed class LayoutServicoInstantaneo : ILayoutServicoPublico
    {
        public Task<LayoutIndiceDto> ObterIndiceAsync(CancellationToken ct) =>
            Task.FromResult(new LayoutIndiceDto { Vigente = null, Versoes = [] });

        public Task<LayoutVersaoDto> ObterVersaoAsync(string versaoSlug, CancellationToken ct) =>
            throw new NotSupportedException("O teste de rate limit só exercita o índice.");

        public Task<CampoLayoutVerbeteDto> ObterCampoAsync(string campoSlug, CancellationToken ct) =>
            throw new NotSupportedException("O teste de rate limit só exercita o índice.");
    }

    /// <summary>Injeta o IP remoto no começo do pipeline, antes do <c>UseRateLimiter</c>.</summary>
    private sealed class FiltroIpDeTeste : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> proximoEstagio)
        {
            return app =>
            {
                app.Use(async (contexto, proximo) =>
                {
                    if (contexto.Request.Headers.TryGetValue(HeaderIpDeTeste, out var valor)
                        && IPAddress.TryParse(valor.ToString(), out var endereco))
                    {
                        contexto.Connection.RemoteIpAddress = endereco;
                    }
                    else
                    {
                        contexto.Connection.RemoteIpAddress = null;
                    }

                    await proximo();
                });

                proximoEstagio(app);
            };
        }
    }
}
