using Codex.Application.Common;
using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>Regras de negócio da área interna de <see cref="Codex.Domain.Entities.Artigo"/> (I23-I29, I36).</summary>
public interface IArtigoServicoInterno
{
    Task<PaginaResultado<ArtigoAdminListaItemDto>> ListarAsync(FiltroArtigoInterno filtro, CancellationToken ct);

    Task<ArtigoAdminDto> ObterDetalheAsync(int id, CancellationToken ct);

    Task<ArtigoAdminDto> CriarAsync(CriarArtigoDto dto, CancellationToken ct);

    Task<ArtigoAdminDto> AtualizarAsync(int id, AtualizarArtigoDto dto, uint versaoEsperada, CancellationToken ct);

    /// <summary>Rejeita slug reservado com 422 <c>ARTIGO_RESERVADO</c> (§5.2, I27).</summary>
    Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct);

    /// <summary>Publicar (<paramref name="publicado"/> = true) é sempre permitido, inclusive reservado. Despublicar reservado é 422 <c>ARTIGO_RESERVADO</c>.</summary>
    Task<ArtigoAdminDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct);

    Task<ArtigoAdminDto> AtualizarVerificacaoAsync(int id, AtualizarVerificacaoArtigoDto dto, uint versaoEsperada, CancellationToken ct);

    /// <summary>I36 — resolução de F-2 (endpoint dedicado, nunca varredura de página).</summary>
    Task<ArtigoExisteDto> ExisteAsync(string slug, CancellationToken ct);

    /// <summary>I44 (P-10) — troca de slug: recusa o artigo reservado (422), grava o redirecionamento e reindexa a busca.</summary>
    Task<ArtigoAdminDto> AtualizarSlugAsync(int id, AlterarSlugDto dto, uint versaoEsperada, CancellationToken ct);
}
