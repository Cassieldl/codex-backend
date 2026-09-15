using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Busca;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// Regressão histórica documentada na análise de backend: a cascata FORTE → PARCIAL → APROX. →
/// VAZIA precisa rodar SEQUENCIAL no mesmo <see cref="Codex.Infrastructure.Data.CodexDbContext"/>
/// (nunca <c>Task.WhenAll</c>/paralela) — se algum passo disparasse concorrentemente, o EF
/// lançaria <see cref="InvalidOperationException"/> ("a second operation was started..."). Este
/// teste força a cascata a percorrer os três passos reais (termo com typo, sem casar em FORTE
/// nem PARCIAL, precisando de autocorreção) e prova que nenhuma exceção de concorrência escapa.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class CascataSequencialTests
{
    private readonly PostgresContainerFixture _fixture;

    public CascataSequencialTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BuscarAsync_TermoComTypoForcandoOsQuatroPassos_NaoLancaExcecaoDeConcorrencia()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];
        var termoCorreto = $"aliquotacascata{sufixo}";
        // Um único caractere trocado — dentro da guarda de levenshtein <= 2 e acima do limiar de similaridade.
        var termoComTypo = $"aliqotacascata{sufixo}";

        // itemId aleatório e alto — a tabela é compartilhada por todos os testes da coleção
        // Postgres (mesmo container), então itemid=1 fixo colidiria com uq_codex_itensbusca_tipoitem_itemid.
        var itemIdUnico = Random.Shared.Next(1_000_000, int.MaxValue);

        await using (var contextoSetup = _fixture.CriarContexto())
        {
            var item = ItemBusca.Criar(TipoItemIndexado.erro, itemIdUnico, titulo: "Erro Cascata Sequencial", url: "/erros/cascata-sequencial", chave: "E-CASCATA");
            item.AtualizarPesos(termoCorreto, null, null, null);
            item.MarcarPublicado(true);
            contextoSetup.ItensBusca.Add(item);
            await contextoSetup.SaveChangesAsync();

            // Popula o léxico com o termo correto — sem isso não há candidato de autocorreção.
            await contextoSetup.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO codex.termosbusca (termo, ocorrencias) VALUES ({termoCorreto}, 5) ON CONFLICT (termo) DO NOTHING");
        }

        await using var contexto = _fixture.CriarContexto();
        var consulta = new ConsultaBuscaPostgres(contexto, new ConfigurationBuilder().Build());

        var acao = async () => await consulta.BuscarAsync(termoComTypo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: false, CancellationToken.None);

        await acao.Should().NotThrowAsync<InvalidOperationException>(
            because: "a cascata FORTE → PARCIAL → APROX. precisa rodar sequencialmente no mesmo DbContext, nunca em paralelo");

        var resultado = await consulta.BuscarAsync(termoComTypo, provedorSlug: null, tipos: null, pagina: 1, tamanhoPagina: 20, incluirRascunho: false, CancellationToken.None);

        resultado.Qualidade.Should().Be("aproximada", because: "o termo com typo só casa depois de passar pelos três passos e chegar à autocorreção");
        resultado.Correcao.Should().NotBeNull();
        resultado.Grupos.SelectMany(g => g.Itens).Should().Contain(i => i.Titulo == "Erro Cascata Sequencial");
    }
}
