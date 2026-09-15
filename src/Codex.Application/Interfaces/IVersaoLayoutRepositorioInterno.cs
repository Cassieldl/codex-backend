using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Repositório de leitura/escrita INTERNA de <see cref="VersaoLayout"/> (I15a-I15d).</summary>
public interface IVersaoLayoutRepositorioInterno
{
    Task<VersaoLayout?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    Task<PaginaResultado<VersaoLayoutAdminDto>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct);

    /// <summary>Próxima ordem livre (<c>max(ordem)+1</c>) — atribuída SÓ pelo service na criação (§4.2, nunca pelo cliente).</summary>
    Task<int> ObterProximaOrdemAsync(CancellationToken ct);

    Task AdicionarAsync(VersaoLayout versaoLayout, CancellationToken ct);

    Task SalvarAlteracoesAsync(CancellationToken ct);
}
