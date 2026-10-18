using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// O detalhe interno do artigo devolve os vínculos rotulados (<c>erros</c> e <c>camposLayout</c>)
/// ao lado de <c>erroIds</c>/<c>campoLayoutIds</c>. Toda asserção lê o JSON cru do fio: o DTO do
/// C# confirmaria o próprio formato, não o que o frontend recebe.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ArtigoVinculosRotuladosHttpTests : IAsyncLifetime
{
    private const string Roles =
        "artigos-buscar,artigos-adicionar,artigos-atualizar,artigos-publicar,"
        + "erros-adicionar,erros-remover,versoeslayout-adicionar,campolayout-remover";

    private static readonly string[] ChavesErro = ["id", "codigo", "titulo", "desativado"];

    private static readonly string[] ChavesCampoLayout =
        ["id", "nome", "caminhoXml", "versaoLayoutId", "versaoLayoutRotulo", "desativado"];

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ArtigoVinculosRotuladosHttpTests(PostgresContainerFixture fixture)
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
    public async Task Detalhe_DevolveErrosECamposLayoutComAsChavesETiposDoContrato()
    {
        var sufixo = NovoSufixo();
        var erro = await CriarErroAsync($"{sufixo}-X", $"Erro contrato {sufixo}");
        var versao = await CriarVersaoLayoutAsync();
        var campo = await SemearCampoLayoutAsync(versao.Id, $"Campo contrato {sufixo}", $"//Contrato{sufixo}");
        var artigoId = await CriarArtigoAsync([campo.Id], [erro.Id]);

        var dados = await ObterDetalheAsync(artigoId);

        dados.GetProperty("erroIds").EnumerateArray().Select(e => e.GetInt32()).Should().Equal(erro.Id);
        dados.GetProperty("campoLayoutIds").EnumerateArray().Select(e => e.GetInt32()).Should().Equal(campo.Id);

        var erroNoFio = dados.GetProperty("erros").EnumerateArray().Single();
        erroNoFio.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(ChavesErro);
        erroNoFio.GetProperty("id").ValueKind.Should().Be(JsonValueKind.Number);
        erroNoFio.GetProperty("id").GetInt32().Should().Be(erro.Id);
        erroNoFio.GetProperty("codigo").GetString().Should().Be($"{sufixo}-X");
        erroNoFio.GetProperty("titulo").GetString().Should().Be($"Erro contrato {sufixo}");
        erroNoFio.GetProperty("desativado").ValueKind.Should().Be(JsonValueKind.False);

        var campoNoFio = dados.GetProperty("camposLayout").EnumerateArray().Single();
        campoNoFio.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(ChavesCampoLayout);
        campoNoFio.GetProperty("id").GetInt32().Should().Be(campo.Id);
        campoNoFio.GetProperty("nome").GetString().Should().Be($"Campo contrato {sufixo}");
        campoNoFio.GetProperty("caminhoXml").GetString().Should().Be($"//Contrato{sufixo}");
        campoNoFio.GetProperty("versaoLayoutId").ValueKind.Should().Be(JsonValueKind.Number);
        campoNoFio.GetProperty("versaoLayoutId").GetInt32().Should().Be(versao.Id);
        campoNoFio.GetProperty("versaoLayoutRotulo").GetString().Should().Be(versao.Rotulo);
        campoNoFio.GetProperty("desativado").ValueKind.Should().Be(JsonValueKind.False);
    }

    [Fact]
    public async Task Detalhe_VinculoComErroECampoRemovidos_AparecemComDesativadoVerdadeiroEOsIdsBatem()
    {
        var sufixo = NovoSufixo();
        var erroAtivo = await CriarErroAsync($"{sufixo}-A", "Erro ativo");
        var erroRemovido = await CriarErroAsync($"{sufixo}-B", "Erro removido");
        var versao = await CriarVersaoLayoutAsync();
        var campoAtivo = await SemearCampoLayoutAsync(versao.Id, "Campo ativo", $"//Ativo{sufixo}");
        var campoRemovido = await SemearCampoLayoutAsync(versao.Id, "Campo removido", $"//Removido{sufixo}");
        var artigoId = await CriarArtigoAsync([campoAtivo.Id, campoRemovido.Id], [erroAtivo.Id, erroRemovido.Id]);

        await RemoverAsync($"/api/v1/interno/erros/{erroRemovido.Id}", erroRemovido.Token);
        await RemoverAsync($"/api/v1/interno/campos-layout/{campoRemovido.Id}", campoRemovido.Token);

        var dados = await ObterDetalheAsync(artigoId);

        DesativadoPorId(dados.GetProperty("erros")).Should().BeEquivalentTo(new Dictionary<int, bool>
        {
            [erroAtivo.Id] = false,
            [erroRemovido.Id] = true,
        });
        DesativadoPorId(dados.GetProperty("camposLayout")).Should().BeEquivalentTo(new Dictionary<int, bool>
        {
            [campoAtivo.Id] = false,
            [campoRemovido.Id] = true,
        });

        IdsDe(dados, "erros").Should().BeEquivalentTo(IdsPlanosDe(dados, "erroIds"));
        IdsDe(dados, "camposLayout").Should().BeEquivalentTo(IdsPlanosDe(dados, "campoLayoutIds"));
    }

    [Fact]
    public async Task Detalhe_OrdenaErrosPorCodigoEIdECamposPorOrdemDaVersaoNomeEId()
    {
        var sufixo = NovoSufixo();
        var erroB1 = await CriarErroAsync($"{sufixo}-B", "Erro B primeiro");
        var erroC = await CriarErroAsync($"{sufixo}-C", "Erro C");
        var erroA = await CriarErroAsync($"{sufixo}-A", "Erro A");
        var erroB2 = await CriarErroAsync($"{sufixo}-B", "Erro B segundo");

        var versaoAnterior = await CriarVersaoLayoutAsync();
        var versaoPosterior = await CriarVersaoLayoutAsync();
        var alfaPrimeiro = await SemearCampoLayoutAsync(versaoPosterior.Id, "Alfa", $"//Alfa1{sufixo}");
        var zeta = await SemearCampoLayoutAsync(versaoAnterior.Id, "Zeta", $"//Zeta{sufixo}");
        var beta = await SemearCampoLayoutAsync(versaoAnterior.Id, "Beta", $"//Beta{sufixo}");
        var alfaSegundo = await SemearCampoLayoutAsync(versaoPosterior.Id, "Alfa", $"//Alfa2{sufixo}");

        var artigoId = await CriarArtigoAsync(
            [alfaSegundo.Id, beta.Id, zeta.Id, alfaPrimeiro.Id],
            [erroB2.Id, erroA.Id, erroC.Id, erroB1.Id]);

        await MoverParaOFimDoHeapAsync(artigoId, erroB1.Id, alfaPrimeiro.Id);

        var dados = await ObterDetalheAsync(artigoId);

        IdsDe(dados, "erros").Should().Equal(erroA.Id, erroB1.Id, erroB2.Id, erroC.Id);
        IdsDe(dados, "camposLayout").Should().Equal(beta.Id, zeta.Id, alfaPrimeiro.Id, alfaSegundo.Id);
    }

    [Fact]
    public async Task CriarEAtualizar_DevolvemAsColecoes_EOPutComOCorpoDoGetNaoPerdeVinculo()
    {
        var sufixo = NovoSufixo();
        var erroAtivo = await CriarErroAsync($"{sufixo}-A", "Erro do round-trip");
        var erroRemovido = await CriarErroAsync($"{sufixo}-B", "Erro removido do round-trip");
        var versao = await CriarVersaoLayoutAsync();
        var campo = await SemearCampoLayoutAsync(versao.Id, "Campo do round-trip", $"//RoundTrip{sufixo}");

        var (statusCriacao, corpoCriacao) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/artigos", new
        {
            tipo = nameof(TipoArtigo.Conceito),
            titulo = $"Artigo round-trip {sufixo}",
            corpo = "Corpo do artigo.",
            campoLayoutIds = new[] { campo.Id },
            erroIds = new[] { erroRemovido.Id, erroAtivo.Id },
        });

        statusCriacao.Should().Be(HttpStatusCode.Created, corpoCriacao.ToString());
        var criado = corpoCriacao.GetProperty("data");
        IdsDe(criado, "erros").Should().Equal(erroAtivo.Id, erroRemovido.Id);
        IdsDe(criado, "camposLayout").Should().Equal(campo.Id);
        var artigoId = criado.GetProperty("id").GetInt32();

        await RemoverAsync($"/api/v1/interno/erros/{erroRemovido.Id}", erroRemovido.Token);

        var detalhe = await ObterDetalheAsync(artigoId);
        var corpoPut = new
        {
            tipo = detalhe.GetProperty("tipo").GetString(),
            titulo = detalhe.GetProperty("titulo").GetString(),
            corpo = detalhe.GetProperty("corpo").GetString(),
            campoLayoutIds = IdsPlanosDe(detalhe, "campoLayoutIds"),
            erroIds = IdsPlanosDe(detalhe, "erroIds"),
        };

        var (statusPut, respostaPut) = await EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/artigos/{artigoId}", corpoPut, TokenDe(detalhe));

        statusPut.Should().Be(HttpStatusCode.OK, respostaPut.ToString());
        var atualizado = respostaPut.GetProperty("data");
        IdsDe(atualizado, "erros").Should().Equal(erroAtivo.Id, erroRemovido.Id);
        DesativadoPorId(atualizado.GetProperty("erros"))[erroRemovido.Id].Should().BeTrue();
        IdsDe(atualizado, "camposLayout").Should().Equal(campo.Id);

        await using var contexto = _fixture.CriarContexto();
        (await contexto.ArtigosErros.AsNoTracking().Where(v => v.ArtigoId == artigoId).Select(v => v.ErroId).ToArrayAsync())
            .Should().BeEquivalentTo([erroAtivo.Id, erroRemovido.Id]);
        (await contexto.ArtigosCamposLayout.AsNoTracking().Where(v => v.ArtigoId == artigoId).Select(v => v.CampoLayoutId).ToArrayAsync())
            .Should().BeEquivalentTo([campo.Id]);
    }

    [Fact]
    public async Task PublicacaoVerificacaoESlug_DevolvemOsMesmosVinculosRotuladosDoDetalhe()
    {
        var sufixo = NovoSufixo();
        var erro = await CriarErroAsync($"{sufixo}-A", "Erro das demais respostas");
        var versao = await CriarVersaoLayoutAsync();
        var campo = await SemearCampoLayoutAsync(versao.Id, "Campo das demais respostas", $"//Demais{sufixo}");
        var artigoId = await CriarArtigoAsync([campo.Id], [erro.Id]);

        var detalhe = await ObterDetalheAsync(artigoId);
        var errosEsperados = detalhe.GetProperty("erros").GetRawText();
        var camposEsperados = detalhe.GetProperty("camposLayout").GetRawText();

        var publicado = await EnviarComSucessoAsync(
            $"/api/v1/interno/artigos/{artigoId}/publicacao", new { publicado = true }, TokenDe(detalhe));
        var verificado = await EnviarComSucessoAsync(
            $"/api/v1/interno/artigos/{artigoId}/verificacao", new { verificadoEm = "2026-01-15" }, TokenDe(publicado));
        var comSlugNovo = await EnviarComSucessoAsync(
            $"/api/v1/interno/artigos/{artigoId}/slug", new { slug = $"artigo-vinculos-{sufixo}" }, TokenDe(verificado));

        foreach (var resposta in new[] { publicado, verificado, comSlugNovo })
        {
            resposta.GetProperty("erros").GetRawText().Should().Be(errosEsperados);
            resposta.GetProperty("camposLayout").GetRawText().Should().Be(camposEsperados);
        }
    }

    // ------------------------------------------------------------------ apoio

    private sealed record ItemComToken(int Id, uint Token);

    private sealed record VersaoLayoutCriada(int Id, string Rotulo);

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static uint TokenDe(JsonElement dados) => dados.GetProperty("versaoConcorrencia").GetUInt32();

    private static int[] IdsDe(JsonElement dados, string colecao) =>
        dados.GetProperty(colecao).EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToArray();

    private static int[] IdsPlanosDe(JsonElement dados, string colecao) =>
        dados.GetProperty(colecao).EnumerateArray().Select(id => id.GetInt32()).ToArray();

    private static Dictionary<int, bool> DesativadoPorId(JsonElement colecao) =>
        colecao.EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetInt32(),
            item => item.GetProperty("desativado").GetBoolean());

    private async Task<JsonElement> ObterDetalheAsync(int artigoId)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/artigos/{artigoId}");
        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        return corpo.GetProperty("data");
    }

    private async Task<JsonElement> EnviarComSucessoAsync(string rota, object corpoRequisicao, uint token)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Put, rota, corpoRequisicao, token);
        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        return corpo.GetProperty("data");
    }

    private async Task<ItemComToken> CriarErroAsync(string codigo, string titulo)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/erros", new
        {
            codigo,
            origem = nameof(OrigemErro.InvoiCy),
            titulo,
            variantes = new[] { $"mensagem {NovoSufixo()}" },
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        var dados = corpo.GetProperty("data");
        return new ItemComToken(dados.GetProperty("id").GetInt32(), TokenDe(dados));
    }

    private async Task<VersaoLayoutCriada> CriarVersaoLayoutAsync()
    {
        var rotulo = $"Layout vinculos {NovoSufixo()}";
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/layout/versoes", new
        {
            rotulo,
            descricao = (string?)null,
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return new VersaoLayoutCriada(corpo.GetProperty("data").GetProperty("id").GetInt32(), rotulo);
    }

    /// <summary>Semeado direto no banco: a API interna não cria campo canônico avulso.</summary>
    private async Task<ItemComToken> SemearCampoLayoutAsync(int versaoLayoutId, string nome, string caminhoXml)
    {
        await using var contexto = _fixture.CriarContexto();
        var campo = CampoLayout.Criar(versaoLayoutId, nome, caminhoXml, "significado", ordem: 1);
        contexto.CamposLayout.Add(campo);
        await contexto.SaveChangesAsync();

        return new ItemComToken(campo.Id, campo.Versao);
    }

    /// <summary>
    /// O EF insere os vínculos em ordem de chave, então a ordem física coincide com a de id. Um
    /// UPDATE reescreve a tupla no fim do heap: sem o desempate por id, o empate passa a sair invertido.
    /// </summary>
    private async Task MoverParaOFimDoHeapAsync(int artigoId, int erroId, int campoLayoutId)
    {
        var agora = DateTimeOffset.UtcNow;
        await using var contexto = _fixture.CriarContexto();

        await contexto.ArtigosErros.Where(v => v.ArtigoId == artigoId && v.ErroId == erroId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.AtualizadoEm, agora));
        await contexto.ArtigosCamposLayout.Where(v => v.ArtigoId == artigoId && v.CampoLayoutId == campoLayoutId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.AtualizadoEm, agora));
        await contexto.Erros.Where(e => e.Id == erroId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.AtualizadoEm, agora));
        await contexto.CamposLayout.Where(c => c.Id == campoLayoutId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.AtualizadoEm, agora));
    }

    private async Task<int> CriarArtigoAsync(int[] campoLayoutIds, int[] erroIds)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/api/v1/interno/artigos", new
        {
            tipo = nameof(TipoArtigo.Conceito),
            titulo = $"Artigo vinculos {NovoSufixo()}",
            corpo = "Corpo do artigo.",
            campoLayoutIds,
            erroIds,
        });

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return corpo.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task RemoverAsync(string rota, uint token)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Delete, rota, ifMatch: token);
        status.Should().Be(HttpStatusCode.NoContent, corpo.ToString());
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, Roles);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo);
    }
}
