using Codex.Application.Common;
using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>
/// Regras de negócio da área interna de <see cref="Codex.Domain.Entities.Provedor"/> (I1-I5,
/// I3b). Criação de aviso de provedor (I14a2) fica em <see cref="IAvisoServicoInterno"/> — a
/// lógica de criação/cascata de <c>Aviso</c> mora toda junto, independente de qual rota aninhada
/// a originou.
/// </summary>
public interface IProvedorServicoInterno
{
    Task<PaginaResultado<ProvedorAdminListaItemDto>> ListarAsync(FiltroProvedorInterno filtro, CancellationToken ct);

    Task<ProvedorAdminDetalheDto> ObterDetalheAsync(int id, CancellationToken ct);

    Task<ProvedorAdminDetalheDto> CriarAsync(CriarProvedorDto dto, CancellationToken ct);

    Task<ProvedorAdminDetalheDto> AtualizarAsync(int id, AtualizarProvedorDto dto, uint versaoEsperada, CancellationToken ct);

    Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct);

    Task<ProvedorAdminDetalheDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct);
}
