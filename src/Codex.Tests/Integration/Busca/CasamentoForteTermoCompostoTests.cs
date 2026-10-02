using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// O passo FORTE precisa alcançar os dois caminhos de entrada que o escopo funcional declara
/// críticos: o código do erro e o trecho de mensagem. Ambos têm mais de um token, e é aí que a
/// cascata falhava.
///
/// <para>
/// <b>Por que o corpus adversarial não pegava isto.</b> Os testes de busca existentes indexam um
/// documento com POUCAS palavras — quase sempre só o termo que vão procurar. Nesse caso a forma
/// compactada do documento ("aliquotacasocascataXYZ") é IGUAL à forma compactada do termo, então
/// o AND de <c>websearch_to_tsquery</c> se satisfaz e o casamento forte acontece.
/// </para>
///
/// <para>
/// Um item real não é assim: o peso A de um erro traz código, título e todas as variantes de
/// mensagem. A compactada desse documento é o texto inteiro colado num único lexema, que nunca
/// coincide com a compactada de um termo curto — e o AND passava a exigir um lexema impossível.
/// Por isso <b>cada caso aqui indexa um documento longo</b>: é a única forma de o teste ver o
/// defeito.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CasamentoForteTermoCompostoTests
{
    private readonly PostgresContainerFixture _fixture;

    public CasamentoForteTermoCompostoTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsulta(CodexDbContext contexto)
        => new(contexto, new ConfigurationBuilder().Build());

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>
    /// Indexa um item com a forma de um erro real: código, título e variantes no mesmo peso A.
    /// O <paramref name="sufixo"/> isola o caso dos demais que dividem o container.
    /// </summary>
    private static async Task IndexarErroRealistaAsync(CodexDbContext contexto, string codigo, string sufixo)
    {
        var itemId = Random.Shared.Next(1_000_000, int.MaxValue);
        var item = ItemBusca.Criar(TipoItemIndexado.erro, itemId, $"Erro {codigo}", $"/erros/item-{itemId}");

        item.AtualizarPesos(
            $"{codigo} Inscrição municipal do prestador não encontrada casocomposto{sufixo} "
                + "Inscrição municipal inválida ou não cadastrada "
                + "Prestador não localizado no cadastro mobiliário",
            "A inscrição enviada não existe no cadastro do município, ou a empresa está com a inscrição suspensa.",
            null,
            null);

        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);
        await contexto.SaveChangesAsync();
    }

    /// <summary>
    /// O caso que motivou a correção: o analista cola a mensagem da prefeitura palavra por
    /// palavra. Antes, o resultado certo vinha rotulado como casamento parcial — a tela dizia
    /// "nenhum item bateu o termo inteiro" sobre o item que batia exatamente.
    /// </summary>
    [Fact]
    public async Task Busca_VarianteDeMensagemIdentica_CasaForte()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarErroRealistaAsync(setup, "E-1023", sufixo);

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"Inscrição municipal inválida ou não cadastrada casocomposto{sufixo}",
            null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("exata");
        resultado.TotalGeral.Should().Be(1);
    }

    /// <summary>
    /// O código como ele aparece em toda a interface e em todo chamado. O hífen fazia a forma
    /// compactada divergir e derrubava o passo forte.
    /// </summary>
    [Fact]
    public async Task Busca_CodigoComHifen_CasaForte()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarErroRealistaAsync(setup, $"E-{sufixo}", sufixo);

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"E-{sufixo} casocomposto{sufixo}", null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("exata");
        resultado.TotalGeral.Should().Be(1);
    }

    /// <summary>Duas palavras comuns contidas no documento — o caso mais banal de todos.</summary>
    [Fact]
    public async Task Busca_DuasPalavrasContidasNoDocumento_CasaForte()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarErroRealistaAsync(setup, "E-2145", sufixo);

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"inscricao municipal casocomposto{sufixo}", null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("exata");
        resultado.TotalGeral.Should().Be(1);
    }

    /// <summary>
    /// Controle indispensável: a distinção forte × parcial tem de continuar existindo. Sem este
    /// caso, "sempre devolver exata" passaria nos três testes acima e destruiria a cascata
    /// inteira — o aviso de aproximação deixaria de aparecer quando ele é verdadeiro.
    ///
    /// <para>
    /// O termo abaixo tem uma palavra que o documento NÃO contém, então o AND falha, a cascata
    /// cai para o passo parcial e a qualidade precisa refletir isso.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Busca_ComPalavraAusenteNoDocumento_ContinuaParcial()
    {
        var sufixo = NovoSufixo();
        await using (var setup = _fixture.CriarContexto())
            await IndexarErroRealistaAsync(setup, "E-3001", sufixo);

        await using var contexto = _fixture.CriarContexto();
        var resultado = await CriarConsulta(contexto).BuscarAsync(
            $"casocomposto{sufixo} aliquota", null, null, 1, 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("parcial");
        resultado.TotalGeral.Should().Be(1);
    }
}
