using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// A JUNÇÃO do protocolo de concorrência otimista, medida pela FRONTEIRA — não pelas duas metades.
///
/// <para>
/// O defeito que estes testes fecham (QA-01) nasceu de um teste que provava a metade errada: os
/// testes de backend montavam o <c>If-Match</c> ELES MESMOS a partir do corpo, e os de frontend
/// passavam um <c>etag</c> literal para o interceptor. Nenhum dos dois perguntava a única coisa que
/// importa: <b>o valor que o cliente consegue LER de uma resposta é aceito de volta?</b> Não era —
/// nenhum endpoint interno emitia <c>ETag</c> e o corpo nunca teve campo <c>etag</c>, então toda
/// escrita interna disparada pela UI recebia <c>400 IF_MATCH_OBRIGATORIO</c>.
/// </para>
///
/// <para>
/// Regra destes testes: o valor de <c>If-Match</c> NUNCA é construído aqui. Cada caso extrai o
/// token de uma resposta real, por um dos caminhos que um cliente HTTP tem à disposição
/// (header <c>ETag</c> ou campo <c>versaoConcorrencia</c> do corpo), e devolve o que leu.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ConcorrenciaFronteiraHttpTests : IAsyncLifetime
{
    private const string TodasAsRoles =
        "provedores-buscar,provedores-adicionar,provedores-atualizar,provedores-remover,provedores-publicar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ConcorrenciaFronteiraHttpTests(PostgresContainerFixture fixture)
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

    /// <summary>
    /// Resposta crua: o teste só pode olhar o que um cliente olharia — status, headers e corpo.
    /// </summary>
    private sealed record Resposta(HttpStatusCode Status, string? ETag, JsonElement Corpo);

    private async Task<Resposta> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, string? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, TodasAsRoles);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        // TryAddWithoutValidation: o valor vai exatamente como o cliente o leu, sem o
        // HttpClient normalizar/reformatar a ETag no caminho.
        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();

        return new Resposta(
            resposta.StatusCode,
            resposta.Headers.TryGetValues("ETag", out var etags) ? string.Concat(etags) : null,
            string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    private async Task<(int Id, Resposta Criacao)> CriarProvedorAsync(string sufixo)
    {
        var criacao = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/provedores", new
        {
            nome = $"Fronteira {sufixo}",
            identificadorInvoicy = $"FRT-{sufixo}",
            categoria = "Nacional",
            aliases = Array.Empty<string>(),
        });

        criacao.Status.Should().Be(HttpStatusCode.Created);
        return (criacao.Corpo.GetProperty("data").GetProperty("id").GetInt32(), criacao);
    }

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..8];

    private static object CorpoDeAtualizacao(string nome) => new
    {
        nome,
        categoria = "Nacional",
        aliases = Array.Empty<string>(),
    };

    // --------------------------------------------------------------- caminho do header ETag

    /// <summary>
    /// Caminho de quem segue o protocolo HTTP: ler <c>ETag</c> da resposta e devolvê-lo VERBATIM
    /// em <c>If-Match</c>. É o caminho de dois hooks do frontend (<c>response.headers.etag</c>) e
    /// era o que não existia — nenhum endpoint interno emitia o header.
    /// </summary>
    [Fact]
    public async Task DetalheInterno_EmiteETag_EOValorLidoDoHeaderEAceitoDeVoltaEmIfMatch()
    {
        var sufixo = NovoSufixo();
        var (id, _) = await CriarProvedorAsync(sufixo);

        var detalhe = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores/{id}");
        detalhe.Status.Should().Be(HttpStatusCode.OK);
        detalhe.ETag.Should().NotBeNullOrWhiteSpace(
            because: "o recurso EXIGE If-Match na escrita; não anunciar o validador na leitura torna a escrita inalcançável");

        var atualizacao = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}",
            CorpoDeAtualizacao($"Fronteira header {sufixo}"), ifMatch: detalhe.ETag);

        atualizacao.Status.Should().Be(HttpStatusCode.OK,
            because: "o valor foi lido da resposta e devolvido sem transformação nenhuma — se isto der 400, o par ETag/If-Match não fecha");
        atualizacao.Corpo.GetProperty("data").GetProperty("nome").GetString().Should().Be($"Fronteira header {sufixo}");
    }

    /// <summary>
    /// A resposta da própria mutação também traz o validador novo: o cliente consegue encadear
    /// duas edições sem um GET no meio (é o que a UI faz ao salvar e publicar em sequência).
    /// </summary>
    [Fact]
    public async Task MutacaoInterna_DevolveETagNovo_QueServeParaAMutacaoSeguinte()
    {
        var sufixo = NovoSufixo();
        var (id, criacao) = await CriarProvedorAsync(sufixo);

        criacao.ETag.Should().NotBeNullOrWhiteSpace(because: "quem acabou de criar precisa poder editar sem reler");

        var primeira = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}",
            CorpoDeAtualizacao($"Fronteira encadeada A {sufixo}"), ifMatch: criacao.ETag);
        primeira.Status.Should().Be(HttpStatusCode.OK);
        primeira.ETag.Should().NotBeNullOrWhiteSpace().And.NotBe(criacao.ETag,
            because: "o recurso mudou, então o validador tem de mudar com ele");

        var publicacao = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}/publicacao",
            new { publicado = true }, ifMatch: primeira.ETag);

        publicacao.Status.Should().Be(HttpStatusCode.OK,
            because: "publicar é PUT como qualquer outro e o validador da mutação anterior é o corrente");
    }

    // --------------------------------------------------------------- caminho do corpo (item de lista)

    /// <summary>
    /// AP-API-002 — o token tem de ser alcançável a partir de um ITEM DE LISTA, onde header não
    /// serve (é um por resposta). O caminho é o campo <c>versaoConcorrencia</c> do corpo, e este
    /// teste o consome exatamente como o cliente faria: lê do item e manda o número como veio.
    /// </summary>
    [Fact]
    public async Task ItemDeLista_CarregaOTokenNoCorpo_EEleServeParaEscreverSemGetDeDetalhe()
    {
        var sufixo = NovoSufixo();
        var (id, _) = await CriarProvedorAsync(sufixo);

        var lista = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores?q={sufixo}&tamanhoPagina=50");
        lista.Status.Should().Be(HttpStatusCode.OK);

        var item = lista.Corpo.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == id);

        // Exatamente o que o cliente tem em mãos: o número do corpo, virado string.
        var tokenDoItem = item.GetProperty("versaoConcorrencia").GetUInt32().ToString();

        var atualizacao = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}",
            CorpoDeAtualizacao($"Fronteira lista {sufixo}"), ifMatch: tokenDoItem);

        atualizacao.Status.Should().Be(HttpStatusCode.OK,
            because: "editar direto da lista é o requisito duro de AP-API-002 — header não cobre item de lista");
    }

    /// <summary>
    /// A mesma leitura do corpo, no formato de ETag fraca que o backend emite no header. Prova que
    /// o backend aceita as duas grafias do MESMO token, e por isso o cliente pode padronizar em uma
    /// só sem depender de qual endpoint respondeu.
    /// </summary>
    [Theory]
    [InlineData("{0}")]
    [InlineData("\"{0}\"")]
    [InlineData("W/\"{0}\"")]
    public async Task TokenDoCorpo_EmQualquerDasTresGrafiasDeIfMatch_EAceito(string formato)
    {
        var sufixo = NovoSufixo();
        var (id, criacao) = await CriarProvedorAsync(sufixo);

        var versaoConcorrencia = criacao.Corpo.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();
        var ifMatch = string.Format(System.Globalization.CultureInfo.InvariantCulture, formato, versaoConcorrencia);

        var atualizacao = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}",
            CorpoDeAtualizacao($"Fronteira formato {sufixo}"), ifMatch: ifMatch);

        atualizacao.Status.Should().Be(HttpStatusCode.OK);
    }

    // --------------------------------------------------------------- controles negativos

    /// <summary>
    /// Controle negativo do arranjo: sem o header a escrita continua barrada. Sem este caso, os
    /// testes acima ficariam verdes até se o <c>If-Match</c> deixasse de ser exigido.
    /// </summary>
    [Fact]
    public async Task SemIfMatch_AEscritaInternaContinuaBarrada()
    {
        var sufixo = NovoSufixo();
        var (id, _) = await CriarProvedorAsync(sufixo);

        var atualizacao = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}", CorpoDeAtualizacao($"Sem if match {sufixo}"));

        atualizacao.Status.Should().Be(HttpStatusCode.BadRequest);
        atualizacao.Corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("IF_MATCH_OBRIGATORIO");
    }

    /// <summary>
    /// Controle negativo da concorrência: o token de ANTES da edição não vale depois dela. Prova
    /// que o valor emitido é o <c>xmin</c> corrente e não um carimbo constante que sempre passa.
    /// </summary>
    [Fact]
    public async Task ETagAntigo_DepoisDeUmaEdicao_DevolveConflito()
    {
        var sufixo = NovoSufixo();
        var (id, criacao) = await CriarProvedorAsync(sufixo);

        var primeira = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}",
            CorpoDeAtualizacao($"Conflito A {sufixo}"), ifMatch: criacao.ETag);
        primeira.Status.Should().Be(HttpStatusCode.OK);

        var comTokenVelho = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/provedores/{id}",
            CorpoDeAtualizacao($"Conflito B {sufixo}"), ifMatch: criacao.ETag);

        comTokenVelho.Status.Should().Be(HttpStatusCode.Conflict);
        comTokenVelho.Corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("CONFLITO_CONCORRENCIA");
    }

    /// <summary>
    /// <c>DELETE</c> fecha o conjunto: é a outra metade da escrita que exige <c>If-Match</c>, e a
    /// resposta <c>204</c> não tem corpo de onde tirar token — logo o valor tem de vir da leitura
    /// anterior.
    /// </summary>
    [Fact]
    public async Task Delete_AceitaOETagLidoNaLeituraAnterior()
    {
        var sufixo = NovoSufixo();
        var (id, _) = await CriarProvedorAsync(sufixo);

        var detalhe = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/provedores/{id}");
        detalhe.ETag.Should().NotBeNullOrWhiteSpace();

        var remocao = await EnviarAsync(
            HttpMethod.Delete, $"/api/v1/interno/provedores/{id}", ifMatch: detalhe.ETag);

        remocao.Status.Should().Be(HttpStatusCode.NoContent);
    }
}
