using System.Net;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// <c>GET /comunicados</c> é anônimo e consulta o banco a cada chamada fora do cache: sem teto de
/// taxa, quem ignora o <c>Cache-Control</c> vira carga direta no Postgres. Ele usa a política
/// "vitrine", a mesma da Home.
///
/// <para>
/// O teto é exercitado pela partição compartilhada (o <c>TestServer</c> não preenche
/// <c>RemoteIpAddress</c>), de 20 permissões por minuto. O serviço é trocado por um que responde
/// na hora, para a janela de um minuto não resetar no meio por lentidão do banco.
/// </para>
/// </summary>
public sealed class ComunicadosRateLimitTests
{
    private const string Rota = "/api/v1/comunicados";

    /// <summary>Espelha <c>RateLimitingExtensoes.LimiteVitrineSemIp</c>.</summary>
    private const int LimiteVitrineSemIp = 20;

    [Fact]
    public async Task AcimaDoTetoDaVitrine_Devolve429_ComEnvelopeEmTodoOResto200()
    {
        using var fabrica = new CodexApiFactory();
        using var derivada = fabrica.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IComunicadoServicoPublico>();
                services.AddSingleton<IComunicadoServicoPublico, ServicoInstantaneo>();
            }));
        using var cliente = derivada.CreateClient();

        var status = new List<HttpStatusCode>();
        HttpResponseMessage? rejeitada = null;

        for (var i = 0; i < LimiteVitrineSemIp + 1; i++)
        {
            var resposta = await cliente.GetAsync(Rota);
            status.Add(resposta.StatusCode);

            if (resposta.StatusCode == HttpStatusCode.TooManyRequests)
                rejeitada = resposta;
            else
                resposta.Dispose();
        }

        status.Take(LimiteVitrineSemIp).Should().OnlyContain(s => s == HttpStatusCode.OK);
        status[^1].Should().Be(HttpStatusCode.TooManyRequests,
            because: "sem teto, quem ignora o cache controla a carga do banco");

        using (rejeitada)
        {
            var corpo = await rejeitada!.Content.ReadAsStringAsync();
            corpo.Should().Contain("LIMITE_REQUISICOES");
            rejeitada.Headers.RetryAfter.Should().NotBeNull();
        }
    }

    private sealed class ServicoInstantaneo : IComunicadoServicoPublico
    {
        public Task<IReadOnlyCollection<ComunicadoPublicoDto>> ListarAsync(
            IReadOnlyCollection<string>? provedorSlugs, string? versaoSlug, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<ComunicadoPublicoDto>>([]);
    }
}
