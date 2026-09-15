using Codex.Application.Common;
using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>Regras de negócio da área interna de <see cref="Codex.Domain.Entities.Comunicado"/> (I30-I35).</summary>
public interface IComunicadoServicoInterno
{
    Task<PaginaResultado<ComunicadoAdminListaItemDto>> ListarAsync(FiltroComunicadoInterno filtro, CancellationToken ct);

    Task<ComunicadoAdminDto> ObterDetalheAsync(int id, CancellationToken ct);

    Task<ComunicadoAdminDto> CriarAsync(CriarComunicadoDto dto, CancellationToken ct);

    Task<ComunicadoAdminDto> AtualizarAsync(int id, AtualizarComunicadoDto dto, uint versaoEsperada, CancellationToken ct);

    Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct);

    /// <summary>
    /// Publicar um comunicado já expirado lança <c>RegraDeNegocioException</c> com o código
    /// <c>COMUNICADO_EXPIRADO</c> (decisoes.md B-4) — a mensagem diz o próximo passo, e é
    /// requisito. Despublicar é sempre permitido: é o mesmo estado a que o job converge.
    /// </summary>
    Task<ComunicadoAdminDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct);
}

