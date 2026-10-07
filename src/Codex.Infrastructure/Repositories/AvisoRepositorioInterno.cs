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

    public async Task<Aviso?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        var query = Contexto.Avisos.Where(a => a.Id == id && a.DesativadoEm == null);

        if (!rastreada)
            query = query.AsNoTracking();

        var aviso = await query.FirstOrDefaultAsync(ct);

        if (aviso is not null)
            await PreencherCamposAfetadosAsync([aviso], ct);

        return aviso;
    }

    public async Task<IReadOnlyCollection<Aviso>> ListarPorProvedorAsync(int provedorId, CancellationToken ct) =>
        await Contexto.Avisos.AsNoTracking()
            .Where(a => a.ProvedorId == provedorId && a.DesativadoEm == null)
            .OrderByDescending(a => a.VigenciaInicio)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<Aviso>> ListarPorVersaoAsync(int versaoProvedorId, CancellationToken ct)
    {
        var avisos = await Contexto.Avisos.AsNoTracking()
            .Where(a => a.VersaoProvedorId == versaoProvedorId && a.DesativadoEm == null)
            .OrderByDescending(a => a.VigenciaInicio)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        await PreencherCamposAfetadosAsync(avisos, ct);

        return avisos;
    }

    public async Task<IReadOnlyCollection<Aviso>> ListarTodosAtivosAsync(CancellationToken ct) =>
        await Contexto.Avisos.AsNoTracking().Where(a => a.DesativadoEm == null).ToListAsync(ct);

    public async Task<IReadOnlyCollection<int>> ListarCamposAtivosDaVersaoAsync(
        int versaoProvedorId, IReadOnlyCollection<int> campoVersaoIds, CancellationToken ct)
    {
        if (campoVersaoIds.Count == 0)
            return [];

        var ids = campoVersaoIds.Distinct().ToArray();

        return await Contexto.CamposVersao.AsNoTracking()
            .Where(c => c.VersaoProvedorId == versaoProvedorId && c.DesativadoEm == null && ids.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task SubstituirCamposAfetadosAsync(int avisoId, IReadOnlyCollection<int> campoVersaoIds, CancellationToken ct)
    {
        var existentes = await Contexto.AvisosCampos.Where(ac => ac.AvisoId == avisoId).ToListAsync(ct);
        Contexto.AvisosCampos.RemoveRange(existentes);
        await Contexto.AvisosCampos.AddRangeAsync(campoVersaoIds.Distinct().Select(id => AvisoCampo.Criar(avisoId, id)), ct);
    }

    public async Task AdicionarAsync(Aviso aviso, CancellationToken ct) =>
        await Contexto.Avisos.AddAsync(aviso, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);

    /// <summary>
    /// Uma consulta para todos os avisos de versão da lista. Só entram campos ativos: o vínculo de
    /// um campo removido continua na junção (soft delete), mas não é mais "afetado" por nada
    /// visível, e devolvê-lo faria o PUT seguinte reenviar um id que a validação recusa.
    /// </summary>
    private async Task PreencherCamposAfetadosAsync(IReadOnlyCollection<Aviso> avisos, CancellationToken ct)
    {
        var idsDeAvisosDeVersao = avisos.Where(a => a.VersaoProvedorId is not null).Select(a => a.Id).ToArray();
        if (idsDeAvisosDeVersao.Length == 0)
            return;

        var vinculos = await (
            from ac in Contexto.AvisosCampos.AsNoTracking()
            join cv in Contexto.CamposVersao.AsNoTracking() on ac.CampoVersaoId equals cv.Id
            where idsDeAvisosDeVersao.Contains(ac.AvisoId) && ac.DesativadoEm == null && cv.DesativadoEm == null
            select new { ac.AvisoId, ac.CampoVersaoId }
        ).ToListAsync(ct);

        var campoIdsPorAviso = vinculos
            .GroupBy(v => v.AvisoId)
            .ToDictionary(g => g.Key, g => g.Select(v => v.CampoVersaoId).ToArray());

        foreach (var aviso in avisos.Where(a => a.VersaoProvedorId is not null))
            aviso.SubstituirCamposAfetados(campoIdsPorAviso.GetValueOrDefault(aviso.Id, []));
    }
}
