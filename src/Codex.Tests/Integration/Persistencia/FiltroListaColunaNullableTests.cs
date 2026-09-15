using Codex.Domain.Entities;
using Codex.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Persistencia;

/// <summary>
/// AP-BACKEND-002: <c>Contains</c> direto sobre coluna nullable já não traduziu em cenários
/// passados — o padrão correto é <c>x.Campo.HasValue &amp;&amp; lista.Contains(x.Campo.Value)</c>.
/// Este teste prova, contra Postgres real, que o padrão correto traduz e filtra certo (nunca
/// InMemory — AP-BACKEND-005).
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class FiltroListaColunaNullableTests
{
    private readonly PostgresContainerFixture _fixture;

    public FiltroListaColunaNullableTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Contains_EmColunaNullable_ComPadraoHasValue_TraduzECorretamenteFiltra()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var maiorOrdemAtual = contextoSetup.VersoesLayout.Any() ? contextoSetup.VersoesLayout.Max(v => v.Ordem) : 0;

        var versaoOrigem = VersaoLayout.Criar($"Origem {sufixo}", maiorOrdemAtual + 1);
        var versaoDescontinuacao = VersaoLayout.Criar($"Descontinuacao {sufixo}", maiorOrdemAtual + 2);
        contextoSetup.VersoesLayout.AddRange(versaoOrigem, versaoDescontinuacao);
        await contextoSetup.SaveChangesAsync();

        var campoAtivo = CampoLayout.Criar(versaoOrigem.Id, $"CampoAtivo{sufixo}", $"caminho/ativo/{sufixo}", "significado", 1);
        var campoDescontinuado = CampoLayout.Criar(versaoOrigem.Id, $"CampoDescontinuado{sufixo}", $"caminho/descontinuado/{sufixo}", "significado", 2);
        contextoSetup.CamposLayout.AddRange(campoAtivo, campoDescontinuado);
        await contextoSetup.SaveChangesAsync();

        var campoDescontinuadoRastreado = await contextoSetup.CamposLayout.Include(c => c.VersaoLayoutOrigem).FirstAsync(c => c.Id == campoDescontinuado.Id);
        var versaoDescontinuacaoRastreada = await contextoSetup.VersoesLayout.FirstAsync(v => v.Id == versaoDescontinuacao.Id);
        campoDescontinuadoRastreado.Descontinuar(versaoDescontinuacaoRastreada);
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var idsAlvo = new[] { versaoDescontinuacao.Id };

        // Padrão correto (AP-BACKEND-002) — nunca `idsAlvo.Contains(c.VersaoLayoutDescontinuadoId)` puro.
        var resultado = await contexto.CamposLayout
            .Where(c => c.VersaoLayoutDescontinuadoId.HasValue && idsAlvo.Contains(c.VersaoLayoutDescontinuadoId.Value))
            .ToListAsync();

        resultado.Should().ContainSingle(c => c.Id == campoDescontinuado.Id);
        resultado.Should().NotContain(c => c.Id == campoAtivo.Id,
            because: "campo ainda Ativo tem VersaoLayoutDescontinuadoId nulo — não deve casar com o filtro");
    }
}
