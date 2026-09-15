using Codex.Application.Services;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Repositories;
using FluentAssertions;
using Npgsql;

namespace Codex.Tests.Integration.Interno;

/// <summary>
/// Job de expiração, metade da regra (analise-backend.md §10.2/§10.4) — contra Postgres real,
/// porque o que se prova aqui é o <c>UPDATE</c> e o seu predicado, não a intenção do C#.
///
/// <para>
/// Cada caso usa uma <b>referência própria, numa época distante no passado</b>, e faz uma varredura
/// antes do Act. Sem isso, a contagem de linhas convergidas dependeria do que outras classes de
/// teste deixaram no banco compartilhado, e a asserção "convergiu exatamente 1" viraria intermitente.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ExpiracaoComunicadosServiceTests
{
    private readonly PostgresContainerFixture _fixture;

    public ExpiracaoComunicadosServiceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecutarAsync_DespublicaSomenteOsVencidosEPublicados()
    {
        var referencia = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await VarrerAsync(referencia);

        var vencidoPublicado = await InserirAsync("A-vencido-publicado", referencia.AddMinutes(-1), publicado: true);
        var vigentePublicado = await InserirAsync("B-vigente-publicado", referencia.AddHours(1), publicado: true);
        var vencidoRascunho = await InserirAsync("C-vencido-rascunho", referencia.AddMinutes(-1), publicado: false);

        var convergidos = await ExecutarAsync(referencia);

        convergidos.Should().Be(1,
            because: "só A muda de estado — B ainda não venceu e C já estava despublicado");

        (await PublicadoAsync(vencidoPublicado)).Should().BeFalse(because: "A venceu e estava no ar");
        (await PublicadoAsync(vigentePublicado)).Should().BeTrue(because: "despublicar o que ainda vale tiraria conteúdo válido do ar");
        (await PublicadoAsync(vencidoRascunho)).Should().BeFalse(because: "rascunho vencido continua rascunho");
    }

    [Fact]
    public async Task ExecutarAsync_ChamadoDuasVezes_SegundaConvergeZero()
    {
        var referencia = new DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await VarrerAsync(referencia);

        await InserirAsync("idempotencia-1", referencia.AddMinutes(-1), publicado: true);
        await InserirAsync("idempotencia-2", referencia.AddMinutes(-1), publicado: true);

        (await ExecutarAsync(referencia)).Should().Be(2, because: "as duas linhas vencidas convergem na primeira passada");
        (await ExecutarAsync(referencia)).Should().Be(0,
            because: "a condição é sobre o estado atual da linha, não sobre 'já processei este id' — rodar de novo não pode contar de novo");
    }

    [Fact]
    public async Task ExecutarAsync_IgnoraComunicadoDesativado()
    {
        var referencia = new DateTimeOffset(2003, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await VarrerAsync(referencia);

        var removido = await InserirAsync("removido-vencido", referencia.AddMinutes(-1), publicado: true, desativado: true);

        (await ExecutarAsync(referencia)).Should().Be(0,
            because: "comunicado removido saiu do acervo — reescrevê-lo só produziria escrita inútil e ruído de auditoria");

        (await PublicadoAsync(removido)).Should().BeTrue(because: "a linha desativada não foi tocada pelo UPDATE");
    }

    [Fact]
    public async Task ExecutarAsync_UsaAReferenciaRecebida_NaoORelogioDaMaquina()
    {
        var referencia = new DateTimeOffset(2004, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await VarrerAsync(referencia);

        // Já vencido pelo relógio real (ontem), mas ainda vigente para a referência recebida.
        var id = await InserirAsync("referencia-manda", DateTimeOffset.UtcNow.AddDays(-1), publicado: true);

        (await ExecutarAsync(referencia)).Should().Be(0);
        (await PublicadoAsync(id)).Should().BeTrue(
            because: "quem decide o que venceu é a referência recebida — ler DateTimeOffset.UtcNow dentro do método tornaria o job intestável");
    }

    // ------------------------------------------------------------------ apoio

    private async Task<int> ExecutarAsync(DateTimeOffset referencia)
    {
        await using var contexto = _fixture.CriarContexto();
        var servico = new ExpiracaoComunicadosService(new ComunicadoRepositorioInterno(contexto));

        return await servico.ExecutarAsync(referencia, CancellationToken.None);
    }

    /// <summary>Converge o que já existia no banco compartilhado, para a contagem do Act ser só das linhas deste teste.</summary>
    private Task VarrerAsync(DateTimeOffset referencia) => ExecutarAsync(referencia);

    private async Task<int> InserirAsync(string rotulo, DateTimeOffset expiraEm, bool publicado, bool desativado = false)
    {
        await using var contexto = _fixture.CriarContexto();

        var comunicado = Comunicado.CriarGlobal(
            $"Expiração {rotulo} {Guid.NewGuid():N}", "corpo do comunicado", Severidade.Alerta, expiraEm);

        if (publicado)
            comunicado.Publicar();

        if (desativado)
            comunicado.Desativar();

        contexto.Comunicados.Add(comunicado);
        await contexto.SaveChangesAsync();

        return comunicado.Id;
    }

    private async Task<bool> PublicadoAsync(int comunicadoId)
    {
        await using var conexao = new NpgsqlConnection(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT publicado FROM codex.comunicados WHERE comunicadoid = @id";
        comando.Parameters.AddWithValue("id", comunicadoId);

        return (bool)(await comando.ExecuteScalarAsync())!;
    }
}
