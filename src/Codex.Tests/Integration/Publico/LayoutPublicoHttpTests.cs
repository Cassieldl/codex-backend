using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.ValueObjects;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// P10/P11/P12 (analise-backend.md §4) medidos pelo JSON literal da resposta HTTP, contra um
/// Postgres real — objeto C# correto não é contrato HTTP correto, e o predicado A1 (§4.1) e a
/// fórmula M12 (§4.2) vivem parte em SQL (o JOIN da versão de origem) e parte em memória.
///
/// <para>
/// O cenário é desenhado para que cada limite de M12 tenha um campo dedicado, e para que a
/// remoção de qualquer metade do predicado A1 deixe um campo visível que não deveria estar lá:
/// </para>
///
/// <list type="bullet">
/// <item><b>Voculta</b> (menor ordem, NÃO publicada) hospeda <b>campoOculto</b>, que está
/// publicado. Só a metade "origem publicada" de A1 o mantém invisível — e a ordem dele é a menor
/// de todas, então sem A1 ele apareceria nas TRÊS versões.</item>
/// <item><b>campoSempre</b> nasce em V1 e nunca é descontinuado.</item>
/// <item><b>campoNovoEmV2</b> nasce em V2 (limites "origem == Y" e "origem posterior a Y").</item>
/// <item><b>campoDescontinuadoEmV2</b> nasce em V1 e é descontinuado em V2 (limites
/// "descontinuado exatamente em Y" e "descontinuado na versão anterior a Y").</item>
/// <item><b>campoFuturoEmV3</b> nasce em V3, depois da Vigente — é o ramo 2 de A3 em P12
/// (<c>versaoExibicao</c> cai na versão de origem porque o campo não está ativo na Vigente).</item>
/// </list>
///
/// <para>
/// O cenário ocupa o slot GLOBAL de <c>Estado = Vigente</c> (índice único parcial
/// <c>uq_codex_versoeslayout_estado_vigente</c>) e por isso limpa tudo que semeia no
/// <see cref="DisposeAsync"/> — hard delete, porque soft delete deixaria o slot ocupado para as
/// outras classes da mesma <c>Collection</c>.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class LayoutPublicoHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private Cenario _cenario = null!;

    public LayoutPublicoHttpTests(PostgresContainerFixture fixture)
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

    // ------------------------------------------------------------------------------------ P10

    [Fact]
    public async Task P10_ListaAsVersoesPublicadas_EmOrdemDecrescenteDeOrdem_ComAVigenteDestacada()
    {
        var indice = await ObterDadosAsync("/api/v1/layout");

        var slugs = indice.GetProperty("versoes").EnumerateArray()
            .Select(v => v.GetProperty("slug").GetString()!)
            .ToList();

        // Três elementos, semeados fora de ordem (V2, V1, V3): a ordem esperada não coincide nem
        // com a de inserção nem com o inverso dela.
        var posicoes = new[] { _cenario.V3Slug, _cenario.V2Slug, _cenario.V1Slug }
            .Select(slug => slugs.IndexOf(slug))
            .ToList();

        posicoes.Should().NotContain(-1, because: "as três versões publicadas do cenário precisam estar no índice");
        posicoes.Should().BeInAscendingOrder(because: "P10 ordena por Ordem DESC — V3, depois V2, depois V1");

        slugs.Should().NotContain(_cenario.VocultaSlug, because: "versão não publicada não entra no índice público");

        indice.GetProperty("vigente").GetProperty("slug").GetString().Should().Be(_cenario.V2Slug);
        indice.GetProperty("vigente").GetProperty("estado").GetString().Should().Be("Vigente");
    }

    [Fact]
    public async Task P10_Novidades_ComparamAVigenteComAAnteriorPublicada()
    {
        var indice = await ObterDadosAsync("/api/v1/layout");
        var novidades = indice.GetProperty("novidades");

        novidades.GetProperty("versao").GetProperty("slug").GetString().Should().Be(_cenario.V2Slug);
        // A anterior é a publicada logo abaixo: a oculta, entre ordens mais baixas, não conta.
        novidades.GetProperty("anterior").GetProperty("slug").GetString().Should().Be(_cenario.V1Slug);
        novidades.GetProperty("descricao").GetString().Should().Be(DescricaoDaV2);
        novidades.GetProperty("camposNovos").GetInt32().Should().Be(1, because: "só o campoNovo nasce na V2");
        novidades.GetProperty("camposDescontinuados").GetInt32().Should().Be(1, because: "só o campoSaindo sai na V2");
    }

    [Theory]
    // V1: campoSempre + campoDescontinuadoEmV2.
    // V2: campoSempre + campoDescontinuadoEmV2 (ainda ativo na versão que o descontinua) + campoNovoEmV2.
    // V3: campoSempre + campoNovoEmV2 + campoFuturoEmV3 (campoDescontinuadoEmV2 já saiu).
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    public async Task P10_TotalCamposDoResumo_ESempreOTotalDeP11DaMesmaVersao(int numeroDaVersao, int camposDoCenarioEsperados)
    {
        var versaoSlug = numeroDaVersao switch
        {
            1 => _cenario.V1Slug,
            2 => _cenario.V2Slug,
            _ => _cenario.V3Slug,
        };

        var indice = await ObterDadosAsync("/api/v1/layout");
        var versao = await ObterDadosAsync($"/api/v1/layout/versoes/{versaoSlug}");

        TotalCamposDe(indice, versaoSlug).Should().Be(versao.GetProperty("totalCampos").GetInt32(),
            because: "o resumo do índice e o dicionário da versão contam o MESMO conjunto (M12 sobre A1)");

        // Contagem restrita ao cenário: o banco é compartilhado pela Collection, e o total global
        // depende do que outras classes publicaram.
        TodosOsSlugs(versao).Count(slug => _cenario.SlugsDeCampo.Contains(slug))
            .Should().Be(camposDoCenarioEsperados);
    }

    // ------------------------------------------------------------------------------------ P11

    [Fact]
    public async Task P11_MontaODicionarioDaVersao_ComAnteriorEAsFlagsDeFronteiraDeM12()
    {
        var versao = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V2Slug}");

        versao.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["versao", "descricao", "anterior", "totalCampos", "camposPorGrupo"]);

        versao.GetProperty("versao").GetProperty("slug").GetString().Should().Be(_cenario.V2Slug);
        versao.GetProperty("anterior").GetProperty("slug").GetString().Should().Be(_cenario.V1Slug);

        // M12, limite "origem == Y": aparece e é o novo da versão.
        var novo = LocalizarCampo(versao, _cenario.CampoNovoEmV2Slug);
        novo.GetProperty("novoNestaVersao").GetBoolean().Should().BeTrue();
        novo.GetProperty("descontinuadoNestaVersao").GetBoolean().Should().BeFalse();
        novo.GetProperty("reformaTributaria").GetBoolean().Should().BeTrue();
        novo.GetProperty("urlCanonica").GetString().Should().Be($"/layout/campos/{_cenario.CampoNovoEmV2Slug}");

        // M12, limite "descontinuado exatamente em Y": ainda aparece, sinalizado.
        var descontinuado = LocalizarCampo(versao, _cenario.CampoDescontinuadoEmV2Slug);
        descontinuado.GetProperty("descontinuadoNestaVersao").GetBoolean().Should().BeTrue();
        descontinuado.GetProperty("novoNestaVersao").GetBoolean().Should().BeFalse();

        // Campo herdado de uma versão anterior: aparece sem nenhuma das duas flags.
        var sempre = LocalizarCampo(versao, _cenario.CampoSempreSlug);
        sempre.GetProperty("novoNestaVersao").GetBoolean().Should().BeFalse();
        sempre.GetProperty("descontinuadoNestaVersao").GetBoolean().Should().BeFalse();
        sempre.GetProperty("reformaTributaria").GetBoolean().Should().BeFalse();

        // R3 — vocabulário canônico puro: comportamento (tipo/tamanho/máscara/obrigatoriedade)
        // é de CampoVersao e não pode vazar para o dicionário do layout.
        sempre.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["slug", "nome", "caminhoXml", "significado", "ordem", "reformaTributaria", "novoNestaVersao", "descontinuadoNestaVersao", "urlCanonica"]);

        TodosOsSlugs(versao).Should().NotContain(_cenario.CampoFuturoEmV3Slug,
            because: "M12: campo cuja origem é POSTERIOR a Y não aparece em Y");
    }

    [Fact]
    public async Task P11_CampoDescontinuadoNaVersaoAnterior_NaoApareceNaVersaoSeguinte()
    {
        var v3 = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V3Slug}");
        var slugs = TodosOsSlugs(v3);

        // M12, limite "descontinuado na versão ANTERIOR a Y".
        slugs.Should().NotContain(_cenario.CampoDescontinuadoEmV2Slug);
        slugs.Should().Contain(_cenario.CampoSempreSlug);
        slugs.Should().Contain(_cenario.CampoNovoEmV2Slug);
        slugs.Should().Contain(_cenario.CampoFuturoEmV3Slug);
    }

    /// <summary>
    /// Só versão publicada e não desativada descontinua para o público. As três descontinuações
    /// têm ordem menor que a da versão alvo, então qualquer uma que contasse tiraria o campo dela.
    /// </summary>
    [Fact]
    public async Task P11_DescontinuacaoEmRascunhoOuVersaoDesativada_NaoTiraOCampoDoDicionario()
    {
        string slugBase, slugAlvo, campoNoRascunho, campoNaDesativada, campoNaPublicada;

        await using (var contexto = _fixture.CriarContexto())
        {
            var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => v.Ordem);

            var vBase = VersaoLayout.Criar($"Layout Base {_sufixo}", maiorOrdem + 1);
            var vRascunho = VersaoLayout.Criar($"Layout Rascunho {_sufixo}", maiorOrdem + 2);
            var vDesativada = VersaoLayout.Criar($"Layout Desativada {_sufixo}", maiorOrdem + 3);
            var vPublicada = VersaoLayout.Criar($"Layout Publicada {_sufixo}", maiorOrdem + 4);
            var vAlvo = VersaoLayout.Criar($"Layout Alvo {_sufixo}", maiorOrdem + 5);

            foreach (var publicada in new[] { vBase, vDesativada, vPublicada, vAlvo })
                publicada.Publicar();
            vDesativada.Desativar();

            contexto.VersoesLayout.AddRange(vBase, vRascunho, vDesativada, vPublicada, vAlvo);
            await contexto.SaveChangesAsync();

            var noRascunho = CampoLayout.Criar(vBase.Id, $"sai no rascunho {_sufixo}", $"//raiz/SaiRascunho{_sufixo}", "Sai num rascunho", ordem: 80);
            var naDesativada = CampoLayout.Criar(vBase.Id, $"sai na desativada {_sufixo}", $"//raiz/SaiDesativada{_sufixo}", "Sai numa versão removida", ordem: 81);
            var naPublicada = CampoLayout.Criar(vBase.Id, $"sai na publicada {_sufixo}", $"//raiz/SaiPublicada{_sufixo}", "Sai numa versão publicada", ordem: 82);

            foreach (var campo in new[] { noRascunho, naDesativada, naPublicada })
                campo.Publicar();

            contexto.CamposLayout.AddRange(noRascunho, naDesativada, naPublicada);
            await contexto.SaveChangesAsync();

            foreach (var (campo, versao) in new[] { (noRascunho, vRascunho), (naDesativada, vDesativada), (naPublicada, vPublicada) })
            {
                var rastreado = await contexto.CamposLayout.Include(c => c.VersaoLayoutOrigem).FirstAsync(c => c.Id == campo.Id);
                rastreado.Descontinuar(versao);
            }

            await contexto.SaveChangesAsync();

            (slugBase, slugAlvo) = (vBase.Slug, vAlvo.Slug);
            (campoNoRascunho, campoNaDesativada, campoNaPublicada) = (noRascunho.Slug, naDesativada.Slug, naPublicada.Slug);
        }

        var versaoBase = await ObterDadosAsync($"/api/v1/layout/versoes/{slugBase}");
        var versaoAlvo = await ObterDadosAsync($"/api/v1/layout/versoes/{slugAlvo}");
        var slugsDoAlvo = TodosOsSlugs(versaoAlvo);

        slugsDoAlvo.Should().Contain(campoNoRascunho).And.Contain(campoNaDesativada);
        slugsDoAlvo.Should().NotContain(campoNaPublicada, because: "descontinuação em versão publicada continua valendo");

        versaoAlvo.GetProperty("totalCampos").GetInt32().Should().Be(
            versaoBase.GetProperty("totalCampos").GetInt32() - 1,
            because: "entre a base e o alvo só a descontinuação publicada tira campo");
        versaoAlvo.GetProperty("versao").GetProperty("totalCampos").GetInt32()
            .Should().Be(versaoAlvo.GetProperty("totalCampos").GetInt32());
    }

    [Fact]
    public async Task P10EP11_CalendarioDaVersao_SaiComDatasEmTexto_ENullSemODado()
    {
        var indice = await ObterDadosAsync("/api/v1/layout");
        var v2 = (await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V2Slug}")).GetProperty("versao");
        var v1 = indice.GetProperty("versoes").EnumerateArray().Single(v => v.GetProperty("slug").GetString() == _cenario.V1Slug);

        foreach (var comCalendario in new[] { v2, indice.GetProperty("vigente") })
        {
            comCalendario.GetProperty("normaOrigem").GetString().Should().Be("NT 009 v1.01");
            comCalendario.GetProperty("homologacaoDesde").GetString().Should().Be("2026-10-01");
            comCalendario.GetProperty("liberadaNoInvoicyEm").GetString().Should().Be("2026-10-22");
            comCalendario.GetProperty("obrigatoriaEm").GetString().Should().Be("2026-12-01");
        }

        foreach (var chave in new[] { "normaOrigem", "homologacaoDesde", "liberadaNoInvoicyEm", "obrigatoriaEm" })
            v1.GetProperty(chave).ValueKind.Should().Be(JsonValueKind.Null, because: $"versão sem {chave} devolve a chave com null");
    }

    [Fact]
    public async Task P11_CampoComOrigemPosteriorAVersao_NaoApareceNaVersaoAnterior()
    {
        var v1 = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V1Slug}");
        var slugs = TodosOsSlugs(v1);

        // M12, limite "origem POSTERIOR a Y".
        slugs.Should().NotContain(_cenario.CampoNovoEmV2Slug);
        slugs.Should().NotContain(_cenario.CampoFuturoEmV3Slug);
        slugs.Should().Contain(_cenario.CampoSempreSlug);
        slugs.Should().Contain(_cenario.CampoDescontinuadoEmV2Slug);
    }

    [Fact]
    public async Task P11_TotalCampos_BateComOSomatorioDosGrupos()
    {
        var versao = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V2Slug}");

        var somaDosGrupos = versao.GetProperty("camposPorGrupo").EnumerateArray()
            .Sum(grupo => grupo.GetProperty("campos").GetArrayLength());

        versao.GetProperty("totalCampos").GetInt32().Should().Be(somaDosGrupos);
    }

    [Fact]
    public async Task P11_VersaoInexistente_Responde404()
    {
        var (status, corpo) = await ObterAsync($"/api/v1/layout/versoes/nao-existe-{_sufixo}");

        status.Should().Be(HttpStatusCode.NotFound);
        Codigo(corpo).Should().Be("NAO_ENCONTRADO");
    }

    [Fact]
    public async Task P11_VersaoExistenteNaoPublicada_Responde410ComCodigoEspecifico()
    {
        var (status, corpo) = await ObterAsync($"/api/v1/layout/versoes/{_cenario.VocultaSlug}");

        status.Should().Be(HttpStatusCode.Gone);
        Codigo(corpo).Should().Be("VERSAO_LAYOUT_DESPUBLICADA",
            because: "o código genérico CONTEUDO_DESPUBLICADO não permite ao cliente distinguir o caso (§0.7)");
    }

    /// <summary>
    /// Soft delete não é despublicação: a linha existe, mas para o público a versão nunca existiu.
    /// Devolver <c>410</c> aqui confirmaria a um anônimo que aquele slug já foi usado.
    /// </summary>
    [Fact]
    public async Task P11_VersaoSoftDeletada_Responde404()
    {
        var (status, corpo) = await ObterAsync($"/api/v1/layout/versoes/{_cenario.VdesativadaSlug}");

        status.Should().Be(HttpStatusCode.NotFound);
        Codigo(corpo).Should().Be("NAO_ENCONTRADO");
    }

    // ------------------------------------------------------------------------------------ P12

    [Fact]
    public async Task P12_CampoAtivoNaVigente_ResolveVersaoExibicaoParaAVigente_EListaOndeEUsado()
    {
        var verbete = await ObterDadosAsync($"/api/v1/layout/campos/{_cenario.CampoSempreSlug}");

        verbete.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["slug", "nome", "caminhoXml", "significado", "reformaTributaria", "estado", "versaoExibicao", "versaoOrigem", "versaoDescontinuado", "ondeEUsado"]);

        verbete.GetProperty("estado").GetString().Should().Be("Ativo");
        verbete.GetProperty("versaoExibicao").GetProperty("slug").GetString().Should().Be(_cenario.V2Slug);
        verbete.GetProperty("versaoOrigem").GetProperty("slug").GetString().Should().Be(_cenario.V1Slug);
        verbete.GetProperty("versaoDescontinuado").ValueKind.Should().Be(JsonValueKind.Null);

        var uso = verbete.GetProperty("ondeEUsado").EnumerateArray().Single();
        uso.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["provedorSlug", "provedorNome", "versaoSlug", "versaoRotulo", "destinoXml", "estadoPreenchimento"]);
        uso.GetProperty("provedorSlug").GetString().Should().Be(_cenario.ProvedorSlug);
        uso.GetProperty("versaoSlug").GetString().Should().Be(_cenario.VersaoProvedorSlug);
        uso.GetProperty("destinoXml").ValueKind.Should().Be(JsonValueKind.Null);
        uso.GetProperty("estadoPreenchimento").ValueKind.Should().Be(JsonValueKind.String);
        uso.GetProperty("estadoPreenchimento").GetString().Should().Be("Obrigatorio");
    }

    [Fact]
    public async Task P12_CampoNaoAtivoNaVigente_ResolveVersaoExibicaoParaAOrigemPublicada()
    {
        // campoFuturoEmV3 nasce DEPOIS da Vigente: o ramo 1 de A3 não se aplica e a resolução cai
        // na versão de origem.
        var verbete = await ObterDadosAsync($"/api/v1/layout/campos/{_cenario.CampoFuturoEmV3Slug}");

        verbete.GetProperty("versaoExibicao").GetProperty("slug").GetString().Should().Be(_cenario.V3Slug);
        verbete.GetProperty("versaoOrigem").GetProperty("slug").GetString().Should().Be(_cenario.V3Slug);
        verbete.GetProperty("ondeEUsado").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task P12_CampoDescontinuado_TrazAVersaoDeDescontinuacao()
    {
        var verbete = await ObterDadosAsync($"/api/v1/layout/campos/{_cenario.CampoDescontinuadoEmV2Slug}");

        verbete.GetProperty("estado").GetString().Should().Be("Descontinuado");
        verbete.GetProperty("versaoOrigem").GetProperty("slug").GetString().Should().Be(_cenario.V1Slug);
        verbete.GetProperty("versaoDescontinuado").GetProperty("slug").GetString().Should().Be(_cenario.V2Slug);
    }

    [Fact]
    public async Task P12_CampoDescontinuadoEmVersaoNaoPublicada_NaoExpoeNadaDessaVersao()
    {
        const string NormaDoRascunho = "NT rascunho";
        string slugDoCampo;
        string rotuloDoRascunho;

        await using (var contexto = _fixture.CriarContexto())
        {
            var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => v.Ordem);
            var v1 = await contexto.VersoesLayout.SingleAsync(v => v.Slug == _cenario.V1Slug);

            var rascunho = VersaoLayout.Criar($"Layout Rascunho {_sufixo}", maiorOrdem + 1);
            rascunho.AtualizarCalendario(NormaDoRascunho, new DateOnly(2027, 1, 1), new DateOnly(2027, 2, 1), new DateOnly(2027, 3, 1));
            contexto.VersoesLayout.Add(rascunho);
            await contexto.SaveChangesAsync();

            var campo = CampoLayout.Criar(v1.Id, $"campo em revisao {_sufixo}", $"//raiz/Revisao{_sufixo}", "Campo que sai num rascunho", ordem: 70);
            campo.Publicar();
            contexto.CamposLayout.Add(campo);
            await contexto.SaveChangesAsync();

            rascunho.AtualizarMudancas([new MudancaLayout(TipoMudancaLayout.Saiu, campo.CaminhoXml, null, "Sai no rascunho.")]);
            var rastreado = await contexto.CamposLayout.Include(c => c.VersaoLayoutOrigem).FirstAsync(c => c.Id == campo.Id);
            rastreado.Descontinuar(rascunho);
            await contexto.SaveChangesAsync();

            slugDoCampo = campo.Slug;
            rotuloDoRascunho = rascunho.Rotulo;
        }

        using var resposta = await _cliente.GetAsync($"/api/v1/layout/campos/{slugDoCampo}");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: texto);
        texto.Should().NotContain(rotuloDoRascunho).And.NotContain(NormaDoRascunho).And.NotContain("Sai no rascunho");

        var verbete = JsonDocument.Parse(texto).RootElement.GetProperty("data");
        verbete.GetProperty("versaoDescontinuado").ValueKind.Should().Be(JsonValueKind.Null);
        verbete.GetProperty("estado").GetString().Should().Be("Ativo", because: "a descontinuação ainda não é pública");
    }

    [Fact]
    public async Task P12_CampoInexistente_Responde404()
    {
        var (status, corpo) = await ObterAsync($"/api/v1/layout/campos/nao-existe-{_sufixo}");

        status.Should().Be(HttpStatusCode.NotFound);
        Codigo(corpo).Should().Be("NAO_ENCONTRADO");
    }

    /// <summary>
    /// O 410 tinha detector só para o campo invisível por herança da origem (A1). Campo
    /// despublicado por si, dentro de versão publicada, é a outra metade do mesmo predicado — e
    /// nenhum teste a exercitava.
    /// </summary>
    [Fact]
    public async Task P12_CampoDespublicadoPorSi_Responde410()
    {
        var (status, corpo) = await ObterAsync($"/api/v1/layout/campos/{_cenario.CampoDespublicadoSlug}");

        status.Should().Be(HttpStatusCode.Gone);
        Codigo(corpo).Should().Be("CAMPO_LAYOUT_DESPUBLICADO");
    }

    [Fact]
    public async Task P11_CampoDespublicadoPorSi_NaoApareceNoDicionarioDaVersaoPublicada()
    {
        var versao = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V1Slug}");

        TodosOsSlugs(versao).Should().NotContain(_cenario.CampoDespublicadoSlug);
    }

    // --------------------------------------------------------------------- predicado A1 (§4.1)

    [Fact]
    public async Task A1_CampoPublicadoComOrigemDespublicada_EInvisivelNosTresEndpoints()
    {
        // P12: existe, mas não é visível — 410, nunca 404 nem 200.
        var (status, corpo) = await ObterAsync($"/api/v1/layout/campos/{_cenario.CampoOcultoSlug}");
        status.Should().Be(HttpStatusCode.Gone);
        Codigo(corpo).Should().Be("CAMPO_LAYOUT_DESPUBLICADO");

        // P11: a origem dele tem a MENOR ordem do cenário — sem A1 ele apareceria nas três.
        foreach (var versaoSlug in new[] { _cenario.V1Slug, _cenario.V2Slug, _cenario.V3Slug })
        {
            var versao = await ObterDadosAsync($"/api/v1/layout/versoes/{versaoSlug}");
            TodosOsSlugs(versao).Should().NotContain(_cenario.CampoOcultoSlug);
        }

        // P10: não pode ser contado em totalCampos de nenhuma versão. Como a contagem é global,
        // a prova é a igualdade com o somatório de P11, que já não o contém.
        var indice = await ObterDadosAsync("/api/v1/layout");
        var v2 = await ObterDadosAsync($"/api/v1/layout/versoes/{_cenario.V2Slug}");
        TotalCamposDe(indice, _cenario.V2Slug).Should().Be(v2.GetProperty("totalCampos").GetInt32());
    }

    // ------------------------------------------------------------------------------- anonimato

    [Theory]
    [InlineData("/api/v1/layout")]
    [InlineData("/api/v1/layout/versoes/{versao}")]
    [InlineData("/api/v1/layout/campos/{campo}")]
    public async Task RotasDeLayout_RespondemSemAuthorization(string molde)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaDe(molde));
        using var resposta = await _cliente.SendAsync(requisicao);

        requisicao.Headers.Authorization.Should().BeNull();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "o portal público é 100% aberto — leitura de layout nunca exige token");
    }

    // ----------------------------------------------------------------------------------- cache

    /// <summary>
    /// As três rotas são anônimas e leem do banco a cada chamada. A Home, agregador anônimo irmão,
    /// publica <c>Cache-Control</c>; o Layout ficou sem — e é quem serve a tabela de campos
    /// inteira. A asserção cobre as três de uma vez porque o esquecimento foi por action.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/layout")]
    [InlineData("/api/v1/layout/versoes/{versao}")]
    [InlineData("/api/v1/layout/campos/{campo}")]
    public async Task RotasDeLayout_PublicamCacheControl(string molde)
    {
        using var resposta = await _cliente.GetAsync(RotaDe(molde));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Headers.CacheControl.Should().NotBeNull(
            because: "rota anônima sem cache deixa a taxa de requisições virar carga direta no banco");
        resposta.Headers.CacheControl!.Public.Should().BeTrue();
        resposta.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
        resposta.Headers.CacheControl.ToString().Should().Contain("stale-while-revalidate=300");
    }

    /// <summary>
    /// Despublicar uma versão precisa propagar: o <c>410</c> não pode ser cacheado junto com o
    /// caminho feliz. O header é escrito depois do serviço, então a exceção sobe antes dele.
    /// </summary>
    [Fact]
    public async Task VersaoDespublicada_NaoPublicaCacheControl()
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/layout/versoes/{_cenario.VocultaSlug}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Gone);
        resposta.Headers.CacheControl.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------- apoio

    private string RotaDe(string molde) => molde
        .Replace("{versao}", _cenario.V2Slug, StringComparison.Ordinal)
        .Replace("{campo}", _cenario.CampoSempreSlug, StringComparison.Ordinal);

    private const string DescricaoDaV2 = "Nota da V2.\nCampos novos: campo novo";

    private sealed record Cenario
    {
        public required string VocultaSlug { get; init; }
        public required string VdesativadaSlug { get; init; }
        public required string CampoDespublicadoSlug { get; init; }
        public required string V1Slug { get; init; }
        public required string V2Slug { get; init; }
        public required string V3Slug { get; init; }
        public required string CampoOcultoSlug { get; init; }
        public required string CampoSempreSlug { get; init; }
        public required string CampoNovoEmV2Slug { get; init; }
        public required string CampoDescontinuadoEmV2Slug { get; init; }
        public required string CampoFuturoEmV3Slug { get; init; }
        public required string ProvedorSlug { get; init; }
        public required string VersaoProvedorSlug { get; init; }

        /// <summary>Todos os campos semeados, inclusive os invisíveis — a contagem restrita ao cenário precisa notar se um deles vazar.</summary>
        public IReadOnlyCollection<string> SlugsDeCampo =>
        [
            CampoOcultoSlug, CampoDespublicadoSlug, CampoSempreSlug, CampoNovoEmV2Slug,
            CampoDescontinuadoEmV2Slug, CampoFuturoEmV3Slug,
        ];
    }

    private async Task<Cenario> SemearAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => (int?)v.Ordem) ?? 0;

        var voculta = VersaoLayout.Criar($"Layout Oculto {_sufixo}", maiorOrdem + 1);
        var v1 = VersaoLayout.Criar($"Layout Um {_sufixo}", maiorOrdem + 2);
        var v2 = VersaoLayout.Criar($"Layout Dois {_sufixo}", maiorOrdem + 3, DescricaoDaV2);
        var v3 = VersaoLayout.Criar($"Layout Tres {_sufixo}", maiorOrdem + 4);

        // Publicada e depois soft-deletada: o caso que distingue "nunca existiu" (404) de
        // "existe e está despublicada" (410) — sem ele, remover o filtro de DesativadoEm da
        // consulta por slug passaria em silêncio.
        var vdesativada = VersaoLayout.Criar($"Layout Removido {_sufixo}", maiorOrdem + 5);

        v1.Publicar();
        v2.Publicar();
        v2.MarcarVigente();
        v2.AtualizarCalendario("NT 009 v1.01", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 22), new DateOnly(2026, 12, 1));
        v3.Publicar();
        vdesativada.Publicar();
        vdesativada.Desativar();

        // Inserção fora de ordem de propósito: ordenação por Ordem DESC (V3, V2, V1) não pode
        // coincidir nem com a ordem de inserção nem com o inverso dela.
        contexto.VersoesLayout.AddRange(v2, v1, v3, voculta, vdesativada);
        await contexto.SaveChangesAsync();

        var campoOculto = CampoLayout.Criar(voculta.Id, $"campo oculto {_sufixo}", $"//raiz/Oculto{_sufixo}", "Campo de versão não publicada", ordem: 10);
        var campoSempre = CampoLayout.Criar(v1.Id, $"campo sempre {_sufixo}", $"//raiz/Sempre{_sufixo}", "Campo presente em todas as versões", ordem: 20, grupo: "Identificação");
        var campoDescontinuado = CampoLayout.Criar(v1.Id, $"campo saindo {_sufixo}", $"//raiz/Saindo{_sufixo}", "Campo que sai em V2", ordem: 30, grupo: "Identificação");
        var campoNovo = CampoLayout.Criar(v2.Id, $"campo novo {_sufixo}", $"//raiz/Novo{_sufixo}", "Campo que nasce em V2", ordem: 40, reformaTributaria: true);
        var campoFuturo = CampoLayout.Criar(v3.Id, $"campo futuro {_sufixo}", $"//raiz/Futuro{_sufixo}", "Campo que nasce depois da vigente", ordem: 50);

        // Campo despublicado por si, dentro de versão publicada — distinto do campoOculto, que é
        // invisível por herança da origem. Cobre o 410 do próprio campo em P12.
        var campoDespublicado = CampoLayout.Criar(v1.Id, $"campo rascunho {_sufixo}", $"//raiz/Rascunho{_sufixo}", "Campo ainda não publicado", ordem: 60);

        foreach (var campo in new[] { campoOculto, campoSempre, campoDescontinuado, campoNovo, campoFuturo })
            campo.Publicar();

        contexto.CamposLayout.AddRange(campoOculto, campoSempre, campoDescontinuado, campoNovo, campoFuturo, campoDespublicado);
        await contexto.SaveChangesAsync();

        // Descontinuar exige a navegação de origem carregada (invariante de M12 na entidade).
        var descontinuadoRastreado = await contexto.CamposLayout
            .Include(c => c.VersaoLayoutOrigem)
            .FirstAsync(c => c.Id == campoDescontinuado.Id);
        descontinuadoRastreado.Descontinuar(v2);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor Layout {_sufixo}", $"LAY-{_sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versaoProvedor = VersaoProvedor.Criar(provedor.Id, v2.Id, $"Versao Layout {_sufixo}");
        versaoProvedor.Publicar();
        contexto.VersoesProvedor.Add(versaoProvedor);
        await contexto.SaveChangesAsync();

        contexto.CamposVersao.Add(
            CampoVersao.Criar(versaoProvedor.Id, campoSempre.Id, "C", EstadoPreenchimento.Obrigatorio, ordem: 1));
        await contexto.SaveChangesAsync();

        return new Cenario
        {
            VocultaSlug = voculta.Slug,
            VdesativadaSlug = vdesativada.Slug,
            CampoDespublicadoSlug = campoDespublicado.Slug,
            V1Slug = v1.Slug,
            V2Slug = v2.Slug,
            V3Slug = v3.Slug,
            CampoOcultoSlug = campoOculto.Slug,
            CampoSempreSlug = campoSempre.Slug,
            CampoNovoEmV2Slug = campoNovo.Slug,
            CampoDescontinuadoEmV2Slug = campoDescontinuado.Slug,
            CampoFuturoEmV3Slug = campoFuturo.Slug,
            ProvedorSlug = provedor.Slug,
            VersaoProvedorSlug = versaoProvedor.Slug,
        };
    }

    /// <summary>
    /// Hard delete: o cenário ocupa o slot global de <c>Vigente</c> e soft delete o manteria
    /// ocupado para as demais classes da <c>Collection</c>.
    /// </summary>
    private async Task LimparAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var versoesLayout = await contexto.VersoesLayout.Where(v => v.Rotulo.Contains(_sufixo)).ToListAsync();
        var idsLayout = versoesLayout.Select(v => v.Id).ToList();

        var campos = await contexto.CamposLayout.Where(c => idsLayout.Contains(c.VersaoLayoutId)).ToListAsync();
        var idsCampo = campos.Select(c => c.Id).ToList();

        var camposVersao = await contexto.CamposVersao.Where(cv => idsCampo.Contains(cv.CampoLayoutId)).ToListAsync();
        var versoesProvedor = await contexto.VersoesProvedor.Where(v => idsLayout.Contains(v.VersaoLayoutId)).ToListAsync();
        var idsProvedor = versoesProvedor.Select(v => v.ProvedorId).Distinct().ToList();
        var provedores = await contexto.Provedores.Where(p => idsProvedor.Contains(p.Id)).ToListAsync();

        contexto.CamposVersao.RemoveRange(camposVersao);
        contexto.VersoesProvedor.RemoveRange(versoesProvedor);
        contexto.Provedores.RemoveRange(provedores);
        contexto.CamposLayout.RemoveRange(campos);
        contexto.VersoesLayout.RemoveRange(versoesLayout);

        await contexto.SaveChangesAsync();
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ObterAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        var texto = await resposta.Content.ReadAsStringAsync();

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone());
    }

    private async Task<JsonElement> ObterDadosAsync(string rota)
    {
        var (status, corpo) = await ObterAsync(rota);

        status.Should().Be(HttpStatusCode.OK, because: $"{rota} deveria responder 200");
        return corpo.GetProperty("data").Clone();
    }

    private static string? Codigo(JsonElement corpo) =>
        corpo.GetProperty("error").GetProperty("code").GetString();

    private static int TotalCamposDe(JsonElement indice, string versaoSlug) =>
        indice.GetProperty("versoes").EnumerateArray()
            .Single(v => v.GetProperty("slug").GetString() == versaoSlug)
            .GetProperty("totalCampos").GetInt32();

    private static List<string> TodosOsSlugs(JsonElement versao) =>
        versao.GetProperty("camposPorGrupo").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("campos").EnumerateArray())
            .Select(campo => campo.GetProperty("slug").GetString()!)
            .ToList();

    private static JsonElement LocalizarCampo(JsonElement versao, string slug)
    {
        var campo = versao.GetProperty("camposPorGrupo").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("campos").EnumerateArray())
            .FirstOrDefault(c => c.GetProperty("slug").GetString() == slug);

        campo.ValueKind.Should().Be(JsonValueKind.Object, because: $"o campo '{slug}' deveria estar visível nesta versão");
        return campo;
    }
}
