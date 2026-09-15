using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>Regras de negócio da área interna de <see cref="Codex.Domain.Entities.CampoVersao"/> (I11-I13b).</summary>
public interface ICampoVersaoServicoInterno
{
    Task<CamposVersaoAgregadoDto> ListarAgregadoAsync(int versaoProvedorId, CancellationToken ct);

    Task<CampoVersaoAdminDto> CriarAsync(int versaoProvedorId, CriarCampoVersaoDto dto, CancellationToken ct);

    Task<CampoVersaoAdminDto> AtualizarAsync(int id, AtualizarCampoVersaoDto dto, uint versaoEsperada, CancellationToken ct);

    Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct);
}
