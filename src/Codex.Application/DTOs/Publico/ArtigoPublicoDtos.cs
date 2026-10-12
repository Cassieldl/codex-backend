using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Publico;

/// <summary>Filtro de listagem pública de artigos (P7). <c>Tipo</c> repetível — filtro OR interno entre os valores.</summary>
public sealed record FiltroArtigoPublico(
    TipoArtigo[]? Tipo,
    string? Q,
    int Pagina,
    int TamanhoPagina);

/// <summary>
/// Item de listagem pública de artigos (P7) — só artigos com <c>Publicado = true</c>, nunca inclui
/// o slug reservado (<see cref="Codex.Domain.Constantes.SlugsReservadosArtigo"/>).
/// </summary>
public sealed record ArtigoListaItemDto
{
    public required string Slug { get; init; }

    /// <summary>Enum mapeado para <c>string</c> explicitamente (<c>TipoArtigo.ToString()</c>) — nunca enum cru na resposta.</summary>
    public required string Tipo { get; init; }

    public required string Titulo { get; init; }

    /// <summary>Derivado no backend (primeiro parágrafo, texto puro) — <see cref="Codex.Application.Common.ResumoArtigo"/>. <c>null</c> é válido, nunca "—".</summary>
    public string? Resumo { get; init; }

    /// <summary>Data pura ISO (§0.4 — nunca <c>DateTimeOffset</c>, que vazaria hora/offset).</summary>
    public DateOnly? VerificadoEm { get; init; }
}

/// <summary>Página do artigo (P8). Espelha <c>ErroDetalheDto</c> — mesmo padrão de 404/410/308.</summary>
public sealed record ArtigoDetalheDto
{
    public required string Slug { get; init; }

    public required string Tipo { get; init; }

    public required string Titulo { get; init; }

    /// <summary>Markdown cru, nunca HTML (U-4/D11) — sanitização e renderização ficam no frontend.</summary>
    public required string Corpo { get; init; }

    public DateOnly? VerificadoEm { get; init; }

    /// <summary>Fonte única: <see cref="Codex.Domain.Constantes.SlugsReservadosArtigo.RotaCanonica"/>.</summary>
    public required string UrlCanonica { get; init; }

    public required bool Reservado { get; init; }

    /// <summary>
    /// Só campos com <c>CampoLayout.Publicado == true</c> E <c>VersaoLayout.Publicado == true</c>
    /// (cascata de visibilidade, §4.3) — superfície pública nunca revela rascunho.
    /// </summary>
    public required IReadOnlyCollection<ReferenciaCampoLayoutDto> CamposLayoutRelacionados { get; init; }

    /// <summary>Só erros com <c>Publicado == true</c> (§4.3, B-3).</summary>
    public required IReadOnlyCollection<ReferenciaErroDto> ErrosRelacionados { get; init; }
}

/// <summary>Referência pública a um campo do layout, a partir de um artigo (§4.3).</summary>
public sealed record ReferenciaCampoLayoutDto
{
    public string Rotulo => "CAMPO DO LAYOUT";

    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public required string CaminhoXml { get; init; }

    /// <summary>O que o campo é — o leitor entende a referência sem sair do artigo.</summary>
    public required string Significado { get; init; }

    /// <summary>"/layout/campos/{slug}" — a página pública do campo.</summary>
    public required string Url { get; init; }
}

/// <summary>Referência pública a um erro conhecido, a partir de um artigo (§4.3).</summary>
public sealed record ReferenciaErroDto
{
    public string Rotulo => "REJEIÇÃO";

    public required string Slug { get; init; }

    public required string Codigo { get; init; }

    public required string Titulo { get; init; }

    public required string Url { get; init; }
}
