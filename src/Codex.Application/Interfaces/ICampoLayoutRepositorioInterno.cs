using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Repositório de leitura/escrita INTERNA de <see cref="CampoLayout"/> (I16/I17a/I17b/I17c).</summary>
public interface ICampoLayoutRepositorioInterno
{
    /// <summary>
    /// <paramref name="comOrigemCarregada"/> inclui (<c>Include</c>) <see cref="CampoLayout.VersaoLayoutOrigem"/>
    /// — obrigatório antes de chamar <see cref="CampoLayout.Descontinuar"/> (risco #2 do DBA).
    /// </summary>
    Task<CampoLayout?> ObterPorIdAsync(int id, bool rastreada, bool comOrigemCarregada, CancellationToken ct);

    Task<IReadOnlyCollection<CampoLayoutResponseDto>> ListarPorVersaoLayoutAsync(int versaoLayoutId, CancellationToken ct);

    /// <summary>Todos os campos canônicos ativos — usado por I22 (reconstrução total do índice).</summary>
    Task<IReadOnlyCollection<CampoLayout>> ListarTodosAtivosAsync(CancellationToken ct);

    Task AdicionarAsync(CampoLayout campoLayout, CancellationToken ct);

    Task SalvarAlteracoesAsync(CancellationToken ct);
}
