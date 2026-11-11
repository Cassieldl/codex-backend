using System.Net;
using System.Text.Json;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// I22 — reconstrução total do índice de busca (analise-backend.md §3.4). A prova exigida pela
/// dívida do PRD §2.4 é FUNCIONAL: semear drift nos <b>seis</b> tipos ao mesmo tempo, rodar a
/// reconstrução e afirmar que os seis convergiram.
///
/// <para>
/// Um caso que só semeasse drift em artigo provaria pouco — e um que semeasse nos outros cinco
/// foi literalmente o que deixou artigo de fora da varredura por uma demanda inteira: a suíte
/// ficava verde porque ninguém afirmava o sexto. Por isso os dois sabores de drift aparecem aqui:
/// linha AUSENTE (nunca indexada) e linha PODRE (indexada e depois divergida da origem).
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ReconstrucaoIndiceDriftTests : IAsyncLifetime
{
    private const string RoleDeReconstrucao = "indicebusca-atualizar";
    private const string TituloPodre = "TITULO QUE NAO EXISTE MAIS NA ORIGEM";
    private const string UrlPodre = "/url-que-nao-existe-mais";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ReconstrucaoIndiceDriftTests(PostgresContainerFixture fixture)
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

    /// <summary>Uma linha esperada de <c>codex.itensbusca</c> depois da convergência.</summary>
    private sealed record Esperado(TipoItemIndexado Tipo, int ItemId, string Titulo, string Url);

    [Fact]
    public async Task I22_ComDriftNosSeisTipos_ReconstroiTodosELevaCadaLinhaDeVoltaAOrigem()
    {
        var esperados = await SemearDriftNosSeisTiposAsync();

        // Pré-condição: o cenário precisa estar REALMENTE divergente antes de reconstruir; sem
        // isto, um caso verde não distinguiria "convergiu" de "já estava certo".
        foreach (var esperado in esperados)
        {
            var antes = await ObterLinhaAsync(esperado.Tipo, esperado.ItemId);
            (antes is null || antes.Value.Titulo == TituloPodre).Should().BeTrue(
                because: $"o drift de {esperado.Tipo} tem de existir antes da reconstrução");
        }

        var (status, corpo) = await ReconstruirAsync();

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("itensReindexados").GetInt32().Should().BeGreaterThanOrEqualTo(esperados.Count);

        foreach (var esperado in esperados)
        {
            var depois = await ObterLinhaAsync(esperado.Tipo, esperado.ItemId);

            depois.Should().NotBeNull(because: $"a linha de {esperado.Tipo} tinha de ter sido (re)criada pela reconstrução");
            depois!.Value.Titulo.Should().Be(esperado.Titulo, because: $"o título de {esperado.Tipo} voltou da origem");
            depois.Value.Url.Should().Be(esperado.Url, because: $"a url de {esperado.Tipo} voltou da origem");
            depois.Value.Publicado.Should().BeTrue(
                because: $"linha nascida na reconstrução começa despublicada — a cascata de visibilidade de {esperado.Tipo} precisa rodar depois");
        }
    }

    // ------------------------------------------------------------------ apoio

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>
    /// Semeia um item de cada tipo e o drift correspondente: metade sem linha nenhuma em
    /// <c>itensbusca</c> (nunca indexada), metade com linha divergente da origem.
    /// </summary>
    private async Task<IReadOnlyList<Esperado>> SemearDriftNosSeisTiposAsync()
    {
        var sufixo = NovoSufixo();

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout drift {sufixo}", maiorOrdem + 1);
        versaoLayout.Publicar();
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var campoLayout = CampoLayout.Criar(versaoLayout.Id, $"Campo drift {sufixo}", $"//nfe/drift{sufixo}", "Significado.", 1);
        campoLayout.Publicar();
        contexto.CamposLayout.Add(campoLayout);

        var provedor = Provedor.Criar($"Provedor drift {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao drift {sufixo}");
        versao.Publicar();
        contexto.VersoesProvedor.Add(versao);

        var erro = Erro.Criar($"E-{sufixo}", OrigemErro.InvoiCy, $"Erro drift {sufixo}");
        erro.Publicar();
        contexto.Erros.Add(erro);

        var artigo = Artigo.Criar(TipoArtigo.Conceito, $"Artigo drift {sufixo}", "Corpo.");
        artigo.Publicar();
        contexto.Artigos.Add(artigo);
        await contexto.SaveChangesAsync();

        var aviso = Aviso.CriarDeProvedor(provedor.Id, Severidade.Informativo, $"Aviso drift {sufixo}", "Corpo.");
        aviso.Publicar();
        contexto.Avisos.Add(aviso);
        await contexto.SaveChangesAsync();

        var esperados = new List<Esperado>
        {
            new(TipoItemIndexado.provedor, provedor.Id, provedor.Nome, $"/provedores/{provedor.Slug}"),
            new(TipoItemIndexado.versaoprovedor, versao.Id, versao.Rotulo, $"/provedores/{provedor.Slug}"),
            new(TipoItemIndexado.aviso, aviso.Id, aviso.Titulo, $"/provedores/{provedor.Slug}#avisos"),
            new(TipoItemIndexado.erro, erro.Id, erro.Titulo, $"/erros/{erro.Slug}"),
            new(TipoItemIndexado.campolayout, campoLayout.Id, campoLayout.Nome, $"/layout/campos/{campoLayout.Slug}"),
            new(TipoItemIndexado.artigo, artigo.Id, artigo.Titulo, SlugsReservadosArtigo.RotaCanonica(artigo.Slug)),
        };

        // Drift PODRE nos três primeiros, AUSENTE nos três últimos — os dois sabores em paralelo,
        // porque só o ausente exerce o INSERT e só o podre exerce o UPDATE da projeção.
        foreach (var esperado in esperados.Take(3))
        {
            var item = ItemBusca.Criar(esperado.Tipo, esperado.ItemId, TituloPodre, UrlPodre, provedorIds: []);
            item.AtualizarPesos("drift", null, null, null);
            item.MarcarPublicado(true);
            contexto.ItensBusca.Add(item);
        }

        await contexto.SaveChangesAsync();

        return esperados;
    }

    private async Task<(string Titulo, string Url, bool Publicado)?> ObterLinhaAsync(TipoItemIndexado tipo, int itemId)
    {
        await using var contexto = _fixture.CriarContexto();

        var linha = await contexto.ItensBusca.AsNoTracking()
            .Where(i => i.TipoItem == tipo && i.ItemId == itemId)
            .Select(i => new { i.Titulo, i.Url, i.Publicado })
            .SingleOrDefaultAsync();

        return linha is null ? null : (linha.Titulo, linha.Url, linha.Publicado);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ReconstruirAsync()
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interno/indice-busca/reconstrucao");
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RoleDeReconstrucao);

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone());
    }
}
