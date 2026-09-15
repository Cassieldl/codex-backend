namespace Codex.Application.DTOs.Interno;

/// <summary>
/// Payload compartilhado pelos 6 endpoints de publicação (I3b, I9, I14d, I15d, I17c, I20c — M19).
/// Nunca aceita outro campo além de <see cref="Publicado"/> — publicação só muda por este DTO
/// dedicado, nunca embutida em <c>AtualizarXDto</c> (analise-backend.md §4.2, nota "sem mass
/// assignment").
/// </summary>
public sealed record AtualizarPublicacaoDto
{
    public required bool Publicado { get; init; }
}
