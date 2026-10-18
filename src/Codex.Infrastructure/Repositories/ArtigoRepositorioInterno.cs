using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IArtigoRepositorioInterno"/> — enxerga rascunho (I23-I29, I36).</summary>
public sealed class ArtigoRepositorioInterno : RepositorioBase<CodexDbContext>, IArtigoRepositorioInterno
{
    public ArtigoRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<Artigo?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        IQueryable<Artigo> query = Contexto.Artigos.Where(a => a.Id == id && a.DesativadoEm == null);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    public Task<Artigo?> ObterPorSlugAsync(string slug, CancellationToken ct) =>
        Contexto.Artigos.AsNoTracking()
            .Where(a => a.Slug == slug && a.DesativadoEm == null)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyCollection<Artigo>> ListarTodosAtivosAsync(CancellationToken ct) =>
        await Contexto.Artigos.AsNoTracking()
            .Where(a => a.DesativadoEm == null)
            .ToListAsync(ct);

    public async Task<PaginaResultado<ArtigoAdminListaItemDto>> ListarAsync(FiltroArtigoInterno filtro, CancellationToken ct)
    {
        var query = Contexto.Artigos.AsNoTracking().Where(a => a.DesativadoEm == null);

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var padrao = PadraoBusca.Contendo(filtro.Busca);
            query = query.Where(a => EF.Functions.ILike(a.Titulo, padrao, PadraoBusca.CaractereDeEscape));
        }

        if (filtro.Tipo.HasValue)
            query = query.Where(a => a.Tipo == filtro.Tipo.Value);

        if (filtro.Publicado.HasValue)
            query = query.Where(a => a.Publicado == filtro.Publicado.Value);

        var totalCount = await query.CountAsync(ct);

        var brutos = await query
            .OrderByDescending(a => a.CriadoEm)
            .ThenByDescending(a => a.Id)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .Select(a => new { a.Id, a.Slug, a.Tipo, a.Titulo, a.Publicado, a.VerificadoEm, a.Versao })
            .ToListAsync(ct);

        var itens = brutos.Select(a => new ArtigoAdminListaItemDto
        {
            Id = a.Id,
            Slug = a.Slug,
            Tipo = a.Tipo.ToString(),
            Titulo = a.Titulo,
            Publicado = a.Publicado,
            Reservado = SlugsReservadosArtigo.EhReservado(a.Slug),
            VerificadoEm = a.VerificadoEm.HasValue ? DateOnly.FromDateTime(a.VerificadoEm.Value.UtcDateTime) : null,
            VersaoConcorrencia = a.Versao,
        }).ToArray();

        return new PaginaResultado<ArtigoAdminListaItemDto>(itens, totalCount, filtro.Pagina, filtro.TamanhoPagina);
    }

    public async Task<IReadOnlyList<CampoLayoutVinculadoArtigoDto>> ListarCamposLayoutVinculadosAsync(int artigoId, CancellationToken ct) =>
        await (
            from vinculo in Contexto.ArtigosCamposLayout.AsNoTracking()
            join campo in Contexto.CamposLayout.AsNoTracking() on vinculo.CampoLayoutId equals campo.Id
            join versao in Contexto.VersoesLayout.AsNoTracking() on campo.VersaoLayoutId equals versao.Id
            where vinculo.ArtigoId == artigoId
            orderby versao.Ordem, campo.Nome, campo.Id
            select new CampoLayoutVinculadoArtigoDto
            {
                Id = campo.Id,
                Nome = campo.Nome,
                CaminhoXml = campo.CaminhoXml,
                VersaoLayoutId = versao.Id,
                VersaoLayoutRotulo = versao.Rotulo,
                Desativado = campo.DesativadoEm != null,
            }).ToListAsync(ct);

    public async Task<IReadOnlyList<ErroVinculadoArtigoDto>> ListarErrosVinculadosAsync(int artigoId, CancellationToken ct) =>
        await (
            from vinculo in Contexto.ArtigosErros.AsNoTracking()
            join erro in Contexto.Erros.AsNoTracking() on vinculo.ErroId equals erro.Id
            where vinculo.ArtigoId == artigoId
            orderby erro.Codigo, erro.Id
            select new ErroVinculadoArtigoDto
            {
                Id = erro.Id,
                Codigo = erro.Codigo,
                Titulo = erro.Titulo,
                Desativado = erro.DesativadoEm != null,
            }).ToListAsync(ct);

    public async Task SubstituirCamposLayoutRelacionadosAsync(int artigoId, IReadOnlyCollection<int> campoLayoutIds, CancellationToken ct)
    {
        var existentes = await Contexto.ArtigosCamposLayout.Where(acl => acl.ArtigoId == artigoId).ToListAsync(ct);
        Contexto.ArtigosCamposLayout.RemoveRange(existentes);
        await Contexto.ArtigosCamposLayout.AddRangeAsync(campoLayoutIds.Distinct().Select(id => ArtigoCampoLayout.Criar(artigoId, id)), ct);
    }

    public async Task SubstituirErrosRelacionadosAsync(int artigoId, IReadOnlyCollection<int> erroIds, CancellationToken ct)
    {
        var existentes = await Contexto.ArtigosErros.Where(ae => ae.ArtigoId == artigoId).ToListAsync(ct);
        Contexto.ArtigosErros.RemoveRange(existentes);
        await Contexto.ArtigosErros.AddRangeAsync(erroIds.Distinct().Select(id => ArtigoErro.Criar(artigoId, id)), ct);
    }

    public async Task AdicionarAsync(Artigo artigo, CancellationToken ct) =>
        await Contexto.Artigos.AddAsync(artigo, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);
}
