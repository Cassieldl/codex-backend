using System.Globalization;
using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Exceptions;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IVersaoLayoutRepositorioInterno"/> (I15a-I15d).</summary>
public sealed class VersaoLayoutRepositorioInterno : RepositorioBase<CodexDbContext>, IVersaoLayoutRepositorioInterno
{
    /// <summary>Índice único parcial que garante uma única <c>Vigente</c> viva, criado na <c>InitialCreate</c> da 001.</summary>
    private const string ConstraintVersaoVigente = "uq_codex_versoeslayout_estado_vigente";

    public VersaoLayoutRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<VersaoLayout?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        var query = Contexto.VersoesLayout.Where(v => v.Id == id && v.DesativadoEm == null);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    public async Task<PaginaResultado<VersaoLayoutAdminDto>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var query = Contexto.VersoesLayout.AsNoTracking().Where(v => v.DesativadoEm == null);

        var totalCount = await query.CountAsync(ct);

        var itens = await query
            .OrderBy(v => v.Ordem)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(v => new VersaoLayoutAdminDto
            {
                Id = v.Id,
                Rotulo = v.Rotulo,
                Slug = v.Slug,
                Estado = v.Estado.ToString(),
                Ordem = v.Ordem,
                Publicado = v.Publicado,
                Descricao = v.Descricao,
                VersaoConcorrencia = v.Versao,
            })
            .ToListAsync(ct);

        return new PaginaResultado<VersaoLayoutAdminDto>(itens, totalCount, pagina, tamanhoPagina);
    }

    public async Task<int> ObterProximaOrdemAsync(CancellationToken ct)
    {
        var maiorOrdem = await Contexto.VersoesLayout.AsNoTracking()
            .Select(v => (int?)v.Ordem)
            .MaxAsync(ct);

        return (maiorOrdem ?? 0) + 1;
    }

    public async Task AdicionarAsync(VersaoLayout versaoLayout, CancellationToken ct) =>
        await Contexto.VersoesLayout.AddAsync(versaoLayout, ct);

    /// <summary>
    /// SQL direto e parametrizado: pela entidade dividida, o EF filtraria <c>estrutura IS NOT NULL</c>
    /// (dependente opcional) e a primeira gravação nunca acharia a linha. O <c>xmin</c> no WHERE
    /// mantém a concorrência otimista na mesma instrução.
    /// </summary>
    public async Task<bool> SubstituirEstruturaAsync(int id, string conteudo, uint versaoEsperada, CancellationToken ct)
    {
        var versaoTexto = versaoEsperada.ToString(CultureInfo.InvariantCulture);
        var alteradas = await Contexto.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE codex.versoeslayout SET estrutura = {conteudo}::jsonb, atualizadoem = now() WHERE versaolayoutid = {id} AND desativadoem IS NULL AND xmin::text = {versaoTexto}",
            ct);

        return alteradas == 1;
    }

    /// <summary>
    /// PQ-17 — a colisão de <c>uq_codex_versoeslayout_estado_vigente</c> (segunda versão promovida
    /// a <c>Vigente</c>) continua sendo `409`, mas com código e mensagem de NEGÓCIO em vez do
    /// genérico da base, que expunha o nome cru da constraint ao editor. A identificação é por
    /// <see cref="PostgresException.ConstraintName"/> — nunca por texto de mensagem, que muda sem
    /// aviso. As demais colisões da entidade (slug, ordem) seguem no tratamento genérico.
    /// </summary>
    public async Task SalvarAlteracoesAsync(CancellationToken ct)
    {
        try
        {
            await SalvarAsync(ct);
        }
        catch (RegistroDuplicadoException ex) when (
            ex.InnerException is DbUpdateException
            {
                InnerException: PostgresException { ConstraintName: ConstraintVersaoVigente }
            })
        {
            throw new RegistroDuplicadoException(
                "Já existe uma versão de layout marcada como Vigente. Marque a versão atual como "
                + "Anterior antes de promover outra.",
                ex.InnerException,
                CodigosErro.VersaoLayoutVigenteJaExiste);
        }
    }
}
