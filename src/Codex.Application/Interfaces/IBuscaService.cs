using Codex.Application.DTOs.Publico;

namespace Codex.Application.Interfaces;

/// <summary>
/// Orquestra uma consulta de busca: valida/normaliza a entrada (proteção de custo — cap de
/// termo/página, analise-backend.md §6.1–§6.5) e delega a cascata real de consulta para
/// <see cref="IConsultaBusca"/>. Implementação em <c>Codex.Application/UseCases/Busca/BuscaService.cs</c>
/// (BACK-2) — mantida deliberadamente fina para ser testável com <see cref="IConsultaBusca"/>
/// mockado (§6.7: mock prova que o C# chamou o método, não que o SQL funciona).
/// </summary>
public interface IBuscaService
{
    /// <summary>
    /// <paramref name="incluirRascunho"/> distingue P1 (público, <c>false</c>) de I21 (interno,
    /// M15, <c>true</c>) — o mesmo contrato de saída serve às duas superfícies.
    /// </summary>
    Task<ResultadoBuscaDto> BuscarAsync(BuscaConsultaEntrada entrada, bool incluirRascunho, CancellationToken ct);
}
