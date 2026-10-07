using Codex.Application.Common;
using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>Regras da fila de versões com verificação vencida (escopo funcional v3.1 §9.1).</summary>
public interface IVerificacaoVencidaServicoInterno
{
    Task<PaginaResultado<VersaoVerificacaoVencidaDto>> ListarAsync(FiltroVerificacaoVencida filtro, CancellationToken ct);
}
