using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Repositório de leitura/escrita INTERNA de <see cref="CampoVersao"/> (I11/I12/I13a/I13b).</summary>
public interface ICampoVersaoRepositorioInterno
{
    Task<CampoVersao?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    Task<IReadOnlyCollection<CampoVersaoAdminDto>> ListarPorVersaoAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>Campos canônicos ATIVOS da versão de layout de <paramref name="versaoProvedorId"/> que ainda não têm <see cref="CampoVersao"/> nesta versão de provedor (I11 <c>camposLayoutAusentes</c>).</summary>
    Task<IReadOnlyCollection<CampoLayoutResponseDto>> ListarCamposLayoutAusentesAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>Verifica se <paramref name="campoLayoutId"/> existe, está Ativo e pertence à versão de layout usada por <paramref name="versaoProvedorId"/> (regra cross-entity de §5.2).</summary>
    Task<bool> CampoLayoutEstaAtivoNaVersaoDoProvedorAsync(int campoLayoutId, int versaoProvedorId, CancellationToken ct);

    Task<int> ContarPorVersaoAsync(int versaoProvedorId, CancellationToken ct);

    Task AdicionarAsync(CampoVersao campoVersao, CancellationToken ct);

    Task SalvarAlteracoesAsync(CancellationToken ct);
}
