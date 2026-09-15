using Codex.Application.Interfaces;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>
/// Implementação de <see cref="IUnidadeDeTrabalho"/> sobre <see cref="CodexDbContext"/> — ver
/// interface para o porquê. Vive em <c>Repositories/</c> (não em <c>Data/</c>) porque é escrita
/// interna (BACK-4); <c>Data/</c> está sob edição concorrente de outra unidade (FIX-SCHEMA) nesta
/// rodada.
/// </summary>
public sealed class UnidadeDeTrabalhoInterna : IUnidadeDeTrabalho
{
    private readonly CodexDbContext _contexto;

    public UnidadeDeTrabalhoInterna(CodexDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task ExecutarAsync(Func<CancellationToken, Task> operacao, CancellationToken ct)
    {
        var estrategia = _contexto.Database.CreateExecutionStrategy();

        await estrategia.ExecuteAsync(async () =>
        {
            await using var transacao = await _contexto.Database.BeginTransactionAsync(ct);
            await operacao(ct);
            await transacao.CommitAsync(ct);
        });
    }
}
