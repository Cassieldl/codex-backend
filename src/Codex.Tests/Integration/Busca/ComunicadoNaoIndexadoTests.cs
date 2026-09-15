using Codex.Domain.Entities;
using Codex.Domain.Enums;
using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// Regressão de PQ-05 (arquitetura.md §6, decisoes.md M-1) — comunicado NUNCA aciona
/// <c>IIndiceBuscaWriter</c> nem gera linha em <c>codex.itensbusca</c>, em nenhuma transição de
/// estado. Não há <c>TipoItemBusca.Comunicado</c> — a ausência do valor no enum já impede a
/// chamada de compilar para esse tipo, mas quem "conserta por simetria" sem ler a decisão
/// (M-1: <c>visivelpublico</c> é coluna GERADA que exige expressão IMMUTABLE, e
/// <c>expiraem &gt; now()</c> não é) poderia introduzir uma via alternativa. Este teste prova
/// pela AUSÊNCIA de efeito colateral, não pela leitura do enum.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ComunicadoNaoIndexadoTests
{
    private readonly PostgresContainerFixture _fixture;

    public ComunicadoNaoIndexadoTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<long> ContarLinhasDeItensBuscaAsync()
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM codex.itensbusca";

        return (long)(await comando.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task CriarPublicarEDespublicarComunicado_NuncaCriaLinhaEmItensBusca()
    {
        var antes = await ContarLinhasDeItensBuscaAsync();

        int comunicadoId;
        await using (var contexto = _fixture.CriarContexto())
        {
            var comunicado = Comunicado.CriarGlobal(
                $"Comunicado não indexado {Guid.NewGuid():N}", "corpo do comunicado", Severidade.Critico,
                DateTimeOffset.UtcNow.AddDays(1));
            contexto.Comunicados.Add(comunicado);
            await contexto.SaveChangesAsync();
            comunicadoId = comunicado.Id;
        }

        await using (var contexto = _fixture.CriarContexto())
        {
            var comunicadoRastreado = await contexto.Comunicados.FindAsync(comunicadoId);
            comunicadoRastreado!.Publicar();
            await contexto.SaveChangesAsync();
        }

        await using (var contexto = _fixture.CriarContexto())
        {
            var comunicadoRastreado = await contexto.Comunicados.FindAsync(comunicadoId);
            comunicadoRastreado!.Despublicar();
            await contexto.SaveChangesAsync();
        }

        var depois = await ContarLinhasDeItensBuscaAsync();

        depois.Should().Be(antes,
            because: "M-1: comunicado é transitório e não tem coluna imutável de expiração — " +
                     "nenhuma transição de publicação pode gerar/atualizar linha em itensbusca");
    }
}
