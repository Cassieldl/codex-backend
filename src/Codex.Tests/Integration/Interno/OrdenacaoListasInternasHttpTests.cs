using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Ordenação das listagens internas de erros e artigos por HTTP real, com <c>sortBy</c> e
/// <c>sortDirection</c> literais na URL. Cada cenário filtra por um sufixo próprio (<c>q</c>), e a
/// ordem de criação difere de propósito da ordem pedida, para o padrão nunca passar por acaso.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class OrdenacaoListasInternasHttpTests : IAsyncLifetime
{
    private const string RotaErros = "/api/v1/interno/erros";
    private const string RotaArtigos = "/api/v1/interno/artigos";
    private const string RolesDeLeitura = "erros-buscar,artigos-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public OrdenacaoListasInternasHttpTests(PostgresContainerFixture fixture)
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
    public async Task Erros_SortByCodigo_AscOrdenaPeloCodigo_DescInverte_SemDiferenciarMaiuscula()
    {
        var sufixo = await SemearErrosAsync();

        var (statusAsc, corpoAsc) = await ListarAsync($"{RotaErros}?q={sufixo}&sortBy=CODIGO&sortDirection=ASC");
        var (statusDesc, corpoDesc) = await ListarAsync($"{RotaErros}?q={sufixo}&sortBy=codigo&sortDirection=desc");

        statusAsc.Should().Be(HttpStatusCode.OK);
        statusDesc.Should().Be(HttpStatusCode.OK);
        Codigos(corpoAsc).Should().Equal($"A{sufixo}", $"B{sufixo}", $"C{sufixo}");
        Codigos(corpoDesc).Should().Equal($"C{sufixo}", $"B{sufixo}", $"A{sufixo}");
    }

    [Fact]
    public async Task Erros_OrdenacaoComFiltroEPaginacao_Pagina2ContinuaAPagina1()
    {
        var sufixo = await SemearErrosAsync();
        var consulta = $"{RotaErros}?q={sufixo}&sortBy=codigo&sortDirection=asc&tamanhoPagina=2";

        var (_, pagina1) = await ListarAsync($"{consulta}&pagina=1");
        var (_, pagina2) = await ListarAsync($"{consulta}&pagina=2");

        pagina1.GetProperty("totalCount").GetInt32().Should().Be(3);
        Codigos(pagina1).Should().Equal($"A{sufixo}", $"B{sufixo}");
        Codigos(pagina2).Should().Equal($"C{sufixo}");
    }

    [Fact]
    public async Task Erros_SortByDesconhecido_Devolve200NaOrdemPadrao()
    {
        var sufixo = await SemearErrosAsync();

        var (status, corpo) = await ListarAsync($"{RotaErros}?q={sufixo}&sortBy=passwordhash&sortDirection=asc");

        status.Should().Be(HttpStatusCode.OK);
        Codigos(corpo).Should().Equal([$"A{sufixo}", $"C{sufixo}", $"B{sufixo}"],
            because: "coluna fora da whitelist cai no padrão, mais recentes primeiro, ignorando a direção");
    }

    [Fact]
    public async Task Erros_SortByTitulo_AcentoEMinusculaOrdenamComoOPortugues()
    {
        var sufixo = NovoSufixo();

        await using (var contexto = _fixture.CriarContexto())
        {
            foreach (var (codigo, prefixo) in new[] { ("T1", "Eficaz"), ("T2", "efeito"), ("T3", "Édito"), ("T4", "Edital") })
            {
                contexto.Erros.Add(Erro.Criar($"{codigo}{sufixo}", OrigemErro.Schema, $"{prefixo} {sufixo}"));
                await contexto.SaveChangesAsync();
            }
        }

        var (status, corpo) = await ListarAsync($"{RotaErros}?q={sufixo}&sortBy=titulo&sortDirection=asc");

        status.Should().Be(HttpStatusCode.OK);
        Titulos(corpo).Should().Equal(
            [$"Edital {sufixo}", $"Édito {sufixo}", $"efeito {sufixo}", $"Eficaz {sufixo}"],
            because: "por byte o acentuado iria para o fim e a minúscula para depois de toda maiúscula");
    }

    [Fact]
    public async Task Artigos_SortByVerificadoEm_NuncaVerificadoFicaNoFimNasDuasDirecoes()
    {
        var sufixo = await SemearArtigosAsync();

        var (status, corpoAsc) = await ListarAsync($"{RotaArtigos}?q={sufixo}&sortBy=verificadoem&sortDirection=asc");
        var (_, corpoDesc) = await ListarAsync($"{RotaArtigos}?q={sufixo}&sortBy=VerificadoEm&sortDirection=desc");

        status.Should().Be(HttpStatusCode.OK);
        DatasVerificacao(corpoAsc).Should().Equal("2026-01-05", "2026-03-10", "2026-05-20", null);
        DatasVerificacao(corpoDesc).Should().Equal("2026-05-20", "2026-03-10", "2026-01-05", null);
    }

    [Fact]
    public async Task Artigos_SortByTitulo_Pagina2ContinuaAPagina1()
    {
        var sufixo = await SemearArtigosAsync();
        var consulta = $"{RotaArtigos}?q={sufixo}&sortBy=titulo&sortDirection=asc&tamanhoPagina=2";

        var (_, pagina1) = await ListarAsync($"{consulta}&pagina=1");
        var (_, pagina2) = await ListarAsync($"{consulta}&pagina=2");

        Titulos(pagina1).Should().Equal($"edital {sufixo}", $"Édito {sufixo}");
        Titulos(pagina2).Should().Equal($"Efeito {sufixo}", $"Eficaz {sufixo}");
    }

    // Criados na ordem B, C, A: o padrão (mais recentes primeiro) devolve A, C, B.
    private async Task<string> SemearErrosAsync()
    {
        var sufixo = NovoSufixo();

        await using var contexto = _fixture.CriarContexto();

        foreach (var (codigo, origem, titulo) in new[]
        {
            ($"B{sufixo}", OrigemErro.Schema, $"Erro {sufixo} alfa"),
            ($"C{sufixo}", OrigemErro.Prefeitura, $"Erro {sufixo} gama"),
            ($"A{sufixo}", OrigemErro.InvoiCy, $"Erro {sufixo} beta"),
        })
        {
            contexto.Erros.Add(Erro.Criar(codigo, origem, titulo));
            await contexto.SaveChangesAsync();
        }

        return sufixo;
    }

    // Ordem de criação, de título e de verificação diferentes entre si; um artigo nunca verificado.
    // Os títulos só ficam em ordem com a collation do português: por byte, "edital" e "Édito" iriam para o fim.
    private async Task<string> SemearArtigosAsync()
    {
        var sufixo = NovoSufixo();

        await using var contexto = _fixture.CriarContexto();

        foreach (var (titulo, verificadoEm) in new (string, DateTimeOffset?)[]
        {
            ($"Efeito {sufixo}", new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero)),
            ($"Édito {sufixo}", null),
            ($"edital {sufixo}", new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero)),
            ($"Eficaz {sufixo}", new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero)),
        })
        {
            var artigo = Artigo.Criar(TipoArtigo.Conceito, titulo, "Corpo do artigo de ordenação.");
            if (verificadoEm.HasValue)
                artigo.RegistrarVerificacao(verificadoEm.Value);

            contexto.Artigos.Add(artigo);
            await contexto.SaveChangesAsync();
        }

        return sufixo;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ListarAsync(string rota)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeLeitura);

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone());
    }

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static List<string?> Codigos(JsonElement corpo) => Coluna(corpo, "codigo");

    private static List<string?> Titulos(JsonElement corpo) => Coluna(corpo, "titulo");

    private static List<string?> DatasVerificacao(JsonElement corpo) => Coluna(corpo, "verificadoEm");

    private static List<string?> Coluna(JsonElement corpo, string propriedade) =>
        [.. corpo.GetProperty("items").EnumerateArray().Select(item => item.GetProperty(propriedade).GetString())];
}
