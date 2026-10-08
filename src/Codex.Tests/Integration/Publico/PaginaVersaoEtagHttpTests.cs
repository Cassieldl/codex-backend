using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// A página da versão mostra erros que não são linhas dela: o resumo de erros e os erros de cada
/// campo. Uma ETag tirada só da linha da versão não muda quando um erro é publicado ou ligado a um
/// campo, e todo cliente que já abriu a página revalida, recebe 304 e segue sem o erro novo.
/// Medido com o Emissor Nacional: a mesma ETag para um corpo com 0 e outro com 79 campos com erros.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class PaginaVersaoEtagHttpTests : IAsyncLifetime
{
    private const string RolesDeErro = "erros-adicionar,erros-atualizar,erros-publicar,erros-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public PaginaVersaoEtagHttpTests(PostgresContainerFixture fixture)
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
    public async Task PublicarErroDaVersao_InvalidaAETagDaPagina()
    {
        var cenario = await SemearAsync();
        var erroId = await CriarErroAsync(cenario, camposLayoutIds: []);

        var etagAntes = await LerETagAsync(cenario.Rota);
        (await ObterAsync(cenario.Rota, etagAntes)).Status.Should().Be(HttpStatusCode.NotModified,
            because: "sem escrita nenhuma a mesma ETag ainda vale — controle positivo do caso");

        await PublicarErroAsync(erroId);

        var (status, corpo) = await ObterAsync(cenario.Rota, etagAntes);
        status.Should().Be(HttpStatusCode.OK, because: "a ETag antiga serviria a página sem o erro publicado");
        corpo.GetProperty("data").GetProperty("errosResumo").GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task LigarErroACampo_InvalidaAETagDaPagina()
    {
        var cenario = await SemearAsync();
        var erroId = await CriarErroAsync(cenario, camposLayoutIds: []);
        await PublicarErroAsync(erroId);

        var etagAntes = await LerETagAsync(cenario.Rota);
        (await ObterAsync(cenario.Rota, etagAntes)).Status.Should().Be(HttpStatusCode.NotModified);

        var (_, detalhe) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/erros/{erroId}");
        var dados = detalhe.GetProperty("data");
        var (statusPut, corpoPut) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/erros/{erroId}", new
        {
            origem = dados.GetProperty("origem").GetString(),
            titulo = dados.GetProperty("titulo").GetString(),
            causaProvavel = (string?)null,
            solucaoSugerida = (string?)null,
            variantes = new[] { "Mensagem observada do erro." },
            versoesProvedorIds = new[] { cenario.VersaoId },
            camposLayoutIds = new[] { cenario.CampoLayoutId },
        }, dados.GetProperty("versaoConcorrencia").GetUInt32());
        statusPut.Should().Be(HttpStatusCode.OK, corpoPut.ToString());

        var (status, corpo) = await ObterAsync(cenario.Rota, etagAntes);
        status.Should().Be(HttpStatusCode.OK, because: "a ETag antiga serviria o campo sem o erro ligado a ele");
        corpo.GetProperty("data").GetProperty("camposPorGrupo").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("campos").EnumerateArray())
            .Single()
            .GetProperty("errosConhecidos").GetArrayLength().Should().Be(1);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record Cenario(int VersaoId, int CampoLayoutId, string Rota);

    private async Task<Cenario> SemearAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync() ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout etag {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor etag {sufixo}", $"ETAG-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versão etag {sufixo}");
        versao.Publicar();
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        var campoLayout = CampoLayout.Criar(versaoLayout.Id, $"Campo {sufixo}", $"//Servico/Campo{sufixo}", "significado", 1);
        contexto.CamposLayout.Add(campoLayout);
        await contexto.SaveChangesAsync();

        contexto.CamposVersao.Add(CampoVersao.Criar(versao.Id, campoLayout.Id, "texto", EstadoPreenchimento.Obrigatorio));
        await contexto.SaveChangesAsync();

        return new Cenario(versao.Id, campoLayout.Id, $"/api/v1/provedores/{provedor.Slug}/versoes/{versao.Slug}");
    }

    private async Task<int> CriarErroAsync(Cenario cenario, int[] camposLayoutIds)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", new
        {
            codigo = $"E{Random.Shared.Next(10000, 99999)}",
            origem = nameof(OrigemErro.Prefeitura),
            titulo = "Erro da página da versão",
            variantes = new[] { "Mensagem observada do erro." },
            versoesProvedorIds = new[] { cenario.VersaoId },
            camposLayoutIds,
        });
        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task PublicarErroAsync(int erroId)
    {
        var (_, detalhe) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/erros/{erroId}");
        var token = detalhe.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();
        var (status, corpo) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/erros/{erroId}/publicacao", new { publicado = true }, token);
        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
    }

    private async Task<string> LerETagAsync(string rota)
    {
        var resposta = await _cliente.GetAsync(rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var etag = resposta.Headers.ETag?.ToString();
        etag.Should().NotBeNullOrEmpty();
        return etag!;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ObterAsync(string rota, string ifNoneMatch)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        return (resposta.StatusCode, string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpo = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeErro);
        if (corpo is not null) requisicao.Content = JsonContent.Create(corpo);
        if (ifMatch is not null) requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        return (resposta.StatusCode, string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }
}
