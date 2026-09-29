using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IProvedorRepositorioPublico"/> — ver §3.3 da análise de backend.</summary>
public sealed class ProvedorRepositorioPublico : RepositorioBase<CodexDbContext>, IProvedorRepositorioPublico
{
    public ProvedorRepositorioPublico(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<Provedor?> ObterPorSlugAsync(string slug, CancellationToken ct)
    {
        // Leitura pura (nunca vira Update) — AsNoTracking correto aqui. NÃO filtra Publicado:
        // M7/§4.1 exige distinguir 404 (não existe) de 410 (existe, despublicado) — quem decide
        // isso é o service (ProvedorServicoPublico), depois de checar Provedor.Publicado.
        return Contexto.Provedores
            .AsNoTracking()
            .Where(p => p.Slug == slug && p.DesativadoEm == null)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaginaResultado<ProvedorListaItemDto>> ListarAsync(FiltroProvedorPublico filtro, CancellationToken ct)
    {
        var query = Contexto.Provedores.AsNoTracking()
            .Where(p => p.Publicado && p.DesativadoEm == null);

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var padrao = PadraoBusca.Contendo(filtro.Busca);
            query = query.Where(p => EF.Functions.ILike(p.Nome, padrao, PadraoBusca.CaractereDeEscape));
        }

        if (filtro.Categoria.HasValue)
            query = query.Where(p => p.Categoria == filtro.Categoria.Value);

        var totalCount = await query.CountAsync(ct);

        var projetada = query.Select(p => new
        {
            p.Id,
            p.Slug,
            p.Nome,
            p.Categoria,
            p.IdentificadorInvoicy,
            p.CriadoEm,
            VerificadoEm = Contexto.VersoesProvedor
                .Where(v => v.ProvedorId == p.Id && v.Publicado && v.DesativadoEm == null)
                .Select(v => (DateOnly?)v.VerificadoEm)
                .Max(),
        });

        // Whitelist explícita de coluna de ordenação (nunca OrderBy(string) — AP-SECURITY-005).
        var sortDesc = !string.Equals(filtro.Direcao, "asc", StringComparison.OrdinalIgnoreCase);
        var ordenada = filtro.OrdenarPor?.ToLowerInvariant() switch
        {
            "verificadoem" => sortDesc ? projetada.OrderByDescending(p => p.VerificadoEm) : projetada.OrderBy(p => p.VerificadoEm),
            "nome" => sortDesc ? projetada.OrderByDescending(p => p.Nome) : projetada.OrderBy(p => p.Nome),
            _ => projetada.OrderByDescending(p => p.CriadoEm), // default (rules/api.md) — mais recente primeiro
        };

        // Desempate por chave única em qualquer dos ramos: OFFSET sobre ordem parcial não tem
        // ordem total no Postgres — a linha reescrita por um UPDATE muda de página.
        var itens = await ordenada
            .ThenBy(p => p.Slug) // slug é a chave única desta superfície pública
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .Select(p => new ProvedorListaItemDto
            {
                Slug = p.Slug,
                Nome = p.Nome,
                Categoria = p.Categoria.ToString(),
                IdentificadorInvoicy = p.IdentificadorInvoicy,
                VerificadoEm = p.VerificadoEm,
                // Uma subconsulta por linha da página, não por linha da tabela: o `Take` acima já
                // limitou o conjunto. Mesma ordem do detalhe, para os dois lugares que oferecem o
                // salto direto ("ir para a versão") concordarem sobre qual é a mais recente.
                Versoes = Contexto.VersoesProvedor
                    .Where(v => v.ProvedorId == p.Id && v.Publicado && v.DesativadoEm == null)
                    .OrderByDescending(v => v.CriadoEm)
                    .Select(v => new VersaoResumoMinimaDto { Slug = v.Slug, Rotulo = v.Rotulo })
                    .ToList(),
            })
            .ToListAsync(ct);

        return new PaginaResultado<ProvedorListaItemDto>(itens, totalCount, filtro.Pagina, filtro.TamanhoPagina);
    }

    public async Task<IReadOnlyCollection<Aviso>> ListarAvisosPublicadosDoProvedorAsync(int provedorId, CancellationToken ct) =>
        await Contexto.Avisos.AsNoTracking()
            .Where(a => a.ProvedorId == provedorId && a.Publicado && a.DesativadoEm == null)
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<VersaoProvedor>> ListarVersoesPublicadasAsync(int provedorId, CancellationToken ct) =>
        await Contexto.VersoesProvedor.AsNoTracking()
            .Where(v => v.ProvedorId == provedorId && v.Publicado && v.DesativadoEm == null)
            .ToListAsync(ct);

    public async Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct) =>
        await Contexto.Redirecionamentos.AsNoTracking()
            .Where(r => r.Origem == $"/provedores/{slugAntigo}"
                && r.TipoItem == TipoItemIndexado.provedor
                && r.DesativadoEm == null)
            .Select(r => r.Destino)
            .FirstOrDefaultAsync(ct);
}
