using Codex.Application.Common;
using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>Consulta de versões com verificação vencida (escopo funcional v3.1 §9.1).</summary>
public interface IVerificacaoVencidaRepositorioInterno
{
    /// <summary>
    /// Versões ativas de provedores ativos nunca verificadas ou verificadas em
    /// <paramref name="dataLimite"/> ou antes. Nunca verificadas primeiro; depois da mais antiga para a
    /// mais nova; desempate pelo id da versão.
    /// </summary>
    Task<PaginaResultado<VersaoVerificacaoVencidaBrutaDto>> ListarAsync(
        DateOnly dataLimite, int pagina, int tamanhoPagina, CancellationToken ct);
}
