using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de referência de <see cref="IProvedorRepositorioInterno"/> — ver §3.3 da análise de backend.</summary>
public sealed class ProvedorRepositorioInterno : RepositorioBase<CodexDbContext>, IProvedorRepositorioInterno
{
    public ProvedorRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<Provedor?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        // rastreada=true é o caminho usado antes de Update/Delete/Publicar/Despublicar — NUNCA
        // AsNoTracking aqui, senão xmin (concorrência otimista) quebra no próximo SaveChanges
        // (ver comentário completo em RepositorioBase). rastreada=false é só leitura de detalhe.
        //
        // Os aliases vêm SEMPRE, rastreada ou não: o detalhe interno os devolve ao formulário
        // (antes devolvia `aliases: []` com aliases no banco) e a edição precisa deles carregados
        // para removê-los antes de substituir a coleção.
        IQueryable<Provedor> query = Contexto.Provedores
            .Where(p => p.Id == id && p.DesativadoEm == null)
            .Include(p => p.Aliases);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    public async Task<PaginaResultado<ProvedorAdminListaItemDto>> ListarAsync(FiltroProvedorInterno filtro, CancellationToken ct)
    {
        var query = Contexto.Provedores.AsNoTracking().Where(p => p.DesativadoEm == null);

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var padrao = PadraoBusca.Contendo(filtro.Busca);
            query = query.Where(p => EF.Functions.ILike(p.Nome, padrao, PadraoBusca.CaractereDeEscape));
        }

        if (filtro.Categoria.HasValue)
            query = query.Where(p => p.Categoria == filtro.Categoria.Value);

        if (filtro.Publicado.HasValue)
            query = query.Where(p => p.Publicado == filtro.Publicado.Value);

        var totalCount = await query.CountAsync(ct);

        // Whitelist explícita de coluna de ordenação (nunca OrderBy(string) — AP-SECURITY-005).
        var sortDesc = !string.Equals(filtro.Direcao, "asc", StringComparison.OrdinalIgnoreCase);
        var ordenada = filtro.OrdenarPor?.ToLowerInvariant() switch
        {
            "nome" => sortDesc ? query.OrderByDescending(p => p.Nome) : query.OrderBy(p => p.Nome),
            _ => query.OrderByDescending(p => p.CriadoEm),
        };

        // Desempate por chave única em qualquer dos ramos: OFFSET sobre ordem parcial não tem
        // ordem total no Postgres — a linha reescrita por um UPDATE muda de página.
        var itens = await ordenada
            .ThenByDescending(p => p.Id)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .Select(p => new ProvedorAdminListaItemDto
            {
                Id = p.Id,
                Slug = p.Slug,
                Nome = p.Nome,
                Categoria = p.Categoria.ToString(),
                IdentificadorInvoicy = p.IdentificadorInvoicy,
                Publicado = p.Publicado,
                VersaoConcorrencia = p.Versao,
            })
            .ToListAsync(ct);

        return new PaginaResultado<ProvedorAdminListaItemDto>(itens, totalCount, filtro.Pagina, filtro.TamanhoPagina);
    }

    public Task<Provedor?> ObterPorIdComAliasesAsync(int id, CancellationToken ct) =>
        // Rastreada de propósito (mesma razão de ObterPorIdAsync(rastreada: true)) e com os
        // aliases carregados, porque quem chama reindexa a busca sem substituí-los antes.
        Contexto.Provedores
            .Where(p => p.Id == id && p.DesativadoEm == null)
            .Include(p => p.Aliases)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyCollection<Provedor>> ListarTodosAtivosComAliasesAsync(CancellationToken ct) =>
        await Contexto.Provedores.AsNoTracking()
            .Where(p => p.DesativadoEm == null)
            .Include(p => p.Aliases)
            .ToListAsync(ct);

    /// <summary>
    /// Remoção explícita dos aliases: as instâncias já rastreadas (trazidas por
    /// <see cref="ObterPorIdAsync"/>) passam a <c>Deleted</c> pelo mesmo identity map. Sem isto,
    /// <c>Provedor.SubstituirAliases</c> faria <c>Clear()</c> numa coleção carregada com FK
    /// obrigatória e <c>OnDelete(Restrict)</c> e o EF lançaria <c>InvalidOperationException</c>.
    /// </summary>
    public async Task RemoverAliasesAsync(int provedorId, CancellationToken ct)
    {
        var existentes = await Contexto.AliasesProvedor.Where(a => a.ProvedorId == provedorId).ToListAsync(ct);
        Contexto.AliasesProvedor.RemoveRange(existentes);
    }

    public async Task AdicionarAsync(Provedor provedor, CancellationToken ct) =>
        await Contexto.Provedores.AddAsync(provedor, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);
}
