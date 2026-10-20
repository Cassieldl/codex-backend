using System.Net;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>Segundo POST que colide em índice único responde 409 REGISTRO_DUPLICADO sem citar o schema.</summary>
[Collection(PostgresCollection.Nome)]
public sealed class PostDuplicadoPorIndiceUnicoHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private SemeadorInternoHttp _semeador = null!;

    public PostDuplicadoPorIndiceUnicoHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        _semeador = new SemeadorInternoHttp(_cliente);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private sealed record Colisao(string Rota, object Primeiro, object Segundo);

    public static TheoryData<string> IndicesUnicos() => new()
    {
        "uq_codex_provedores_slug",
        "uq_codex_provedores_identificadorinvoicy",
        "uq_codex_erros_slug",
        "uq_codex_artigos_slug",
        "uq_codex_camposlayout_slug",
        "uq_codex_camposlayout_caminhoxml",
        "uq_codex_versoesprovedor_provedorid_slug",
        "uq_codex_versoeslayout_slug",
        "uq_codex_camposversao_versaoprovedorid_campolayoutid",
    };

    [Theory]
    [MemberData(nameof(IndicesUnicos))]
    public async Task SegundoPost_QueColideNoIndiceUnico_Devolve409SemVazarSchema(string indice)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];
        var colisao = await PrepararAsync(indice, sufixo);

        await _semeador.CriarAsync(colisao.Rota, colisao.Primeiro);
        var (status, texto) = await _semeador.EnviarAsync(HttpMethod.Post, colisao.Rota, colisao.Segundo);

        status.Should().Be(HttpStatusCode.Conflict, because: $"segundo POST em {colisao.Rota} colide em {indice}: {texto}");
        JsonDocument.Parse(texto).RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("REGISTRO_DUPLICADO");
        texto.Should().NotContain("uq_codex_").And.NotContain("at Codex.");
    }

    private async Task<Colisao> PrepararAsync(string indice, string sufixo)
    {
        switch (indice)
        {
            case "uq_codex_provedores_slug":
                return new Colisao("/api/v1/interno/provedores",
                    SemeadorInternoHttp.CorpoDeProvedor($"Provedor Duplicado {sufixo}", $"DUP-A-{sufixo}"),
                    SemeadorInternoHttp.CorpoDeProvedor($"provedor duplicado {sufixo}", $"DUP-B-{sufixo}"));

            case "uq_codex_provedores_identificadorinvoicy":
                return new Colisao("/api/v1/interno/provedores",
                    SemeadorInternoHttp.CorpoDeProvedor($"Provedor A {sufixo}", $"DUP-{sufixo}"),
                    SemeadorInternoHttp.CorpoDeProvedor($"Provedor B {sufixo}", $"DUP-{sufixo}"));

            case "uq_codex_erros_slug":
                return new Colisao("/api/v1/interno/erros",
                    SemeadorInternoHttp.CorpoDeErro($"E{sufixo}", "Erro Duplicado"),
                    SemeadorInternoHttp.CorpoDeErro($"e{sufixo}", "erro duplicado"));

            case "uq_codex_artigos_slug":
                return new Colisao("/api/v1/interno/artigos",
                    SemeadorInternoHttp.CorpoDeArtigo($"Artigo Duplicado {sufixo}"),
                    SemeadorInternoHttp.CorpoDeArtigo($"artigo duplicado {sufixo}"));

            case "uq_codex_camposlayout_slug":
                return new Colisao(await RotaDeCamposLayoutAsync(sufixo),
                    SemeadorInternoHttp.CorpoDeCampoLayout("Campo A", $"Raiz/CampoDuplicado{sufixo}"),
                    SemeadorInternoHttp.CorpoDeCampoLayout("Campo B", $"raiz/campoduplicado{sufixo}"));

            case "uq_codex_camposlayout_caminhoxml":
                return new Colisao(await RotaDeCamposLayoutAsync(sufixo),
                    SemeadorInternoHttp.CorpoDeCampoLayout("Campo A", $"Raiz/CampoDuplicado{sufixo}"),
                    SemeadorInternoHttp.CorpoDeCampoLayout("Campo B", $"Raiz/CampoDuplicado{sufixo}"));

            case "uq_codex_versoesprovedor_provedorid_slug":
            {
                var versaoLayoutId = await _semeador.CriarVersaoLayoutAsync(sufixo);
                var provedorId = await _semeador.CriarProvedorAsync(sufixo);
                return new Colisao($"/api/v1/interno/provedores/{provedorId}/versoes",
                    SemeadorInternoHttp.CorpoDeVersaoProvedor(versaoLayoutId, $"Versao Duplicada {sufixo}"),
                    SemeadorInternoHttp.CorpoDeVersaoProvedor(versaoLayoutId, $"versao duplicada {sufixo}"));
            }

            case "uq_codex_versoeslayout_slug":
                return new Colisao("/api/v1/interno/layout/versoes",
                    SemeadorInternoHttp.CorpoDeVersaoLayout($"Layout Duplicado {sufixo}"),
                    SemeadorInternoHttp.CorpoDeVersaoLayout($"layout duplicado {sufixo}"));

            case "uq_codex_camposversao_versaoprovedorid_campolayoutid":
            {
                var versaoLayoutId = await _semeador.CriarVersaoLayoutAsync(sufixo);
                var campoLayoutId = await _semeador.CriarCampoLayoutAsync(versaoLayoutId, sufixo);
                var provedorId = await _semeador.CriarProvedorAsync(sufixo);
                var versaoProvedorId = await _semeador.CriarVersaoProvedorAsync(provedorId, versaoLayoutId, sufixo);
                return new Colisao($"/api/v1/interno/versoes/{versaoProvedorId}/campos",
                    SemeadorInternoHttp.CorpoDeCampoVersao(campoLayoutId),
                    SemeadorInternoHttp.CorpoDeCampoVersao(campoLayoutId));
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(indice), indice, null);
        }
    }

    private async Task<string> RotaDeCamposLayoutAsync(string sufixo) =>
        $"/api/v1/interno/layout/versoes/{await _semeador.CriarVersaoLayoutAsync(sufixo)}/campos";
}
