using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Reproduz o cenário de produção INTEIRO, não só
/// a metade que <c>DetalheErroVariantesHttpTests</c> cobre. O defeito real não era "o GET devolve
/// array vazio": era "o formulário interno lê o GET, devolve o MESMO corpo por PUT, e o PUT apaga
/// as variantes reais via <c>Erro.SubstituirVariantes</c>" — <c>AtualizarErroDto.Variantes</c> tem
/// default <c>[]</c>, então um corpo de PUT montado a partir de um GET que não trouxe variantes
/// sempre as apaga, sem erro nenhum.
///
/// <para>
/// Rodado contra o <see cref="Codex.Infrastructure.Repositories.ErroRepositorioInterno"/> ANTES do
/// fix de D2 (<c>rastreada: false</c> sem <c>Include(Variantes)</c>), este teste cai: o GET inicial
/// devolve <c>variantes: []</c>, o corpo do PUT monta com essa lista vazia, e o GET final confirma
/// as variantes reais apagadas do banco — a mutação que prova o detector.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ErroVariantesRoundTripHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita = "erros-buscar,erros-adicionar,erros-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ErroVariantesRoundTripHttpTests(PostgresContainerFixture fixture)
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
    public async Task GetSeguidoDePut_ComOCorpoDevolvidoPeloGet_PreservaAsVariantes()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];
        var variante1 = $"mensagem um {sufixo}";
        var variante2 = $"mensagem dois {sufixo}";

        var (statusCriar, corpoCriado, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", new
        {
            codigo = $"E-{sufixo}",
            origem = nameof(OrigemErro.InvoiCy),
            titulo = $"Erro {sufixo}",
            variantes = new[] { variante1, variante2 },
        });
        statusCriar.Should().Be(HttpStatusCode.Created);
        var id = corpoCriado.GetProperty("data").GetProperty("id").GetInt32();

        // 1. Formulário interno abre: GET do detalhe.
        var (statusGet, corpoGet, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/erros/{id}");
        statusGet.Should().Be(HttpStatusCode.OK);
        var detalhe = corpoGet.GetProperty("data");

        var variantesLidas = detalhe.GetProperty("variantes").EnumerateArray().Select(v => v.GetString()).ToArray();
        var versaoConcorrencia = detalhe.GetProperty("versaoConcorrencia").GetUInt32();

        // 2. Usuário edita só o título e salva — o PUT é montado com EXATAMENTE o que o GET devolveu
        // para os demais campos, como o formulário real faz.
        var tituloEditado = $"Erro editado {sufixo}";
        var (statusPut, corpoPut, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/erros/{id}",
            new
            {
                origem = detalhe.GetProperty("origem").GetString(),
                titulo = tituloEditado,
                causaProvavel = detalhe.TryGetProperty("causaProvavel", out var cp) ? cp.GetString() : null,
                solucaoSugerida = detalhe.TryGetProperty("solucaoSugerida", out var ss) ? ss.GetString() : null,
                variantes = variantesLidas,
                versoesProvedorIds = detalhe.GetProperty("versoesProvedorIds").EnumerateArray().Select(v => v.GetInt32()).ToArray(),
                camposLayoutIds = detalhe.GetProperty("camposLayoutIds").EnumerateArray().Select(v => v.GetInt32()).ToArray(),
            },
            versaoConcorrencia);

        statusPut.Should().Be(HttpStatusCode.OK, corpoPut.ToString());
        corpoPut.GetProperty("data").GetProperty("titulo").GetString().Should().Be(tituloEditado);

        // 3. Confirma no banco (GET final) — não só no envelope do PUT.
        var (statusFinal, corpoFinal, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/erros/{id}");
        statusFinal.Should().Be(HttpStatusCode.OK);

        var variantesFinal = corpoFinal.GetProperty("data").GetProperty("variantes")
            .EnumerateArray().Select(v => v.GetString()).ToArray();

        variantesFinal.Should().BeEquivalentTo([variante1, variante2],
            because: "o ciclo GET -> PUT com o corpo devolvido é o que o formulário interno faz de verdade; " +
                     "se o GET não trouxer variantes, o PUT as apaga em silêncio");
    }

    /// <summary>
    /// Conjunto vazio: U9 exige ao menos uma variante para QUALQUER origem
    /// (<c>AtualizarErroDtoValidator</c>) — então o PUT nunca deveria alcançar o repositório com
    /// <c>variantes: []</c>. Fecha o terceiro caso pedido na rodada 2: igual (teste acima), trocado
    /// (<c>ErroSubstituirVariantesHttpTests</c>) e vazio (aqui). Nunca pode ser <c>500</c> — a troca
    /// de <c>Erro.SubstituirVariantes</c> por uma lista vazia é uma operação válida do domínio; quem
    /// barra é a validação, antes do service.
    /// </summary>
    [Fact]
    public async Task Put_ComConjuntoDeVariantesVazio_Devolve422SemAlcancarOBanco()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        var (statusCriar, corpoCriado, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", new
        {
            codigo = $"E-{sufixo}",
            origem = nameof(OrigemErro.InvoiCy),
            titulo = $"Erro {sufixo}",
            variantes = new[] { $"original {sufixo}" },
        });
        statusCriar.Should().Be(HttpStatusCode.Created);
        var id = corpoCriado.GetProperty("data").GetProperty("id").GetInt32();
        var versao = corpoCriado.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

        var (statusPut, corpoPut, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/erros/{id}",
            new
            {
                origem = nameof(OrigemErro.InvoiCy),
                titulo = $"Erro editado {sufixo}",
                variantes = Array.Empty<string>(),
                versoesProvedorIds = Array.Empty<int>(),
                camposLayoutIds = Array.Empty<int>(),
            },
            versao);

        statusPut.Should().Be(HttpStatusCode.UnprocessableEntity, corpoPut.ToString());

        var (statusGet, corpoGet, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/erros/{id}");
        var variantesNoBanco = corpoGet.GetProperty("data").GetProperty("variantes")
            .EnumerateArray().Select(v => v.GetString()).ToArray();

        variantesNoBanco.Should().BeEquivalentTo([$"original {sufixo}"],
            because: "o PUT rejeitado não pode ter alterado nada — a variante original continua lá");
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeEscrita);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }
}
