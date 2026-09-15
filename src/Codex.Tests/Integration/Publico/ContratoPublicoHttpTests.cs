using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// PROBE DE QA — P1–P6 exercitados por HTTP REAL contra Postgres real. A suíte entregue prova o
/// contrato público só em nível de service/repositório: <c>EnvelopeResponseTests</c> registra que
/// "nenhum 200 real é alcançável neste ambiente sem banco", e <c>ErrosHttpTests</c> só cobre o
/// 400 de If-Match. Ou seja: 404/410/308/304, cap de paginação e whitelist de ordenação nunca
/// passaram pelo pipeline HTTP. Este probe percorre esse caminho.
///
/// <para>Vocabulário isolado por sufixo aleatório (AP-QA-002): o corpus é compartilhado entre
/// classes de teste, então nada aqui usa palavra que outro teste possa casar no passo PARCIAL.</para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ContratoPublicoHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ContratoPublicoHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private sealed record Cenario(
        string ProvedorPublicadoSlug,
        string VersaoPublicadaSlug,
        string ProvedorDespublicadoSlug,
        string VersaoDoProvedorDespublicadoSlug,
        string VersaoDespublicadaSlug,
        string ErroPublicadoSlug,
        string ErroDespublicadoSlug,
        string ErroGeralSlug,
        string Sufixo);

    private async Task<Cenario> MontarAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var layout = VersaoLayout.Criar($"Layout probe {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(layout);
        await contexto.SaveChangesAsync();

        var provedorOk = Provedor.Criar($"Probe Publicado {sufixo}", $"PRB-{sufixo}", CategoriaProvedor.Nacional);
        provedorOk.Publicar();
        var provedorOff = Provedor.Criar($"Probe Despublicado {sufixo}", $"PRD-{sufixo}", CategoriaProvedor.Nacional);
        contexto.Provedores.AddRange(provedorOk, provedorOff);
        await contexto.SaveChangesAsync();

        var versaoOk = VersaoProvedor.Criar(provedorOk.Id, layout.Id, $"Versao Ok {sufixo}");
        versaoOk.Publicar();
        var versaoOff = VersaoProvedor.Criar(provedorOk.Id, layout.Id, $"Versao Off {sufixo}");
        var versaoSobProvedorOff = VersaoProvedor.Criar(provedorOff.Id, layout.Id, $"Versao Sob Off {sufixo}");
        versaoSobProvedorOff.Publicar();
        contexto.VersoesProvedor.AddRange(versaoOk, versaoOff, versaoSobProvedorOff);
        await contexto.SaveChangesAsync();

        var erroOk = Erro.Criar($"PRBE-{sufixo}", OrigemErro.InvoiCy, $"Erro publicado {sufixo}");
        erroOk.SubstituirVariantes([$"variante {sufixo}"]);
        erroOk.Publicar();

        var erroOff = Erro.Criar($"PRBD-{sufixo}", OrigemErro.InvoiCy, $"Erro despublicado {sufixo}");
        erroOff.SubstituirVariantes([$"variante off {sufixo}"]);

        var erroGeral = Erro.Criar($"PRBG-{sufixo}", OrigemErro.InvoiCy, $"Erro geral {sufixo}");
        erroGeral.SubstituirVariantes([$"variante geral {sufixo}"]);
        erroGeral.Publicar();

        contexto.Erros.AddRange(erroOk, erroOff, erroGeral);
        await contexto.SaveChangesAsync();

        // ErroConfiguration faz builder.Ignore(VersoesAplicaveisIds): o vinculo N:N so existe se a
        // linha de codex.errosversoes for gravada explicitamente (o service interno faz isso).
        contexto.ErrosVersoes.Add(ErroVersao.Criar(erroOk.Id, versaoOk.Id));
        await contexto.SaveChangesAsync();

        return new Cenario(
            provedorOk.Slug, versaoOk.Slug, provedorOff.Slug, versaoSobProvedorOff.Slug,
            versaoOff.Slug, erroOk.Slug, erroOff.Slug, erroGeral.Slug, sufixo);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ObterAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto)
            ? default
            : JsonDocument.Parse(texto).RootElement.Clone();
        return (resposta.StatusCode, corpo);
    }

    // ---------------------------------------------------------------- P2 / P5 (listas)

    [Fact]
    public async Task P2_Listar_DevolvePaginatedResponseCruComProvedorPublicado_ENuncaODespublicado()
    {
        var cenario = await MontarAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/provedores?q={cenario.Sufixo}&tamanhoPagina=100");

        status.Should().Be(HttpStatusCode.OK);
        corpo.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo("items", "totalCount", "page", "pageSize", "totalPages");

        var slugs = corpo.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()).ToList();
        slugs.Should().Contain(cenario.ProvedorPublicadoSlug);
        slugs.Should().NotContain(cenario.ProvedorDespublicadoSlug, because: "P2 só lista Publicado = true");
    }

    [Fact]
    public async Task P2_TamanhoPaginaAbsurdo_ECapadoEm100_NoCorpoENaQuantidadeDeItens()
    {
        await MontarAsync();

        var (status, corpo) = await ObterAsync("/api/v1/provedores?tamanhoPagina=99999");

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("pageSize").GetInt32().Should().Be(100, because: "cap de 100 em P2 (§4.1)");
        corpo.GetProperty("items").GetArrayLength().Should().BeLessThanOrEqualTo(100);
    }

    [Theory]
    [InlineData("publicado")]
    [InlineData("identificadorInvoicy; DROP TABLE codex.provedores--")]
    [InlineData("1=1")]
    public async Task P2_OrdenarPorForaDaWhitelist_NaoQuebraNemOrdenaPorColunaArbitraria(string ordenarPor)
    {
        await MontarAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/provedores?ordenarPor={Uri.EscapeDataString(ordenarPor)}&tamanhoPagina=5");

        status.Should().Be(HttpStatusCode.OK, because: "valor fora da whitelist cai no default, nunca em 500 nem em SQL arbitrário");
        corpo.GetProperty("items").GetArrayLength().Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task P5_FiltroCodigoExato_AchaOErroPublicadoENaoODespublicado()
    {
        var cenario = await MontarAsync();

        var (statusOk, corpoOk) = await ObterAsync($"/api/v1/erros?codigo=PRBE-{cenario.Sufixo}");
        statusOk.Should().Be(HttpStatusCode.OK);
        corpoOk.GetProperty("items").GetArrayLength().Should().Be(1);

        var (statusOff, corpoOff) = await ObterAsync($"/api/v1/erros?codigo=PRBD-{cenario.Sufixo}");
        statusOff.Should().Be(HttpStatusCode.OK);
        corpoOff.GetProperty("items").GetArrayLength().Should().Be(0, because: "P5 nunca devolve erro despublicado");
    }

    // ---------------------------------------------------------------- P3 (404 × 410, M7)

    [Fact]
    public async Task P3_SlugInexistente_Retorna404ComCodigoNaoEncontrado()
    {
        var (status, corpo) = await ObterAsync($"/api/v1/provedores/nunca-existiu-{Guid.NewGuid():N}");

        status.Should().Be(HttpStatusCode.NotFound);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("NAO_ENCONTRADO");
    }

    [Fact]
    public async Task P3_ProvedorDespublicado_Retorna410ComCodigoProvedorDespublicado()
    {
        var cenario = await MontarAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/provedores/{cenario.ProvedorDespublicadoSlug}");

        status.Should().Be(HttpStatusCode.Gone);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("PROVEDOR_DESPUBLICADO");
    }

    [Fact]
    public async Task P3_ProvedorPublicado_Retorna200ComEnvelopeDataESemVersaoDespublicada()
    {
        var cenario = await MontarAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/provedores/{cenario.ProvedorPublicadoSlug}");

        status.Should().Be(HttpStatusCode.OK);
        corpo.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("data", "message");

        var versoes = corpo.GetProperty("data").GetProperty("versoes").EnumerateArray()
            .Select(v => v.GetProperty("slug").GetString()).ToList();

        versoes.Should().Contain(cenario.VersaoPublicadaSlug);
        versoes.Should().NotContain(cenario.VersaoDespublicadaSlug);
    }

    // ---------------------------------------------------------------- P4 (404 × 410 × ETag)

    [Fact]
    public async Task P4_VersaoDespublicada_ProvedorPublicado_Retorna410VersaoDespublicada()
    {
        var cenario = await MontarAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/provedores/{cenario.ProvedorPublicadoSlug}/versoes/{cenario.VersaoDespublicadaSlug}");

        status.Should().Be(HttpStatusCode.Gone);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("VERSAO_DESPUBLICADA");
    }

    [Fact]
    public async Task P4_ProvedorDespublicado_ComVersaoPublicada_Retorna410ProvedorDespublicado()
    {
        var cenario = await MontarAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/provedores/{cenario.ProvedorDespublicadoSlug}/versoes/{cenario.VersaoDoProvedorDespublicadoSlug}");

        status.Should().Be(HttpStatusCode.Gone);
        corpo.GetProperty("error").GetProperty("code").GetString().Should()
            .Be("PROVEDOR_DESPUBLICADO", because: "M7 exige distinguir QUAL ancestral falhou");
    }

    [Fact]
    public async Task P4_SlugsInexistentes_Retorna404()
    {
        var (status, _) = await ObterAsync($"/api/v1/provedores/nada-{Guid.NewGuid():N}/versoes/nada-2");
        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task P4_Publicada_Retorna200ComETag_EIfNoneMatchDevolve304()
    {
        var cenario = await MontarAsync();
        var rota = $"/api/v1/provedores/{cenario.ProvedorPublicadoSlug}/versoes/{cenario.VersaoPublicadaSlug}";

        using var primeira = await _cliente.GetAsync(rota);
        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        var etag = primeira.Headers.ETag?.ToString();
        etag.Should().NotBeNullOrEmpty(because: "P4 devolve ETag (§4.1)");

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.TryAddWithoutValidation("If-None-Match", etag);
        using var segunda = await _cliente.SendAsync(requisicao);

        segunda.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    // ---------------------------------------------------------------- P6 (U9)

    [Fact]
    public async Task P6_ErroDespublicado_Retorna410ComCodigoErroDespublicado()
    {
        var cenario = await MontarAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/erros/{cenario.ErroDespublicadoSlug}");

        status.Should().Be(HttpStatusCode.Gone);
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("ERRO_DESPUBLICADO");
    }

    [Fact]
    public async Task P6_ErroSemVersaoAplicavel_TrazVersoesAplicaveisComoArrayVazio_NuncaNull()
    {
        var cenario = await MontarAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/erros/{cenario.ErroGeralSlug}");

        status.Should().Be(HttpStatusCode.OK);
        var versoes = corpo.GetProperty("data").GetProperty("versoesAplicaveis");
        versoes.ValueKind.Should().Be(JsonValueKind.Array, because: "U9/§4.1: array vazio, nunca null nem omitido");
        versoes.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task P6_ErroComVersaoAplicavel_TrazAVersaoPublicada()
    {
        var cenario = await MontarAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/erros/{cenario.ErroPublicadoSlug}");

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("versoesAplicaveis").GetArrayLength().Should().Be(1);
    }

    // ---------------------------------------------------------------- §5.9 redirecionamento

    [Fact]
    public async Task P3_SlugAntigoComRedirecionamento_Retorna308ParaOSlugAtual()
    {
        var cenario = await MontarAsync();
        var slugAntigo = $"probe-antigo-{cenario.Sufixo}";

        await using (var contexto = _fixture.CriarContexto())
        {
            contexto.Redirecionamentos.Add(Redirecionamento.Criar(
                $"/provedores/{slugAntigo}", $"/provedores/{cenario.ProvedorPublicadoSlug}", TipoItemIndexado.provedor));
            await contexto.SaveChangesAsync();
        }

        using var resposta = await _cliente.GetAsync($"/api/v1/provedores/{slugAntigo}");

        resposta.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        resposta.Headers.Location!.ToString().Should().Be($"/provedores/{cenario.ProvedorPublicadoSlug}");
    }
}
