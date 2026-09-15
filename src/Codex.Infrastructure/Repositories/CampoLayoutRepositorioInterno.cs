using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="ICampoLayoutRepositorioInterno"/> (I16/I17a/I17b/I17c).</summary>
public sealed class CampoLayoutRepositorioInterno : RepositorioBase<CodexDbContext>, ICampoLayoutRepositorioInterno
{
    public CampoLayoutRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<CampoLayout?> ObterPorIdAsync(int id, bool rastreada, bool comOrigemCarregada, CancellationToken ct)
    {
        IQueryable<CampoLayout> query = Contexto.CamposLayout.Where(c => c.Id == id && c.DesativadoEm == null);

        if (comOrigemCarregada)
            query = query.Include(c => c.VersaoLayoutOrigem);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    public Task<IReadOnlyCollection<CampoLayoutResponseDto>> ListarPorVersaoLayoutAsync(int versaoLayoutId, CancellationToken ct) =>
        MaterializarAsync(Contexto.CamposLayout.AsNoTracking()
            .Where(c => c.VersaoLayoutId == versaoLayoutId && c.DesativadoEm == null)
            .OrderBy(c => c.Ordem), ct);

    public async Task<IReadOnlyCollection<CampoLayout>> ListarTodosAtivosAsync(CancellationToken ct) =>
        await Contexto.CamposLayout.AsNoTracking().Where(c => c.DesativadoEm == null).ToListAsync(ct);

    public async Task AdicionarAsync(CampoLayout campoLayout, CancellationToken ct) =>
        await Contexto.CamposLayout.AddAsync(campoLayout, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);

    private static async Task<IReadOnlyCollection<CampoLayoutResponseDto>> MaterializarAsync(IQueryable<CampoLayout> query, CancellationToken ct) =>
        await query.Select(cl => new CampoLayoutResponseDto
        {
            Id = cl.Id,
            VersaoLayoutId = cl.VersaoLayoutId,
            VersaoLayoutDescontinuadoId = cl.VersaoLayoutDescontinuadoId,
            Nome = cl.Nome,
            CaminhoXml = cl.CaminhoXml,
            Significado = cl.Significado,
            Grupo = cl.Grupo,
            Ordem = cl.Ordem,
            Slug = cl.Slug,
            Estado = cl.Estado.ToString(),
            Publicado = cl.Publicado,
            VersaoConcorrencia = cl.Versao,
        }).ToListAsync(ct);
}
