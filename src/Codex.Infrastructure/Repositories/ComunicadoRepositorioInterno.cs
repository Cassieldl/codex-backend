using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IComunicadoRepositorioInterno"/> — enxerga rascunho e encerrado (I30-I35).</summary>
public sealed class ComunicadoRepositorioInterno : RepositorioBase<CodexDbContext>, IComunicadoRepositorioInterno
{
    public ComunicadoRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    /// <summary>
    /// Delega em <see cref="Comunicado.EhVigenteEm"/>, o dono único do predicado "o que está no ar"
    /// (modelo-dados.md §7.1): publicado, ativo e ainda não vencido. É a rede de segurança da
    /// decisão U-5 — a leitura pública nunca depende de o job de expiração ter rodado —, e o
    /// índice parcial <c>idx_codex_comunicados_vigentes</c> sustenta exatamente estas três
    /// condições.
    ///
    /// <para>
    /// NÃO é o predicado de I30: lá o filtro <c>incluirEncerrados=false</c> esconde apenas o que
    /// está ENCERRADO (<c>expiraem &lt;= referencia</c>), nunca o rascunho — a tela 12 mostra os
    /// três estados (rascunho · vigente · encerrado) e o badge "Rascunho" desapareceria da visão
    /// padrão do editor se este predicado fosse reusado lá.
    /// </para>
    /// </summary>
    public static IQueryable<Comunicado> QueryVigentes(IQueryable<Comunicado> origem, DateTimeOffset referencia) =>
        origem.Where(Comunicado.EhVigenteEm(referencia));

    public Task<Comunicado?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        IQueryable<Comunicado> query = Contexto.Comunicados.Where(c => c.Id == id && c.DesativadoEm == null);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    public async Task<PaginaResultado<ComunicadoAdminListaItemDto>> ListarAsync(
        FiltroComunicadoInterno filtro, DateTimeOffset referencia, CancellationToken ct)
    {
        var query = Contexto.Comunicados.AsNoTracking().Where(c => c.DesativadoEm == null);

        if (filtro.Severidade.HasValue)
            query = query.Where(c => c.Severidade == filtro.Severidade.Value);

        if (filtro.Abrangencia.HasValue)
            query = query.Where(c => c.Abrangencia == filtro.Abrangencia.Value);

        if (filtro.ProvedorId.HasValue)
            query = query.Where(c => c.ProvedorId == filtro.ProvedorId.Value);

        if (!filtro.IncluirEncerrados)
            query = query.Where(c => c.ExpiraEm > referencia);

        var totalCount = await query.CountAsync(ct);

        var brutos = await query
            .OrderByDescending(c => c.ExpiraEm)
            .ThenByDescending(c => c.Id)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .Select(c => new
            {
                c.Id,
                c.Titulo,
                c.Severidade,
                c.Abrangencia,
                c.ProvedorId,
                c.VersaoProvedorId,
                c.ExpiraEm,
                c.Publicado,
                c.Versao,
            })
            .ToListAsync(ct);

        // Rótulos de exibição resolvidos por JOIN só nos ids da página — nunca coluna
        // denormalizada, nunca uma consulta por linha.
        var nomesDeProvedor = await CarregarNomesDeProvedorAsync(brutos.Select(c => c.ProvedorId), ct);
        var rotulosDeVersao = await CarregarRotulosDeVersaoAsync(brutos.Select(c => c.VersaoProvedorId), ct);

        var itens = brutos.Select(c => new ComunicadoAdminListaItemDto
        {
            Id = c.Id,
            Titulo = c.Titulo,
            Severidade = c.Severidade.ToString(),
            Abrangencia = c.Abrangencia.ToString(),
            ProvedorNome = c.ProvedorId is int provedorId && nomesDeProvedor.TryGetValue(provedorId, out var nome) ? nome : null,
            VersaoRotulo = c.VersaoProvedorId is int versaoId && rotulosDeVersao.TryGetValue(versaoId, out var rotulo) ? rotulo : null,
            ExpiraEm = c.ExpiraEm,
            Publicado = c.Publicado,
            Encerrado = c.ExpiraEm <= referencia,
            VersaoConcorrencia = c.Versao,
        }).ToArray();

        return new PaginaResultado<ComunicadoAdminListaItemDto>(itens, totalCount, filtro.Pagina, filtro.TamanhoPagina);
    }

    public async Task<RotulosEscopoComunicado> ObterRotulosEscopoAsync(int? provedorId, int? versaoProvedorId, CancellationToken ct)
    {
        var provedorNome = provedorId is null
            ? null
            : await Contexto.Provedores.AsNoTracking()
                .Where(p => p.Id == provedorId.Value)
                .Select(p => p.Nome)
                .FirstOrDefaultAsync(ct);

        var versaoRotulo = versaoProvedorId is null
            ? null
            : await Contexto.VersoesProvedor.AsNoTracking()
                .Where(v => v.Id == versaoProvedorId.Value)
                .Select(v => v.Rotulo)
                .FirstOrDefaultAsync(ct);

        return new RotulosEscopoComunicado(provedorNome, versaoRotulo);
    }

    /// <summary>
    /// Direção oposta de <see cref="QueryVigentes"/> sobre o mesmo índice parcial
    /// (<c>idx_codex_comunicados_vigentes</c>): mesmas três condições, range invertido em
    /// <c>expiraem</c>. Statement único, atômico por si — não precisa de transação explícita,
    /// porque não há um segundo comando a coordenar.
    ///
    /// <para>
    /// Idempotente por construção: a condição é sobre o ESTADO ATUAL da linha, não sobre "já
    /// processei este id". Rodar de novo com a mesma referência (ou uma posterior) afeta zero
    /// linhas, porque quem convergiu já está com <c>publicado = false</c>. Duas réplicas rodando
    /// junto competem pelo mesmo <c>UPDATE</c> e o Postgres serializa no nível de linha — a soma
    /// de linhas afetadas pode se dividir, o estado final do banco é o mesmo.
    /// </para>
    /// </summary>
    public Task<int> DespublicarVencidosAsync(DateTimeOffset referencia, CancellationToken ct) =>
        Contexto.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE codex.comunicados
             SET publicado = false
             WHERE publicado AND expiraem <= {referencia} AND desativadoem IS NULL
             """,
            ct);

    public async Task AdicionarAsync(Comunicado comunicado, CancellationToken ct) =>
        await Contexto.Comunicados.AddAsync(comunicado, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);

    private async Task<Dictionary<int, string>> CarregarNomesDeProvedorAsync(IEnumerable<int?> provedorIds, CancellationToken ct)
    {
        var ids = provedorIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        return await Contexto.Provedores.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Nome, ct);
    }

    private async Task<Dictionary<int, string>> CarregarRotulosDeVersaoAsync(IEnumerable<int?> versaoIds, CancellationToken ct)
    {
        var ids = versaoIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        return await Contexto.VersoesProvedor.AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Rotulo, ct);
    }
}
