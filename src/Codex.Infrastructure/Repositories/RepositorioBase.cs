using Codex.Domain.Exceptions;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Codex.Infrastructure.Repositories;

/// <summary>
/// Base reutilizável para repositórios EF Core (padrão de §3.3 da análise de backend). Concentra
/// as duas regras que, historicamente, já quebraram esta base quando violadas:
///
/// 1. <b>Rastreamento.</b> Toda entidade que vai ser alvo de <c>Update</c>/<c>Delete</c> tem que
///    ser carregada RASTREADA (sem <c>AsNoTracking()</c>). <c>xmin</c> (concorrência otimista,
///    ver Configurations) é uma shadow/property de sistema que só existe de verdade numa entry
///    rastreada — se a entidade for lida com <c>AsNoTracking()</c> e depois reanexada via
///    <c>Attach</c> + <c>EntityState.Modified</c>, o EF assume o valor original de <c>xmin</c>
///    como zero, e o `UPDATE ... WHERE xmin = 0` nunca casa uma linha real: 100% dos
///    PUT/DELETE falham com <see cref="DbUpdateConcurrencyException"/> FALSA (não é conflito de
///    verdade, é o bug de rastreamento). NUNCA "arrumar" isso adicionando <c>AsNoTracking()</c>
///    "por consistência" com os métodos de leitura pública — é exatamente o oposto do que essa
///    entidade precisa. Métodos de leitura pura (público, sem intenção de mutar) continuam
///    usando <c>AsNoTracking()</c> normalmente — a regra é sobre o caminho de escrita.
/// 2. <b>Tradução de erro de banco.</b> Colisão de UNIQUE (slug, código, <c>ordem</c> de
///    <c>versoeslayout</c>) chega como <see cref="DbUpdateException"/> com um
///    <see cref="PostgresException"/> interno (SqlState 23505) — nunca deixar isso vazar cru
///    para o controller; traduzir para <see cref="RegistroDuplicadoException"/> (409).
/// </summary>
public abstract class RepositorioBase<TDbContext>
    where TDbContext : DbContext
{
    protected TDbContext Contexto { get; }

    protected RepositorioBase(TDbContext contexto)
    {
        Contexto = contexto;
    }

    /// <summary>
    /// Persiste as mudanças pendentes, traduzindo violação de UNIQUE do Postgres (23505) em
    /// <see cref="RegistroDuplicadoException"/>. Conflito real de concorrência otimista
    /// (<see cref="DbUpdateConcurrencyException"/>, xmin divergente) NÃO é traduzido aqui — sobe
    /// como está, para o middleware de erro mapear para 409 com o código
    /// <c>CONFLITO_CONCORRENCIA</c> (analise-backend.md §8).
    /// </summary>
    protected async Task SalvarAsync(CancellationToken ct)
    {
        try
        {
            await Contexto.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } postgresEx)
        {
            throw new RegistroDuplicadoException(
                $"Registro duplicado: {postgresEx.ConstraintName ?? "restrição única violada"}.", ex);
        }
    }
}
