using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// PROBE DE QA (2ª rodada) — a TRANSIÇÃO entre os passos da cascata e dois vetores adversariais
/// que o corpus existente não toca.
///
/// <para>O corpus de <c>BuscaCorpusAdversarialTests</c> afirma <c>qualidade == "exata"</c> em
/// todos os cenários de normalização, ou seja: só exercita o passo FORTE. Os degraus
/// PARCIAL e VAZIA nunca são afirmados por rótulo de qualidade — e é na fronteira entre passos
/// que a cascata engana.</para>
///
/// <para>Vocabulário isolado por token único (AP-QA-002): no passo PARCIAL o predicado é OR, então
/// nenhum token aqui é palavra comum — todos levam o sufixo aleatório colado.</para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CascataTransicaoProbeTests
{
    private readonly PostgresContainerFixture _fixture;

    public CascataTransicaoProbeTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsulta(CodexDbContext contexto)
        => new(contexto, new ConfigurationBuilder().Build());

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    private static async Task<int> IndexarItemAsync(
        CodexDbContext contexto,
        string pesoA,
        string? titulo = null,
        TipoItemIndexado tipoItem = TipoItemIndexado.erro)
    {
        var itemId = Random.Shared.Next(1_000_000, int.MaxValue);
        var item = ItemBusca.Criar(tipoItem, itemId, titulo ?? $"Item {itemId}", $"/erros/item-{itemId}");
        item.AtualizarPesos(pesoA, null, null, null);
        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);
        await contexto.SaveChangesAsync();
        return itemId;
    }

    // ------------------------------------------------------- transição FORTE → PARCIAL

    /// <summary>
    /// Dois tokens, só um indexado: o AND do passo FORTE não casa, o OR do PARCIAL casa. A
    /// qualidade tem de descer para "parcial" — e continuar achando o item.
    /// </summary>
    [Fact]
    public async Task Cascata_UmTokenDeDoisNaoExiste_DesceParaParcialEAindaAcha()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarItemAsync(setup, $"casoparc{sufixo}");

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"casoparc{sufixo} zzausente{sufixo}", null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("parcial",
            because: "o passo FORTE é AND — faltando um token ele falha e a cascata desce para o OR do PARCIAL");
        resultado.TotalGeral.Should().Be(1);
        resultado.Correcao.Should().BeNull(because: "PARCIAL casou, então a autocorreção nem deve ser tentada");
    }

    /// <summary>Os dois tokens indexados: o passo FORTE resolve e a qualidade NÃO pode descer.</summary>
    [Fact]
    public async Task Cascata_TodosOsTokensExistem_PermaneceExata()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarItemAsync(setup, $"casoforte{sufixo} outroforte{sufixo}");

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"casoforte{sufixo} outroforte{sufixo}", null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("exata");
        resultado.TotalGeral.Should().Be(1);
    }

    // ------------------------------------------------------- transição → VAZIA

    /// <summary>
    /// Termo que não casa em nenhum passo e não tem vizinho no léxico: qualidade "vazia",
    /// zero grupos, e ainda assim resposta de sucesso (P1 nunca 404 para busca sem resultado).
    /// </summary>
    [Fact]
    public async Task Cascata_TermoSemNenhumCasamentoNemVizinhoNoLexico_TerminaEmVazia()
    {
        var sufixo = NovoSufixo();

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"qqzzwwxx{sufixo}", null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("vazia");
        resultado.TotalGeral.Should().Be(0);
        resultado.Grupos.Should().BeEmpty();
        resultado.Correcao.Should().BeNull(because: "não houve autocorreção aceita — nunca corrige em silêncio");
    }

    // ------------------------------------------------------- adversarial: filtro de tipo

    /// <summary>
    /// <c>?tipo=</c> com valor que não existe no enum. Duas leituras possíveis: rejeitar (400) ou
    /// devolver vazio. O que NÃO é defensável é ignorar o filtro e devolver tudo — o cliente pediu
    /// um recorte e recebeu o conjunto inteiro achando que é o recorte.
    /// </summary>
    [Fact]
    public async Task FiltroDeTipo_ComValorInexistente_NaoPodeSerSilenciosamenteIgnorado()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarItemAsync(setup, $"casotipo{sufixo}");

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"casotipo{sufixo}", null, ["tipoinexistente"], 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.TotalGeral.Should().Be(0,
            because: "filtrar por um tipo que não existe não pode devolver itens de OUTROS tipos — "
                     + "o filtro inteiro está sendo descartado quando nenhum valor sobrevive à validação");
    }

    // ------------------------------------------------------- adversarial: delimitador no conteúdo

    /// <summary>
    /// Os fragmentos de destaque são montados com <c>ts_headline</c> usando os delimitadores
    /// ⟦/⟧ e depois reconstruídos por <c>Split</c> + PARIDADE do índice
    /// (ConsultaBuscaPostgres.MontarTrechos). Se o próprio conteúdo editorial contiver um desses
    /// caracteres, a paridade desloca e o destaque passa a marcar texto que não casou com a busca.
    /// </summary>
    [Fact]
    public async Task Trechos_ConteudoQueContemODelimitadorDeDestaque_NaoCorrompeAParidadeDoDestaque()
    {
        var sufixo = NovoSufixo();
        var termo = $"casodelim{sufixo}";

        await using (var setup = _fixture.CriarContexto())
            await IndexarItemAsync(setup, $"prefixo ⟦ intruso {termo} sufixo");

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            termo, null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.TotalGeral.Should().Be(1);
        var trechos = resultado.Grupos.Single().Itens.Single().Trechos;

        var destacados = trechos.Where(t => t.Destacado).Select(t => t.Texto).ToList();

        destacados.Should().NotBeEmpty(because: "o termo casou, então algo tem de estar destacado");
        destacados.Should().AllSatisfy(
            texto => texto.Should().Contain(sufixo,
                because: "só o que casou com o termo pode vir destacado — se um trecho sem o termo aparece "
                         + "destacado, a paridade do Split foi deslocada pelo delimitador presente no conteúdo"));
    }
}
