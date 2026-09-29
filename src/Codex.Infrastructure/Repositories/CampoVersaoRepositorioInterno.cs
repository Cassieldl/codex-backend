using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="ICampoVersaoRepositorioInterno"/> (I11/I12/I13a/I13b).</summary>
public sealed class CampoVersaoRepositorioInterno : RepositorioBase<CodexDbContext>, ICampoVersaoRepositorioInterno
{
    public CampoVersaoRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<CampoVersao?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        var query = Contexto.CamposVersao.Where(c => c.Id == id && c.DesativadoEm == null);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    public Task<IReadOnlyCollection<CampoVersaoAdminDto>> ListarPorVersaoAsync(int versaoProvedorId, CancellationToken ct) =>
        MaterializarAsync(Contexto.CamposVersao.AsNoTracking()
            .Where(c => c.VersaoProvedorId == versaoProvedorId && c.DesativadoEm == null)
            .OrderBy(c => c.Ordem), ct);

    public async Task<CampoVersaoAdminDto?> ObterDtoPorIdAsync(int id, CancellationToken ct)
    {
        var encontrados = await MaterializarAsync(
            Contexto.CamposVersao.AsNoTracking().Where(c => c.Id == id && c.DesativadoEm == null),
            ct);

        return encontrados.SingleOrDefault();
    }

    public async Task<IReadOnlyCollection<CampoLayoutResponseDto>> ListarCamposLayoutAusentesAsync(int versaoProvedorId, CancellationToken ct)
    {
        var versaoLayoutId = await Contexto.VersoesProvedor.AsNoTracking()
            .Where(v => v.Id == versaoProvedorId)
            .Select(v => v.VersaoLayoutId)
            .FirstOrDefaultAsync(ct);

        var idsJaPreenchidos = Contexto.CamposVersao.AsNoTracking()
            .Where(c => c.VersaoProvedorId == versaoProvedorId && c.DesativadoEm == null)
            .Select(c => c.CampoLayoutId);

        return await Contexto.CamposLayout.AsNoTracking()
            .Where(cl => cl.VersaoLayoutId == versaoLayoutId
                && cl.Estado == EstadoCampoLayout.Ativo
                && cl.DesativadoEm == null
                && !idsJaPreenchidos.Contains(cl.Id))
            .OrderBy(cl => cl.Ordem)
            .Select(cl => new CampoLayoutResponseDto
            {
                Id = cl.Id,
                VersaoLayoutId = cl.VersaoLayoutId,
                VersaoLayoutDescontinuadoId = cl.VersaoLayoutDescontinuadoId,
                Nome = cl.Nome,
                CaminhoXml = cl.CaminhoXml,
                Significado = cl.Significado,
                Grupo = cl.Grupo,
                Ordem = cl.Ordem,
                Slug = cl.Slug,
                Estado = cl.Estado.ToString(),
                Publicado = cl.Publicado,
                VersaoConcorrencia = cl.Versao,
            })
            .ToListAsync(ct);
    }

    public async Task<bool> CampoLayoutEstaAtivoNaVersaoDoProvedorAsync(int campoLayoutId, int versaoProvedorId, CancellationToken ct)
    {
        var versaoLayoutId = await Contexto.VersoesProvedor.AsNoTracking()
            .Where(v => v.Id == versaoProvedorId)
            .Select(v => (int?)v.VersaoLayoutId)
            .FirstOrDefaultAsync(ct);

        if (versaoLayoutId is null)
            return false;

        return await Contexto.CamposLayout.AsNoTracking()
            .AnyAsync(cl => cl.Id == campoLayoutId
                && cl.VersaoLayoutId == versaoLayoutId.Value
                && cl.Estado == EstadoCampoLayout.Ativo
                && cl.DesativadoEm == null, ct);
    }

    public Task<int> ContarPorVersaoAsync(int versaoProvedorId, CancellationToken ct) =>
        Contexto.CamposVersao.AsNoTracking()
            .CountAsync(c => c.VersaoProvedorId == versaoProvedorId && c.DesativadoEm == null, ct);

    public async Task AdicionarAsync(CampoVersao campoVersao, CancellationToken ct) =>
        await Contexto.CamposVersao.AddAsync(campoVersao, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);

    /// <summary>
    /// O nome do campo canônico vem por subconsulta correlacionada, e não por navegação: a
    /// entidade <see cref="CampoVersao"/> guarda só o id. É o nome que a tela exibe na coluna
    /// "Campo" e usa no rótulo acessível de cada ação — sem ele, a linha não se identifica.
    /// </summary>
    private async Task<IReadOnlyCollection<CampoVersaoAdminDto>> MaterializarAsync(IQueryable<CampoVersao> query, CancellationToken ct) =>
        await query.Select(c => new CampoVersaoAdminDto
        {
            Id = c.Id,
            VersaoProvedorId = c.VersaoProvedorId,
            CampoLayoutId = c.CampoLayoutId,
            CampoLayoutNome = Contexto.CamposLayout
                .Where(cl => cl.Id == c.CampoLayoutId)
                .Select(cl => cl.Nome)
                .FirstOrDefault() ?? string.Empty,
            Tipo = c.Tipo,
            Tamanho = c.Tamanho,
            Mascara = c.Mascara,
            Dominio = c.Dominio,
            EstadoPreenchimento = c.EstadoPreenchimento.ToString(),
            Condicao = c.Condicao,
            Exemplo = c.Exemplo,
            NotaPreenchimento = c.NotaPreenchimento,
            DestinoXml = c.DestinoXml,
            NotaTransformacao = c.NotaTransformacao,
            Ordem = c.Ordem,
            VersaoConcorrencia = c.Versao,
        }).ToListAsync(ct);
}
