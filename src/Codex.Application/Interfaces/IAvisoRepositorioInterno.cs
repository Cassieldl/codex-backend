using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Repositório de leitura/escrita INTERNA de <see cref="Aviso"/> — enxerga rascunho e publicado (I2, I14a/I14a2/I14b/I14c/I14d).</summary>
public interface IAvisoRepositorioInterno
{
    Task<Aviso?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    /// <summary>TODOS os avisos de escopo PROVEDOR daquele provedor, publicados e rascunho (I2, diferente de P3/U8).</summary>
    Task<IReadOnlyCollection<Aviso>> ListarPorProvedorAsync(int provedorId, CancellationToken ct);

    /// <summary>
    /// TODOS os avisos de escopo VERSÃO daquela versão, publicados e rascunho — o par de
    /// <see cref="ListarPorProvedorAsync"/> para o detalhe de versão (I8a), que edita esses avisos
    /// no lugar.
    /// </summary>
    Task<IReadOnlyCollection<Aviso>> ListarPorVersaoAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>Todos os avisos ativos (qualquer escopo) — usado por I22 (reconstrução total do índice).</summary>
    Task<IReadOnlyCollection<Aviso>> ListarTodosAtivosAsync(CancellationToken ct);

    Task AdicionarAsync(Aviso aviso, CancellationToken ct);

    Task SalvarAlteracoesAsync(CancellationToken ct);
}
