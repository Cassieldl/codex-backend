using Codex.Domain.Entities;
using Codex.Domain.Exceptions;
using Codex.Infrastructure.Data;
using Codex.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Codex.Tests.Integration;
using FluentAssertions;

namespace Codex.Tests.Integration.Persistencia;

/// <summary>
/// Prova que colisão de <c>ordem</c> em <c>versoeslayout</c> (uq_codex_versoeslayout_ordem) é
/// um erro REAL do banco (23505), traduzido por <see cref="RepositorioBase{TDbContext}"/> para
/// <see cref="RegistroDuplicadoException"/> — nunca uma checagem prévia em memória que poderia
/// divergir do banco sob concorrência.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ReordenarVersaoLayoutTests
{
    private readonly PostgresContainerFixture _fixture;

    public ReordenarVersaoLayoutTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class RepositorioDeTeste : RepositorioBase<CodexDbContext>
    {
        public RepositorioDeTeste(CodexDbContext contexto) : base(contexto)
        {
        }

        public Task SalvarAsyncPublico(CancellationToken ct) => SalvarAsync(ct);
    }

    [Fact]
    public async Task ReordenarPara_ColidindoComOrdemExistente_LancaRegistroDuplicadoException()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        await using var contextoSetup = _fixture.CriarContexto();
        var maiorOrdemAtual = contextoSetup.VersoesLayout.Any()
            ? contextoSetup.VersoesLayout.Max(v => v.Ordem)
            : 0;

        var ordemA = maiorOrdemAtual + 1;
        var ordemB = maiorOrdemAtual + 2;

        var versaoA = VersaoLayout.Criar($"Layout A {sufixo}", ordemA);
        var versaoB = VersaoLayout.Criar($"Layout B {sufixo}", ordemB);

        contextoSetup.VersoesLayout.AddRange(versaoA, versaoB);
        await contextoSetup.SaveChangesAsync();

        await using var contexto = _fixture.CriarContexto();
        var repositorio = new RepositorioDeTeste(contexto);

        var versaoBRastreada = await contexto.VersoesLayout.FirstAsync(v => v.Id == versaoB.Id);
        versaoBRastreada.ReordenarPara(ordemA); // colide com a ordem de versaoA

        var acao = async () => await repositorio.SalvarAsyncPublico(CancellationToken.None);

        await acao.Should().ThrowAsync<RegistroDuplicadoException>(
            because: "uq_codex_versoeslayout_ordem deve rejeitar a colisão — erro real de banco, não checagem em memória");
    }
}
