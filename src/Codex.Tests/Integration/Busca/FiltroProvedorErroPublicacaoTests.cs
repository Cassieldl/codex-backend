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
/// P-08 — coerência entre o FILTRO <c>?provedor=</c> e a EXIBIÇÃO do resultado, para erros.
///
/// <para>
/// O vínculo erro↔provedor é indireto (<c>errosversoes</c> × <c>versoesprovedor</c>) e
/// publicação-dependente. A incoerência corrigida aqui: o filtro se apoiava no snapshot
/// <c>itensbusca.provedorids</c>, montado SEM filtrar publicação, enquanto a hidratação de rótulos
/// filtra. Resultado: um erro ligado só a uma versão NÃO PUBLICADA de X aparecia sob
/// <c>?provedor=X</c> exibindo o selo "Geral" — o filtro afirmava um vínculo que a tela negava na
/// mesma resposta.
/// </para>
///
/// <para>
/// Os cenários vêm em pares de propósito: cada exclusão é acompanhada da inclusão equivalente com
/// a publicação em ordem. Sem o par, "não aparece" seria satisfeito por um filtro que
/// simplesmente exclui todo erro.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class FiltroProvedorErroPublicacaoTests
{
    private readonly PostgresContainerFixture _fixture;

    public FiltroProvedorErroPublicacaoTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsulta(CodexDbContext contexto)
        => new(contexto, new ConfigurationBuilder().Build());

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>
    /// Token único por teste: casa no passo FORTE (AND) e nunca alcança item de outro teste —
    /// AP-QA-002, vocabulário comum contaminaria vizinhos pelo fallback PARCIAL (OR).
    /// </summary>
    private static string NovoTermo() => $"filtroprovedorerro{NovoSufixo()}";

    private sealed record Cenario(int ProvedorId, string ProvedorSlug, string ProvedorNome, int VersaoId);

    // ---------------------------------------------------------------- versão não publicada

    [Fact]
    public async Task ErroLigadoSoAVersaoNaoPublicada_NaoApareceSobOFiltroDoProvedor()
    {
        var termo = NovoTermo();

        await using (var contexto = _fixture.CriarContexto())
        {
            var cenario = await CriarCenarioAsync(contexto, provedorPublicado: true, versaoPublicada: false);
            await IndexarErroLigadoAsync(contexto, termo, "Erro de versao rascunho", cenario);

            (await BuscarAsync(termo, cenario.ProvedorSlug)).Should().BeEmpty(
                because: "a única versão que liga o erro a este provedor não está publicada — " +
                         "afirmar o vínculo no filtro contradiria o rótulo, que sairia como \"Geral\"");
        }
    }

    [Fact]
    public async Task ErroLigadoAVersaoPublicada_ApareceSobOFiltro_ComORotuloDoProvedor()
    {
        var termo = NovoTermo();

        await using (var contexto = _fixture.CriarContexto())
        {
            var cenario = await CriarCenarioAsync(contexto, provedorPublicado: true, versaoPublicada: true);
            await IndexarErroLigadoAsync(contexto, termo, "Erro de versao publicada", cenario);

            var item = (await BuscarAsync(termo, cenario.ProvedorSlug)).Should().ContainSingle().Subject;

            item.ProvedorNome.Should().Be(cenario.ProvedorNome,
                because: "filtro e exibição concordam: entrou pelo filtro do provedor e exibe esse provedor");
        }
    }

    // ---------------------------------------------------------------- provedor não publicado

    [Fact]
    public async Task ErroLigadoAVersaoPublicadaDeProvedorNaoPublicado_NaoApareceSobOFiltro()
    {
        var termo = NovoTermo();

        await using (var contexto = _fixture.CriarContexto())
        {
            var cenario = await CriarCenarioAsync(contexto, provedorPublicado: false, versaoPublicada: true);
            await IndexarErroLigadoAsync(contexto, termo, "Erro de provedor rascunho", cenario);

            (await BuscarAsync(termo, cenario.ProvedorSlug)).Should().BeEmpty(
                because: "M19/M28: provedor despublicado esconde a versão dependente, então o vínculo " +
                         "não é público mesmo com a versão publicada");
        }
    }

    // ---------------------------------------------------------------- versão soft-deletada

    [Fact]
    public async Task ErroLigadoSoAVersaoDesativada_NaoApareceSobOFiltro()
    {
        var termo = NovoTermo();

        await using (var contexto = _fixture.CriarContexto())
        {
            var cenario = await CriarCenarioAsync(contexto, provedorPublicado: true, versaoPublicada: true);

            var versao = await contexto.VersoesProvedor.FirstAsync(v => v.Id == cenario.VersaoId);
            versao.Desativar();
            await contexto.SaveChangesAsync();

            await IndexarErroLigadoAsync(contexto, termo, "Erro de versao desativada", cenario);

            (await BuscarAsync(termo, cenario.ProvedorSlug)).Should().BeEmpty(
                because: "soft delete da versão remove o vínculo, exatamente como na hidratação de rótulos");
        }
    }

    // ---------------------------------------------------------------- busca interna vê rascunho

    /// <summary>
    /// O predicado acompanha <c>incluirRascunho</c>: a mesma ligação por versão em rascunho que o
    /// público não vê é justamente o que a busca INTERNA precisa ver. Prova que a correção não
    /// virou "erro em rascunho nunca casa com filtro", o que cegaria a ferramenta interna.
    /// </summary>
    [Fact]
    public async Task BuscaInterna_ComIncluirRascunho_VeErroLigadoAVersaoNaoPublicada()
    {
        var termo = NovoTermo();

        await using (var contexto = _fixture.CriarContexto())
        {
            var cenario = await CriarCenarioAsync(contexto, provedorPublicado: true, versaoPublicada: false);
            await IndexarErroLigadoAsync(contexto, termo, "Erro visivel no interno", cenario);

            (await BuscarAsync(termo, cenario.ProvedorSlug, incluirRascunho: true)).Should().ContainSingle(
                because: "a busca interna existe para enxergar rascunho — o filtro segue o mesmo " +
                         "predicado da exibição interna, que também inclui rascunho");
        }
    }

    // ---------------------------------------------------------------- apoio

    private async Task<IReadOnlyList<BuscaItemDto>> BuscarAsync(
        string termo, string provedorSlug, bool incluirRascunho = false)
    {
        await using var contexto = _fixture.CriarContexto();
        var consulta = CriarConsulta(contexto);

        var resultado = await consulta.BuscarAsync(
            termo, provedorSlug, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho, CancellationToken.None);

        return resultado.Grupos.SelectMany(g => g.Itens).ToList();
    }

    private static async Task<Cenario> CriarCenarioAsync(
        CodexDbContext contexto, bool provedorPublicado, bool versaoPublicada)
    {
        var sufixo = NovoSufixo();

        var maiorOrdemAtual = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout p08 {sufixo}", maiorOrdemAtual + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor p08 {sufixo}", $"P08-{sufixo}", CategoriaProvedor.Nacional);
        if (provedorPublicado)
            provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao p08 {sufixo}");
        if (versaoPublicada)
            versao.Publicar();
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return new Cenario(provedor.Id, provedor.Slug, provedor.Nome, versao.Id);
    }

    /// <summary>
    /// Erro publicado e indexado, vinculado à versão do cenário. O <c>provedorIds</c> da projeção
    /// recebe o provedor DE PROPÓSITO, reproduzindo o snapshot bruto que o writer grava: é
    /// exatamente esse valor que o filtro não pode mais tomar como verdade.
    /// </summary>
    private static async Task IndexarErroLigadoAsync(
        CodexDbContext contexto, string termo, string titulo, Cenario cenario)
    {
        var erro = Erro.Criar($"E-P08-{NovoSufixo()}", OrigemErro.InvoiCy, titulo);
        erro.Publicar();
        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        contexto.ErrosVersoes.Add(ErroVersao.Criar(erro.Id, cenario.VersaoId));

        var item = ItemBusca.Criar(
            TipoItemIndexado.erro, erro.Id, titulo, $"/erros/{erro.Slug}",
            chave: erro.Codigo, provedorIds: [cenario.ProvedorId]);
        item.AtualizarPesos(termo, null, null, null);
        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);

        await contexto.SaveChangesAsync();
    }
}
