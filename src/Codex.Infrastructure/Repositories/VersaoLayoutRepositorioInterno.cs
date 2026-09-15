using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IVersaoLayoutRepositorioInterno"/> (I15a-I15d).</summary>
public sealed class VersaoLayoutRepositorioInterno : RepositorioBase<CodexDbContext>, IVersaoLayoutRepositorioInterno
{
    public VersaoLayoutRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<VersaoLayout?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        var query = Contexto.VersoesLayout.Where(v => v.Id == id && v.DesativadoEm == null);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    public async Task<PaginaResultado<VersaoLayoutAdminDto>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var query = Contexto.VersoesLayout.AsNoTracking().Where(v => v.DesativadoEm == null);

        var totalCount = await query.CountAsync(ct);

        var itens = await query
            .OrderBy(v => v.Ordem)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(v => new VersaoLayoutAdminDto
            {
                Id = v.Id,
                Rotulo = v.Rotulo,
                Slug = v.Slug,
                Estado = v.Estado.ToString(),
                Ordem = v.Ordem,
                Publicado = v.Publicado,
                Descricao = v.Descricao,
                VersaoConcorrencia = v.Versao,
            })
            .ToListAsync(ct);

        return new PaginaResultado<VersaoLayoutAdminDto>(itens, totalCount, pagina, tamanhoPagina);
    }

    public async Task<int> ObterProximaOrdemAsync(CancellationToken ct)
    {
        var maiorOrdem = await Contexto.VersoesLayout.AsNoTracking()
            .Select(v => (int?)v.Ordem)
            .MaxAsync(ct);

        return (maiorOrdem ?? 0) + 1;
    }

    public async Task AdicionarAsync(VersaoLayout versaoLayout, CancellationToken ct) =>
        await Contexto.VersoesLayout.AddAsync(versaoLayout, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);
}
