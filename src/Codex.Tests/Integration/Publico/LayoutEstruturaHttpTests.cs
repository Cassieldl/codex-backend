using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// Estrutura do documento de uma versão do layout, gravada pelo PUT interno e lida pela rota
/// pública, contra um Postgres real e pelo JSON literal da resposta. Nenhuma versão do cenário vira
/// Vigente: o slot é global na <c>Collection</c>.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class LayoutEstruturaHttpTests : IAsyncLifetime
{
    private const string RoleAtualizar = "versoeslayout-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private Cenario _cenario = null!;

    public LayoutEstruturaHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _cenario = await SemearAsync();
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        await LimparAsync();
    }

    [Fact]
    public async Task EstruturaGravada_Responde200_ComOsDoisFormatos_ESlugSoDoCampoPublicado()
    {
        (await GravarAsync(_cenario.PublicadaId, _cenario.PublicadaVersao, ArvoresComCampos())).Should().Be(HttpStatusCode.OK);

        using var resposta = await _cliente.GetAsync($"/api/v1/layout/versoes/{_cenario.PublicadaSlug}/estrutura");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Headers.ETag.Should().NotBeNull();
        resposta.Headers.CacheControl!.ToString().Should().Be("public, max-age=60, stale-while-revalidate=300");

        var dados = (await LerJsonAsync(resposta)).GetProperty("data");
        dados.EnumerateObject().Select(p => p.Name).Should().Equal("versao", "xml", "json");
        dados.GetProperty("versao").GetProperty("slug").GetString().Should().Be(_cenario.PublicadaSlug);

        var xml = dados.GetProperty("xml");
        xml.GetProperty("nome").GetString().Should().Be("Envio");
        var lista = xml.GetProperty("filhos")[0];
        var item = lista.GetProperty("filhos")[0];
        item.GetProperty("formato").GetString().Should().Be("soXml");
        item.GetProperty("repete").GetBoolean().Should().BeTrue();

        var publicado = item.GetProperty("filhos")[0];
        publicado.EnumerateObject().Select(p => p.Name).Should().Equal(
            "nome", "caminho", "ehGrupo", "repete", "escolha", "valorModelo", "campoSlug", "formato", "filhos");
        publicado.GetProperty("campoSlug").GetString().Should().Be(_cenario.CampoPublicadoSlug);
        publicado.GetProperty("valorModelo").GetString().Should().Be("0");

        var rascunho = item.GetProperty("filhos")[1];
        rascunho.GetProperty("campoSlug").ValueKind.Should().Be(JsonValueKind.Null,
            because: "campo despublicado não pode vazar o slug pela estrutura");

        var json = dados.GetProperty("json");
        json.GetProperty("nome").GetString().Should().Be("Documento");
        json.GetProperty("filhos")[0].GetProperty("filhos")[0].GetProperty("campoSlug").GetString()
            .Should().Be(_cenario.CampoPublicadoSlug);
    }

    [Fact]
    public async Task MesmaEstrutura_ComIfNoneMatchDaRespostaAnterior_Responde304()
    {
        (await GravarAsync(_cenario.PublicadaId, _cenario.PublicadaVersao, ArvoresComCampos())).Should().Be(HttpStatusCode.OK);
        var rota = $"/api/v1/layout/versoes/{_cenario.PublicadaSlug}/estrutura";

        using var primeira = await _cliente.GetAsync(rota);
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.TryAddWithoutValidation("If-None-Match", primeira.Headers.ETag!.ToString());
        using var segunda = await _cliente.SendAsync(requisicao);

        segunda.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Theory]
    [InlineData("inexistente")]
    [InlineData("despublicada")]
    [InlineData("sem-estrutura")]
    public async Task VersaoInexistenteDespublicadaOuSemEstrutura_Responde404(string caso)
    {
        await GravarAsync(_cenario.RascunhoId, _cenario.RascunhoVersao, ArvoresComCampos());
        var slug = caso switch
        {
            "inexistente" => $"nunca-existiu-{_sufixo}",
            "despublicada" => _cenario.RascunhoSlug,
            _ => _cenario.SemEstruturaSlug,
        };

        using var resposta = await _cliente.GetAsync($"/api/v1/layout/versoes/{slug}/estrutura");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LerJsonAsync(resposta)).GetProperty("error").GetProperty("code").GetString().Should().Be("NAO_ENCONTRADO");
    }

    [Fact]
    public async Task Gravacao_ComIfMatchVencido_Responde409()
    {
        (await GravarAsync(_cenario.PublicadaId, _cenario.PublicadaVersao, ArvoresComCampos())).Should().Be(HttpStatusCode.OK);

        (await GravarAsync(_cenario.PublicadaId, _cenario.PublicadaVersao, ArvoresComCampos())).Should().Be(HttpStatusCode.Conflict,
            because: "o If-Match da primeira gravação já não é a versão da linha");
    }

    [Fact]
    public async Task Gravacao_ComArvoreGrandeDemais_Responde422()
    {
        var folhas = Enumerable.Range(0, 5001)
            .Select(indice => new { nome = "F", caminho = $"Envio/F{indice}", formato = "ambos", filhos = Array.Empty<object>() })
            .ToArray();
        var raiz = new { nome = "Envio", caminho = "Envio", ehGrupo = true, formato = "ambos", filhos = folhas };

        var status = await GravarAsync(_cenario.PublicadaId, _cenario.PublicadaVersao, new { xml = raiz, json = raiz });

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CampoSlug_SoParaCampoEmVigorNaVersaoDaEstrutura()
    {
        var (antiga, nova) = await SemearDuasVersoesAsync();
        (await GravarAsync(antiga.Id, antiga.Versao, ArvoreDeTresCampos())).Should().Be(HttpStatusCode.OK);
        (await GravarAsync(nova.Id, nova.Versao, ArvoreDeTresCampos())).Should().Be(HttpStatusCode.OK);

        var naNova = await SlugsPorNomeAsync(nova.Slug);
        naNova["Mantido"].Should().Be(_cenarioDuasVersoes.MantidoSlug);
        naNova["Saiu"].Should().BeNull(because: "descontinuado na versão nova já não é campo do documento dela");
        naNova["Novo"].Should().Be(_cenarioDuasVersoes.NovoSlug);

        var naAntiga = await SlugsPorNomeAsync(antiga.Slug);
        naAntiga["Mantido"].Should().Be(_cenarioDuasVersoes.MantidoSlug);
        naAntiga["Saiu"].Should().Be(_cenarioDuasVersoes.SaiuSlug);
        naAntiga["Novo"].Should().BeNull(because: "campo que nasce na versão nova não existe na antiga");
    }

    // ---------------------------------------------------------------------------------- apoio

    private CenarioDuasVersoes _cenarioDuasVersoes = null!;

    private sealed record VersaoSemeada(int Id, uint Versao, string Slug);

    private sealed record CenarioDuasVersoes(string MantidoSlug, string SaiuSlug, string NovoSlug, string Prefixo);

    private async Task<(VersaoSemeada Antiga, VersaoSemeada Nova)> SemearDuasVersoesAsync()
    {
        await using var contexto = _fixture.CriarContexto();
        var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => (int?)v.Ordem) ?? 0;

        var antiga = VersaoLayout.Criar($"Estrutura Antiga {_sufixo}", maiorOrdem + 1);
        var nova = VersaoLayout.Criar($"Estrutura Nova {_sufixo}", maiorOrdem + 2);
        antiga.Publicar();
        nova.Publicar();
        contexto.VersoesLayout.AddRange(antiga, nova);
        await contexto.SaveChangesAsync();

        var prefixo = $"//Doc{_sufixo}";
        var mantido = CampoLayout.Criar(antiga.Id, "Mantido", $"{prefixo}/Mantido", "Segue nas duas.", ordem: 1);
        var saiu = CampoLayout.Criar(antiga.Id, "Saiu", $"{prefixo}/Saiu", "Sai na nova.", ordem: 2);
        var novo = CampoLayout.Criar(nova.Id, "Novo", $"{prefixo}/Novo", "Nasce na nova.", ordem: 3);
        foreach (var campo in new[] { mantido, saiu, novo })
            campo.Publicar();
        contexto.CamposLayout.AddRange(mantido, saiu, novo);
        await contexto.SaveChangesAsync();

        var saiuRastreado = await contexto.CamposLayout.Include(c => c.VersaoLayoutOrigem).FirstAsync(c => c.Id == saiu.Id);
        saiuRastreado.Descontinuar(nova);
        await contexto.SaveChangesAsync();

        _cenarioDuasVersoes = new CenarioDuasVersoes(mantido.Slug, saiu.Slug, novo.Slug, prefixo);
        return (new VersaoSemeada(antiga.Id, antiga.Versao, antiga.Slug), new VersaoSemeada(nova.Id, nova.Versao, nova.Slug));
    }

    private object ArvoreDeTresCampos()
    {
        var prefixo = _cenarioDuasVersoes.Prefixo;
        object Folha(string nome) => new
        {
            nome,
            caminho = $"Envio/{nome}",
            formato = "ambos",
            valorModelo = "0",
            caminhoCampo = $"{prefixo}/{nome}",
            filhos = Array.Empty<object>(),
        };

        var raiz = new { nome = "Envio", caminho = "Envio", ehGrupo = true, formato = "ambos", filhos = new[] { Folha("Mantido"), Folha("Saiu"), Folha("Novo") } };
        return new { xml = raiz, json = raiz };
    }

    private async Task<Dictionary<string, string?>> SlugsPorNomeAsync(string versaoSlug)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/layout/versoes/{versaoSlug}/estrutura");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await LerJsonAsync(resposta)).GetProperty("data").GetProperty("xml").GetProperty("filhos").EnumerateArray()
            .ToDictionary(no => no.GetProperty("nome").GetString()!, no => no.GetProperty("campoSlug").GetString());
    }

    private object ArvoresComCampos()
    {
        object Folha(string nome, string caminho, string caminhoCampo) => new
        {
            nome,
            caminho,
            ehGrupo = false,
            repete = false,
            escolha = false,
            valorModelo = "0",
            formato = "ambos",
            caminhoCampo,
            filhos = Array.Empty<object>(),
        };

        object Grupo(string nome, string caminho, bool repete, string formato, params object[] filhos) => new
        {
            nome,
            caminho,
            ehGrupo = true,
            repete,
            escolha = false,
            valorModelo = (string?)null,
            formato,
            caminhoCampo = (string?)null,
            filhos,
        };

        var publicado = _cenario.CampoPublicadoCaminho;
        var rascunho = _cenario.CampoRascunhoCaminho;

        return new
        {
            xml = Grupo("Envio", "Envio", false, "ambos",
                Grupo("ListaItens", "Envio/ListaItens", false, "ambos",
                    Grupo("Item", "Envio/ListaItens/Item", true, "soXml",
                        Folha("ItemSeq", "Envio/ListaItens/Item/ItemSeq", publicado),
                        Folha("ItemCod", "Envio/ListaItens/Item/ItemCod", rascunho)))),
            json = Grupo("Documento", "Documento", false, "ambos",
                Grupo("ListaItens", "Documento/ListaItens", true, "ambos",
                    Folha("ItemSeq", "Documento/ListaItens/ItemSeq", publicado),
                    Folha("ItemCod", "Documento/ListaItens/ItemCod", rascunho))),
        };
    }

    private async Task<HttpStatusCode> GravarAsync(int versaoLayoutId, uint versao, object corpo)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/layout/versoes/{versaoLayoutId}/estrutura")
        {
            Content = new StringContent(JsonSerializer.Serialize(corpo, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
        };
        requisicao.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RoleAtualizar);
        requisicao.Headers.TryAddWithoutValidation("If-Match", versao.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var resposta = await _cliente.SendAsync(requisicao);
        return resposta.StatusCode;
    }

    private static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta) =>
        JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();

    private sealed record Cenario
    {
        public required int PublicadaId { get; init; }
        public required uint PublicadaVersao { get; init; }
        public required string PublicadaSlug { get; init; }
        public required int RascunhoId { get; init; }
        public required uint RascunhoVersao { get; init; }
        public required string RascunhoSlug { get; init; }
        public required string SemEstruturaSlug { get; init; }
        public required string CampoPublicadoSlug { get; init; }
        public required string CampoPublicadoCaminho { get; init; }
        public required string CampoRascunhoCaminho { get; init; }
    }

    private async Task<Cenario> SemearAsync()
    {
        await using var contexto = _fixture.CriarContexto();
        var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => (int?)v.Ordem) ?? 0;

        var publicada = VersaoLayout.Criar($"Estrutura Publicada {_sufixo}", maiorOrdem + 1);
        var rascunho = VersaoLayout.Criar($"Estrutura Rascunho {_sufixo}", maiorOrdem + 2);
        var semEstrutura = VersaoLayout.Criar($"Estrutura Vazia {_sufixo}", maiorOrdem + 3);
        publicada.Publicar();
        semEstrutura.Publicar();
        contexto.VersoesLayout.AddRange(publicada, rascunho, semEstrutura);
        await contexto.SaveChangesAsync();

        var campoPublicado = CampoLayout.Criar(publicada.Id, "ItemSeq", $"//ListaItens/Item/ItemSeq{_sufixo}", "Sequencial do item.", ordem: 1);
        var campoRascunho = CampoLayout.Criar(publicada.Id, "ItemCod", $"//ListaItens/Item/ItemCod{_sufixo}", "Código do item.", ordem: 2);
        campoPublicado.Publicar();
        contexto.CamposLayout.AddRange(campoPublicado, campoRascunho);
        await contexto.SaveChangesAsync();

        return new Cenario
        {
            PublicadaId = publicada.Id,
            PublicadaVersao = publicada.Versao,
            PublicadaSlug = publicada.Slug,
            RascunhoId = rascunho.Id,
            RascunhoVersao = rascunho.Versao,
            RascunhoSlug = rascunho.Slug,
            SemEstruturaSlug = semEstrutura.Slug,
            CampoPublicadoSlug = campoPublicado.Slug,
            CampoPublicadoCaminho = campoPublicado.CaminhoXml,
            CampoRascunhoCaminho = campoRascunho.CaminhoXml,
        };
    }

    private async Task LimparAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var versoes = await contexto.VersoesLayout.Where(v => v.Rotulo.Contains(_sufixo)).ToListAsync();
        var ids = versoes.Select(v => v.Id).ToList();

        contexto.CamposLayout.RemoveRange(await contexto.CamposLayout.Where(c => ids.Contains(c.VersaoLayoutId)).ToListAsync());
        contexto.VersoesLayout.RemoveRange(versoes);
        await contexto.SaveChangesAsync();
    }
}
