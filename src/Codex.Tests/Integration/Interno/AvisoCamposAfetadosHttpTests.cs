using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Campos afetados do aviso de versão, pela pipeline HTTP real e o banco real. O defeito era
/// "aceita, devolve na resposta e descarta": <c>Aviso.CamposAfetadosIds</c> não é coluna, e nada
/// gravava <c>avisoscampos</c> — o GET seguinte voltava vazio e o PUT, que reenvia o que o GET
/// mostrou, apagava o vínculo.
///
/// <para>
/// Cada caso confere o BANCO ou o GET seguinte, nunca só o eco da resposta de escrita (o eco era
/// justamente o que mentia). A segunda metade prova o que o leitor da página pública enxerga.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class AvisoCamposAfetadosHttpTests : IAsyncLifetime
{
    private const string Roles =
        "avisos-adicionar,avisos-atualizar,avisos-publicar,avisos-remover,versoesprovedor-buscar,provedores-buscar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public AvisoCamposAfetadosHttpTests(PostgresContainerFixture fixture)
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

    private sealed record CampoSemeado(int CampoVersaoId, int CampoLayoutId, string Slug, string Nome, string CaminhoXml);

    private sealed record Cenario(
        int ProvedorId,
        string ProvedorSlug,
        int VersaoId,
        string VersaoSlug,
        CampoSemeado CampoA,
        CampoSemeado CampoB,
        CampoSemeado CampoDeOutraVersao)
    {
        public string RotaPaginaPublica => $"/api/v1/provedores/{ProvedorSlug}/versoes/{VersaoSlug}";

        public string RotaProvedorPublica => $"/api/v1/provedores/{ProvedorSlug}";
    }

    // ------------------------------------------------------------------ gravação e leitura interna

    [Fact]
    public async Task Post_ComCampos_OGetSeguinteDaVersaoDevolveOsCamposGravadosEOBancoTemAsLinhas()
    {
        var cenario = await SemearAsync();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/versoes/{cenario.VersaoId}/avisos",
            CorpoDeAviso("Aviso com campos", [cenario.CampoB.CampoVersaoId, cenario.CampoA.CampoVersaoId]));

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        var avisoId = corpo.GetProperty("data").GetProperty("id").GetInt32();

        (await LerCamposAfetadosDoAvisoAsync(cenario.VersaoId, avisoId))
            .Should().Equal([cenario.CampoA.CampoVersaoId, cenario.CampoB.CampoVersaoId],
                because: "o que o POST aceitou tem de estar no banco, em ordem crescente de id");
        (await ContarVinculosAsync(avisoId)).Should().Be(2);
    }

    [Fact]
    public async Task Post_ComCamposRepetidos_GravaCadaCampoUmaVezSo()
    {
        var cenario = await SemearAsync();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/versoes/{cenario.VersaoId}/avisos",
            CorpoDeAviso("Aviso repetido", [cenario.CampoA.CampoVersaoId, cenario.CampoA.CampoVersaoId]));

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        var avisoId = corpo.GetProperty("data").GetProperty("id").GetInt32();

        (await ContarVinculosAsync(avisoId)).Should().Be(1);
    }

    [Fact]
    public async Task Put_ComCampos_SubstituiOConjuntoEOGetSeguinteDevolveOQueFoiGravado()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId]);

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{aviso.Id}",
            CorpoDeAviso("Aviso editado", [cenario.CampoB.CampoVersaoId]), aviso.Token);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        IdsDe(corpo.GetProperty("data"), "camposAfetadosIds").Should().Equal([cenario.CampoB.CampoVersaoId]);
        (await LerCamposAfetadosDoAvisoAsync(cenario.VersaoId, aviso.Id))
            .Should().Equal([cenario.CampoB.CampoVersaoId], because: "A saiu e B entrou — o conjunto inteiro é substituído");
    }

    [Fact]
    public async Task Put_SoDosCampos_MoveOTokenEUmSegundoPutComOTokenAntigoDa409()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId]);

        // Mesmo título e corpo: só o conjunto de campos muda, e o EF não emite UPDATE na linha do aviso.
        var (statusPrimeiro, corpoPrimeiro, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{aviso.Id}",
            CorpoDeAviso(aviso.Titulo, [cenario.CampoB.CampoVersaoId]), aviso.Token);
        statusPrimeiro.Should().Be(HttpStatusCode.OK, corpoPrimeiro.ToString());
        TokenDe(corpoPrimeiro).Should().NotBe(aviso.Token, because: "filho mudou, o token do pai tem de andar");

        var (statusSegundo, corpoSegundo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{aviso.Id}",
            CorpoDeAviso(aviso.Titulo, []), aviso.Token);

        statusSegundo.Should().Be(HttpStatusCode.Conflict, corpoSegundo.ToString());
        corpoSegundo.GetProperty("error").GetProperty("code").GetString().Should().Be("CONFLITO_CONCORRENCIA");
        corpoSegundo.GetRawText().Should().NotContain("uq_codex_").And.NotContain("at Codex.");
        (await LerCamposAfetadosDoAvisoAsync(cenario.VersaoId, aviso.Id))
            .Should().Equal([cenario.CampoB.CampoVersaoId], because: "o PUT recusado não pode ter tocado nos vínculos");
    }

    [Fact]
    public async Task Put_SemCampoVersaoIds_NaoMexeNosCamposGravados()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId, cenario.CampoB.CampoVersaoId]);

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{aviso.Id}",
            new { severidade = nameof(Severidade.Alerta), titulo = "Só o texto mudou", corpo = "Novo corpo." }, aviso.Token);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        (await LerCamposAfetadosDoAvisoAsync(cenario.VersaoId, aviso.Id)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Put_ComListaVazia_LimpaOsCampos()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId]);

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{aviso.Id}",
            CorpoDeAviso(aviso.Titulo, []), aviso.Token);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        (await LerCamposAfetadosDoAvisoAsync(cenario.VersaoId, aviso.Id)).Should().BeEmpty();
        (await ContarVinculosAsync(aviso.Id)).Should().Be(0);
    }

    /// <summary>
    /// A sequência inteira do formulário: o GET mostra os campos, o PUT devolve exatamente o que o
    /// GET trouxe. Remover e regravar a MESMA chave composta numa só transação já deu 500 em outra
    /// junção — por isso a repetição.
    /// </summary>
    [Fact]
    public async Task GetSeguidoDePut_ReenviandoOCorpoDoGet_NaoDa500EPreservaOsCampos()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId, cenario.CampoB.CampoVersaoId]);

        var tokenAtual = aviso.Token;
        for (var rodada = 1; rodada <= 2; rodada++)
        {
            var doGet = await LerAvisoDaVersaoAsync(cenario.VersaoId, aviso.Id);

            var (status, corpo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{aviso.Id}",
                CorpoDeAviso(doGet.GetProperty("titulo").GetString()!, IdsDe(doGet, "camposAfetadosIds")), tokenAtual);

            status.Should().Be(HttpStatusCode.OK, $"rodada {rodada}: {corpo}");
            tokenAtual = TokenDe(corpo);
        }

        (await LerCamposAfetadosDoAvisoAsync(cenario.VersaoId, aviso.Id))
            .Should().Equal([cenario.CampoA.CampoVersaoId, cenario.CampoB.CampoVersaoId]);
    }

    [Fact]
    public async Task Post_ComCampoDeOutraVersao_Devolve422ENadaEGravado()
    {
        var cenario = await SemearAsync();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/versoes/{cenario.VersaoId}/avisos",
            CorpoDeAviso("Aviso inválido", [cenario.CampoA.CampoVersaoId, cenario.CampoDeOutraVersao.CampoVersaoId]));

        status.Should().Be(HttpStatusCode.UnprocessableEntity, corpo.ToString());
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("CAMPO_AFETADO_INVALIDO");
        (await LerAvisosDaVersaoAsync(cenario.VersaoId)).Should().BeEmpty(because: "a criação recusada não pode deixar aviso pela metade");
    }

    [Fact]
    public async Task Put_ComCampoDeOutraVersao_Devolve422EOsVinculosAntigosPermanecem()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId]);

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{aviso.Id}",
            CorpoDeAviso(aviso.Titulo, [cenario.CampoDeOutraVersao.CampoVersaoId]), aviso.Token);

        status.Should().Be(HttpStatusCode.UnprocessableEntity, corpo.ToString());
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("CAMPO_AFETADO_INVALIDO");
        (await LerCamposAfetadosDoAvisoAsync(cenario.VersaoId, aviso.Id)).Should().Equal([cenario.CampoA.CampoVersaoId]);
    }

    [Fact]
    public async Task Post_ComCampoInexistente_Devolve422()
    {
        var cenario = await SemearAsync();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/versoes/{cenario.VersaoId}/avisos",
            CorpoDeAviso("Aviso fantasma", [int.MaxValue - 7]));

        status.Should().Be(HttpStatusCode.UnprocessableEntity, corpo.ToString());
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("CAMPO_AFETADO_INVALIDO");
    }

    [Fact]
    public async Task Put_ComCampoJaRemovido_Devolve422()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId]);
        await DesativarCampoVersaoAsync(cenario.CampoB.CampoVersaoId);

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{aviso.Id}",
            CorpoDeAviso(aviso.Titulo, [cenario.CampoB.CampoVersaoId]), aviso.Token);

        status.Should().Be(HttpStatusCode.UnprocessableEntity, corpo.ToString());
        corpo.GetProperty("error").GetProperty("code").GetString().Should().Be("CAMPO_AFETADO_INVALIDO");
    }

    [Fact]
    public async Task Post_ComMaisDe2000Campos_Devolve422()
    {
        var cenario = await SemearAsync();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/versoes/{cenario.VersaoId}/avisos",
            CorpoDeAviso("Aviso gigante", Enumerable.Range(1, 2001).ToArray()));

        status.Should().Be(HttpStatusCode.UnprocessableEntity, corpo.ToString());
        corpo.GetProperty("error").GetProperty("code").GetString().Should().NotBe("CAMPO_AFETADO_INVALIDO",
            because: "o teto é barrado pela validação do corpo, antes de consultar o banco — os ids não existem e cairiam no 422 de pertencimento por outro motivo");
    }

    [Fact]
    public async Task Get_CampoRemovidoDepoisDoVinculo_SomeDosCamposAfetadosEOPutDoGetNaoQuebra()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId, cenario.CampoB.CampoVersaoId]);
        await DesativarCampoVersaoAsync(cenario.CampoB.CampoVersaoId);

        var doGet = await LerAvisoDaVersaoAsync(cenario.VersaoId, aviso.Id);
        IdsDe(doGet, "camposAfetadosIds").Should().Equal([cenario.CampoA.CampoVersaoId]);

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{aviso.Id}",
            CorpoDeAviso(aviso.Titulo, IdsDe(doGet, "camposAfetadosIds")), aviso.Token);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        (await ContarVinculosAsync(aviso.Id)).Should().Be(1, because: "o PUT regrava só o que é visível; o vínculo do campo removido sai");
    }

    [Fact]
    public async Task Put_DeAvisoDeProvedor_ComListaVazia_Devolve200_EComCampo_Devolve422()
    {
        var cenario = await SemearAsync();
        var (statusCriar, corpoCriado, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{cenario.ProvedorId}/avisos",
            new { severidade = nameof(Severidade.Informativo), titulo = "Aviso de provedor", corpo = "Corpo." });
        statusCriar.Should().Be(HttpStatusCode.Created, corpoCriado.ToString());
        var avisoId = corpoCriado.GetProperty("data").GetProperty("id").GetInt32();
        var token = TokenDe(corpoCriado);

        // O formulário de aviso de provedor envia `campoVersaoIds: []` ao editar.
        var (statusVazio, corpoVazio, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{avisoId}",
            CorpoDeAviso("Aviso de provedor editado", []), token);
        statusVazio.Should().Be(HttpStatusCode.OK, corpoVazio.ToString());

        var (statusComCampo, corpoComCampo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{avisoId}",
            CorpoDeAviso("Aviso de provedor editado", [cenario.CampoA.CampoVersaoId]), TokenDe(corpoVazio));
        statusComCampo.Should().Be(HttpStatusCode.UnprocessableEntity, corpoComCampo.ToString());
        (await ContarVinculosAsync(avisoId)).Should().Be(0);
    }

    // ------------------------------------------------------------------ o que o leitor enxerga

    [Fact]
    public async Task Publico_P4_AvisoPublicadoTrazIdEscopoECamposAfetadosComSlugNomeECaminho()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoB.CampoVersaoId, cenario.CampoA.CampoVersaoId]);
        await PublicarAvisoAsync(aviso.Id);

        var (status, corpo, _) = await ObterPublicoAsync(cenario.RotaPaginaPublica);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        var avisoPublico = corpo.GetProperty("data").GetProperty("avisos").EnumerateArray().Single();
        avisoPublico.GetProperty("id").GetInt32().Should().Be(aviso.Id);
        avisoPublico.GetProperty("escopo").GetString().Should().Be("versao");
        avisoPublico.GetProperty("severidade").GetString().Should().Be("Informativo");

        var camposAfetados = avisoPublico.GetProperty("camposAfetados").EnumerateArray().ToArray();
        camposAfetados.Select(c => c.GetProperty("campoLayoutSlug").GetString())
            .Should().Equal([cenario.CampoA.Slug, cenario.CampoB.Slug], because: "mesma ordem canônica da tabela de campos");
        camposAfetados[0].GetProperty("nome").GetString().Should().Be(cenario.CampoA.Nome);
        camposAfetados[0].GetProperty("caminhoXml").GetString().Should().Be(cenario.CampoA.CaminhoXml);
    }

    [Fact]
    public async Task Publico_P4_AvisoDeVersaoSemCampos_TrazListaVaziaNuncaAusente()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, []);
        await PublicarAvisoAsync(aviso.Id);

        var (_, corpo, _) = await ObterPublicoAsync(cenario.RotaPaginaPublica);

        var camposAfetados = corpo.GetProperty("data").GetProperty("avisos").EnumerateArray().Single().GetProperty("camposAfetados");
        camposAfetados.ValueKind.Should().Be(JsonValueKind.Array);
        camposAfetados.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Publico_P3_AvisoDeProvedorTrazIdEEscopoProvedorComCamposAfetadosVazios()
    {
        var cenario = await SemearAsync();
        var (statusCriar, corpoCriado, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/provedores/{cenario.ProvedorId}/avisos",
            new { severidade = nameof(Severidade.Critico), titulo = "Aviso do provedor", corpo = "Vale para todas as versões." });
        statusCriar.Should().Be(HttpStatusCode.Created, corpoCriado.ToString());
        var avisoId = corpoCriado.GetProperty("data").GetProperty("id").GetInt32();
        await PublicarAvisoAsync(avisoId);

        var (status, corpo, _) = await ObterPublicoAsync(cenario.RotaProvedorPublica);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        var avisoPublico = corpo.GetProperty("data").GetProperty("avisos").EnumerateArray().Single();
        avisoPublico.GetProperty("id").GetInt32().Should().Be(avisoId);
        avisoPublico.GetProperty("escopo").GetString().Should().Be("provedor");
        avisoPublico.GetProperty("camposAfetados").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Publico_AvisoEmRascunho_NaoApareceNaPagina()
    {
        var cenario = await SemearAsync();
        await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId]);

        var (_, corpo, _) = await ObterPublicoAsync(cenario.RotaPaginaPublica);

        corpo.GetProperty("data").GetProperty("avisos").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Publico_CampoRemovido_NaoVazaNosCamposAfetados()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId, cenario.CampoB.CampoVersaoId]);
        await PublicarAvisoAsync(aviso.Id);
        await DesativarCampoVersaoAsync(cenario.CampoB.CampoVersaoId);

        var (_, corpo, _) = await ObterPublicoAsync(cenario.RotaPaginaPublica);

        CamposAfetadosPublicos(corpo).Should().Equal([cenario.CampoA.Slug],
            because: "campo que a tabela da versão não mostra não pode ser linkado pelo aviso");
    }

    [Fact]
    public async Task Publico_CampoCanonicoRemovido_NaoVazaNosCamposAfetados()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId, cenario.CampoB.CampoVersaoId]);
        await PublicarAvisoAsync(aviso.Id);
        await DesativarCampoLayoutAsync(cenario.CampoA.CampoLayoutId);

        var (_, corpo, _) = await ObterPublicoAsync(cenario.RotaPaginaPublica);

        CamposAfetadosPublicos(corpo).Should().Equal([cenario.CampoB.Slug]);
    }

    /// <summary>
    /// Defesa em profundidade: mesmo que a junção aponte, por escrita direta ou deriva de dados,
    /// para um campo de OUTRA versão, a página desta versão não o lista nem expõe o nome dele.
    /// </summary>
    [Fact]
    public async Task Publico_VinculoParaCampoDeOutraVersao_NaoVaza()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId]);
        await PublicarAvisoAsync(aviso.Id);

        await using (var contexto = _fixture.CriarContexto())
        {
            contexto.AvisosCampos.Add(AvisoCampo.Criar(aviso.Id, cenario.CampoDeOutraVersao.CampoVersaoId));
            await contexto.SaveChangesAsync();
        }

        var (_, corpo, resposta) = await ObterPublicoAsync(cenario.RotaPaginaPublica);

        CamposAfetadosPublicos(corpo).Should().Equal([cenario.CampoA.Slug]);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(cenario.CampoDeOutraVersao.Slug);
    }

    // ------------------------------------------------------------------ cache da página pública

    [Fact]
    public async Task Etag_EditarOsCamposDoAviso_InvalidaAETagDaPaginaPublica()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId]);
        await PublicarAvisoAsync(aviso.Id);

        var etagAntes = await LerETagAsync(cenario.RotaPaginaPublica);
        (await ObterPublicoAsync(cenario.RotaPaginaPublica, etagAntes)).Status.Should().Be(HttpStatusCode.NotModified,
            because: "sem escrita nenhuma a mesma ETag ainda vale — controle positivo do caso");

        var tokenAtual = (await LerAvisoDaVersaoAsync(cenario.VersaoId, aviso.Id)).GetProperty("versaoConcorrencia").GetUInt32();
        var (statusPut, corpoPut, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{aviso.Id}",
            CorpoDeAviso(aviso.Titulo, [cenario.CampoB.CampoVersaoId]), tokenAtual);
        statusPut.Should().Be(HttpStatusCode.OK, corpoPut.ToString());

        var (status, corpo, _) = await ObterPublicoAsync(cenario.RotaPaginaPublica, etagAntes);

        status.Should().Be(HttpStatusCode.OK, because: "a ETag antiga serviria o aviso apontando para o campo errado");
        CamposAfetadosPublicos(corpo).Should().Equal([cenario.CampoB.Slug]);
    }

    [Fact]
    public async Task Etag_PublicarOAviso_InvalidaAETagDaPaginaPublica()
    {
        var cenario = await SemearAsync();
        var aviso = await CriarAvisoAsync(cenario, [cenario.CampoA.CampoVersaoId]);

        var etagAntes = await LerETagAsync(cenario.RotaPaginaPublica);
        await PublicarAvisoAsync(aviso.Id);

        var (status, corpo, _) = await ObterPublicoAsync(cenario.RotaPaginaPublica, etagAntes);

        status.Should().Be(HttpStatusCode.OK);
        corpo.GetProperty("data").GetProperty("avisos").EnumerateArray().Should().HaveCount(1);
    }

    // ------------------------------------------------------------------ apoio

    private sealed record AvisoCriado(int Id, string Titulo, uint Token);

    private static object CorpoDeAviso(string titulo, IReadOnlyCollection<int> campoVersaoIds) => new
    {
        severidade = nameof(Severidade.Informativo),
        titulo,
        corpo = "Corpo do aviso.",
        campoVersaoIds,
    };

    private static uint TokenDe(JsonElement corpo) => corpo.GetProperty("data").GetProperty("versaoConcorrencia").GetUInt32();

    private static int[] IdsDe(JsonElement objeto, string propriedade) =>
        objeto.GetProperty(propriedade).EnumerateArray().Select(e => e.GetInt32()).ToArray();

    private static string[] CamposAfetadosPublicos(JsonElement corpo) =>
        corpo.GetProperty("data").GetProperty("avisos").EnumerateArray().Single()
            .GetProperty("camposAfetados").EnumerateArray()
            .Select(c => c.GetProperty("campoLayoutSlug").GetString()!)
            .ToArray();

    private async Task<AvisoCriado> CriarAvisoAsync(Cenario cenario, IReadOnlyCollection<int> campoVersaoIds)
    {
        var titulo = $"Aviso {Guid.NewGuid():N}"[..20];

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/api/v1/interno/versoes/{cenario.VersaoId}/avisos",
            CorpoDeAviso(titulo, campoVersaoIds));

        status.Should().Be(HttpStatusCode.Created, corpo.ToString());
        return new AvisoCriado(corpo.GetProperty("data").GetProperty("id").GetInt32(), titulo, TokenDe(corpo));
    }

    private async Task PublicarAvisoAsync(int avisoId)
    {
        var token = await LerTokenDoAvisoAsync(avisoId);

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Put, $"/api/v1/interno/avisos/{avisoId}/publicacao",
            new { publicado = true }, token);

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
    }

    private async Task<uint> LerTokenDoAvisoAsync(int avisoId)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.Avisos.AsNoTracking().Where(a => a.Id == avisoId).Select(a => a.Versao).SingleAsync();
    }

    private async Task<JsonElement[]> LerAvisosDaVersaoAsync(int versaoId)
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, $"/api/v1/interno/versoes/{versaoId}");

        status.Should().Be(HttpStatusCode.OK, corpo.ToString());
        return corpo.GetProperty("data").GetProperty("avisos").EnumerateArray().ToArray();
    }

    private async Task<JsonElement> LerAvisoDaVersaoAsync(int versaoId, int avisoId) =>
        (await LerAvisosDaVersaoAsync(versaoId)).Single(a => a.GetProperty("id").GetInt32() == avisoId);

    private async Task<int[]> LerCamposAfetadosDoAvisoAsync(int versaoId, int avisoId) =>
        IdsDe(await LerAvisoDaVersaoAsync(versaoId, avisoId), "camposAfetadosIds");

    private async Task<int> ContarVinculosAsync(int avisoId)
    {
        await using var contexto = _fixture.CriarContexto();
        return await contexto.AvisosCampos.AsNoTracking().CountAsync(ac => ac.AvisoId == avisoId);
    }

    private async Task DesativarCampoVersaoAsync(int campoVersaoId)
    {
        await using var contexto = _fixture.CriarContexto();
        var campo = await contexto.CamposVersao.SingleAsync(c => c.Id == campoVersaoId);
        campo.Desativar();
        await contexto.SaveChangesAsync();
    }

    private async Task DesativarCampoLayoutAsync(int campoLayoutId)
    {
        await using var contexto = _fixture.CriarContexto();
        var campo = await contexto.CamposLayout.SingleAsync(c => c.Id == campoLayoutId);
        campo.Desativar();
        await contexto.SaveChangesAsync();
    }

    private async Task<string> LerETagAsync(string rotaPublica)
    {
        var (status, _, resposta) = await ObterPublicoAsync(rotaPublica);

        status.Should().Be(HttpStatusCode.OK);
        var etag = resposta.Headers.ETag?.ToString();
        etag.Should().NotBeNullOrEmpty();

        return etag!;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> ObterPublicoAsync(
        string rota, string? ifNoneMatch = null)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);

        // Verbatim: o valor vai exatamente como o cliente o leu, sem o HttpClient reformatar.
        if (ifNoneMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, HttpResponseMessage Resposta)> EnviarAsync(
        HttpMethod metodo, string rota, object? corpoRequisicao = null, uint? ifMatch = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, Roles);

        if (corpoRequisicao is not null)
            requisicao.Content = JsonContent.Create(corpoRequisicao);

        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());

        var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();

        return (resposta.StatusCode, corpo, resposta);
    }

    /// <summary>
    /// Provedor e versão publicados, com dois campos na versão e um terceiro numa segunda versão
    /// do mesmo provedor. Semeado direto no banco: o que se testa é o aviso, não o cadastro.
    /// </summary>
    private async Task<Cenario> SemearAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdemAtual = await contexto.VersoesLayout.AnyAsync() ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout avc {sufixo}", maiorOrdemAtual + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor avc {sufixo}", $"AVC-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versão avc {sufixo}");
        versao.Publicar();
        var outraVersao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Outra versão avc {sufixo}");
        outraVersao.Publicar();
        contexto.VersoesProvedor.AddRange(versao, outraVersao);
        await contexto.SaveChangesAsync();

        var campoA = await SemearCampoAsync(contexto, versao.Id, versaoLayout.Id, $"Alfa {sufixo}", ordem: 1);
        var campoB = await SemearCampoAsync(contexto, versao.Id, versaoLayout.Id, $"Beta {sufixo}", ordem: 2);
        var campoDeOutraVersao = await SemearCampoAsync(contexto, outraVersao.Id, versaoLayout.Id, $"Gama {sufixo}", ordem: 3);

        return new Cenario(provedor.Id, provedor.Slug, versao.Id, versao.Slug, campoA, campoB, campoDeOutraVersao);
    }

    private static async Task<CampoSemeado> SemearCampoAsync(
        CodexDbContext contexto, int versaoProvedorId, int versaoLayoutId, string nome, int ordem)
    {
        var caminhoXml = $"//Servico/{nome.Replace(' ', '_')}";
        var campoLayout = CampoLayout.Criar(versaoLayoutId, nome, caminhoXml, "significado", ordem);
        contexto.CamposLayout.Add(campoLayout);
        await contexto.SaveChangesAsync();

        var campoVersao = CampoVersao.Criar(versaoProvedorId, campoLayout.Id, "texto", EstadoPreenchimento.Obrigatorio);
        contexto.CamposVersao.Add(campoVersao);
        await contexto.SaveChangesAsync();

        return new CampoSemeado(campoVersao.Id, campoLayout.Id, campoLayout.Slug, nome, caminhoXml);
    }
}
