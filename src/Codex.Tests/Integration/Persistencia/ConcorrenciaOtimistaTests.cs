using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Persistencia;

/// <summary>
/// Prova o ciclo GET rastreado → mutação → SaveChanges sem <see cref="DbUpdateConcurrencyException"/>
/// falsa, e que conflito real de <c>xmin</c> ainda é detectado. Nunca usar <c>AsNoTracking()</c>
/// no caminho de escrita — ver comentário completo em <c>RepositorioBase</c>.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ConcorrenciaOtimistaTests
{
    private readonly PostgresContainerFixture _fixture;

    public ConcorrenciaOtimistaTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CicloRastreado_GetMutacaoSaveChanges_NaoLancaConflitoFalso()
    {
        int provedorId;
        await using (var contextoCriacao = _fixture.CriarContexto())
        {
            var provedor = Provedor.Criar($"Provedor {Guid.NewGuid()}", $"INV-{Guid.NewGuid():N}", CategoriaProvedor.Nacional);
            contextoCriacao.Provedores.Add(provedor);
            await contextoCriacao.SaveChangesAsync();
            provedorId = provedor.Id;
        }

        // GET RASTREADO — nunca AsNoTracking no caminho de escrita.
        await using var contexto = _fixture.CriarContexto();
        var carregado = await contexto.Provedores.FirstAsync(p => p.Id == provedorId);

        carregado.Renomear("Nome Atualizado");

        var acao = async () => await contexto.SaveChangesAsync();

        await acao.Should().NotThrowAsync<DbUpdateConcurrencyException>(
            because: "entidade rastreada tem xmin real capturado na leitura — falso conflito só acontece com AsNoTracking()+Attach");
    }

    [Fact]
    public async Task ConflitoReal_DuasEdicoesConcorrentes_LancaDbUpdateConcurrencyException()
    {
        int provedorId;
        await using (var contextoCriacao = _fixture.CriarContexto())
        {
            var provedor = Provedor.Criar($"Provedor {Guid.NewGuid()}", $"INV-{Guid.NewGuid():N}", CategoriaProvedor.Nacional);
            contextoCriacao.Provedores.Add(provedor);
            await contextoCriacao.SaveChangesAsync();
            provedorId = provedor.Id;
        }

        await using var contextoA = _fixture.CriarContexto();
        await using var contextoB = _fixture.CriarContexto();

        var provedorA = await contextoA.Provedores.FirstAsync(p => p.Id == provedorId);
        var provedorB = await contextoB.Provedores.FirstAsync(p => p.Id == provedorId);

        provedorA.Renomear("Editado Primeiro");
        await contextoA.SaveChangesAsync(); // xmin avança no banco

        provedorB.Renomear("Editado Segundo — deveria falhar");
        var acao = async () => await contextoB.SaveChangesAsync();

        await acao.Should().ThrowAsync<DbUpdateConcurrencyException>(
            because: "contextoB ainda carrega o xmin antigo — conflito real precisa continuar sendo detectado");
    }
}
