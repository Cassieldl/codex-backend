using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IVersaoProvedorRepositorioInterno"/> — enxerga rascunho (I6, I8a-I10).</summary>
public sealed class VersaoProvedorRepositorioInterno : RepositorioBase<CodexDbContext>, IVersaoProvedorRepositorioInterno
{
    public VersaoProvedorRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<VersaoProvedor?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        var query = Contexto.VersoesProvedor.Where(v => v.Id == id && v.DesativadoEm == null);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyCollection<VersaoAdminListaItemDto>> ListarPorProvedorAsync(int provedorId, CancellationToken ct) =>
        await Contexto.VersoesProvedor.AsNoTracking()
            .Where(v => v.ProvedorId == provedorId && v.DesativadoEm == null)
            .OrderByDescending(v => v.CriadoEm)
            .Select(v => new VersaoAdminListaItemDto
            {
                Id = v.Id,
                ProvedorId = v.ProvedorId,
                Slug = v.Slug,
                Rotulo = v.Rotulo,
                VersaoLayoutId = v.VersaoLayoutId,
                Publicado = v.Publicado,
                VerificadoEm = v.VerificadoEm,
                VersaoConcorrencia = v.Versao,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<VersaoProvedor>> ListarTodosAtivosAsync(CancellationToken ct) =>
        await Contexto.VersoesProvedor.AsNoTracking().Where(v => v.DesativadoEm == null).ToListAsync(ct);

    public async Task AdicionarAsync(VersaoProvedor versao, CancellationToken ct) =>
        await Contexto.VersoesProvedor.AddAsync(versao, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);
}
