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
/// O filtro <c>?provedor=</c> tem a visibilidade da superfície que o usa (MED-B02).
///
/// <para>
/// Todo o resto da superfície pública é meticuloso com <c>Publicado</c>; a resolução do filtro era
/// o único caminho público sem esse portão, e devolvia o NOME de um provedor em rascunho a um
/// anônimo. Pior que o nome: a resposta distinguia "slug não existe" (<c>filtroProvedor: null</c>)
/// de "existe e não foi publicado" (objeto preenchido) — um oráculo de enumeração, com o slug
/// derivado do nome.
/// </para>
///
/// <para>
/// Par obrigatório: a busca INTERNA precisa continuar enxergando o provedor em rascunho, senão a
/// correção cega a ferramenta da redação — foi essa a lição do P-08.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class FiltroProvedorVisibilidadeBuscaTests
{
    private readonly PostgresContainerFixture _fixture;

    public FiltroProvedorVisibilidadeBuscaTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsulta(CodexDbContext contexto)
        => new(contexto, new ConfigurationBuilder().Build());

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private sealed record Cenario(string Slug, string Nome, string Termo);

    private async Task<ResultadoBuscaDto> BuscarAsync(string termo, string? provedorSlug, bool incluirRascunho = false)
    {
        await using var contexto = _fixture.CriarContexto();
        return await CriarConsulta(contexto).BuscarAsync(
            termo, provedorSlug, null, 1, 20, incluirRascunho, CancellationToken.None);
    }

    /// <summary>Provedor com uma linha de projeção publicada e encontrável — a publicação do PROVEDOR é o que cada caso varia.</summary>
    private async Task<Cenario> MontarAsync(bool provedorPublicado)
    {
        var sufixo = NovoSufixo();
        var termo = $"filtrovis{sufixo}";

        await using var contexto = _fixture.CriarContexto();

        var provedor = Provedor.Criar($"Provedor filtrovis {sufixo}", $"FVIS-{sufixo}", CategoriaProvedor.Nacional);
        if (provedorPublicado)
            provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var item = ItemBusca.Criar(
            TipoItemIndexado.provedor, provedor.Id, provedor.Nome, $"/provedores/{provedor.Slug}",
            provedorId: provedor.Id, provedorIds: [provedor.Id]);
        item.AtualizarPesos(termo, null, null, null);
        item.MarcarPublicado(provedorPublicado);
        contexto.ItensBusca.Add(item);
        await contexto.SaveChangesAsync();

        return new Cenario(provedor.Slug, provedor.Nome, termo);
    }

    [Fact]
    public async Task BuscaPublica_ComSlugDeProvedorEmRascunho_NaoEchoaOFiltroNemDevolveONome()
    {
        var cenario = await MontarAsync(provedorPublicado: false);

        var resultado = await BuscarAsync(cenario.Termo, cenario.Slug);

        resultado.FiltroProvedor.Should().BeNull(
            because: "confirmar a existência do provedor em rascunho, e ainda devolver o nome, vaza conteúdo pré-lançamento a anônimo");
        resultado.TotalGeral.Should().Be(0);
    }

    /// <summary>
    /// A resposta do slug em rascunho tem de ser INDISTINGUÍVEL da do slug inexistente, senão a
    /// diferença é o oráculo: quem varre slugs derivados de nomes descobre o que ainda não foi
    /// anunciado sem nunca ver o conteúdo.
    /// </summary>
    [Fact]
    public async Task BuscaPublica_SlugEmRascunhoESlugInexistente_ProduzemAMesmaResposta()
    {
        var cenario = await MontarAsync(provedorPublicado: false);

        var emRascunho = await BuscarAsync(cenario.Termo, cenario.Slug);
        var inexistente = await BuscarAsync(cenario.Termo, $"nao-existe-{NovoSufixo()}");

        emRascunho.FiltroProvedor.Should().BeNull();
        inexistente.FiltroProvedor.Should().BeNull();
        emRascunho.Qualidade.Should().Be(inexistente.Qualidade);
        emRascunho.TotalGeral.Should().Be(inexistente.TotalGeral);
        emRascunho.Grupos.Should().BeEmpty();
        inexistente.Grupos.Should().BeEmpty();
    }

    /// <summary>
    /// Recorte pedido cujo alvo não existe nesta superfície devolve VAZIO — nunca o conjunto
    /// inteiro. Tratar "não resolveu" como "sem filtro" entregava a quem pediu um recorte o
    /// resultado completo, que é o mesmo defeito do filtro de tipo com valores desconhecidos.
    /// </summary>
    [Fact]
    public async Task BuscaPublica_ComSlugQueNaoResolve_NaoDegradaParaBuscaSemFiltro()
    {
        var cenario = await MontarAsync(provedorPublicado: true);

        var semFiltro = await BuscarAsync(cenario.Termo, provedorSlug: null);
        semFiltro.TotalGeral.Should().Be(1, because: "sanidade: sem filtro o item é encontrável");

        var comSlugInexistente = await BuscarAsync(cenario.Termo, $"nao-existe-{NovoSufixo()}");

        comSlugInexistente.TotalGeral.Should().Be(0);
        comSlugInexistente.Grupos.Should().BeEmpty();
    }

    [Fact]
    public async Task BuscaInterna_ComSlugDeProvedorEmRascunho_ContinuaEnxergandoOFiltroEOItem()
    {
        var cenario = await MontarAsync(provedorPublicado: false);

        var resultado = await BuscarAsync(cenario.Termo, cenario.Slug, incluirRascunho: true);

        resultado.FiltroProvedor.Should().NotBeNull();
        resultado.FiltroProvedor!.Nome.Should().Be(cenario.Nome,
            because: "a redação filtra pelo provedor que ela mesma está preparando — cegar isto inviabiliza a tela interna");
        resultado.TotalGeral.Should().Be(1);
    }

    [Fact]
    public async Task BuscaPublica_ComSlugDeProvedorPublicado_EchoaOFiltroNormalmente()
    {
        var cenario = await MontarAsync(provedorPublicado: true);

        var resultado = await BuscarAsync(cenario.Termo, cenario.Slug);

        resultado.FiltroProvedor.Should().NotBeNull();
        resultado.FiltroProvedor!.Slug.Should().Be(cenario.Slug);
        resultado.FiltroProvedor.Nome.Should().Be(cenario.Nome);
        resultado.TotalGeral.Should().Be(1);
    }
}
