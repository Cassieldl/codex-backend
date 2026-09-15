using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>I22 — reconstrução total do índice de busca a partir das entidades de origem (drift detection/correção, modelo-dados.md §5.6).</summary>
public interface IIndiceBuscaReconstrucaoServico
{
    Task<ReconstrucaoIndiceBuscaResultadoDto> ReconstruirAsync(CancellationToken ct);
}
