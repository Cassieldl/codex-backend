using System.Net;
using System.Text.Json;
using Codex.API.Jobs;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Repositories;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Critério de aceite de U-5/PQ-11 (analise-backend.md §10.4), não sugestão: o predicado de leitura
/// é a rede de segurança, e precisa segurar sozinho — <b>com o job desligado</b>. Se estes casos só
/// passassem com o job ligado, a correção do que aparece na tela dependeria de o job ter rodado, que
/// é exatamente o risco que a decisão recusou.
///
/// <para>
/// "Job desligado" aqui não é suposição: o primeiro caso afirma, no host que serve os outros, que
/// nenhum <see cref="ExpiracaoComunicadosJob"/> está registrado.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ExpiracaoComunicadosPredicadoTests : IAsyncLifetime
{
    private const string RoleDeLeitura = "comunicados-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ExpiracaoComunicadosPredicadoTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void OHostDesteTeste_NaoTemOJobDeExpiracaoRodando()
    {
        _factory.Services.GetServices<IHostedService>().OfType<ExpiracaoComunicadosJob>().Should().BeEmpty(
            because: "o predicado só é provado se nada estiver convergindo o estado por baixo — "
                     + "com o job ligado, os casos abaixo passariam pelo motivo errado");
    }

    [Fact]
    public async Task ComunicadoPublicadoEVencido_NaoApareceSemIncluirEncerrados_ComJobDesligado()
    {
        var provedorId = await MontarProvedorAsync();
        var vencido = await InserirAsync(provedorId, "vencido", DateTimeOffset.UtcNow.AddHours(-1), publicado: true);

        var titulos = await ListarTitulosAsync(provedorId, incluirEncerrados: false);

        titulos.Should().NotContain(vencido,
            because: "U-5 — a leitura filtra por conta própria; depender do job deixaria conteúdo vencido no ar até o próximo tique");
    }

    [Fact]
    public async Task ComunicadoPublicadoEVencido_ApareceComIncluirEncerrados_MarcadoComoEncerrado()
    {
        var provedorId = await MontarProvedorAsync();
        var vencido = await InserirAsync(provedorId, "historico", DateTimeOffset.UtcNow.AddHours(-1), publicado: true);

        var itens = await ListarAsync(provedorId, incluirEncerrados: true);

        var item = itens.Single(i => i.GetProperty("titulo").GetString() == vencido);
        item.GetProperty("encerrado").GetBoolean().Should().BeTrue(
            because: "o histórico existe para o editor ver o que encerrou — sem o marcador ele não distingue de um vigente");
        item.GetProperty("publicado").GetBoolean().Should().BeTrue(
            because: "'encerrado' é derivado da expiração; 'publicado' continua sendo o estado persistido, e os dois não se confundem");
    }

    [Fact]
    public async Task RascunhoAindaVigente_ContinuaVisivelSemIncluirEncerrados()
    {
        var provedorId = await MontarProvedorAsync();
        var rascunho = await InserirAsync(provedorId, "rascunho", DateTimeOffset.UtcNow.AddDays(3), publicado: false);

        var titulos = await ListarTitulosAsync(provedorId, incluirEncerrados: false);

        titulos.Should().Contain(rascunho,
            because: "o filtro esconde o que ENCERROU, nunca o que ainda não foi publicado — o badge 'Rascunho' da tela 12 "
                     + "sumiria da visão padrão do editor");
    }

    [Fact]
    public async Task RascunhoJaVencido_NaoApareceSemIncluirEncerrados()
    {
        var provedorId = await MontarProvedorAsync();
        var rascunhoVencido = await InserirAsync(provedorId, "rascunho-vencido", DateTimeOffset.UtcNow.AddHours(-1), publicado: false);

        var titulos = await ListarTitulosAsync(provedorId, incluirEncerrados: false);

        titulos.Should().NotContain(rascunhoVencido,
            because: "o critério do filtro é 'encerrado', e encerrado é só expiração — não depende do estado de publicação");
    }

    [Fact]
    public async Task QueryVigentes_DevolveSomenteOPublicadoAtivoENaoVencido()
    {
        var provedorId = await MontarProvedorAsync();

        var noAr = await InserirAsync(provedorId, "no-ar", DateTimeOffset.UtcNow.AddDays(2), publicado: true);
        await InserirAsync(provedorId, "vencido", DateTimeOffset.UtcNow.AddHours(-1), publicado: true);
        await InserirAsync(provedorId, "rascunho", DateTimeOffset.UtcNow.AddDays(2), publicado: false);
        await InserirAsync(provedorId, "removido", DateTimeOffset.UtcNow.AddDays(2), publicado: true, removido: true);

        await using var contexto = _fixture.CriarContexto();

        var vigentes = await ComunicadoRepositorioInterno
            .QueryVigentes(contexto.Comunicados.AsNoTracking().Where(c => c.ProvedorId == provedorId), DateTimeOffset.UtcNow)
            .Select(c => c.Titulo)
            .ToListAsync();

        vigentes.Should().BeEquivalentTo([noAr],
            because: "este é o predicado ÚNICO de 'o que está no ar' que a leitura pública da 003 vai consumir — "
                     + "as três condições precisam valer juntas, e cada uma sozinha deixaria passar um caso errado");
    }

    // ------------------------------------------------------------------ apoio

    private async Task<int> MontarProvedorAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        await using var contexto = _fixture.CriarContexto();

        var provedor = Provedor.Criar($"Provedor predicado {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        return provedor.Id;
    }

    /// <summary>Insere DIRETO no banco — nunca pelo job, que nem existe neste host, e nunca por I32, que recusa data passada.</summary>
    private async Task<string> InserirAsync(int provedorId, string marcador, DateTimeOffset expiraEm, bool publicado, bool removido = false)
    {
        var titulo = $"Predicado {marcador} {Guid.NewGuid():N}";

        await using var contexto = _fixture.CriarContexto();

        var comunicado = Comunicado.CriarDeProvedor(provedorId, titulo, "corpo do comunicado", Severidade.Alerta, expiraEm);

        if (publicado)
            comunicado.Publicar();

        if (removido)
            comunicado.Desativar();

        contexto.Comunicados.Add(comunicado);
        await contexto.SaveChangesAsync();

        return titulo;
    }

    private async Task<IReadOnlyList<string>> ListarTitulosAsync(int provedorId, bool incluirEncerrados) =>
        (await ListarAsync(provedorId, incluirEncerrados))
            .Select(item => item.GetProperty("titulo").GetString()!)
            .ToList();

    private async Task<IReadOnlyList<JsonElement>> ListarAsync(int provedorId, bool incluirEncerrados)
    {
        var rota = $"/api/v1/interno/comunicados?provedorId={provedorId}&incluirEncerrados={incluirEncerrados.ToString().ToLowerInvariant()}";

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RoleDeLeitura);

        using var resposta = await _cliente.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        return corpo.GetProperty("items").EnumerateArray().Select(item => item.Clone()).ToList();
    }
}
