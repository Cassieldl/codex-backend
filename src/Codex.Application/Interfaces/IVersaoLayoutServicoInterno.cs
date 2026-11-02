using Codex.Application.Common;
using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>Regras de negócio da área interna de <see cref="Codex.Domain.Entities.VersaoLayout"/> (I15a-I15d).</summary>
public interface IVersaoLayoutServicoInterno
{
    Task<PaginaResultado<VersaoLayoutAdminDto>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct);

    Task<VersaoLayoutAdminDto> CriarAsync(CriarVersaoLayoutDto dto, CancellationToken ct);

    Task<VersaoLayoutAdminDto> AtualizarAsync(int id, AtualizarVersaoLayoutDto dto, uint versaoEsperada, CancellationToken ct);

    Task<VersaoLayoutAdminDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct);

    Task<VersaoLayoutAdminDto> AtualizarEstruturaAsync(int id, AtualizarEstruturaVersaoLayoutDto dto, uint versaoEsperada, CancellationToken ct);
}
