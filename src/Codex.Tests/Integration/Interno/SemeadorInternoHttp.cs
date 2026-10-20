using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Interno;

/// <summary>Cria recursos internos pela API com todas as roles do mapa.</summary>
internal sealed class SemeadorInternoHttp
{
    private readonly HttpClient _cliente;

    public SemeadorInternoHttp(HttpClient cliente)
    {
        _cliente = cliente;
    }

    public static string TodasAsRolesInternas() => string.Join(',', PipelineAutorizacaoTests.EndpointsInternos()
        .Select(linha => (string)linha[2])
        .Distinct(StringComparer.Ordinal));

    public async Task<(HttpStatusCode Status, string Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpo, string? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, TodasAsRolesInternas());

        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        using var resposta = await _cliente.SendAsync(requisicao);
        return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
    }

    public async Task<int> CriarAsync(string rota, object corpo)
    {
        var (status, texto) = await EnviarAsync(HttpMethod.Post, rota, corpo);

        status.Should().Be(HttpStatusCode.Created, because: $"POST {rota} é preparação do teste: {texto}");
        return JsonDocument.Parse(texto).RootElement.GetProperty("data").GetProperty("id").GetInt32();
    }

    public Task<int> CriarProvedorAsync(string sufixo) =>
        CriarAsync("/api/v1/interno/provedores", CorpoDeProvedor($"Provedor semeado {sufixo}", $"SEM-{sufixo}"));

    public Task<int> CriarVersaoLayoutAsync(string sufixo) =>
        CriarAsync("/api/v1/interno/layout/versoes", CorpoDeVersaoLayout($"Layout semeado {sufixo}"));

    public Task<int> CriarCampoLayoutAsync(int versaoLayoutId, string sufixo) =>
        CriarAsync($"/api/v1/interno/layout/versoes/{versaoLayoutId}/campos",
            CorpoDeCampoLayout($"Campo semeado {sufixo}", $"Raiz/CampoSemeado{sufixo}"));

    public Task<int> CriarVersaoProvedorAsync(int provedorId, int versaoLayoutId, string sufixo) =>
        CriarAsync($"/api/v1/interno/provedores/{provedorId}/versoes",
            CorpoDeVersaoProvedor(versaoLayoutId, $"Versao semeada {sufixo}"));

    public Task<int> CriarCampoVersaoAsync(int versaoProvedorId, int campoLayoutId) =>
        CriarAsync($"/api/v1/interno/versoes/{versaoProvedorId}/campos", CorpoDeCampoVersao(campoLayoutId));

    public static object CorpoDeProvedor(string nome, string identificadorInvoicy) => new
    {
        nome,
        identificadorInvoicy,
        categoria = "Nacional",
        aliases = Array.Empty<string>(),
    };

    public static object CorpoDeVersaoLayout(string rotulo) => new { rotulo };

    public static object CorpoDeCampoLayout(string nome, string caminhoXml) => new
    {
        nome,
        caminhoXml,
        significado = "Campo semeado para teste.",
        ordem = 0,
    };

    public static object CorpoDeVersaoProvedor(int versaoLayoutId, string rotulo) => new { versaoLayoutId, rotulo };

    public static object CorpoDeCampoVersao(int campoLayoutId) => new
    {
        campoLayoutId,
        tipo = "texto",
        estadoPreenchimento = "Opcional",
        ordem = 0,
    };

    public static object CorpoDeErro(string codigo, string titulo) => new
    {
        codigo,
        origem = "InvoiCy",
        titulo,
        variantes = new[] { $"Mensagem de {titulo}" },
    };

    public static object CorpoDeArtigo(string titulo) => new
    {
        tipo = "Conceito",
        titulo,
        corpo = "Corpo semeado para teste.",
        campoLayoutIds = Array.Empty<int>(),
        erroIds = Array.Empty<int>(),
    };
}
