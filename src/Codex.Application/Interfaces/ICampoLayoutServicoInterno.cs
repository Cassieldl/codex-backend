using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>Regras de negócio da área interna de <see cref="Codex.Domain.Entities.CampoLayout"/> (I16-I17c).</summary>
public interface ICampoLayoutServicoInterno
{
    Task<IReadOnlyCollection<CampoLayoutResponseDto>> ListarPorVersaoLayoutAsync(int versaoLayoutId, CancellationToken ct);

    Task<CampoLayoutResponseDto> CriarAsync(int versaoLayoutId, CriarCampoLayoutDto dto, CancellationToken ct);

    Task<CampoLayoutResponseDto> AtualizarAsync(int id, AtualizarCampoLayoutDto dto, uint versaoEsperada, CancellationToken ct);

    Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct);

    Task<CampoLayoutResponseDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct);
}
