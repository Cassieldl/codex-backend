using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IErroRepositorioInterno"/> — enxerga rascunho (I18-I20c).</summary>
public sealed class ErroRepositorioInterno : RepositorioBase<CodexDbContext>, IErroRepositorioInterno
{
    public ErroRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    /// <summary>
    /// Traz <see cref="Erro.Variantes"/> SEMPRE, rastreada ou não: o detalhe interno (I18b) e a
    /// edição (I19) leem pelo mesmo método, e `rastreada: false` nunca pode significar "sem
    /// variantes" — foi o que esvaziava o formulário interno e fazia o PUT seguinte apagar as
    /// variantes existentes via `SubstituirVariantes`.
    /// </summary>
    public Task<Erro?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        IQueryable<Erro> query = Contexto.Erros
            .Where(e => e.Id == id && e.DesativadoEm == null)
            .Include(e => e.Variantes);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    public async Task<PaginaResultado<ErroAdminListaItemDto>> ListarAsync(FiltroErroInterno filtro, CancellationToken ct)
    {
        var query = Contexto.Erros.AsNoTracking().Where(e => e.DesativadoEm == null);

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var padrao = PadraoBusca.Contendo(filtro.Busca);
            query = query.Where(e =>
                EF.Functions.ILike(e.Titulo, padrao, PadraoBusca.CaractereDeEscape)
                || EF.Functions.ILike(e.Codigo, padrao, PadraoBusca.CaractereDeEscape));
        }

        if (filtro.Origem.HasValue)
            query = query.Where(e => e.Origem == filtro.Origem.Value);

        if (filtro.Publicado.HasValue)
            query = query.Where(e => e.Publicado == filtro.Publicado.Value);

        if (!string.IsNullOrWhiteSpace(filtro.ProvedorSlug))
        {
            // A área interna enxerga rascunho DE PROPÓSITO (nada de filtrar Publicado aqui), mas
            // vínculo por versão/provedor REMOVIDO não é vínculo: mesmo predicado da busca interna
            // (ConsultaBuscaPostgres.CarregarErroIdsDoProvedorAsync com incluirRascunho).
            var idsDeErrosDoProvedor = from ev in Contexto.ErrosVersoes.AsNoTracking()
                                       join vp in Contexto.VersoesProvedor.AsNoTracking() on ev.VersaoProvedorId equals vp.Id
                                       join p in Contexto.Provedores.AsNoTracking() on vp.ProvedorId equals p.Id
                                       where p.Slug == filtro.ProvedorSlug
                                           && vp.DesativadoEm == null && p.DesativadoEm == null
                                       select ev.ErroId;

            query = query.Where(e => idsDeErrosDoProvedor.Contains(e.Id));
        }

        var totalCount = await query.CountAsync(ct);

        var itens = await query
            .OrderByDescending(e => e.CriadoEm)
            .ThenByDescending(e => e.Id)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .Select(e => new ErroAdminListaItemDto
            {
                Id = e.Id,
                Slug = e.Slug,
                Codigo = e.Codigo,
                Origem = e.Origem.ToString(),
                Titulo = e.Titulo,
                Publicado = e.Publicado,
                VersaoConcorrencia = e.Versao,
            })
            .ToListAsync(ct);

        return new PaginaResultado<ErroAdminListaItemDto>(itens, totalCount, filtro.Pagina, filtro.TamanhoPagina);
    }

    public async Task<IReadOnlyCollection<Erro>> ListarTodosAtivosComVariantesAsync(CancellationToken ct) =>
        await Contexto.Erros.AsNoTracking()
            .Where(e => e.DesativadoEm == null)
            .Include(e => e.Variantes)
            .ToListAsync(ct);

    public Task<IReadOnlyCollection<int>> ListarVersoesAplicaveisIdsAsync(int erroId, CancellationToken ct) =>
        MaterializarAsync(Contexto.ErrosVersoes.AsNoTracking().Where(ev => ev.ErroId == erroId).Select(ev => ev.VersaoProvedorId), ct);

    public Task<IReadOnlyCollection<int>> ListarCamposRelacionadosIdsAsync(int erroId, CancellationToken ct) =>
        MaterializarAsync(Contexto.ErrosCampos.AsNoTracking().Where(ec => ec.ErroId == erroId).Select(ec => ec.CampoLayoutId), ct);

    public async Task SubstituirVersoesAplicaveisAsync(int erroId, IReadOnlyCollection<int> versaoProvedorIds, CancellationToken ct)
    {
        var existentes = await Contexto.ErrosVersoes.Where(ev => ev.ErroId == erroId).ToListAsync(ct);
        Contexto.ErrosVersoes.RemoveRange(existentes);
        await Contexto.ErrosVersoes.AddRangeAsync(versaoProvedorIds.Distinct().Select(id => ErroVersao.Criar(erroId, id)), ct);
    }

    public async Task SubstituirCamposRelacionadosAsync(int erroId, IReadOnlyCollection<int> campoLayoutIds, CancellationToken ct)
    {
        var existentes = await Contexto.ErrosCampos.Where(ec => ec.ErroId == erroId).ToListAsync(ct);
        Contexto.ErrosCampos.RemoveRange(existentes);
        await Contexto.ErrosCampos.AddRangeAsync(campoLayoutIds.Distinct().Select(id => ErroCampo.Criar(erroId, id)), ct);
    }

    /// <summary>
    /// Remoção explícita: sem isso, chamar <c>Erro.SubstituirVariantes</c>
    /// num <see cref="Erro"/> rastreado (que já traz <see cref="Erro.Variantes"/> por
    /// <see cref="ObterPorIdAsync"/>) deixa o EF com filhos órfãos de FK obrigatória e
    /// <c>OnDelete(Restrict)</c> — <c>SaveChanges</c> lança em vez de apagar. As linhas usam o
    /// MESMO identity map da consulta que trouxe <paramref name="erroId"/>, então isto marca as
    /// instâncias já rastreadas como <c>Deleted</c>, nunca cria um rastreamento paralelo.
    /// </summary>
    public async Task RemoverVariantesAsync(int erroId, CancellationToken ct)
    {
        var existentes = await Contexto.VariantesMensagem.Where(v => v.ErroId == erroId).ToListAsync(ct);
        Contexto.VariantesMensagem.RemoveRange(existentes);
    }

    public async Task AdicionarAsync(Erro erro, CancellationToken ct) =>
        await Contexto.Erros.AddAsync(erro, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);

    private static async Task<IReadOnlyCollection<int>> MaterializarAsync(IQueryable<int> query, CancellationToken ct) =>
        await query.ToListAsync(ct);
}
