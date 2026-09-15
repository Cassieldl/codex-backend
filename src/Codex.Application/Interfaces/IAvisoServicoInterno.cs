using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>
/// Regras de negócio da área interna de <see cref="Codex.Domain.Entities.Aviso"/> — as duas
/// rotas de criação (I14a de versão, I14a2 de provedor) e a rota única de atualização (I14b, o
/// escopo nunca muda depois de criado).
/// </summary>
public interface IAvisoServicoInterno
{
    Task<AvisoAdminDto> CriarDeVersaoAsync(int versaoProvedorId, CriarAvisoDeVersaoDto dto, CancellationToken ct);

    Task<AvisoAdminDto> CriarDeProvedorAsync(int provedorId, CriarAvisoDeProvedorDto dto, CancellationToken ct);

    Task<AvisoAdminDto> AtualizarAsync(int id, AtualizarAvisoDto dto, uint versaoEsperada, CancellationToken ct);

    Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct);

    Task<AvisoAdminDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct);
}
