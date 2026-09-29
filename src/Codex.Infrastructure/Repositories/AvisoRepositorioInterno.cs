using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IAvisoRepositorioInterno"/> — enxerga rascunho e publicado.</summary>
public sealed class AvisoRepositorioInterno : RepositorioBase<CodexDbContext>, IAvisoRepositorioInterno
{
    public AvisoRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<Aviso?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        var query = Contexto.Avisos.Where(a => a.Id == id && a.DesativadoEm == null);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyCollection<Aviso>> ListarPorProvedorAsync(int provedorId, CancellationToken ct) =>
        await Contexto.Avisos.AsNoTracking()
            .Where(a => a.ProvedorId == provedorId && a.DesativadoEm == null)
            .OrderByDescending(a => a.VigenciaInicio)
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<Aviso>> ListarPorVersaoAsync(int versaoProvedorId, CancellationToken ct) =>
        await Contexto.Avisos.AsNoTracking()
            .Where(a => a.VersaoProvedorId == versaoProvedorId && a.DesativadoEm == null)
            .OrderByDescending(a => a.VigenciaInicio)
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<Aviso>> ListarTodosAtivosAsync(CancellationToken ct) =>
        await Contexto.Avisos.AsNoTracking().Where(a => a.DesativadoEm == null).ToListAsync(ct);

    public async Task AdicionarAsync(Aviso aviso, CancellationToken ct) =>
        await Contexto.Avisos.AddAsync(aviso, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);
}
