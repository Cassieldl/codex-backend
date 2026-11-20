using System.Net;
using System.Text.Json;
using Codex.Application.Common;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// <c>campoCasado</c> lido do JSON literal da busca pública: é por ele que o card decide se mostra
/// o trecho, e valor serializado pelo próprio .NET pode sair número sem nenhum teste C# notar.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CampoCasadoBuscaHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..10];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public CampoCasadoBuscaHttpTests(PostgresContainerFixture fixture)
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
    public async Task GetBusca_Aliquota_TodoErroDizOCampoCasado_EOE999VemPelaMensagem()
    {
        string provedorSlug;

        await using (var contexto = _fixture.CriarContexto())
        {
            var (provedorId, versaoId, slug) = await CriarProvedorPublicadoAsync(contexto);
            provedorSlug = slug;

            // Mensagem com prefixo, como a do Betha: o título repete o texto e o trecho também.
            var e999 = Erro.Criar(
                "E999", OrigemErro.Prefeitura,
                $"Alíquota efetiva da União para CBS incorreta. ValorInformado == 0.90 && ValorCalculado == 0 {_sufixo}",
                causaProvavel: "O envio de IBS/CBS depende da alíquota parametrizada.");
            e999.SubstituirVariantes(["Erro interno no processamento Alíquota efetiva da União para CBS incorreta. ValorInformado == 0.90 && ValorCalculado == 0"]);

            var pelaCausa = Erro.Criar(
                "E0700", OrigemErro.Prefeitura, $"Base de cálculo divergente {_sufixo}",
                causaProvavel: "A alíquota informada difere da cadastrada na prefeitura.",
                solucaoSugerida: "Corrija o cadastro do serviço.");

            var pelaSolucao = Erro.Criar(
                "E0701", OrigemErro.Prefeitura, $"Valor do ISS incorreto {_sufixo}",
                causaProvavel: "O valor não confere com o cálculo da prefeitura.",
                solucaoSugerida: "Informe a alíquota do município.");

            // Mensagem tem duas origens: a variante observada e o título. Cada uma com um erro só dela.
            var soPelaVariante = Erro.Criar("E0702", OrigemErro.Prefeitura, $"Valor informado não confere {_sufixo}");
            soPelaVariante.SubstituirVariantes(["Alíquota do ISS divergente do cadastro."]);

            var soPeloTitulo = Erro.Criar("E0703", OrigemErro.Prefeitura, $"Alíquota zerada no serviço {_sufixo}");

            foreach (var erro in new[] { e999, pelaCausa, pelaSolucao, soPelaVariante, soPeloTitulo })
                await IndexarErroLigadoAsync(contexto, erro, provedorId, versaoId);
        }

        var itens = await BuscarItensAsync($"/api/v1/busca?q=aliquota&provedor={provedorSlug}&tamanhoPagina=50");

        var erros = itens.Where(item => item.GetProperty("tipo").GetString() == "erro").ToList();
        erros.Should().HaveCount(5);
        erros.Should().AllSatisfy(item => item.GetProperty("campoCasado").ValueKind.Should().Be(JsonValueKind.String));

        CampoCasadoPorChave(erros).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["E999"] = "mensagem",
            ["E0700"] = "causa",
            ["E0701"] = "solucao",
            ["E0702"] = "mensagem",
            ["E0703"] = "mensagem",
        });
    }

    [Fact]
    public async Task GetBusca_ErroCasadoPelaCausa_TrechoNaoRepeteOTitulo()
    {
        const string Titulo = "Inscrição municipal do tomador não confere com o cadastro";
        string provedorSlug;

        await using (var contexto = _fixture.CriarContexto())
        {
            var (provedorId, versaoId, slug) = await CriarProvedorPublicadoAsync(contexto);
            provedorSlug = slug;

            var erro = Erro.Criar(
                "E0710", OrigemErro.Prefeitura, $"{Titulo} {_sufixo}",
                causaProvavel: "A alíquota enviada é de outro município.");
            erro.SubstituirVariantes([$"{Titulo} informado na DPS."]);
            await IndexarErroLigadoAsync(contexto, erro, provedorId, versaoId);
        }

        var itens = await BuscarItensAsync($"/api/v1/busca?q=aliquota&provedor={provedorSlug}&tamanhoPagina=50");

        var erro0710 = itens.Should().ContainSingle(item => item.GetProperty("chave").GetString() == "E0710").Subject;
        erro0710.GetProperty("campoCasado").GetString().Should().Be("causa");

        var trecho = string.Concat(erro0710.GetProperty("trechos").EnumerateArray().Select(t => t.GetProperty("texto").GetString()));
        trecho.Should().Contain("alíquota").And.NotContain("Inscrição municipal");
    }

    [Fact]
    public async Task GetBusca_ErroComTituloCortado_VoltaComAVarianteInteira_SoQuandoElaComecaPeloTitulo()
    {
        var inicio = $"É obrigatório informar alíquota {_sufixo} para o prestador na data de";
        string provedorSlug;

        await using (var contexto = _fixture.CriarContexto())
        {
            var (provedorId, versaoId, slug) = await CriarProvedorPublicadoAsync(contexto);
            provedorSlug = slug;

            var comVariante = Erro.Criar("E0720", OrigemErro.Prefeitura, $"{inicio}…");
            comVariante.SubstituirVariantes(["Mensagem antiga de outra regra.", $"{inicio}\ncompetência informada na DPS."]);

            var semVariante = Erro.Criar("E0721", OrigemErro.Prefeitura, $"{inicio} emissão…");
            semVariante.SubstituirVariantes(["Texto que não começa pelo título."]);

            await IndexarErroLigadoAsync(contexto, comVariante, provedorId, versaoId);
            await IndexarErroLigadoAsync(contexto, semVariante, provedorId, versaoId);
        }

        var itens = await BuscarItensAsync($"/api/v1/busca?q=obrigatorio&provedor={provedorSlug}&tamanhoPagina=50");
        var tituloPorChave = itens
            .Where(item => item.GetProperty("tipo").GetString() == "erro")
            .ToDictionary(item => item.GetProperty("chave").GetString()!, item => item.GetProperty("titulo").GetString());

        tituloPorChave["E0720"].Should().Be($"{inicio} competência informada na DPS.");
        tituloPorChave["E0721"].Should().Be($"{inicio} emissão…", because: "sem variante que comece pelo título, o título fica como está");
    }

    [Fact]
    public async Task GetBusca_PeloCodigo_ErroVemPeloCodigo_EItemQueNaoEErroTemCampoCasadoNulo()
    {
        var codigo = $"E9{_sufixo[..6]}";

        await using (var contexto = _fixture.CriarContexto())
        {
            var erro = Erro.Criar(codigo, OrigemErro.InvoiCy, $"Erro de contrato {_sufixo}");
            erro.Publicar();
            contexto.Erros.Add(erro);
            await contexto.SaveChangesAsync();

            var itemErro = ItemBusca.Criar(TipoItemIndexado.erro, erro.Id, erro.Titulo, $"/erros/{erro.Slug}", chave: erro.Codigo);
            var (a, b, c, d) = PesosBusca.DoErro(erro);
            itemErro.AtualizarPesos(a, b, c, d);
            itemErro.MarcarPublicado(true);

            var artigo = Artigo.Criar(TipoArtigo.Conceito, $"Artigo sobre {codigo}", "Corpo.");
            artigo.Publicar();
            contexto.Artigos.Add(artigo);
            await contexto.SaveChangesAsync();

            var itemArtigo = ItemBusca.Criar(TipoItemIndexado.artigo, artigo.Id, artigo.Titulo, $"/artigos/{artigo.Slug}", provedorIds: []);
            itemArtigo.AtualizarPesos(artigo.Titulo, null, null, null);
            itemArtigo.MarcarPublicado(true);

            contexto.ItensBusca.AddRange(itemErro, itemArtigo);
            await contexto.SaveChangesAsync();
        }

        var itens = await BuscarItensAsync($"/api/v1/busca?q={codigo}&tamanhoPagina=50");

        var doErro = itens.Should().ContainSingle(item => item.GetProperty("tipo").GetString() == "erro").Subject;
        doErro.GetProperty("campoCasado").GetString().Should().Be("codigo");

        var doArtigo = itens.Should().ContainSingle(item => item.GetProperty("tipo").GetString() == "artigo").Subject;
        doArtigo.TryGetProperty("campoCasado", out var campoDoArtigo).Should().BeTrue(because: "a chave nunca é omitida");
        campoDoArtigo.ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ---------------------------------------------------------------------------------- apoio

    private async Task<List<JsonElement>> BuscarItensAsync(string rota)
    {
        using var resposta = await _cliente.GetAsync(rota);
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: texto);

        using var documento = JsonDocument.Parse(texto);
        return documento.RootElement.GetProperty("data").GetProperty("grupos").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("itens").EnumerateArray())
            .Select(item => item.Clone())
            .ToList();
    }

    private static Dictionary<string, string> CampoCasadoPorChave(IEnumerable<JsonElement> erros) =>
        erros.ToDictionary(
            item => item.GetProperty("chave").GetString()!,
            item => item.GetProperty("campoCasado").GetString()!);

    private async Task<(int ProvedorId, int VersaoId, string Slug)> CriarProvedorPublicadoAsync(CodexDbContext contexto)
    {
        var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => (int?)v.Ordem) ?? 0;

        var versaoLayout = VersaoLayout.Criar($"Layout casado {_sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Betha casado {_sufixo}", $"CAS-{_sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao casado {_sufixo}");
        versao.Publicar();
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return (provedor.Id, versao.Id, provedor.Slug);
    }

    private static async Task IndexarErroLigadoAsync(CodexDbContext contexto, Erro erro, int provedorId, int versaoId)
    {
        erro.Publicar();
        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        contexto.ErrosVersoes.Add(ErroVersao.Criar(erro.Id, versaoId));

        var item = ItemBusca.Criar(
            TipoItemIndexado.erro, erro.Id, erro.Titulo, $"/erros/{erro.Slug}",
            chave: erro.Codigo, provedorIds: [provedorId]);
        var (a, b, c, d) = PesosBusca.DoErro(erro);
        item.AtualizarPesos(a, b, c, d);
        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);
        await contexto.SaveChangesAsync();
    }
}
