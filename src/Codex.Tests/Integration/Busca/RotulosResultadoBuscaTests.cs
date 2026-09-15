using Codex.Application.DTOs.Publico;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// Contexto de exibição de cada item de resultado (<c>provedorNome</c>, <c>versaoRotulo</c>,
/// <c>versoes</c>, <c>meta</c>), hidratado por JOIN sobre os ids da página. O caso que dá nome a
/// tudo isto é U9: os três primeiros campos nulos AO MESMO TEMPO são o sinal que a UI usa para
/// renderizar o selo "Geral" — enquanto o backend devolvia sempre nulo, TODO erro parecia geral e
/// a busca nunca dizia a que versão um erro pertencia. Por isso cada cenário aqui verifica também
/// o que NÃO é geral, não só o que é.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class RotulosResultadoBuscaTests
{
    private readonly PostgresContainerFixture _fixture;

    public RotulosResultadoBuscaTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsulta(CodexDbContext contexto)
        => new(contexto, new ConfigurationBuilder().Build());

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>
    /// Termo de um único token, exclusivo do teste. Casa no passo FORTE (AND) e nunca alcança
    /// item de outro teste — AP-QA-002: isolamento por sufixo só é confiável no passo FORTE, e
    /// palavra comum contaminaria a suíte inteira pelo fallback PARCIAL (OR).
    /// </summary>
    private static string NovoTermo() => $"rotulobusca{NovoSufixo()}";

    private sealed record VersaoCriada(int ProvedorId, string ProvedorNome, string ProvedorSlug, int VersaoId, string VersaoRotulo, string VersaoSlug);

    /// <summary>Provedor + versão reais (não só a linha de projeção) — a hidratação lê daqui, não de <c>itensbusca</c>.</summary>
    private static async Task<VersaoCriada> CriarProvedorComVersaoAsync(
        CodexDbContext contexto,
        string discriminador,
        bool publicado = true,
        DateOnly? verificadoEm = null)
    {
        var sufixo = NovoSufixo();

        var maiorOrdemAtual = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout {discriminador} {sufixo}", maiorOrdemAtual + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor {discriminador} {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        if (publicado)
            provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = await CriarVersaoAsync(contexto, provedor.Id, versaoLayout.Id, discriminador, publicado, verificadoEm);

        return new VersaoCriada(provedor.Id, provedor.Nome, provedor.Slug, versao.Id, versao.Rotulo, versao.Slug);
    }

    private static async Task<VersaoProvedor> CriarVersaoAsync(
        CodexDbContext contexto,
        int provedorId,
        int versaoLayoutId,
        string discriminador,
        bool publicado = true,
        DateOnly? verificadoEm = null)
    {
        var versao = VersaoProvedor.Criar(provedorId, versaoLayoutId, $"Versao {discriminador} {NovoSufixo()}");
        if (publicado)
            versao.Publicar();
        if (verificadoEm is not null)
            versao.RegistrarVerificacao(verificadoEm.Value);

        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return versao;
    }

    /// <summary>Erro publicado, com os vínculos N:N reais em <c>errosversoes</c> e a linha de projeção correspondente.</summary>
    private static async Task IndexarErroAsync(
        CodexDbContext contexto,
        string termo,
        string titulo,
        IReadOnlyCollection<int> versaoProvedorIds,
        IReadOnlyCollection<int> provedorIds)
    {
        var erro = Erro.Criar($"E-{NovoSufixo()}", OrigemErro.InvoiCy, titulo);
        erro.Publicar();
        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        foreach (var versaoProvedorId in versaoProvedorIds)
            contexto.ErrosVersoes.Add(ErroVersao.Criar(erro.Id, versaoProvedorId));

        var item = ItemBusca.Criar(
            TipoItemIndexado.erro, erro.Id, titulo, $"/erros/{erro.Slug}",
            chave: erro.Codigo, provedorIds: provedorIds);
        item.AtualizarPesos(termo, null, null, null);
        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);

        await contexto.SaveChangesAsync();
    }

    private static async Task IndexarVersaoAsync(CodexDbContext contexto, string termo, VersaoCriada versao)
    {
        var item = ItemBusca.Criar(
            TipoItemIndexado.versaoprovedor, versao.VersaoId, versao.VersaoRotulo,
            $"/provedores/{versao.ProvedorSlug}/{versao.VersaoSlug}",
            subtitulo: versao.ProvedorNome, provedorId: versao.ProvedorId, versaoProvedorId: versao.VersaoId,
            provedorIds: [versao.ProvedorId]);
        item.AtualizarPesos(termo, null, null, null);
        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);

        await contexto.SaveChangesAsync();
    }

    private static async Task IndexarAvisoDeVersaoAsync(CodexDbContext contexto, string termo, VersaoCriada versao, string titulo)
    {
        var aviso = Aviso.CriarDeVersao(versao.VersaoId, Severidade.Informativo, titulo, "corpo do aviso");
        aviso.Publicar();
        contexto.Avisos.Add(aviso);
        await contexto.SaveChangesAsync();

        var item = ItemBusca.Criar(
            TipoItemIndexado.aviso, aviso.Id, titulo,
            $"/provedores/{versao.ProvedorSlug}/{versao.VersaoSlug}#avisos",
            subtitulo: versao.ProvedorNome, provedorId: versao.ProvedorId, versaoProvedorId: versao.VersaoId,
            provedorIds: [versao.ProvedorId]);
        item.AtualizarPesos(termo, null, null, null);
        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);

        await contexto.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<BuscaItemDto>> BuscarItensAsync(string termo, bool incluirRascunho = false)
    {
        await using var contexto = _fixture.CriarContexto();
        var consulta = CriarConsulta(contexto);

        var resultado = await consulta.BuscarAsync(
            termo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho, CancellationToken.None);

        return resultado.Grupos.SelectMany(g => g.Itens).ToList();
    }

    [Fact]
    public async Task BuscarAsync_ErroComUmaVersaoAplicavel_PreencheProvedorVersaoEColecao()
    {
        var termo = NovoTermo();
        VersaoCriada versao;

        await using (var contexto = _fixture.CriarContexto())
        {
            versao = await CriarProvedorComVersaoAsync(contexto, "uma-versao");
            await IndexarErroAsync(contexto, termo, "Erro com uma versao aplicavel", [versao.VersaoId], [versao.ProvedorId]);
        }

        var item = (await BuscarItensAsync(termo)).Should().ContainSingle().Subject;

        item.ProvedorNome.Should().Be(versao.ProvedorNome);
        item.VersaoRotulo.Should().Be(versao.VersaoRotulo, because: "com uma única versão aplicável existe rótulo único a exibir");
        item.Versoes.Should().ContainSingle().Which.Should().Be(versao.VersaoRotulo);
        item.Meta.Should().BeNull(because: "data de verificação é atributo da versão, não do erro");
    }

    [Fact]
    public async Task BuscarAsync_ErroSemVersaoAplicavel_DevolveOsTresCamposNulos()
    {
        var termo = NovoTermo();

        await using (var contexto = _fixture.CriarContexto())
        {
            await IndexarErroAsync(contexto, termo, "Erro geral sem versao", versaoProvedorIds: [], provedorIds: []);
        }

        var item = (await BuscarItensAsync(termo)).Should().ContainSingle().Subject;

        item.ProvedorNome.Should().BeNull();
        item.VersaoRotulo.Should().BeNull();
        item.Versoes.Should().BeNull(
            because: "a UI decide 'Geral' (U9) pelos TRÊS campos nulos — coleção vazia não dispararia o selo e o card sairia em branco");
    }

    [Fact]
    public async Task BuscarAsync_ErroEmDuasVersoesDoMesmoProvedor_ListaOsRotulosSemQualificar()
    {
        var termo = NovoTermo();
        VersaoCriada primeira;
        VersaoProvedor segunda;

        await using (var contexto = _fixture.CriarContexto())
        {
            primeira = await CriarProvedorComVersaoAsync(contexto, "duas-versoes");

            var versaoLayoutId = await contexto.VersoesProvedor.AsNoTracking()
                .Where(v => v.Id == primeira.VersaoId)
                .Select(v => v.VersaoLayoutId)
                .FirstAsync();

            segunda = await CriarVersaoAsync(contexto, primeira.ProvedorId, versaoLayoutId, "duas-versoes-b");

            await IndexarErroAsync(
                contexto, termo, "Erro em duas versoes do mesmo provedor",
                [primeira.VersaoId, segunda.Id], [primeira.ProvedorId]);
        }

        var item = (await BuscarItensAsync(termo)).Should().ContainSingle().Subject;

        item.ProvedorNome.Should().Be(primeira.ProvedorNome, because: "todas as versões aplicáveis são do mesmo provedor");
        item.VersaoRotulo.Should().BeNull(because: "com duas versões não existe rótulo único — a lista é que responde");
        item.Versoes.Should().BeEquivalentTo([primeira.VersaoRotulo, segunda.Rotulo]);
    }

    [Fact]
    public async Task BuscarAsync_ErroEmVersoesDeProvedoresDiferentes_QualificaCadaRotuloComOProvedor()
    {
        var termo = NovoTermo();
        VersaoCriada primeiro;
        VersaoCriada segundo;

        await using (var contexto = _fixture.CriarContexto())
        {
            primeiro = await CriarProvedorComVersaoAsync(contexto, "multi-a");
            segundo = await CriarProvedorComVersaoAsync(contexto, "multi-b");

            await IndexarErroAsync(
                contexto, termo, "Erro em provedores diferentes",
                [primeiro.VersaoId, segundo.VersaoId], [primeiro.ProvedorId, segundo.ProvedorId]);
        }

        var item = (await BuscarItensAsync(termo)).Should().ContainSingle().Subject;

        item.ProvedorNome.Should().BeNull(because: "não existe um provedor único a exibir ao lado da lista");
        item.VersaoRotulo.Should().BeNull();
        item.Versoes.Should().BeEquivalentTo([
            $"{primeiro.ProvedorNome} · {primeiro.VersaoRotulo}",
            $"{segundo.ProvedorNome} · {segundo.VersaoRotulo}",
        ], because: "sem provedor único ao lado, o rótulo cru ('Versao X') seria ambíguo na tela");
    }

    [Fact]
    public async Task BuscarAsync_VersaoAplicavelDespublicada_SoApareceNaBuscaInterna()
    {
        var termo = NovoTermo();
        VersaoCriada publicada;
        VersaoCriada rascunho;

        await using (var contexto = _fixture.CriarContexto())
        {
            publicada = await CriarProvedorComVersaoAsync(contexto, "visivel");
            rascunho = await CriarProvedorComVersaoAsync(contexto, "rascunho", publicado: false);

            await IndexarErroAsync(
                contexto, termo, "Erro com versao em rascunho",
                [publicada.VersaoId, rascunho.VersaoId], [publicada.ProvedorId, rascunho.ProvedorId]);
        }

        var itemPublico = (await BuscarItensAsync(termo)).Should().ContainSingle().Subject;

        itemPublico.ProvedorNome.Should().Be(publicada.ProvedorNome, because: "a versão em rascunho não conta para o público, sobrando um provedor só");
        itemPublico.Versoes.Should().ContainSingle().Which.Should().Be(publicada.VersaoRotulo);

        var itemInterno = (await BuscarItensAsync(termo, incluirRascunho: true)).Should().ContainSingle().Subject;

        itemInterno.ProvedorNome.Should().BeNull();
        itemInterno.Versoes.Should().BeEquivalentTo([
            $"{publicada.ProvedorNome} · {publicada.VersaoRotulo}",
            $"{rascunho.ProvedorNome} · {rascunho.VersaoRotulo}",
        ], because: "a busca interna enxerga rascunho, e o contexto exibido tem de acompanhar essa visibilidade");
    }

    [Fact]
    public async Task BuscarAsync_VersaoDeProvedorVerificada_PreencheMetaEstruturadaComAData()
    {
        var termo = NovoTermo();
        VersaoCriada versao;

        await using (var contexto = _fixture.CriarContexto())
        {
            versao = await CriarProvedorComVersaoAsync(contexto, "verificada", verificadoEm: new DateOnly(2026, 5, 12));
            await IndexarVersaoAsync(contexto, termo, versao);
        }

        var item = (await BuscarItensAsync(termo)).Should().ContainSingle().Subject;

        item.ProvedorNome.Should().Be(versao.ProvedorNome);
        item.VersaoRotulo.Should().Be(versao.VersaoRotulo);
        item.Meta.Should().BeEquivalentTo(new BuscaMetaDto { Data = new DateOnly(2026, 5, 12) },
            because: "P-12: meta é estruturada — a formatação pt-BR passa a ser responsabilidade do frontend");
        item.Versoes.Should().BeNull(because: "a coleção só existe para erro, que pode valer para várias versões ao mesmo tempo");
    }

    [Fact]
    public async Task BuscarAsync_VersaoNuncaVerificada_DeixaMetaNulaMasMantemORotulo()
    {
        var termo = NovoTermo();
        VersaoCriada versao;

        await using (var contexto = _fixture.CriarContexto())
        {
            versao = await CriarProvedorComVersaoAsync(contexto, "sem-verificacao");
            await IndexarVersaoAsync(contexto, termo, versao);
        }

        var item = (await BuscarItensAsync(termo)).Should().ContainSingle().Subject;

        item.VersaoRotulo.Should().Be(versao.VersaoRotulo);
        item.Meta.Should().BeNull();
    }

    [Fact]
    public async Task BuscarAsync_AvisoDeVersao_DizAQueVersaoPertenceSemHerdarAMetaDaVersao()
    {
        var termo = NovoTermo();
        VersaoCriada versao;

        await using (var contexto = _fixture.CriarContexto())
        {
            versao = await CriarProvedorComVersaoAsync(contexto, "aviso", verificadoEm: new DateOnly(2026, 1, 9));
            await IndexarAvisoDeVersaoAsync(contexto, termo, versao, "Aviso de versao com rotulo");
        }

        var item = (await BuscarItensAsync(termo)).Should().ContainSingle().Subject;

        item.ProvedorNome.Should().Be(versao.ProvedorNome);
        item.VersaoRotulo.Should().Be(versao.VersaoRotulo);
        item.Meta.Should().BeNull(
            because: "'verificado em' sob o título de um aviso leria como se o aviso tivesse sido verificado, e quem foi verificada é a versão");
    }
}
