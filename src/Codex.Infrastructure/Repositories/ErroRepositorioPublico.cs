using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IErroRepositorioPublico"/> (P5/P6) — mesmo padrão de <c>ProvedorRepositorioPublico</c>.</summary>
public sealed class ErroRepositorioPublico : RepositorioBase<CodexDbContext>, IErroRepositorioPublico
{
    public ErroRepositorioPublico(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<Erro?> ObterPorSlugAsync(string slug, CancellationToken ct) =>
        // Não filtra Publicado — M7/§4.1: distinguir 404 de 410 é responsabilidade do service.
        Contexto.Erros.AsNoTracking()
            .Where(e => e.Slug == slug && e.DesativadoEm == null)
            .FirstOrDefaultAsync(ct);

    public async Task<PaginaResultado<ErroListaItemDto>> ListarAsync(FiltroErroPublico filtro, CancellationToken ct)
    {
        var query = Contexto.Erros.AsNoTracking()
            .Where(e => e.Publicado && e.DesativadoEm == null);

        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
            query = query.Where(e => e.Codigo == filtro.Codigo); // atalho exato (M2)

        if (filtro.Origem.HasValue)
            query = query.Where(e => e.Origem == filtro.Origem.Value);

        if (!string.IsNullOrWhiteSpace(filtro.ProvedorSlug))
        {
            // Pertinência erro↔provedor aos olhos do PÚBLICO: só conta o vínculo cuja versão e cujo
            // provedor estão publicados e ativos — mesmo predicado de
            // ListarVersoesAplicaveisPublicadasAsync (a lista que P6 exibe) e de
            // ConsultaBuscaPostgres.CarregarErroIdsDoProvedorAsync (o filtro de P1). Sem isto o
            // filtro afirma um vínculo que a exibição nega na mesma resposta: recortar o erro sob o
            // provedor por causa de uma versão em rascunho vaza a PERTINÊNCIA do que não é público.
            var erroIdsDoProvedor =
                from ev in Contexto.ErrosVersoes.AsNoTracking()
                join vp in Contexto.VersoesProvedor.AsNoTracking() on ev.VersaoProvedorId equals vp.Id
                join p in Contexto.Provedores.AsNoTracking() on vp.ProvedorId equals p.Id
                where p.Slug == filtro.ProvedorSlug
                    && vp.Publicado && vp.DesativadoEm == null
                    && p.Publicado && p.DesativadoEm == null
                select ev.ErroId;

            query = query.Where(e => erroIdsDoProvedor.Contains(e.Id));
        }

        var totalCount = await query.CountAsync(ct);

        var itens = await query
            .OrderByDescending(e => e.CriadoEm) // sem ordenarPor definido para P5 — default (rules/api.md)
            .ThenByDescending(e => e.Id) // desempate obrigatório: OFFSET sobre ordem parcial repete e pula linha
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .Select(e => new ErroListaItemDto
            {
                Slug = e.Slug,
                Codigo = e.Codigo,
                Origem = e.Origem.ToString(),
                Titulo = e.Titulo,
            })
            .ToListAsync(ct);

        return new PaginaResultado<ErroListaItemDto>(itens, totalCount, filtro.Pagina, filtro.TamanhoPagina);
    }

    public async Task<IReadOnlyCollection<string>> ListarVariantesAsync(int erroId, CancellationToken ct) =>
        await Contexto.VariantesMensagem.AsNoTracking()
            .Where(v => v.ErroId == erroId && v.DesativadoEm == null)
            .Select(v => v.Texto)
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<VersaoAplicavelBrutaDto>> ListarVersoesAplicaveisPublicadasAsync(int erroId, CancellationToken ct) =>
        await (
            from ev in Contexto.ErrosVersoes.AsNoTracking()
            join vp in Contexto.VersoesProvedor.AsNoTracking() on ev.VersaoProvedorId equals vp.Id
            join p in Contexto.Provedores.AsNoTracking() on vp.ProvedorId equals p.Id
            where ev.ErroId == erroId
                && vp.Publicado && vp.DesativadoEm == null
                && p.Publicado && p.DesativadoEm == null
            select new VersaoAplicavelBrutaDto
            {
                VersaoProvedorId = vp.Id,
                ProvedorSlug = p.Slug,
                ProvedorNome = p.Nome,
                VersaoSlug = vp.Slug,
                VersaoRotulo = vp.Rotulo,
            }
        ).ToListAsync(ct);

    public async Task<IReadOnlyCollection<CampoRelacionadoPublicoDto>> ListarCamposRelacionadosAsync(
        int erroId, IReadOnlyCollection<VersaoAplicavelBrutaDto> versoesAplicaveisPublicas, CancellationToken ct)
    {
        // "Campo relacionado" é texto descritivo do vocabulário canônico — não depende de
        // CampoLayout.Publicado (essa coluna só rege a página/índice standalone do campo,
        // que é da demanda 002, M23). Só filtra DesativadoEm, para não citar campo removido.
        var camposRelacionados = await (
            from ec in Contexto.ErrosCampos.AsNoTracking()
            join cl in Contexto.CamposLayout.AsNoTracking() on ec.CampoLayoutId equals cl.Id
            where ec.ErroId == erroId && cl.DesativadoEm == null
            select new { cl.Id, cl.Slug, cl.Nome, cl.CaminhoXml }
        ).ToListAsync(ct);

        if (camposRelacionados.Count == 0)
            return [];

        var versaoProvedorIds = versoesAplicaveisPublicas.Select(v => v.VersaoProvedorId).ToArray();
        var campoLayoutIds = camposRelacionados.Select(c => c.Id).ToArray();

        var camposVersaoExistentes = versaoProvedorIds.Length == 0
            ? []
            : await Contexto.CamposVersao.AsNoTracking()
                .Where(cv => versaoProvedorIds.Contains(cv.VersaoProvedorId) && campoLayoutIds.Contains(cv.CampoLayoutId))
                .Select(cv => new { cv.VersaoProvedorId, cv.CampoLayoutId })
                .ToListAsync(ct);

        var brutaPorVersaoProvedorId = versoesAplicaveisPublicas.ToDictionary(v => v.VersaoProvedorId);

        return camposRelacionados.Select(campo => new CampoRelacionadoPublicoDto
        {
            Slug = campo.Slug,
            Nome = campo.Nome,
            CaminhoXml = campo.CaminhoXml,
            // M10 — só populado quando o campo existe como CampoVersao ativo numa das versões
            // aplicáveis públicas; vazio => frontend renderiza texto simples (nunca link para a
            // página standalone do Layout InvoiCy, que é da demanda 002).
            Ancoras = camposVersaoExistentes
                .Where(cv => cv.CampoLayoutId == campo.Id)
                .Select(cv => brutaPorVersaoProvedorId[cv.VersaoProvedorId])
                .Select(v => new AncoraCampoVersaoPublicaDto { ProvedorSlug = v.ProvedorSlug, VersaoSlug = v.VersaoSlug })
                .ToArray(),
        }).ToArray();
    }

    public async Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct) =>
        await Contexto.Redirecionamentos.AsNoTracking()
            .Where(r => r.Origem == $"/erros/{slugAntigo}"
                && r.TipoItem == TipoItemIndexado.erro
                && r.DesativadoEm == null)
            .Select(r => r.Destino)
            .FirstOrDefaultAsync(ct);
}
