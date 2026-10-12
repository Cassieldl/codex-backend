using System.Net;
using System.Text.Json;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// P7 (índice de artigos) e P8 (artigo por slug) exercitados por **HTTP real** contra Postgres
/// real.
///
/// <para>
/// Por que existe: BACK-B entregou os endpoints com testes de service e de validator, e nenhum
/// que atravesse o pipeline. Objeto C# correto não é contrato HTTP correto — nome de propriedade,
/// caixa, enum virando número, `DateOnly` vazando hora e status de erro só aparecem no wire. É a
/// mesma lacuna que a 001 pagou caro para descobrir.
/// </para>
///
/// <para>Vocabulário isolado por sufixo aleatório: o corpus de busca é compartilhado entre classes
/// de teste, e termo repetido faz um teste casar o dado de outro.</para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ContratoArtigoHttpTests : IAsyncLifetime
{
    /// <summary>A união de tipos que o cliente declara — ASCII, decisão M-5.</summary>
    private static readonly string[] TiposDeContrato = ["Conceito", "Procedimento", "Mudanca"];

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ContratoArtigoHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ P7

    [Fact]
    public async Task P7_ListaSoTrazArtigoPublicado_ENuncaORascunho()
    {
        var cenario = await SemearAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/artigos?q={cenario.Sufixo}&tamanhoPagina=100");

        status.Should().Be(HttpStatusCode.OK);
        var slugs = corpo.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("slug").GetString())
            .ToList();

        slugs.Should().Contain(cenario.PublicadoSlug);
        slugs.Should().NotContain(cenario.RascunhoSlug,
            because: "superfície pública nunca revela conteúdo não publicado");
    }

    [Fact]
    public async Task P7_ItemDeLista_TemExatamenteAsPropriedadesDoContrato()
    {
        var cenario = await SemearAsync();

        var (_, corpo) = await ObterAsync($"/api/v1/artigos?q={cenario.Sufixo}&tamanhoPagina=100");
        var item = PrimeiroItem(corpo, cenario.PublicadoSlug);

        NomesDe(item).Should().BeEquivalentTo(["slug", "tipo", "titulo", "resumo", "verificadoEm"]);
        item.TryGetProperty("corpo", out _).Should().BeFalse(
            because: "item de lista é compacto — carregar o corpo de texto em toda linha da página é desperdício, e o padrão do projeto (ErroAdminListaItemDto) já é esse");
    }

    [Fact]
    public async Task P7_Tipo_ESerializadoComoStringAsciiDaUniaoDoCliente()
    {
        var cenario = await SemearAsync();

        var (_, corpo) = await ObterAsync($"/api/v1/artigos?q={cenario.Sufixo}&tamanhoPagina=100");
        var tipo = PrimeiroItem(corpo, cenario.PublicadoSlug).GetProperty("tipo");

        tipo.ValueKind.Should().Be(JsonValueKind.String,
            because: "enum serializado como número quebra a união de literais do TypeScript");
        tipo.GetString().Should().BeOneOf(TiposDeContrato);
    }

    [Fact]
    public async Task P7_FiltroPorTipoSemNenhumArtigo_Retorna200ComListaVazia_NuncaErro()
    {
        await SemearAsync();

        var (status, corpo) = await ObterAsync("/api/v1/artigos?tipo=Mudanca&q=inexistente-propositalmente");

        status.Should().Be(HttpStatusCode.OK,
            because: "tipo sem acervo é estado legítimo da tela 7, não 404 nem 422");
        corpo.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task P7_VerificadoEm_EDataPura_SemHoraNemOffset()
    {
        var cenario = await SemearAsync();

        var (_, corpo) = await ObterAsync($"/api/v1/artigos?q={cenario.Sufixo}&tamanhoPagina=100");
        var verificadoEm = PrimeiroItem(corpo, cenario.PublicadoSlug).GetProperty("verificadoEm");

        verificadoEm.ValueKind.Should().Be(JsonValueKind.String);
        verificadoEm.GetString().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$",
            because: "o domínio guarda DateTimeOffset; deixar hora e offset vazarem para uma tela que só mostra data é bug de contrato");
    }

    // ------------------------------------------------------------------ P8

    [Fact]
    public async Task P8_ArtigoPublicado_Retorna200ComEnvelopeData()
    {
        var cenario = await SemearAsync();

        var (status, corpo) = await ObterAsync($"/api/v1/artigos/{cenario.PublicadoSlug}");

        status.Should().Be(HttpStatusCode.OK);
        corpo.TryGetProperty("data", out var dados).Should().BeTrue(because: "item único viaja em ApiResponse<T>");
        dados.GetProperty("slug").GetString().Should().Be(cenario.PublicadoSlug);
        dados.GetProperty("corpo").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task P8_SlugInexistente_Retorna404()
    {
        var (status, _) = await ObterAsync($"/api/v1/artigos/nunca-existiu-{Guid.NewGuid():N}");

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task P8_ArtigoDespublicado_Retorna410_DistintoDo404()
    {
        var cenario = await SemearAsync();

        var (status, _) = await ObterAsync($"/api/v1/artigos/{cenario.RascunhoSlug}");

        status.Should().Be(HttpStatusCode.Gone,
            because: "410 significa 'existiu e saiu do ar' — nunca ter existido é 404, e o cliente trata os dois com mensagens diferentes");
    }

    [Fact]
    public async Task P8_ReferenciasCruzadas_TrazemORotuloQueNomeiaANatureza()
    {
        var cenario = await SemearAsync();

        var (_, corpo) = await ObterAsync($"/api/v1/artigos/{cenario.PublicadoSlug}");
        var dados = corpo.GetProperty("data");

        var campos = dados.GetProperty("camposLayoutRelacionados").EnumerateArray().ToList();
        var erros = dados.GetProperty("errosRelacionados").EnumerateArray().ToList();

        campos.Should().NotBeEmpty(because: "o cenário vincula um campo de layout ao artigo");
        erros.Should().NotBeEmpty(because: "o cenário vincula um erro publicado ao artigo");

        campos.Should().OnlyContain(campo => campo.GetProperty("rotulo").GetString() == "CAMPO DO LAYOUT");
        erros.Should().OnlyContain(erro => erro.GetProperty("rotulo").GetString() == "REJEIÇÃO",
            because: "§7.5 do escopo exige rótulo nomeando a natureza da referência, e o acento tem de sobreviver ao wire");
    }

    [Fact]
    public async Task P8_CampoDeLayout_TrazSignificadoELinkParaAPaginaDoCampo()
    {
        var cenario = await SemearAsync();

        var (_, corpo) = await ObterAsync($"/api/v1/artigos/{cenario.PublicadoSlug}");

        var campo = corpo.GetProperty("data").GetProperty("camposLayoutRelacionados").EnumerateArray()
            .Single(item => item.GetProperty("slug").GetString() == cenario.CampoPublicadoSlug);

        campo.GetProperty("significado").GetString().Should().Be("Campo referenciado por artigo");
        campo.GetProperty("url").GetString().Should().Be($"/layout/campos/{cenario.CampoPublicadoSlug}",
            because: "a página pública do campo existe; sem o link, a referência vira texto morto no artigo");
    }

    [Fact]
    public async Task P8_CampoDeLayoutNaoPublicado_NaoApareceNasReferenciasCruzadas()
    {
        var cenario = await SemearAsync();

        var (_, corpo) = await ObterAsync($"/api/v1/artigos/{cenario.PublicadoSlug}");

        var slugs = corpo.GetProperty("data").GetProperty("camposLayoutRelacionados").EnumerateArray()
            .Select(campo => campo.GetProperty("slug").GetString())
            .ToList();

        slugs.Should().Contain(cenario.CampoPublicadoSlug);
        slugs.Should().NotContain(cenario.CampoRascunhoSlug,
            because: "decisão B-3 vale para as duas famílias de referência, não só para erro");
    }

    [Fact]
    public async Task P8_ErroDespublicado_NaoApareceNasReferenciasCruzadas()
    {
        var cenario = await SemearAsync();

        var (_, corpo) = await ObterAsync($"/api/v1/artigos/{cenario.PublicadoSlug}");

        var codigos = corpo.GetProperty("data").GetProperty("errosRelacionados").EnumerateArray()
            .Select(erro => erro.GetProperty("codigo").GetString())
            .ToList();

        codigos.Should().Contain(cenario.ErroPublicadoCodigo);
        codigos.Should().NotContain(cenario.ErroDespublicadoCodigo,
            because: "decisão B-3 — referência cruzada pública só mostra item publicado");
    }

    /// <summary>
    /// Depende de o slug reservado NÃO estar semeado. Uma classe da suíte o semeia de propósito
    /// (<c>ArtigoReservadoCodigoErroHttpTests</c>, que exercita a recusa de I27/I28) e o remove no
    /// seu <c>DisposeAsync</c> justamente por causa deste teste. Se este caso falhar por ter
    /// virado `200`, o suspeito é a limpeza de lá — não a implementação de P8.
    /// </summary>
    [Fact]
    public async Task P8_ArtigoReservadoAusente_Retorna404_NuncaGone()
    {
        var (status, _) = await ObterAsync($"/api/v1/artigos/{SlugsReservadosArtigo.ComoFunciona}");

        status.Should().Be(HttpStatusCode.NotFound,
            because: "nunca ter existido é 404; 410 significa 'existiu e saiu do ar', e a tela 16 distingue os dois para oferecer a criação pré-preenchida");
    }

    // ------------------------------------------------------------------ apoio

    private static IEnumerable<string> NomesDe(JsonElement objeto) =>
        objeto.EnumerateObject().Select(propriedade => propriedade.Name);

    private static JsonElement PrimeiroItem(JsonElement corpo, string slug)
    {
        var item = corpo.GetProperty("items").EnumerateArray()
            .FirstOrDefault(i => i.GetProperty("slug").GetString() == slug);

        item.ValueKind.Should().Be(JsonValueKind.Object, because: $"o cenário publica o artigo '{slug}'");
        return item;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ObterAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
        return (resposta.StatusCode, corpo);
    }

    private sealed record Cenario(
        string Sufixo,
        string PublicadoSlug,
        string RascunhoSlug,
        string ErroPublicadoCodigo,
        string ErroDespublicadoCodigo,
        string CampoPublicadoSlug,
        string CampoRascunhoSlug);

    private async Task<Cenario> SemearAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var layout = VersaoLayout.Criar($"Layout artigo {sufixo}", maiorOrdem + 1);
        layout.Publicar();
        contexto.VersoesLayout.Add(layout);
        await contexto.SaveChangesAsync();

        // A referência cruzada exige o campo E a versão de layout publicados (decisão B-3). O
        // campo em rascunho abaixo é o contraponto que prova a regra pelo lado negativo.
        var campo = CampoLayout.Criar(
            layout.Id, $"campo artigo {sufixo}", $"//raiz/Artigo{sufixo}", "Campo referenciado por artigo", ordem: 1);
        campo.Publicar();

        var campoRascunho = CampoLayout.Criar(
            layout.Id, $"campo rascunho {sufixo}", $"//raiz/Rascunho{sufixo}", "Campo em rascunho", ordem: 2);

        contexto.CamposLayout.AddRange(campo, campoRascunho);

        var erroPublicado = Erro.Criar($"ARTP-{sufixo}", OrigemErro.InvoiCy, $"Erro publicado artigo {sufixo}");
        erroPublicado.SubstituirVariantes([$"variante artigo pub {sufixo}"]);
        erroPublicado.Publicar();

        var erroDespublicado = Erro.Criar($"ARTD-{sufixo}", OrigemErro.InvoiCy, $"Erro rascunho artigo {sufixo}");
        erroDespublicado.SubstituirVariantes([$"variante artigo rasc {sufixo}"]);

        contexto.Erros.AddRange(erroPublicado, erroDespublicado);
        await contexto.SaveChangesAsync();

        var publicado = Artigo.Criar(TipoArtigo.Conceito, $"Artigo publicado {sufixo}", $"Corpo do artigo {sufixo}.");
        publicado.RegistrarVerificacao(DateTimeOffset.UtcNow);
        publicado.Publicar();

        var rascunho = Artigo.Criar(TipoArtigo.Procedimento, $"Artigo rascunho {sufixo}", $"Corpo rascunho {sufixo}.");

        contexto.Artigos.AddRange(publicado, rascunho);
        await contexto.SaveChangesAsync();

        contexto.ArtigosCamposLayout.AddRange(
            ArtigoCampoLayout.Criar(publicado.Id, campo.Id),
            ArtigoCampoLayout.Criar(publicado.Id, campoRascunho.Id));
        contexto.ArtigosErros.AddRange(
            ArtigoErro.Criar(publicado.Id, erroPublicado.Id),
            ArtigoErro.Criar(publicado.Id, erroDespublicado.Id));
        await contexto.SaveChangesAsync();

        return new Cenario(
            sufixo, publicado.Slug, rascunho.Slug,
            erroPublicado.Codigo, erroDespublicado.Codigo,
            campo.Slug, campoRascunho.Slug);
    }
}
