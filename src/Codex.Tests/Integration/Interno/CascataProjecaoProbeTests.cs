using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// PROBE DE QA (2ª rodada) — a cascata de PROJEÇÃO, não a de visibilidade.
///
/// <para>BUG-03 provou que o update do índice deixava titulo/url/subtitulo congelados na criação.
/// A correção (<c>ItemBusca.AtualizarProjecao</c>) re-sincroniza a linha QUE ESTÁ SENDO
/// reindexada. Este probe pergunta o que ninguém perguntou: e as linhas FILHAS que carregam
/// dado denormalizado do pai? E a remoção (I5), que só desativa a própria linha?</para>
///
/// <para>Vocabulário isolado por sufixo aleatório em token único (AP-QA-002) — nada aqui usa
/// palavra que outro teste possa casar no passo PARCIAL (OR).</para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CascataProjecaoProbeTests : IAsyncLifetime
{
    private const string TodasAsRoles =
        "provedores-buscar,provedores-adicionar,provedores-atualizar,provedores-remover,provedores-publicar," +
        "versoesprovedor-buscar,versoesprovedor-adicionar,versoesprovedor-atualizar,versoesprovedor-remover,versoesprovedor-publicar," +
        "avisos-adicionar,avisos-atualizar,avisos-remover,avisos-publicar," +
        "versoeslayout-buscar,versoeslayout-adicionar,versoeslayout-atualizar,versoeslayout-publicar," +
        "campolayout-buscar,campolayout-adicionar,campolayout-atualizar,campolayout-remover,campolayout-publicar," +
        "camposversao-buscar,camposversao-adicionar,camposversao-atualizar,camposversao-remover," +
        "erros-buscar,erros-adicionar,erros-atualizar,erros-remover,erros-publicar," +
        "indicebusca-buscar,indicebusca-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public CascataProjecaoProbeTests(PostgresContainerFixture fixture)
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

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, TodasAsRoles);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }

    /// <summary>Busca interna: é onde a versão de provedor aparece como resultado.</summary>
    private async Task<IReadOnlyList<JsonElement>> BuscarInternoAsync(string termo)
    {
        var (status, corpo) = await EnviarAsync(
            HttpMethod.Get, $"/api/v1/interno/busca?q={Uri.EscapeDataString(termo)}&tamanhoPagina=50");
        status.Should().Be(HttpStatusCode.OK);

        return corpo.GetProperty("data").GetProperty("grupos").EnumerateArray()
            .SelectMany(g => g.GetProperty("itens").EnumerateArray())
            .ToList();
    }

    /// <summary>Cria provedor + versão de layout + versão de provedor, tudo publicado.</summary>
    private async Task<(int ProvedorId, uint ConcorrenciaProvedor, int VersaoId)> MontarPublicadoAsync(
        string nomeProvedor, string identificador, string rotuloVersao, string sufixo)
    {
        var (statusProvedor, corpoProvedor) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome = nomeProvedor,
            identificadorInvoicy = identificador,
            categoria = "Nacional",
            aliases = Array.Empty<string>(),
        });
        statusProvedor.Should().Be(HttpStatusCode.Created);
        var idProvedor = corpoProvedor.GetProperty("data").GetProperty("id").GetInt32();
        var concorrenciaProvedor = corpoProvedor.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

        var (_, corpoLayout) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/layout/versoes", new
        {
            rotulo = $"Layout {sufixo}",
            descricao = (string?)null,
        });
        var idLayout = corpoLayout.GetProperty("data").GetProperty("id").GetInt32();

        var (statusVersao, corpoVersao) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{idProvedor}/versoes", new
        {
            versaoLayoutId = idLayout,
            rotulo = rotuloVersao,
            introducao = (string?)null,
        });
        statusVersao.Should().Be(HttpStatusCode.Created);
        var idVersao = corpoVersao.GetProperty("data").GetProperty("id").GetInt32();
        var concorrenciaVersao = corpoVersao.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

        var (statusPubProvedor, corpoPubProvedor) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{idProvedor}/publicacao", new { publicado = true }, concorrenciaProvedor);
        statusPubProvedor.Should().Be(HttpStatusCode.OK);
        concorrenciaProvedor = corpoPubProvedor.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

        var (statusPubVersao, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{idVersao}/publicacao", new { publicado = true }, concorrenciaVersao);
        statusPubVersao.Should().Be(HttpStatusCode.OK);

        return (idProvedor, concorrenciaProvedor, idVersao);
    }

    // ------------------------------------------------------------------ ACHADO 1: projeção filha

    /// <summary>
    /// I4 renomeia o provedor. O resultado de busca da VERSÃO exibe <c>provedorNome</c> lido do
    /// <c>subtitulo</c> denormalizado da linha da versão (ConsultaBuscaPostgres.cs:661), que só é
    /// reescrito quando a VERSÃO é reindexada — e I4 reindexa apenas o provedor.
    /// </summary>
    [Fact]
    public async Task RenomearProvedor_AtualizaProvedorNomeExibidoNoResultadoDaVersaoFilha()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var rotuloVersao = $"projfilha{sufixo}";
        var nomeAntigo = $"antigoprj{sufixo} Provedor";
        var nomeNovo = $"novoprj{sufixo} Provedor";

        var (idProvedor, concorrencia, _) = await MontarPublicadoAsync(
            nomeAntigo, $"PRJ-{sufixo}", rotuloVersao, sufixo);

        var antes = await BuscarInternoAsync(rotuloVersao);
        antes.Should().HaveCount(1, because: "sanidade: a versão está na busca interna");
        antes[0].GetProperty("provedorNome").GetString().Should().Be(nomeAntigo);

        var (statusRename, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/provedores/{idProvedor}", new
        {
            nome = nomeNovo,
            categoria = "Nacional",
            aliases = Array.Empty<string>(),
        }, concorrencia);
        statusRename.Should().Be(HttpStatusCode.OK);

        var depois = await BuscarInternoAsync(rotuloVersao);
        depois.Should().HaveCount(1);

        depois[0].GetProperty("provedorNome").GetString().Should().Be(nomeNovo,
            because: "o resultado da versão exibe o nome ATUAL do provedor — mesmo defeito do BUG-03, um nível abaixo: "
                     + "a projeção filha carrega o nome do pai e nenhuma cascata a reescreve quando o pai é renomeado");
    }

    // ------------------------------------------------------------------ ACHADO 2: remoção (I5)

    /// <summary>
    /// I5 remove (soft delete) o provedor. <c>DesativarAsync</c> desativa apenas a linha do
    /// próprio provedor; as linhas de versão/aviso mantêm <c>publicado = true</c> e
    /// <c>desativadoem IS NULL</c>, logo <c>visivelpublico</c> continua true.
    /// </summary>
    [Fact]
    public async Task RemoverProvedor_EscondeTambemAsVersoesDependentesDaBuscaPublica()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var rotuloVersao = $"remfilha{sufixo}";

        var (idProvedor, concorrencia, idVersao) = await MontarPublicadoAsync(
            $"remprov{sufixo} Provedor", $"REM-{sufixo}", rotuloVersao, sufixo);

        // A busca pública não lista versão de provedor, então a prova olha a coluna que ela filtra.
        (await VersaoVisivelNoPublicoAsync(idVersao)).Should().BeTrue(because: "sanidade");

        var (statusDelete, _) = await EnviarAsync(
            HttpMethod.Delete, $"/api/v1/interno/provedores/{idProvedor}", corpoRequisicao: null, ifMatch: concorrencia);
        statusDelete.Should().Be(HttpStatusCode.NoContent);

        (await VersaoVisivelNoPublicoAsync(idVersao)).Should().BeFalse(
            because: "removido o provedor, a linha da versão dependente não pode continuar visível no público");
    }

    private async Task<bool> VersaoVisivelNoPublicoAsync(int versaoId)
    {
        await using var contexto = _fixture.CriarContexto();

        return await contexto.ItensBusca.AsNoTracking()
            .Where(i => i.TipoItem == TipoItemIndexado.versaoprovedor && i.ItemId == versaoId)
            .Select(i => i.VisivelPublico)
            .SingleAsync();
    }
}
