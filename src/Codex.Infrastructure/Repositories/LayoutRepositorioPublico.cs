using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>
/// Implementação de <see cref="ILayoutRepositorioPublico"/> (P10/P11/P12). Junta
/// <see cref="CampoLayout"/> com a sua versão de ORIGEM por <c>Include</c> (navegação mapeada em
/// <c>VersaoLayoutConfiguration.HasMany</c>) quando a entidade cheia é necessária, e por join
/// explícito quando só os dados agregados de M12 importam — nunca <c>Include</c> para a versão de
/// DESCONTINUAÇÃO, que não tem navegação mapeada (só FK).
/// </summary>
public sealed class LayoutRepositorioPublico : RepositorioBase<CodexDbContext>, ILayoutRepositorioPublico
{
    public LayoutRepositorioPublico(CodexDbContext contexto) : base(contexto)
    {
    }

    public async Task<IReadOnlyCollection<VersaoLayout>> ListarVersoesPublicadasAsync(CancellationToken ct) =>
        await Contexto.VersoesLayout.AsNoTracking()
            .Where(v => v.Publicado && v.DesativadoEm == null)
            .ToListAsync(ct);

    public Task<VersaoLayout?> ObterVersaoPorSlugAsync(string versaoSlug, CancellationToken ct) =>
        Contexto.VersoesLayout.AsNoTracking()
            .Where(v => v.Slug == versaoSlug && v.DesativadoEm == null)
            .FirstOrDefaultAsync(ct);

    public Task<VersaoLayout?> ObterVersaoPublicadaPorIdAsync(int id, CancellationToken ct) =>
        Contexto.VersoesLayout.AsNoTracking()
            .Where(v => v.Id == id && v.Publicado && v.DesativadoEm == null)
            .FirstOrDefaultAsync(ct);

    public Task<string?> ObterEstruturaAsync(int versaoLayoutId, CancellationToken ct) =>
        Contexto.EstruturasVersaoLayout.AsNoTracking()
            .Where(e => e.VersaoLayoutId == versaoLayoutId)
            .Select(e => e.Conteudo)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyCollection<CampoLayoutVisivelBrutoDto>> ListarCamposVisiveisAsync(CancellationToken ct) =>
        await (
            from cl in Contexto.CamposLayout.AsNoTracking()
            join origem in Contexto.VersoesLayout.AsNoTracking() on cl.VersaoLayoutId equals origem.Id
            join descontinuado in Contexto.VersoesLayout.AsNoTracking()
                on cl.VersaoLayoutDescontinuadoId equals descontinuado.Id into descontinuadoGroup
            from descontinuado in descontinuadoGroup.DefaultIfEmpty()
            where cl.Publicado && cl.DesativadoEm == null
                && origem.Publicado && origem.DesativadoEm == null
            select new CampoLayoutVisivelBrutoDto
            {
                Slug = cl.Slug,
                Nome = cl.Nome,
                CaminhoXml = cl.CaminhoXml,
                Significado = cl.Significado,
                Grupo = cl.Grupo,
                Ordem = cl.Ordem,
                ReformaTributaria = cl.ReformaTributaria,
                VersaoLayoutOrigemId = cl.VersaoLayoutId,
                OrigemOrdem = origem.Ordem,
                VersaoLayoutDescontinuadoId = cl.VersaoLayoutDescontinuadoId,
                DescontinuadoOrdem = descontinuado != null ? (int?)descontinuado.Ordem : null,
            }
        ).ToListAsync(ct);

    public Task<CampoLayout?> ObterCampoPorSlugAsync(string campoSlug, CancellationToken ct) =>
        Contexto.CamposLayout.AsNoTracking()
            .Include(c => c.VersaoLayoutOrigem)
            .Where(c => c.Slug == campoSlug && c.DesativadoEm == null)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyCollection<UsoCampoLayoutDto>> ListarUsosPublicosAsync(int campoLayoutId, CancellationToken ct)
    {
        // Nome do provedor não é único e a ordem física muda a cada UPDATE: o id da versão
        // torna a ordem total. O enum vira texto só depois de materializar.
        var usos = await (
            from cv in Contexto.CamposVersao.AsNoTracking()
            join vp in Contexto.VersoesProvedor.AsNoTracking() on cv.VersaoProvedorId equals vp.Id
            join p in Contexto.Provedores.AsNoTracking() on vp.ProvedorId equals p.Id
            where cv.CampoLayoutId == campoLayoutId && cv.DesativadoEm == null
                && vp.Publicado && vp.DesativadoEm == null
                && p.Publicado && p.DesativadoEm == null
            orderby p.Nome, vp.Id
            select new
            {
                ProvedorSlug = p.Slug,
                ProvedorNome = p.Nome,
                VersaoSlug = vp.Slug,
                VersaoRotulo = vp.Rotulo,
                cv.DestinoXml,
                cv.EstadoPreenchimento,
            }
        ).ToListAsync(ct);

        return usos.Select(uso => new UsoCampoLayoutDto
        {
            ProvedorSlug = uso.ProvedorSlug,
            ProvedorNome = uso.ProvedorNome,
            VersaoSlug = uso.VersaoSlug,
            VersaoRotulo = uso.VersaoRotulo,
            DestinoXml = uso.DestinoXml,
            EstadoPreenchimento = uso.EstadoPreenchimento.ToString(),
        }).ToArray();
    }
}
