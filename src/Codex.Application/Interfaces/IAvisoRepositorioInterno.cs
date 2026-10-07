using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Repositório de leitura/escrita INTERNA de <see cref="Aviso"/> — enxerga rascunho e publicado (I2, I14a/I14a2/I14b/I14c/I14d).</summary>
/// <remarks>
/// <see cref="Aviso.CamposAfetadosIds"/> não é coluna: vive em <c>avisoscampos</c>. Os métodos de
/// leitura de aviso de escopo versão devolvem a entidade já com a coleção preenchida (só campos
/// ainda ativos), rastreada ou não — a leitura nunca pode devolver "sem campos" por omissão, senão
/// o PUT seguinte, que reenvia o que o GET mostrou, apagaria o vínculo.
/// </remarks>
public interface IAvisoRepositorioInterno
{
    Task<Aviso?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    /// <summary>
    /// Dos <paramref name="campoVersaoIds"/> pedidos, os que são <c>CampoVersao</c> ativos DESTA
    /// versão. Quem chama compara com o pedido: o que faltar é id inexistente, removido ou de
    /// outra versão.
    /// </summary>
    Task<IReadOnlyCollection<int>> ListarCamposAtivosDaVersaoAsync(
        int versaoProvedorId, IReadOnlyCollection<int> campoVersaoIds, CancellationToken ct);

    /// <summary>
    /// Troca TODO o vínculo do aviso: remove as linhas atuais de <c>avisoscampos</c> (inclusive as
    /// que apontam para campo já removido) e grava as novas. Não salva — o commit é do chamador.
    /// </summary>
    Task SubstituirCamposAfetadosAsync(int avisoId, IReadOnlyCollection<int> campoVersaoIds, CancellationToken ct);

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
