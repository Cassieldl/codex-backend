using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>Regras de negócio da área interna de <see cref="Codex.Domain.Entities.Exemplo"/> (I38-I42).</summary>
public interface IExemploServicoInterno
{
    Task<IReadOnlyCollection<ExemploAdminDto>> ListarPorVersaoAsync(int versaoProvedorId, CancellationToken ct);

    Task<ExemploAdminDto> ObterPorIdAsync(int id, CancellationToken ct);

    Task<ExemploAdminDto> CriarAsync(int versaoProvedorId, CriarExemploDto dto, CancellationToken ct);

    Task<ExemploAdminDto> AtualizarAsync(int id, AtualizarExemploDto dto, uint versaoEsperada, CancellationToken ct);

    Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct);
}
