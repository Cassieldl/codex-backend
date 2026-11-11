using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IVersaoProvedorRepositorioInterno"/> — enxerga rascunho (I6, I8a-I10).</summary>
public sealed class VersaoProvedorRepositorioInterno : RepositorioBase<CodexDbContext>, IVersaoProvedorRepositorioInterno
{
    public VersaoProvedorRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<VersaoProvedor?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        var query = Contexto.VersoesProvedor.Where(v => v.Id == id && v.DesativadoEm == null);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Parametrizado por interpolação (<c>FromSqlInterpolated</c>/<c>ExecuteSqlInterpolatedAsync</c>
    /// — nunca concatenação, rules/security.md A03). Sem filtro de <c>desativadoem</c> de propósito:
    /// quem chama já validou a linha antes (ela existe e está carregada); o lock é só sobre a
    /// identidade física da linha, não sobre o predicado de negócio.
    /// </summary>
    public async Task BloquearParaAlteracaoAsync(int id, CancellationToken ct) =>
        await Contexto.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM codex.versoesprovedor WHERE versaoprovedorid = {id} FOR UPDATE", ct);

    public async Task<IReadOnlyCollection<VersaoAdminListaItemDto>> ListarPorProvedorAsync(int provedorId, CancellationToken ct) =>
        await Contexto.VersoesProvedor.AsNoTracking()
            .Where(v => v.ProvedorId == provedorId && v.DesativadoEm == null)
            .OrderByDescending(v => v.CriadoEm)
            .Select(v => new VersaoAdminListaItemDto
            {
                Id = v.Id,
                ProvedorId = v.ProvedorId,
                Slug = v.Slug,
                Rotulo = v.Rotulo,
                VersaoLayoutId = v.VersaoLayoutId,
                Publicado = v.Publicado,
                VerificadoEm = v.VerificadoEm,
                VersaoConcorrencia = v.Versao,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<VersaoProvedor>> ListarTodosAtivosAsync(CancellationToken ct) =>
        await Contexto.VersoesProvedor.AsNoTracking().Where(v => v.DesativadoEm == null).ToListAsync(ct);

    public async Task AdicionarAsync(VersaoProvedor versao, CancellationToken ct) =>
        await Contexto.VersoesProvedor.AddAsync(versao, ct);

    public async Task<IReadOnlyCollection<ItemNaturezaOperacaoDto>> ListarNaturezasOperacaoAsync(int versaoProvedorId, CancellationToken ct) =>
        await Contexto.NaturezasOperacao.AsNoTracking()
            .Where(n => n.VersaoProvedorId == versaoProvedorId && n.DesativadoEm == null)
            .OrderBy(n => n.Ordem)
            .Select(n => new ItemNaturezaOperacaoDto
            {
                CodigoInvoicy = n.CodigoInvoicy,
                Descricao = n.Descricao,
                ValorProvedor = n.ValorProvedor,
                Observacao = n.Observacao,
                AConfirmar = n.AConfirmar,
            })
            .ToListAsync(ct);

    public async Task SubstituirNaturezasOperacaoAsync(
        int versaoProvedorId, IReadOnlyCollection<NaturezaOperacao> naturezas, CancellationToken ct)
    {
        await Contexto.NaturezasOperacao.Where(n => n.VersaoProvedorId == versaoProvedorId).ExecuteDeleteAsync(ct);
        await Contexto.NaturezasOperacao.AddRangeAsync(naturezas, ct);
        await SalvarAsync(ct);
    }

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);
}
