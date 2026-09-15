namespace Codex.Application.DTOs.Publico;

/// <summary>Item de listagem pública de erros (P5) — só erros com <c>Publicado = true</c>.</summary>
public sealed record ErroListaItemDto
{
    public required string Slug { get; init; }

    public required string Codigo { get; init; }

    public required string Origem { get; init; }

    public required string Titulo { get; init; }
}

/// <summary>
/// Página do erro (P6). <see cref="VersoesAplicaveis"/> pode ser lista VAZIA (U9) — nunca
/// <c>null</c>/omitida, para não sugerir dado faltando (§4.1).
/// </summary>
public sealed record ErroDetalheDto
{
    public required string Slug { get; init; }

    public required string Codigo { get; init; }

    public required string Origem { get; init; }

    public required string Titulo { get; init; }

    public string? CausaProvavel { get; init; }

    public string? SolucaoSugerida { get; init; }

    public required IReadOnlyCollection<string> Variantes { get; init; }

    public required IReadOnlyCollection<VersaoAplicavelPublicaDto> VersoesAplicaveis { get; init; }

    public required IReadOnlyCollection<CampoRelacionadoPublicoDto> CamposRelacionados { get; init; }

    /// <summary>
    /// Bloco aditivo da 002 (P6, analise-backend.md §4.4) — só artigos publicados (B-3). Sempre
    /// presente, <c>[]</c> quando não há vínculo, nunca <c>null</c>/omitido (mesma regra de
    /// <see cref="VersoesAplicaveis"/>, U9).
    /// </summary>
    public required IReadOnlyCollection<ReferenciaArtigoDto> ArtigosRelacionados { get; init; }
}

/// <summary>Artigo relacionado a um erro (bloco aditivo da 002, P6/§4.4) — só artigos publicados chegam aqui.</summary>
public sealed record ReferenciaArtigoDto
{
    public string Rotulo => "ARTIGO";

    public required string Slug { get; init; }

    /// <summary>Enum mapeado para <c>string</c> explicitamente — nunca enum cru na resposta.</summary>
    public required string Tipo { get; init; }

    public required string Titulo { get; init; }

    public required string Url { get; init; }
}

/// <summary>Versão de provedor aplicável ao erro — só versões (e provedores) publicados chegam aqui (cascata de visibilidade).</summary>
public sealed record VersaoAplicavelPublicaDto
{
    public required string ProvedorSlug { get; init; }

    public required string ProvedorNome { get; init; }

    public required string VersaoSlug { get; init; }

    public required string VersaoRotulo { get; init; }
}

/// <summary>
/// Achado bruto de <c>IErroRepositorioPublico.ListarVersoesAplicaveisPublicadasAsync</c> — carrega
/// <see cref="VersaoProvedorId"/> além dos slugs, porque <c>ListarCamposRelacionadosAsync</c>
/// precisa do id para casar contra <c>CampoVersao</c> (M10).
/// </summary>
public sealed record VersaoAplicavelBrutaDto
{
    public required int VersaoProvedorId { get; init; }

    public required string ProvedorSlug { get; init; }

    public required string ProvedorNome { get; init; }

    public required string VersaoSlug { get; init; }

    public required string VersaoRotulo { get; init; }
}

/// <summary>
/// Campo canônico relacionado ao erro (M10). Sem contexto de provedor/versão aplicável e pública,
/// <see cref="Ancoras"/> vem vazio e o frontend renderiza texto simples — nunca link para uma
/// página pública de Layout InvoiCy (que é da demanda 002).
/// </summary>
public sealed record CampoRelacionadoPublicoDto
{
    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public required string CaminhoXml { get; init; }

    public required IReadOnlyCollection<AncoraCampoVersaoPublicaDto> Ancoras { get; init; }
}

/// <summary>Aponta para a âncora do campo dentro de uma versão de provedor pública específica (M10).</summary>
public sealed record AncoraCampoVersaoPublicaDto
{
    public required string ProvedorSlug { get; init; }

    public required string VersaoSlug { get; init; }
}
