using Codex.Application.Common;
using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>Regras de negócio da fila editorial (escopo funcional v3.1 §9.2).</summary>
public interface ILacunaBuscaServicoInterno
{
    Task<PaginaResultado<LacunaBuscaDto>> ListarAsync(FiltroLacunaBusca filtro, CancellationToken ct);

    Task<PaginaResultado<LacunaBuscaTermoDto>> ListarTermosAsync(FiltroLacunaBuscaTermo filtro, CancellationToken ct);

    Task<LacunaBuscaDto> ObterAsync(int id, CancellationToken ct);

    Task<LacunaBuscaDto> RegistrarAsync(RegistrarLacunaBuscaDto dto, UsuarioDaRequisicao usuario, CancellationToken ct);

    Task<AlteracaoEstadoLacunaBuscaDto> AlterarEstadoAsync(
        int id, AlterarEstadoLacunaBuscaDto dto, uint versaoEsperada, UsuarioDaRequisicao usuario, CancellationToken ct);
}
