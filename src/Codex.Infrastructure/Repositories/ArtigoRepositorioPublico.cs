using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IArtigoRepositorioPublico"/> (P7/P8) — mesmo padrão de <c>ErroRepositorioPublico</c>.</summary>
public sealed class ArtigoRepositorioPublico : RepositorioBase<CodexDbContext>, IArtigoRepositorioPublico
{
    public ArtigoRepositorioPublico(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<Artigo?> ObterPorSlugAsync(string slug, CancellationToken ct) =>
        // Não filtra Publicado — M7/§4.1: distinguir 404 de 410 é responsabilidade do service.
        Contexto.Artigos.AsNoTracking()
            .Where(a => a.Slug == slug && a.DesativadoEm == null)
            .FirstOrDefaultAsync(ct);

    public async Task<PaginaResultado<ArtigoListaItemDto>> ListarAsync(FiltroArtigoPublico filtro, CancellationToken ct)
    {
        var query = Contexto.Artigos.AsNoTracking()
            .Where(a => a.Publicado && a.DesativadoEm == null);

        if (filtro.Tipo is { Length: > 0 })
            query = query.Where(a => filtro.Tipo.Contains(a.Tipo));

        if (!string.IsNullOrWhiteSpace(filtro.Q))
        {
            var padrao = PadraoBusca.Contendo(filtro.Q);
            query = query.Where(a => EF.Functions.ILike(a.Titulo, padrao, PadraoBusca.CaractereDeEscape));
        }

        // Slug reservado nunca aparece na listagem, em nenhuma aba (arquitetura.md §4.1) — evita
        // duplicidade com o item fixo do menu.
        query = query.Where(a => a.Slug != SlugsReservadosArtigo.ComoFunciona);

        var totalCount = await query.CountAsync(ct);

        var brutos = await query
            .OrderByDescending(a => a.CriadoEm) // sem ordenarPor definido para P7 — default (rules/api.md)
            .ThenByDescending(a => a.Id) // desempate obrigatório: OFFSET sobre ordem parcial repete e pula linha
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .Select(a => new { a.Slug, a.Tipo, a.Titulo, a.Corpo, a.VerificadoEm })
            .ToListAsync(ct);

        var itens = brutos.Select(a => new ArtigoListaItemDto
        {
            Slug = a.Slug,
            Tipo = a.Tipo.ToString(),
            Titulo = a.Titulo,
            Resumo = ResumoArtigo.Derivar(a.Corpo),
            VerificadoEm = a.VerificadoEm.HasValue ? DateOnly.FromDateTime(a.VerificadoEm.Value.UtcDateTime) : null,
        }).ToArray();

        return new PaginaResultado<ArtigoListaItemDto>(itens, totalCount, filtro.Pagina, filtro.TamanhoPagina);
    }

    public async Task<IReadOnlyCollection<ReferenciaCampoLayoutDto>> ListarCamposLayoutRelacionadosPublicadosAsync(int artigoId, CancellationToken ct) =>
        await (
            from acl in Contexto.ArtigosCamposLayout.AsNoTracking()
            join cl in Contexto.CamposLayout.AsNoTracking() on acl.CampoLayoutId equals cl.Id
            join vl in Contexto.VersoesLayout.AsNoTracking() on cl.VersaoLayoutId equals vl.Id
            where acl.ArtigoId == artigoId
                && cl.Publicado && cl.DesativadoEm == null
                && vl.Publicado && vl.DesativadoEm == null
            orderby cl.Nome
            select new ReferenciaCampoLayoutDto
            {
                Slug = cl.Slug,
                Nome = cl.Nome,
                CaminhoXml = cl.CaminhoXml,
                Url = null, // PQ-06 — sempre null nesta fatia.
            }
        ).ToListAsync(ct);

    public async Task<IReadOnlyCollection<ReferenciaErroDto>> ListarErrosRelacionadosPublicadosAsync(int artigoId, CancellationToken ct) =>
        await (
            from ae in Contexto.ArtigosErros.AsNoTracking()
            join e in Contexto.Erros.AsNoTracking() on ae.ErroId equals e.Id
            where ae.ArtigoId == artigoId && e.Publicado && e.DesativadoEm == null
            orderby e.Titulo
            select new ReferenciaErroDto
            {
                Slug = e.Slug,
                Codigo = e.Codigo,
                Titulo = e.Titulo,
                Url = $"/erros/{e.Slug}",
            }
        ).ToListAsync(ct);

    public async Task<IReadOnlyCollection<ReferenciaArtigoDto>> ListarRelacionadosAoErroPublicadosAsync(int erroId, CancellationToken ct)
    {
        var brutos = await (
            from ae in Contexto.ArtigosErros.AsNoTracking()
            join a in Contexto.Artigos.AsNoTracking() on ae.ArtigoId equals a.Id
            where ae.ErroId == erroId && a.Publicado && a.DesativadoEm == null
            orderby a.Titulo
            select new { a.Slug, a.Tipo, a.Titulo }
        ).ToListAsync(ct);

        return brutos.Select(a => new ReferenciaArtigoDto
        {
            Slug = a.Slug,
            Tipo = a.Tipo.ToString(),
            Titulo = a.Titulo,
            Url = SlugsReservadosArtigo.RotaCanonica(a.Slug),
        }).ToArray();
    }

    public async Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct) =>
        await Contexto.Redirecionamentos.AsNoTracking()
            .Where(r => r.Origem == $"/artigos/{slugAntigo}"
                && r.TipoItem == TipoItemIndexado.artigo
                && r.DesativadoEm == null)
            .Select(r => r.Destino)
            .FirstOrDefaultAsync(ct);
}
