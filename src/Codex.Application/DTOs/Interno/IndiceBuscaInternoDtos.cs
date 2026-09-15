namespace Codex.Application.DTOs.Interno;

/// <summary>Resultado de I22 — reconstrução total do índice de busca.</summary>
public sealed record ReconstrucaoIndiceBuscaResultadoDto
{
    public required int ItensReindexados { get; init; }

    public required int DuracaoMs { get; init; }
}
