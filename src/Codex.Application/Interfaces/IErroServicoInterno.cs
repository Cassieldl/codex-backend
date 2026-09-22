using Codex.Application.Common;
using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>Regras de negócio da área interna de <see cref="Codex.Domain.Entities.Erro"/> (I18-I20c).</summary>
public interface IErroServicoInterno
{
    Task<PaginaResultado<ErroAdminListaItemDto>> ListarAsync(FiltroErroInterno filtro, CancellationToken ct);

    Task<ErroAdminDetalheDto> ObterDetalheAsync(int id, CancellationToken ct);

    Task<ErroAdminDetalheDto> CriarAsync(CriarErroDto dto, CancellationToken ct);

    Task<ErroAdminDetalheDto> AtualizarAsync(int id, AtualizarErroDto dto, uint versaoEsperada, CancellationToken ct);

    Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct);

    Task<ErroAdminDetalheDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct);

    /// <summary>I43 (P-10) — troca de slug: grava o redirecionamento e reindexa a busca na mesma transação.</summary>
    Task<ErroAdminDetalheDto> AtualizarSlugAsync(int id, AlterarSlugDto dto, uint versaoEsperada, CancellationToken ct);
}
