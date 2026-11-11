using Codex.Application.DTOs.Publico;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// Decisão B (arquitetura.md §5.B, analise-backend.md §3.1): a <c>url</c> de cada resultado é
/// DERIVADA do slug corrente na hidratação, nunca lida da coluna <c>codex.itensbusca.url</c>.
///
/// <para>
/// O critério de aceite é este: com a coluna <b>corrompida de propósito</b>, a busca continua
/// respondendo a URL certa — e para os <b>seis</b> tipos, não um ou dois. São seis caminhos de
/// código diferentes na tabela de §3.1 e qualquer um pode regredir sozinho; um caso por tipo é o
/// que impede que "cinco funcionam" mascare o sexto, que foi exatamente como o buraco de artigo
/// em I22 sobreviveu a uma demanda inteira.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class UrlDerivadaBuscaTests : IAsyncLifetime
{
    private const string UrlCorrompida = "/lixo";

    private readonly PostgresContainerFixture _fixture;

    /// <summary>Id do artigo reservado semeado por ESTA classe — nulo quando ele já existia.</summary>
    private int? _reservadoSemeadoId;

    public UrlDerivadaBuscaTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// O slug reservado é chave natural ÚNICA e global, e o container do Testcontainers é
    /// compartilhado entre as classes da mesma <c>Collection</c>: deixá-lo no banco faz
    /// <c>ContratoArtigoHttpTests.P8_ArtigoReservadoAusente_Retorna404_NuncaGone</c> receber 200.
    ///
    /// <para>
    /// Hard delete, e nas DUAS tabelas: apagar só a linha derivada (<c>itensbusca</c>) dá a
    /// sensação de ter limpado e deixa a chave ocupada — foi exatamente o que aconteceu aqui. O
    /// soft delete teria o mesmo defeito, porque a linha continua ocupando o slug.
    /// </para>
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_reservadoSemeadoId is { } id)
            await RemoverArtigoAsync(id);
    }

    /// <summary>Os seis tipos indexáveis, semeados de uma vez e encontrados por um único termo exclusivo.</summary>
    private sealed record CenarioSeisTipos(
        string Termo,
        string ProvedorSlug,
        string VersaoSlug,
        string ErroSlug,
        string ArtigoSlug,
        string CampoLayoutSlug);

    [Fact]
    public async Task BuscarAsync_ComUrlDoIndiceCorrompida_DerivaAUrlCerta_NosSeisTipos()
    {
        var cenario = await SemearOsSeisTiposAsync();
        await CorromperUrlsAsync(cenario.Termo);

        var itens = await BuscarPorTipoAsync(cenario.Termo);

        itens.Should().HaveCount(7, because: "seis tipos, com aviso aparecendo nos dois escopos (provedor e versão)");

        UrlDe(itens, "provedor").Should().Be($"/provedores/{cenario.ProvedorSlug}");
        UrlDe(itens, "versaoprovedor").Should().Be($"/provedores/{cenario.ProvedorSlug}",
            because: "a página do provedor mostra o conteúdo da versão; a URL não tem segmento de versão");
        UrlDe(itens, "erro").Should().Be($"/erros/{cenario.ErroSlug}");
        UrlDe(itens, "artigo").Should().Be($"/artigos/{cenario.ArtigoSlug}");
        UrlDe(itens, "campolayout").Should().Be($"/layout/campos/{cenario.CampoLayoutSlug}");

        var avisos = itens.Where(i => i.Tipo == "aviso").Select(i => i.Url).ToArray();
        avisos.Should().Equal(
            [$"/provedores/{cenario.ProvedorSlug}#avisos", $"/provedores/{cenario.ProvedorSlug}#avisos"],
            because: "aviso de provedor e de versão ancoram em #avisos da página do provedor");

        itens.Should().NotContain(item => item.Url == UrlCorrompida,
            because: "nenhum dos seis caminhos pode ter continuado lendo a coluna");
    }

    /// <summary>
    /// A linha do PROVEDOR precisa hidratar o próprio provedor — não pode depender de uma linha
    /// irmã (versão ou aviso) ter trazido o mesmo <c>provedorid</c> para o dicionário. O caso
    /// acima, com os sete itens na mesma página, passa mesmo se o tipo <c>provedor</c> ficar de
    /// fora da coleta de ids: as filhas pagam a conta por acidente. Aqui a página tem uma linha
    /// só — é o recorte por tipo que o cliente faz no filtro da tela de busca.
    /// </summary>
    [Fact]
    public async Task BuscarAsync_ComPaginaSoDeProvedor_DerivaAUrlSemDependerDasLinhasFilhas()
    {
        var cenario = await SemearOsSeisTiposAsync();
        await CorromperUrlsAsync(cenario.Termo);

        var itens = await BuscarPorTipoAsync(cenario.Termo, tipos: ["provedor"]);

        itens.Should().ContainSingle(because: "o recorte por tipo deixa a linha do provedor sozinha na página");
        itens.Single().Url.Should().Be($"/provedores/{cenario.ProvedorSlug}");
    }

    /// <summary>
    /// O artigo reservado não mora sob <c>/artigos/</c> na superfície pública. A derivação usa a
    /// mesma <c>SlugsReservadosArtigo.RotaCanonica</c> do writer — interpolar "/artigos/{slug}"
    /// aqui criaria a segunda fórmula de URL e mandaria o leitor para uma rota que não existe.
    /// </summary>
    [Fact]
    public async Task BuscarAsync_ArtigoReservado_DerivaARotaFixaEnaoArtigosBarraSlug()
    {
        var termo = NovoTermo();
        int artigoId;

        await using (var contexto = _fixture.CriarContexto())
        {
            var artigo = await contexto.Artigos.FirstOrDefaultAsync(a => a.Slug == SlugsReservadosArtigo.ComoFunciona);

            if (artigo is null)
            {
                artigo = Artigo.Criar(TipoArtigo.Conceito, "Como funciona", "Corpo obrigatório.");
                artigo.Publicar();
                contexto.Artigos.Add(artigo);
                await contexto.SaveChangesAsync();

                // O artigo inteiro sai no DisposeAsync; só a linha de índice sairia cedo demais.
                _reservadoSemeadoId = artigo.Id;
            }

            artigoId = artigo.Id;
            await IndexarAsync(contexto, TipoItemIndexado.artigo, artigoId, termo, "Como funciona", UrlCorrompida);
        }

        try
        {
            var itens = await BuscarPorTipoAsync(termo);

            UrlDe(itens, "artigo").Should().Be($"/{SlugsReservadosArtigo.ComoFunciona}");
        }
        finally
        {
            // A linha de índice sai sempre: mesmo quando o artigo é de outra classe, esta indexação
            // com url corrompida é desta classe e contaminaria a busca de quem vier depois.
            await LimparIndexacaoAsync(TipoItemIndexado.artigo, artigoId);
        }
    }

    /// <summary>
    /// A URL de versão e de aviso de versão depende só do provedor: com a versão pai desativada
    /// (fora do JOIN de rótulos) a busca não cai na coluna, continua derivando do provedor.
    /// </summary>
    [Fact]
    public async Task BuscarAsync_ComVersaoPaiDesativada_ContinuaDerivandoAUrlDoProvedor()
    {
        var termo = NovoTermo();
        var cenario = await SemearOsSeisTiposAsync(termo);
        await CorromperUrlsAsync(termo);

        await using (var contexto = _fixture.CriarContexto())
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE codex.versoesprovedor SET desativadoem = now()
                WHERE slug = {cenario.VersaoSlug}
                """);
        }

        var itens = await BuscarPorTipoAsync(termo);

        UrlDe(itens, "versaoprovedor").Should().Be($"/provedores/{cenario.ProvedorSlug}");
        itens.Where(i => i.Tipo == "aviso").Select(i => i.Url)
            .Should().AllBe($"/provedores/{cenario.ProvedorSlug}#avisos");
    }

    /// <summary>
    /// Versão de provedor não tem página própria no portal público: a busca pública não lista o
    /// tipo nem com recorte explícito, e total e grupos ficam coerentes com isso. A interna,
    /// sobre o mesmo índice, continua listando a versão.
    /// </summary>
    [Fact]
    public async Task BuscarAsync_Publica_OmiteVersaoDeProvedor_EInternaContinuaListando()
    {
        var cenario = await SemearOsSeisTiposAsync();

        var publica = await BuscarAsync(cenario.Termo, incluirRascunho: false);
        var publicaSoVersao = await BuscarAsync(cenario.Termo, incluirRascunho: false, tipos: ["versaoprovedor"]);
        var interna = await BuscarAsync(cenario.Termo, incluirRascunho: true);

        publica.Grupos.Select(g => g.Tipo).Should().BeEquivalentTo(["provedor", "aviso", "erro", "artigo"],
            because: "campolayout já não é público e versaoprovedor passa a não ser");
        publica.TotalGeral.Should().Be(5).And.Be(publica.Grupos.Sum(g => g.Total));

        var itensPublicos = publica.Grupos.SelectMany(g => g.Itens).ToList();
        UrlDe(itensPublicos, "provedor").Should().Be($"/provedores/{cenario.ProvedorSlug}");
        itensPublicos.Where(i => i.Tipo == "aviso").Select(i => i.Url)
            .Should().AllBe($"/provedores/{cenario.ProvedorSlug}#avisos");

        publicaSoVersao.TotalGeral.Should().Be(0);
        publicaSoVersao.Grupos.Should().BeEmpty();

        interna.Grupos.Should().Contain(g => g.Tipo == "versaoprovedor" && g.Total == 1);
        interna.TotalGeral.Should().Be(7);
    }

    // ------------------------------------------------------------------ apoio

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>Termo de um único token, exclusivo do caso — casa no passo FORTE e nunca alcança item de outro teste (AP-QA-002).</summary>
    private static string NovoTermo() => $"urlderivada{NovoSufixo()}";

    private static string? UrlDe(IReadOnlyCollection<BuscaItemDto> itens, string tipo) =>
        itens.Single(i => i.Tipo == tipo).Url;

    private async Task<CenarioSeisTipos> SemearOsSeisTiposAsync(string? termoExistente = null)
    {
        var termo = termoExistente ?? NovoTermo();
        var sufixo = NovoSufixo();

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout url {sufixo}", maiorOrdem + 1);
        versaoLayout.Publicar();
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var campoLayout = CampoLayout.Criar(versaoLayout.Id, $"Campo url {sufixo}", $"//nfe/campo{sufixo}", "Significado.", 1);
        campoLayout.Publicar();
        contexto.CamposLayout.Add(campoLayout);

        var provedor = Provedor.Criar($"Provedor url {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao url {sufixo}");
        versao.Publicar();
        contexto.VersoesProvedor.Add(versao);

        var erro = Erro.Criar($"E-{sufixo}", OrigemErro.InvoiCy, $"Erro url {sufixo}");
        erro.Publicar();
        contexto.Erros.Add(erro);

        var artigo = Artigo.Criar(TipoArtigo.Conceito, $"Artigo url {sufixo}", "Corpo.");
        artigo.Publicar();
        contexto.Artigos.Add(artigo);
        await contexto.SaveChangesAsync();

        var avisoDeProvedor = Aviso.CriarDeProvedor(provedor.Id, Severidade.Informativo, $"Aviso provedor {sufixo}", "Corpo.");
        avisoDeProvedor.Publicar();
        contexto.Avisos.Add(avisoDeProvedor);

        var avisoDeVersao = Aviso.CriarDeVersao(versao.Id, Severidade.Informativo, $"Aviso versao {sufixo}", "Corpo.");
        avisoDeVersao.Publicar();
        contexto.Avisos.Add(avisoDeVersao);
        await contexto.SaveChangesAsync();

        // As URLs entram CORRETAS (é o que o writer grava) e são corrompidas depois, para o caso
        // provar que a leitura ignora a coluna — e não só que a coluna nunca esteve certa.
        await IndexarAsync(contexto, TipoItemIndexado.provedor, provedor.Id, termo, provedor.Nome,
            $"/provedores/{provedor.Slug}", provedorId: provedor.Id, provedorIds: [provedor.Id]);

        await IndexarAsync(contexto, TipoItemIndexado.versaoprovedor, versao.Id, termo, versao.Rotulo,
            $"/provedores/{provedor.Slug}", provedorId: provedor.Id, versaoProvedorId: versao.Id,
            provedorIds: [provedor.Id]);

        await IndexarAsync(contexto, TipoItemIndexado.aviso, avisoDeProvedor.Id, termo, avisoDeProvedor.Titulo,
            $"/provedores/{provedor.Slug}#avisos", provedorId: provedor.Id, provedorIds: [provedor.Id]);

        await IndexarAsync(contexto, TipoItemIndexado.aviso, avisoDeVersao.Id, termo, avisoDeVersao.Titulo,
            $"/provedores/{provedor.Slug}#avisos", provedorId: provedor.Id, versaoProvedorId: versao.Id,
            provedorIds: [provedor.Id]);

        await IndexarAsync(contexto, TipoItemIndexado.erro, erro.Id, termo, erro.Titulo, $"/erros/{erro.Slug}",
            chave: erro.Codigo);

        await IndexarAsync(contexto, TipoItemIndexado.artigo, artigo.Id, termo, artigo.Titulo,
            SlugsReservadosArtigo.RotaCanonica(artigo.Slug));

        await IndexarAsync(contexto, TipoItemIndexado.campolayout, campoLayout.Id, termo, campoLayout.Nome,
            $"/layout/campos/{campoLayout.Slug}");

        return new CenarioSeisTipos(termo, provedor.Slug, versao.Slug, erro.Slug, artigo.Slug, campoLayout.Slug);
    }

    private static async Task IndexarAsync(
        CodexDbContext contexto,
        TipoItemIndexado tipoItem,
        int itemId,
        string termo,
        string titulo,
        string url,
        string? chave = null,
        int? provedorId = null,
        int? versaoProvedorId = null,
        IReadOnlyCollection<int>? provedorIds = null)
    {
        var item = ItemBusca.Criar(
            tipoItem, itemId, titulo, url,
            chave: chave, provedorId: provedorId, versaoProvedorId: versaoProvedorId,
            provedorIds: provedorIds ?? []);

        item.AtualizarPesos(termo, null, null, null);
        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);

        await contexto.SaveChangesAsync();
    }

    private async Task CorromperUrlsAsync(string termo)
    {
        await using var contexto = _fixture.CriarContexto();

        var afetadas = await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.itensbusca SET url = {UrlCorrompida} WHERE pesoa = {termo}
            """);

        afetadas.Should().Be(7, because: "a corrupção precisa alcançar as 7 linhas do cenário, senão o caso não prova nada");
    }

    private async Task LimparIndexacaoAsync(TipoItemIndexado tipoItem, int itemId)
    {
        await using var contexto = _fixture.CriarContexto();

        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM codex.itensbusca WHERE tipoitem = {tipoItem.ToString()} AND itemid = {itemId}
            """);
    }

    /// <summary>Hard delete do artigo e da linha de projeção — ver <see cref="DisposeAsync"/>.</summary>
    private async Task RemoverArtigoAsync(int id)
    {
        await using var contexto = _fixture.CriarContexto();

        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM codex.itensbusca WHERE tipoitem = 'artigo' AND itemid = {id}
            """);

        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM codex.artigos WHERE artigoid = {id}
            """);
    }

    /// <summary>Busca interna: campolayout e versaoprovedor só convivem com os demais tipos nela.</summary>
    private async Task<IReadOnlyCollection<BuscaItemDto>> BuscarPorTipoAsync(string termo, IReadOnlyCollection<string>? tipos = null)
    {
        var resultado = await BuscarAsync(termo, incluirRascunho: true, tipos);

        return resultado.Grupos.SelectMany(g => g.Itens).ToList();
    }

    private async Task<ResultadoBuscaDto> BuscarAsync(string termo, bool incluirRascunho, IReadOnlyCollection<string>? tipos = null)
    {
        await using var contexto = _fixture.CriarContexto();
        var consulta = new ConsultaBuscaPostgres(contexto, new ConfigurationBuilder().Build());

        return await consulta.BuscarAsync(
            termo, provedorSlug: null, tipos, pagina: 1, tamanhoPagina: 20, incluirRascunho, CancellationToken.None);
    }
}
