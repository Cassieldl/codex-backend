using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Api;

/// <summary>
/// analise-backend.md §9 (U8/risco #3): escopo de aviso na fronteira HTTP.
///
/// <para>
/// <b>Parte 1 (sem banco):</b> <c>POST /interno/provedores/{id}/avisos</c> com um corpo tentando
/// incluir <c>campoVersaoIds</c> não produz erro de binding/validação — a propriedade simplesmente
/// não existe em <c>CriarAvisoDeProvedorDto</c>, então o campo extra é descartado silenciosamente
/// pelo <c>System.Text.Json</c> (prova estrutural determinística e independente de runtime:
/// <c>Unit/Dtos/CriarAvisoDeProvedorDtoEstruturaTests.cs</c>). Aqui provamos o lado HTTP: a
/// resposta nunca é <c>400</c>/<c>422</c> por causa do campo desconhecido.
/// </para>
/// <para>
/// <b>Parte 2 (com banco real via Testcontainers):</b> <c>PUT /interno/avisos/{id}</c> com
/// <c>campoVersaoIds</c> num aviso cujo escopo real (carregado do banco) é PROVEDOR → <c>422</c>.
/// Container próprio desta classe (não compartilha o <see cref="PostgresCollection"/> usado pelos
/// testes fora do escopo desta unidade).
/// </para>
/// </summary>
public sealed class AvisoEscopoEndpointTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres;
    private CodexApiFactoryComBanco _factory = null!;

    public AvisoEscopoEndpointTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task PostAvisoDeProvedor_ComCampoVersaoIdsNoCorpo_CampoEhIgnoradoPeloBinding_NuncaVira400Ou422()
    {
        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interno/provedores/1/avisos")
        {
            // "campoVersaoIds" não existe em CriarAvisoDeProvedorDto — deve ser silenciosamente
            // descartado na desserialização, nunca rejeitado como campo desconhecido.
            Content = JsonContent.Create(new
            {
                severidade = 2, // Severidade.Informativo (sem JsonStringEnumConverter — ver ErrosHttpTests)
                titulo = "Aviso de teste",
                corpo = "Corpo do aviso",
                campoVersaoIds = new[] { 1, 2, 3 },
            }),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "avisos-adicionar");

        using var resposta = await cliente.SendAsync(requisicao);

        var statusInesperados = new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity };
        statusInesperados.Should().NotContain(resposta.StatusCode,
            because: "campoVersaoIds desconhecido não deveria nem chegar a ser um erro de validação — a propriedade nem existe no DTO");
    }

    [Fact]
    public async Task PutAvisoDeProvedor_ComCampoVersaoIds_Retorna422()
    {
        var (avisoId, xmin) = await CriarAvisoDeProvedorPersistidoAsync();

        using var cliente = _factory.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/avisos/{avisoId}")
        {
            Content = JsonContent.Create(new
            {
                severidade = 2,
                titulo = "Aviso atualizado",
                corpo = "Corpo atualizado",
                campoVersaoIds = new[] { 1, 2, 3 }, // aviso é de escopo PROVEDOR — isto é inválido (U8/risco #3).
            }),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "avisos-atualizar");
        requisicao.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{xmin}\""));

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "aviso de escopo PROVEDOR não pode ter campos afetados — o domínio (Aviso.SubstituirCamposAfetados) lança RegraDeNegocioException");
    }

    private async Task<(int AvisoId, uint Xmin)> CriarAvisoDeProvedorPersistidoAsync()
    {
        await using var contexto = _postgres.CriarContexto();

        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var provedor = Provedor.Criar($"Provedor Teste {sufixo}", $"invoicy-{sufixo}", CategoriaProvedor.Nacional);
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var aviso = Aviso.CriarDeProvedor(provedor.Id, Severidade.Informativo, "Título original", "Corpo original");
        contexto.Avisos.Add(aviso);
        await contexto.SaveChangesAsync();

        var xmin = await contexto.Avisos.AsNoTracking()
            .Where(a => a.Id == aviso.Id)
            .Select(a => a.Versao)
            .FirstAsync();

        return (aviso.Id, xmin);
    }
}
