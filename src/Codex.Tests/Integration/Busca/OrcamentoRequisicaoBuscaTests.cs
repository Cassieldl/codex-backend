using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using Codex.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// MED-B05 (arquitetura.md §5.E) — <c>statement_timeout</c> protege CADA statement, não a
/// cascata inteira; a cascata de autocorreção dispara até 18 statements numa única requisição
/// anônima. <c>Busca:OrcamentoRequisicaoMs</c> é um orçamento de PAREDE por requisição: estourado,
/// a cascata para no passo em que está e devolve o melhor resultado já obtido — nunca lança, e
/// nunca "500"/"499" equivalente (aqui: nenhuma exceção não tratada escapa de <c>BuscarAsync</c>).
///
/// <para>
/// Orçamento de 1ms força o estouro de forma determinística sem depender de mock de relógio: uma
/// única volta de rede até o Postgres (mesmo local, via Testcontainers) já excede 1ms — o teste
/// prova o COMPORTAMENTO de segurança (nunca lança, resultado sempre consistente), não o valor
/// exato de qualidade, que depende de qual passo terminou por último.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class OrcamentoRequisicaoBuscaTests
{
    private readonly PostgresContainerFixture _fixture;

    public OrcamentoRequisicaoBuscaTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static ConsultaBuscaPostgres CriarConsultaComOrcamentoMinimo(CodexDbContext contexto)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Busca:OrcamentoRequisicaoMs"] = "1" })
            .Build();

        return new ConsultaBuscaPostgres(contexto, configuracao);
    }

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task BuscarAsync_ComOrcamentoDeParedeEstourado_NuncaLancaEDevolveResultadoConsistente()
    {
        var sufixo = NovoSufixo();
        var termoCorreto = $"orcamentobusca{sufixo}";
        // Typo forçando FORTE → PARCIAL → autocorreção — maximiza o número de statements na cascata.
        var termoComTypo = $"orcametobusca{sufixo}";

        await using (var setup = _fixture.CriarContexto())
        {
            var item = ItemBusca.Criar(TipoItemIndexado.erro, Random.Shared.Next(1_000_000, int.MaxValue),
                "Item Orçamento", "/erros/orcamento", chave: "E-ORCAMENTO");
            item.AtualizarPesos(termoCorreto, null, null, null);
            item.MarcarPublicado(true);
            setup.ItensBusca.Add(item);
            await setup.SaveChangesAsync();

            await setup.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO codex.termosbusca (termo, ocorrencias) VALUES ({termoCorreto}, 5) ON CONFLICT (termo) DO NOTHING");
        }

        await using var contexto = _fixture.CriarContexto();
        var consulta = CriarConsultaComOrcamentoMinimo(contexto);

        var acao = async () => await consulta.BuscarAsync(
            termoComTypo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: false, CancellationToken.None);

        var resultado = await acao.Should().NotThrowAsync(
            because: "MED-B05: orçamento estourado nunca pode propagar exceção — o consumidor HTTP devolveria 500, nunca aceitável para busca anônima");

        resultado.Subject.Should().NotBeNull();
        resultado.Subject.Qualidade.Should().BeOneOf("exata", "parcial", "aproximada", "vazia");

        // Consistência: TotalGeral zero implica qualidade "vazia" (nunca um estado misto onde
        // o orçamento cortou no meio e deixou grupos parciais sem corresponder ao total).
        if (resultado.Subject.TotalGeral == 0)
            resultado.Subject.Qualidade.Should().Be("vazia");
    }

    /// <summary>Guarda de não-regressão: com orçamento generoso, a mesma busca continua achando o item via autocorreção normalmente.</summary>
    [Fact]
    public async Task BuscarAsync_ComOrcamentoPadrao_AindaEncontraViaAutocorrecao()
    {
        var sufixo = NovoSufixo();
        var termoCorreto = $"orcamentofolgado{sufixo}";
        var termoComTypo = $"orcametofolgado{sufixo}";

        await using (var setup = _fixture.CriarContexto())
        {
            var item = ItemBusca.Criar(TipoItemIndexado.erro, Random.Shared.Next(1_000_000, int.MaxValue),
                "Item Orçamento Folgado", "/erros/orcamento-folgado", chave: "E-ORCAMENTO-FOLGADO");
            item.AtualizarPesos(termoCorreto, null, null, null);
            item.MarcarPublicado(true);
            setup.ItensBusca.Add(item);
            await setup.SaveChangesAsync();

            await setup.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO codex.termosbusca (termo, ocorrencias) VALUES ({termoCorreto}, 5) ON CONFLICT (termo) DO NOTHING");
        }

        await using var contexto = _fixture.CriarContexto();
        var consulta = new ConsultaBuscaPostgres(contexto, new ConfigurationBuilder().Build());

        var resultado = await consulta.BuscarAsync(
            termoComTypo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("aproximada");
        resultado.TotalGeral.Should().Be(1);
    }
}
