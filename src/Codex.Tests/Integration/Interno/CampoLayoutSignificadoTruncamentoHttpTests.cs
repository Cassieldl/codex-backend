using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Criar/atualizar campo canônico com <c>significado</c>
/// acima de 300 caracteres derrubava a requisição inteira com <c>500</c>: o writer de índice de
/// busca gravava o significado INTEIRO em <c>itensbusca.subtitulo</c>, <c>varchar(300)</c>
/// (<c>IndiceBuscaWriter.CarregarMetadadosAsync</c>, caso <c>campolayout</c>), e o Postgres recusava
/// com <c>22001 value too long</c>.
///
/// <para>
/// O fix trunca só o METADADO DE EXIBIÇÃO (<c>IndiceBuscaWriter.AplicarTetoDeExibicao</c>) — o
/// texto INDEXADO para busca (pesoC, derivado do mesmo <c>Significado</c>) nunca é tocado; não há
/// como provar isso por HTTP sem expor a coluna interna de busca, então a suíte de
/// <c>IndiceBuscaWriter</c>/<c>PesosBusca</c> já cobre que pesoC carrega o texto inteiro — aqui o
/// contrato provado é "a requisição não cai mais, e o que fica visível (subtitulo) respeita o
/// teto da coluna".
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CampoLayoutSignificadoTruncamentoHttpTests : IAsyncLifetime
{
    private const string RolesDeEscrita =
        "versoeslayout-adicionar,campolayout-adicionar,campolayout-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public CampoLayoutSignificadoTruncamentoHttpTests(PostgresContainerFixture fixture)
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
    public async Task CriarCampoLayout_ComSignificadoDe1000Caracteres_Cria201EIndexaComSubtituloTruncado()
    {
        var versaoLayoutId = await CriarVersaoLayoutAsync();
        var significado = GerarTextoComPalavras(1000);
        significado.Length.Should().Be(1000, "o cenário do QA é especificamente 1000 caracteres");

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/layout/versoes/{versaoLayoutId}/campos", new
            {
                nome = $"Campo {NovoSufixo()}",
                caminhoXml = $"//Campo{NovoSufixo()}",
                significado,
                ordem = 1,
            });

        status.Should().Be(HttpStatusCode.Created,
            because: $"corpo: {(corpo.ValueKind == JsonValueKind.Undefined ? "(vazio)" : corpo.ToString())}");
        var campoLayoutId = corpo.GetProperty("data").GetProperty("id").GetInt32();

        var subtitulo = await ObterSubtituloDoIndiceAsync(campoLayoutId);

        subtitulo.Should().NotBeNull();
        subtitulo!.Length.Should().BeLessOrEqualTo(300, because: "itensbusca.subtitulo é varchar(300)");
        subtitulo.Should().EndWith("…", because: "significado de 1000 caracteres excede o teto — tem de truncar");
    }

    [Fact]
    public async Task CriarCampoLayout_ComSignificadoDeExatamente300Caracteres_NaoTrunca()
    {
        var versaoLayoutId = await CriarVersaoLayoutAsync();
        var significado = GerarTextoComPalavras(300);
        significado.Length.Should().Be(300);

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/layout/versoes/{versaoLayoutId}/campos", new
            {
                nome = $"Campo {NovoSufixo()}",
                caminhoXml = $"//Campo{NovoSufixo()}",
                significado,
                ordem = 1,
            });

        status.Should().Be(HttpStatusCode.Created);
        var campoLayoutId = corpo.GetProperty("data").GetProperty("id").GetInt32();

        var subtitulo = await ObterSubtituloDoIndiceAsync(campoLayoutId);

        subtitulo.Should().Be(significado, because: "exatamente no teto não é 'acima do teto' — não pode truncar");
        subtitulo.Should().NotEndWith("…");
    }

    /// <summary>
    /// S-02 (auditoria de segurança, D3) — reprodutor exato do auditor: 298 'a' + 😀 (par
    /// substituto) + "bbbb", SEM espaço nenhum (exercita o corte cru, não o de última palavra). O
    /// corte por posição caía bem na metade alta do emoji; persistir essa metade solta fazia o
    /// Npgsql (UTF-8 estrito) lançar <c>EncoderFallbackException</c> — <c>500</c> no POST.
    /// </summary>
    [Fact]
    public async Task CriarCampoLayout_ComEmojiNaFronteiraDoTruncamento_Cria201EIndexaComSubtituloValido()
    {
        var versaoLayoutId = await CriarVersaoLayoutAsync();
        var significado = new string('a', 298) + "😀" + "bbbb";

        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/layout/versoes/{versaoLayoutId}/campos", new
            {
                nome = $"Campo {NovoSufixo()}",
                caminhoXml = $"//Campo{NovoSufixo()}",
                significado,
                ordem = 1,
            });

        status.Should().Be(HttpStatusCode.Created,
            because: $"corpo: {(corpo.ValueKind == JsonValueKind.Undefined ? "(vazio)" : corpo.ToString())}");
        var campoLayoutId = corpo.GetProperty("data").GetProperty("id").GetInt32();

        var subtitulo = await ObterSubtituloDoIndiceAsync(campoLayoutId);

        subtitulo.Should().NotBeNull();
        subtitulo!.Length.Should().BeLessOrEqualTo(300, because: "itensbusca.subtitulo é varchar(300)");
    }

    [Fact]
    public async Task AtualizarCampoLayout_ComSignificadoDe1000Caracteres_ReindexaComSubtituloTruncado()
    {
        var versaoLayoutId = await CriarVersaoLayoutAsync();

        var (statusCriar, corpoCriado, _) = await EnviarAsync(
            HttpMethod.Post, $"/api/v1/interno/layout/versoes/{versaoLayoutId}/campos", new
            {
                nome = $"Campo {NovoSufixo()}",
                caminhoXml = $"//Campo{NovoSufixo()}",
                significado = "Significado inicial, curto.",
                ordem = 1,
            });
        statusCriar.Should().Be(HttpStatusCode.Created);
        var campoLayoutId = corpoCriado.GetProperty("data").GetProperty("id").GetInt32();
        var versaoConcorrencia = corpoCriado.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

        var significadoNovo = GerarTextoComPalavras(1000);

        var (statusAtualizar, corpoAtualizado, _) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/campos-layout/{campoLayoutId}",
            new { significado = significadoNovo }, versaoConcorrencia);

        statusAtualizar.Should().Be(HttpStatusCode.OK,
            because: $"corpo: {(corpoAtualizado.ValueKind == JsonValueKind.Undefined ? "(vazio)" : corpoAtualizado.ToString())}");

        var subtitulo = await ObterSubtituloDoIndiceAsync(campoLayoutId);

        subtitulo.Should().NotBeNull();
        subtitulo!.Length.Should().BeLessOrEqualTo(300);
        subtitulo.Should().EndWith("…");
    }

    // ------------------------------------------------------------------ apoio

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>Texto com espaços reais (nunca uma palavra só) — para exercitar o corte na última palavra inteira, não um corte cru.</summary>
    private static string GerarTextoComPalavras(int tamanhoExato)
    {
        var texto = string.Join(' ', Enumerable.Range(0, tamanhoExato / 8 + 5).Select(i => $"palavra{i:D4}"));
        return texto.Length >= tamanhoExato ? texto[..tamanhoExato] : texto.PadRight(tamanhoExato, 'x');
    }

    private async Task<int> CriarVersaoLayoutAsync()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/layout/versoes", new
        {
            rotulo = $"Layout significado {NovoSufixo()}",
            descricao = (string?)null,
        });

        status.Should().Be(HttpStatusCode.Created);
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<string?> ObterSubtituloDoIndiceAsync(int campoLayoutId)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.ItensBusca.AsNoTracking()
            .Where(i => i.TipoItem == TipoItemIndexado.campolayout && i.ItemId == campoLayoutId)
            .Select(i => i.Subtitulo)
            .SingleAsync();
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
