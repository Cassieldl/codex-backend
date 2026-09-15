using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// Achado <c>MED-B03</c>: o <c>UseSerilog</c> substituía todo o provedor de logging e o
/// <c>ReadFrom.Configuration</c> não encontrava seção <c>Serilog</c> nenhuma — resultado, pipeline
/// SEM sink: zero eventos emitidos, e o <c>traceId</c> que a API devolve no corpo do 5xx apontando
/// para um registro que nunca foi escrito. Detector: sink em memória plugado via
/// <c>ReadFrom.Services</c>, afirmando que <c>403</c> e <c>500</c> produzem evento e que o
/// <c>traceId</c> do corpo aparece no evento.
///
/// <para>
/// Registrado ≠ emitido (AP-SECURITY-003): estes testes olham o evento que saiu do pipeline, não a
/// configuração que diz que ele sairia.
/// </para>
/// </summary>
public sealed class LogEstruturadoTests
{
    [Fact]
    public async Task Erro500_EmiteUmEventoDeError_ComOMesmoTraceIdDevolvidoNoCorpo()
    {
        using var fabrica = new FabricaComSinkEmMemoria();
        using var cliente = fabrica.CreateClient();

        // Banco inalcançável de propósito: é o jeito determinístico de chegar ao caminho de 500 do
        // TratamentoErrosMiddleware sem depender de Docker.
        using var resposta = await cliente.GetAsync("/api/v1/provedores");

        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var traceIdDoCorpo = TraceIdDoCorpo(await resposta.Content.ReadAsStringAsync());
        traceIdDoCorpo.Should().NotBeNullOrWhiteSpace();

        var erros = fabrica.Sink.Eventos.Where(evento => evento.Level == LogEventLevel.Error).ToList();

        erros.Should().NotBeEmpty(because: "sem sink configurado nenhum evento é emitido — era exatamente o MED-B03");
        erros.Select(evento => Propriedade(evento, "TraceId")).Should().Contain(traceIdDoCorpo,
            because: "o traceId devolvido ao cliente tem de existir em algum registro, ou é incorrelacionável");
    }

    [Fact]
    public async Task Acesso403_EmiteEventoDeWarning_ComStatusPathETraceId()
    {
        using var fabrica = new FabricaComSinkEmMemoria();
        using var cliente = fabrica.CreateClient();

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interno/layout/versoes")
        {
            Content = JsonContent.Create(new { rotulo = "x", descricao = (string?)null }),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "role-que-nao-existe");

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var evento = EventosDeAcessoNegado(fabrica).Should().ContainSingle().Subject;

        Propriedade(evento, "StatusCode").Should().Be("403");
        evento.Level.Should().Be(LogEventLevel.Warning, because: "4xx é Warning (rules/backend.md)");
        Propriedade(evento, "Path").Should().Be("/api/v1/interno/layout/versoes");
        Propriedade(evento, "TraceId").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Acesso401_EmiteEventoDeWarning_ComUsuarioAnonimo()
    {
        using var fabrica = new FabricaComSinkEmMemoria();
        using var cliente = fabrica.CreateClient();

        using var resposta = await cliente.GetAsync("/api/v1/interno/provedores");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var evento = EventosDeAcessoNegado(fabrica).Should().ContainSingle().Subject;

        Propriedade(evento, "StatusCode").Should().Be("401");
        evento.Level.Should().Be(LogEventLevel.Warning);
        Propriedade(evento, "UsuarioId").Should().Be("anonimo");
    }

    [Fact]
    public async Task RequisicaoBemSucedida_EmiteEventoDeConclusaoComStatusEDuracao()
    {
        using var fabrica = new FabricaComSinkEmMemoria();
        using var cliente = fabrica.CreateClient();

        // 400 de termo curto: não toca o banco e é o caminho mais barato para uma resposta que
        // NÃO é 401/403 — o evento tem de sair mesmo assim (é o log de acesso, não só de erro).
        using var resposta = await cliente.GetAsync("/api/v1/busca?q=a");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var evento = EventosComDescricao(fabrica, "Requisição concluída").Should().ContainSingle().Subject;

        evento.Level.Should().Be(LogEventLevel.Information);
        Propriedade(evento, "StatusCode").Should().Be("400");
        Propriedade(evento, "Metodo").Should().Be("GET");
        Propriedade(evento, "Path").Should().Be("/api/v1/busca");
        evento.Properties.Should().ContainKey("DuracaoMs");
        Propriedade(evento, "TraceId").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task NenhumEventoCarregaTokenOuCabecalhoDeAutorizacao()
    {
        using var fabrica = new FabricaComSinkEmMemoria();
        using var cliente = fabrica.CreateClient();

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/v1/interno/provedores");
        requisicao.Headers.Add("Authorization", "Bearer token-secreto-que-nunca-pode-ser-logado");
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "role-que-nao-existe");

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Sem esta guarda o teste passaria à toa num pipeline sem sink — exatamente o estado que o
        // MED-B03 descreve: zero evento também "não contém token".
        fabrica.Sink.Eventos.Should().NotBeEmpty(because: "sem evento nenhum a asserção abaixo seria vácua");

        var textoDeTodosOsEventos = string.Join(
            "\n",
            fabrica.Sink.Eventos.Select(evento =>
                evento.RenderMessage() + " " + string.Join(" ", evento.Properties.Select(par => $"{par.Key}={par.Value}"))));

        textoDeTodosOsEventos.Should().NotContain("token-secreto-que-nunca-pode-ser-logado");
        textoDeTodosOsEventos.Should().NotContain("Bearer");
    }

    private static IReadOnlyList<LogEvent> EventosComDescricao(FabricaComSinkEmMemoria fabrica, string descricao) =>
        [.. fabrica.Sink.Eventos.Where(evento => Propriedade(evento, "Descricao") == descricao)];

    private static IReadOnlyList<LogEvent> EventosDeAcessoNegado(FabricaComSinkEmMemoria fabrica) =>
        EventosComDescricao(fabrica, "Acesso negado");

    private static string? TraceIdDoCorpo(string corpo)
    {
        using var documento = JsonDocument.Parse(corpo);

        return documento.RootElement.GetProperty("error").TryGetProperty("traceId", out var traceId)
            ? traceId.GetString()
            : null;
    }

    /// <summary>Valor da propriedade estruturada como texto puro (sem as aspas do Serilog).</summary>
    private static string? Propriedade(LogEvent evento, string nome) =>
        evento.Properties.TryGetValue(nome, out var valor)
            ? valor.ToString().Trim('"')
            : null;

    private sealed class SinkEmMemoria : ILogEventSink
    {
        private readonly List<LogEvent> _eventos = [];

        public IReadOnlyList<LogEvent> Eventos
        {
            get
            {
                lock (_eventos)
                    return [.. _eventos];
            }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_eventos)
                _eventos.Add(logEvent);
        }
    }

    /// <summary>
    /// Factory própria (nunca a compartilhada): precisa de connection string inalcançável para
    /// forçar o 500 e de um sink exclusivo, para que os eventos contados sejam só os desta
    /// requisição.
    /// </summary>
    private sealed class FabricaComSinkEmMemoria : WebApplicationFactory<Program>
    {
        public SinkEmMemoria Sink { get; } = new();

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
                // ReadFrom.Services no Program.cs é o que traz este sink para o pipeline real.
                services.AddSingleton<ILogEventSink>(Sink);

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
