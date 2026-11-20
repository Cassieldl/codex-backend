using System.Net;
using System.Text;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// PUT com <c>estado</c> Ativo num campo descontinuado reativa o campo. O corpo vai como JSON
/// literal: enum serializado pelo .NET sairia número e esconderia o contrato real.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CampoLayoutReativacaoHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public CampoLayoutReativacaoHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        await LimparAsync();
    }

    [Fact]
    public async Task Put_EstadoAtivoEmCampoDescontinuado_ReativaEOCampoVoltaAoDicionarioPublico()
    {
        var (campoId, campoSlug, versaoConcorrencia, versaoAlvoSlug) = await SemearCampoDescontinuadoAsync();

        TodosOsSlugs(await ObterVersaoPublicaAsync(versaoAlvoSlug)).Should().NotContain(campoSlug);

        using var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/campos-layout/{campoId}");
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "campolayout-atualizar");
        requisicao.Headers.TryAddWithoutValidation("If-Match", versaoConcorrencia.ToString());
        requisicao.Content = new StringContent("""{ "estado": "Ativo" }""", Encoding.UTF8, "application/json");

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: texto);
        var dados = JsonDocument.Parse(texto).RootElement.GetProperty("data");
        dados.GetProperty("estado").GetString().Should().Be("Ativo");
        dados.GetProperty("versaoLayoutDescontinuadoId").ValueKind.Should().Be(JsonValueKind.Null);

        TodosOsSlugs(await ObterVersaoPublicaAsync(versaoAlvoSlug)).Should().Contain(campoSlug);
    }

    // ---------------------------------------------------------------------------------- apoio

    private async Task<(int CampoId, string CampoSlug, uint VersaoConcorrencia, string VersaoAlvoSlug)> SemearCampoDescontinuadoAsync()
    {
        int campoId;
        string campoSlug, versaoAlvoSlug;

        await using (var contexto = _fixture.CriarContexto())
        {
            var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => (int?)v.Ordem) ?? 0;

            var origem = VersaoLayout.Criar($"Layout Origem {_sufixo}", maiorOrdem + 1);
            var descontinuacao = VersaoLayout.Criar($"Layout Saida {_sufixo}", maiorOrdem + 2);
            var alvo = VersaoLayout.Criar($"Layout Depois {_sufixo}", maiorOrdem + 3);

            foreach (var versao in new[] { origem, descontinuacao, alvo })
                versao.Publicar();

            contexto.VersoesLayout.AddRange(origem, descontinuacao, alvo);
            await contexto.SaveChangesAsync();

            var campo = CampoLayout.Criar(origem.Id, $"campo reativado {_sufixo}", $"//raiz/Reativado{_sufixo}", "Campo que volta", ordem: 1);
            campo.Publicar();
            contexto.CamposLayout.Add(campo);
            await contexto.SaveChangesAsync();

            var rastreado = await contexto.CamposLayout.Include(c => c.VersaoLayoutOrigem).FirstAsync(c => c.Id == campo.Id);
            rastreado.Descontinuar(descontinuacao);
            await contexto.SaveChangesAsync();

            (campoId, campoSlug, versaoAlvoSlug) = (campo.Id, campo.Slug, alvo.Slug);
        }

        await using var leitura = _fixture.CriarContexto();
        var versaoConcorrencia = await leitura.CamposLayout.AsNoTracking()
            .Where(c => c.Id == campoId)
            .Select(c => c.Versao)
            .SingleAsync();

        return (campoId, campoSlug, versaoConcorrencia, versaoAlvoSlug);
    }

    private async Task<JsonElement> ObterVersaoPublicaAsync(string versaoSlug)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/layout/versoes/{versaoSlug}");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: texto);
        return JsonDocument.Parse(texto).RootElement.GetProperty("data").Clone();
    }

    private static List<string> TodosOsSlugs(JsonElement versao) =>
        versao.GetProperty("camposPorGrupo").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("campos").EnumerateArray())
            .Select(campo => campo.GetProperty("slug").GetString()!)
            .ToList();

    /// <summary>Hard delete: versões publicadas de ordem alta mudariam o dicionário das outras classes.</summary>
    private async Task LimparAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var versoes = await contexto.VersoesLayout.Where(v => v.Rotulo.Contains(_sufixo)).ToListAsync();
        var idsVersao = versoes.Select(v => v.Id).ToList();

        var campos = await contexto.CamposLayout.Where(c => idsVersao.Contains(c.VersaoLayoutId)).ToListAsync();
        var idsCampo = campos.Select(c => c.Id).ToList();

        var itensBusca = await contexto.ItensBusca
            .Where(i => i.TipoItem == TipoItemIndexado.campolayout && idsCampo.Contains(i.ItemId))
            .ToListAsync();

        contexto.ItensBusca.RemoveRange(itensBusca);
        contexto.CamposLayout.RemoveRange(campos);
        contexto.VersoesLayout.RemoveRange(versoes);
        await contexto.SaveChangesAsync();
    }
}
