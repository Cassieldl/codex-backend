using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="ILacunaBuscaRepositorioInterno"/>.</summary>
public sealed class LacunaBuscaRepositorioInterno : RepositorioBase<CodexDbContext>, ILacunaBuscaRepositorioInterno
{
    public LacunaBuscaRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task<LacunaBusca?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct)
    {
        IQueryable<LacunaBusca> query = Contexto.LacunasBusca.Where(l => l.Id == id && l.DesativadoEm == null);

        if (!rastreada)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(ct);
    }

    public async Task<PaginaResultado<LacunaBuscaDto>> ListarAsync(FiltroLacunaBusca filtro, CancellationToken ct)
    {
        var query = Contexto.LacunasBusca.AsNoTracking().Where(l => l.DesativadoEm == null);

        if (filtro.Estado.HasValue)
            query = query.Where(l => l.Estado == filtro.Estado.Value);

        var totalCount = await query.CountAsync(ct);

        // A recorrência entra na própria consulta (subconsulta correlacionada sobre o índice
        // termonormalizado+estado): é ela que permite ordenar a fila por prioridade.
        var linhas = query.Select(l => new LinhaLacuna
        {
            Id = l.Id,
            Termo = l.Termo,
            TermoNormalizado = l.TermoNormalizado,
            Parceiro = l.Parceiro,
            NumeroChamado = l.NumeroChamado,
            Estado = l.Estado,
            Nota = l.Nota,
            RegistradoPorNome = l.RegistradoPorNome,
            RegistradoEm = l.CriadoEm,
            ResolvidaEm = l.ResolvidaEm,
            ResolvidoPorNome = l.ResolvidoPorNome,
            OcorrenciasAbertas = Contexto.LacunasBusca.Count(o =>
                o.DesativadoEm == null && o.Estado == EstadoLacunaBusca.Aberta && o.TermoNormalizado == l.TermoNormalizado),
            Versao = l.Versao,
        });

        var itens = await Ordenar(linhas, filtro)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .ToListAsync(ct);

        var dtos = itens.Select(l => new LacunaBuscaDto
        {
            Id = l.Id,
            Termo = l.Termo,
            Parceiro = l.Parceiro,
            NumeroChamado = l.NumeroChamado,
            Estado = l.Estado.ToString(),
            Nota = l.Nota,
            RegistradoPorNome = l.RegistradoPorNome,
            RegistradoEm = l.RegistradoEm,
            ResolvidaEm = l.ResolvidaEm,
            ResolvidoPorNome = l.ResolvidoPorNome,
            OcorrenciasAbertas = l.OcorrenciasAbertas,
            VersaoConcorrencia = l.Versao,
        }).ToArray();

        return new PaginaResultado<LacunaBuscaDto>(dtos, totalCount, filtro.Pagina, filtro.TamanhoPagina);
    }

    public async Task<PaginaResultado<LacunaBuscaTermoDto>> ListarTermosAsync(FiltroLacunaBuscaTermo filtro, CancellationToken ct)
    {
        var estado = filtro.Estado;
        var doEstado = Contexto.LacunasBusca.AsNoTracking().Where(l => l.DesativadoEm == null && l.Estado == estado);

        // O grupo é a coluna termonormalizado — a MESMA que AlterarAbertasDoMesmoTermoAsync usa para
        // tratar as irmãs: o que a fila agrupa é exatamente o que "incluirMesmoTermo" atinge.
        var grupos = doEstado
            .GroupBy(l => l.TermoNormalizado)
            .Select(g => new ResumoGrupo
            {
                TermoNormalizado = g.Key,
                Ocorrencias = g.Count(),
                PrimeiraEm = g.Min(l => l.CriadoEm),
                UltimaEm = g.Max(l => l.CriadoEm),
            });

        var totalCount = await grupos.CountAsync(ct);

        var pagina = await OrdenarGrupos(grupos, filtro)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .ToListAsync(ct);

        if (pagina.Count == 0)
            return new PaginaResultado<LacunaBuscaTermoDto>([], totalCount, filtro.Pagina, filtro.TamanhoPagina);

        var termos = pagina.Select(g => g.TermoNormalizado).ToArray();
        var doEstadoNaPagina = doEstado.Where(l => termos.Contains(l.TermoNormalizado));

        // Ocorrência mais recente de cada grupo: fornece a forma exibível do termo, quem registrou
        // por último e o par id + token que a tela usa na ação sobre o grupo.
        var referencias = (await doEstadoNaPagina
                .GroupBy(l => l.TermoNormalizado)
                .Select(g => g
                    .OrderByDescending(l => l.CriadoEm)
                    .ThenByDescending(l => l.Id)
                    .Select(l => new ReferenciaGrupo
                    {
                        TermoNormalizado = l.TermoNormalizado,
                        Id = l.Id,
                        Termo = l.Termo,
                        RegistradoPorNome = l.RegistradoPorNome,
                        Versao = l.Versao,
                    })
                    .First())
                .ToListAsync(ct))
            .ToDictionary(r => r.TermoNormalizado);

        // Contextos já agregados no banco: uma linha por combinação distinta de parceiro e chamado.
        var contextos = (await doEstadoNaPagina
                .Where(l => l.Parceiro != null || l.NumeroChamado != null)
                .GroupBy(l => new { l.TermoNormalizado, l.Parceiro, l.NumeroChamado })
                .Select(g => new ContextoGrupo
                {
                    TermoNormalizado = g.Key.TermoNormalizado,
                    Parceiro = g.Key.Parceiro,
                    NumeroChamado = g.Key.NumeroChamado,
                    UltimaEm = g.Max(l => l.CriadoEm),
                })
                .ToListAsync(ct))
            .ToLookup(c => c.TermoNormalizado);

        var itens = pagina.Select(grupo =>
        {
            var referencia = referencias[grupo.TermoNormalizado];
            var (parceiros, totalParceiros) = DistintosMaisRecentes(contextos[grupo.TermoNormalizado], c => c.Parceiro);
            var (chamados, totalChamados) = DistintosMaisRecentes(contextos[grupo.TermoNormalizado], c => c.NumeroChamado);

            return new LacunaBuscaTermoDto
            {
                TermoNormalizado = grupo.TermoNormalizado,
                Termo = referencia.Termo,
                Ocorrencias = grupo.Ocorrencias,
                PrimeiraEm = grupo.PrimeiraEm,
                UltimaEm = grupo.UltimaEm,
                Parceiros = parceiros,
                TotalParceiros = totalParceiros,
                Chamados = chamados,
                TotalChamados = totalChamados,
                UltimoRegistradoPorNome = referencia.RegistradoPorNome,
                LacunaReferenciaId = referencia.Id,
                VersaoConcorrencia = referencia.Versao,
            };
        }).ToArray();

        return new PaginaResultado<LacunaBuscaTermoDto>(itens, totalCount, filtro.Pagina, filtro.TamanhoPagina);
    }

    /// <summary>
    /// Valores distintos (sem diferença de caixa) do mais recente ao mais antigo, limitados ao teto,
    /// com o total dos distintos. Vale a grafia da ocorrência mais recente.
    /// </summary>
    private static (IReadOnlyList<string> Valores, int Total) DistintosMaisRecentes(
        IEnumerable<ContextoGrupo> contextos, Func<ContextoGrupo, string?> selecionar)
    {
        var distintos = contextos
            .Select(c => (Valor: selecionar(c), c.UltimaEm))
            .Where(c => c.Valor is not null)
            .GroupBy(c => c.Valor!, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.UltimaEm).ThenBy(c => c.Valor, StringComparer.Ordinal).First())
            .OrderByDescending(c => c.UltimaEm)
            .ThenBy(c => c.Valor, StringComparer.Ordinal)
            .ToList();

        var valores = distintos.Take(LimitesLacunaBusca.ContextosPorTermo).Select(c => c.Valor!).ToArray();

        return (valores, distintos.Count);
    }

    public Task<int> ContarAbertasDoTermoAsync(string termoNormalizado, CancellationToken ct) =>
        Contexto.LacunasBusca.AsNoTracking().CountAsync(
            l => l.DesativadoEm == null && l.Estado == EstadoLacunaBusca.Aberta && l.TermoNormalizado == termoNormalizado, ct);

    public Task<int> AlterarAbertasDoMesmoTermoAsync(LacunaBusca referencia, CancellationToken ct)
    {
        // Copia a resolução já validada e normalizada pela entidade — nota, data e usuário — em
        // vez de reaplicar aqui as regras de aparo.
        var id = referencia.Id;
        var termoNormalizado = referencia.TermoNormalizado;
        var estado = referencia.Estado;
        var nota = referencia.Nota;
        var resolvidaEm = referencia.ResolvidaEm;
        var resolvidoPorId = referencia.ResolvidoPorId;
        var resolvidoPorNome = referencia.ResolvidoPorNome;

        return Contexto.LacunasBusca
            .Where(l => l.TermoNormalizado == termoNormalizado
                && l.Estado == EstadoLacunaBusca.Aberta
                && l.DesativadoEm == null
                && l.Id != id)
            .ExecuteUpdateAsync(
                alteracao => alteracao
                    .SetProperty(l => l.Estado, estado)
                    .SetProperty(l => l.Nota, nota)
                    .SetProperty(l => l.ResolvidaEm, resolvidaEm)
                    .SetProperty(l => l.ResolvidoPorId, resolvidoPorId)
                    .SetProperty(l => l.ResolvidoPorNome, resolvidoPorNome),
                ct);
    }

    public async Task AdicionarAsync(LacunaBusca lacuna, CancellationToken ct) =>
        await Contexto.LacunasBusca.AddAsync(lacuna, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct) => SalvarAsync(ct);

    /// <summary>
    /// Ordenação com whitelist de colunas (<c>sortBy</c> desconhecido cai no padrão: mais recentes
    /// primeiro). Todo caminho termina no <c>Id</c>: sem desempate por chave única, o
    /// <c>OFFSET/LIMIT</c> não tem ordem total e um registro pode aparecer em duas páginas.
    /// </summary>
    private static IOrderedQueryable<LinhaLacuna> Ordenar(IQueryable<LinhaLacuna> linhas, FiltroLacunaBusca filtro)
    {
        var decrescente = !string.Equals(filtro.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return filtro.SortBy?.ToLowerInvariant() switch
        {
            "ocorrencias" => decrescente
                ? linhas.OrderByDescending(l => l.OcorrenciasAbertas).ThenByDescending(l => l.RegistradoEm).ThenByDescending(l => l.Id)
                : linhas.OrderBy(l => l.OcorrenciasAbertas).ThenByDescending(l => l.RegistradoEm).ThenByDescending(l => l.Id),

            "termo" => decrescente
                ? linhas.OrderByDescending(l => l.TermoNormalizado).ThenByDescending(l => l.Id)
                : linhas.OrderBy(l => l.TermoNormalizado).ThenBy(l => l.Id),

            _ => decrescente
                ? linhas.OrderByDescending(l => l.RegistradoEm).ThenByDescending(l => l.Id)
                : linhas.OrderBy(l => l.RegistradoEm).ThenBy(l => l.Id),
        };
    }

    /// <summary>
    /// Ordenação dos grupos com whitelist (<c>sortBy</c> desconhecido cai no padrão: mais recorrentes
    /// primeiro). Todo caminho termina em <c>TermoNormalizado</c>, que é único por grupo: sem esse
    /// desempate o <c>OFFSET/LIMIT</c> repetiria e perderia grupos entre páginas.
    /// </summary>
    private static IOrderedQueryable<ResumoGrupo> OrdenarGrupos(IQueryable<ResumoGrupo> grupos, FiltroLacunaBuscaTermo filtro)
    {
        var decrescente = !string.Equals(filtro.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return filtro.SortBy?.ToLowerInvariant() switch
        {
            "recente" => decrescente
                ? grupos.OrderByDescending(g => g.UltimaEm).ThenBy(g => g.TermoNormalizado)
                : grupos.OrderBy(g => g.UltimaEm).ThenBy(g => g.TermoNormalizado),

            "termo" => decrescente
                ? grupos.OrderByDescending(g => g.TermoNormalizado)
                : grupos.OrderBy(g => g.TermoNormalizado),

            _ => decrescente
                ? grupos.OrderByDescending(g => g.Ocorrencias).ThenByDescending(g => g.UltimaEm).ThenBy(g => g.TermoNormalizado)
                : grupos.OrderBy(g => g.Ocorrencias).ThenByDescending(g => g.UltimaEm).ThenBy(g => g.TermoNormalizado),
        };
    }

    private sealed class ResumoGrupo
    {
        public required string TermoNormalizado { get; init; }

        public required int Ocorrencias { get; init; }

        public required DateTimeOffset PrimeiraEm { get; init; }

        public required DateTimeOffset UltimaEm { get; init; }
    }

    private sealed class ReferenciaGrupo
    {
        public required string TermoNormalizado { get; init; }

        public required int Id { get; init; }

        public required string Termo { get; init; }

        public string? RegistradoPorNome { get; init; }

        public required uint Versao { get; init; }
    }

    private sealed class ContextoGrupo
    {
        public required string TermoNormalizado { get; init; }

        public string? Parceiro { get; init; }

        public string? NumeroChamado { get; init; }

        public required DateTimeOffset UltimaEm { get; init; }
    }

    private sealed class LinhaLacuna
    {
        public required int Id { get; init; }

        public required string Termo { get; init; }

        public required string TermoNormalizado { get; init; }

        public string? Parceiro { get; init; }

        public string? NumeroChamado { get; init; }

        public required EstadoLacunaBusca Estado { get; init; }

        public string? Nota { get; init; }

        public string? RegistradoPorNome { get; init; }

        public required DateTimeOffset RegistradoEm { get; init; }

        public DateTimeOffset? ResolvidaEm { get; init; }

        public string? ResolvidoPorNome { get; init; }

        public required int OcorrenciasAbertas { get; init; }

        public required uint Versao { get; init; }
    }
}
