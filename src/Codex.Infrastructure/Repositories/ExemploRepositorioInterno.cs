using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>
/// Implementação de <see cref="IExemploRepositorioInterno"/> (I38-I42). Exemplo não tem publicação
/// própria (decisão C1): a visibilidade cascateia 100% da versão/provedor, então aqui só existe o
/// filtro de soft delete.
/// </summary>
public sealed class ExemploRepositorioInterno : RepositorioBase<CodexDbContext>, IExemploRepositorioInterno
{
    public ExemploRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<Exemplo?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        var query = Contexto.Exemplos.Where(e => e.Id == id && e.DesativadoEm == null);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// I38 — lista completa (cap de 10 por versão torna paginação desnecessária). Ordem por
    /// criação com desempate pela chave única: <c>criadoem</c> empata quando vários exemplos
    /// nascem na mesma transação, e sem o desempate a ordem devolvida é a física do heap.
    /// </summary>
    public async Task<IReadOnlyCollection<ExemploAdminDto>> ListarPorVersaoAsync(int versaoProvedorId, CancellationToken ct) =>
        await Contexto.Exemplos.AsNoTracking()
            .Where(e => e.VersaoProvedorId == versaoProvedorId && e.DesativadoEm == null)
            .OrderBy(e => e.CriadoEm)
            .ThenBy(e => e.Id)
            .Select(e => new ExemploAdminDto
            {
                Id = e.Id,
                VersaoProvedorId = e.VersaoProvedorId,
                Slug = e.Slug,
                Nome = e.Nome,
                Descricao = e.Descricao,
                Xml = e.Xml,
                VersaoConcorrencia = e.Versao,
            })
            .ToListAsync(ct);

    public Task<int> ContarPorVersaoAsync(int versaoProvedorId, CancellationToken ct) =>
        Contexto.Exemplos.AsNoTracking()
            .CountAsync(e => e.VersaoProvedorId == versaoProvedorId && e.DesativadoEm == null, ct);

    public async Task<IReadOnlyCollection<string>> ListarSlugsDaVersaoAsync(int versaoProvedorId, CancellationToken ct) =>
        await Contexto.Exemplos.AsNoTracking()
            .Where(e => e.VersaoProvedorId == versaoProvedorId)
            .Select(e => e.Slug)
            .ToListAsync(ct);

    public async Task AdicionarAsync(Exemplo exemplo, CancellationToken ct) =>
        await Contexto.Exemplos.AddAsync(exemplo, ct);

    public void TocarVersaoProvedor(VersaoProvedor versaoProvedor) =>
        Contexto.Entry(versaoProvedor).State = EntityState.Modified;

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);
}
