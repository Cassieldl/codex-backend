using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Repositório de leitura/escrita INTERNA de <see cref="VersaoProvedor"/> — enxerga rascunho.</summary>
public interface IVersaoProvedorRepositorioInterno
{
    Task<VersaoProvedor?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    /// <summary>
    /// Trava a linha da versão (<c>SELECT ... FOR UPDATE</c>) até o fim da transação corrente —
    /// Serializa a troca de layout de origem (PUT da versão) contra a criação
    /// de campo (POST de campo), que hoje lê o layout atual sem tocar o <c>xmin</c> do pai, então o
    /// If-Match do PUT não detecta a corrida. Chamar DENTRO da transação de escrita, ANTES de
    /// qualquer `COUNT`/revalidação que dependa do estado atual da versão não mudar sob os pés.
    /// </summary>
    Task BloquearParaAlteracaoAsync(int id, CancellationToken ct);

    /// <summary>I6 — lista completa (sem paginação, <c>ApiResponse&lt;T[]&gt;</c>), enxerga rascunho.</summary>
    Task<IReadOnlyCollection<VersaoAdminListaItemDto>> ListarPorProvedorAsync(int provedorId, CancellationToken ct);

    /// <summary>Todas as versões ativas — usado por I22 (reconstrução total do índice).</summary>
    Task<IReadOnlyCollection<VersaoProvedor>> ListarTodosAtivosAsync(CancellationToken ct);

    Task AdicionarAsync(VersaoProvedor versao, CancellationToken ct);

    Task<IReadOnlyCollection<ItemNaturezaOperacaoDto>> ListarNaturezasOperacaoAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>Apaga as linhas atuais da versão e grava as novas. Chamar dentro da transação de escrita.</summary>
    Task SubstituirNaturezasOperacaoAsync(int versaoProvedorId, IReadOnlyCollection<NaturezaOperacao> naturezas, CancellationToken ct);

    Task SalvarAlteracoesAsync(CancellationToken ct);
}
