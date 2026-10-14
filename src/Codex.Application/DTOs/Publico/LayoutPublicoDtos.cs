namespace Codex.Application.DTOs.Publico;

/// <summary>Índice público do vocabulário canônico do Layout InvoiCy (P10, analise-backend.md §4.3/§4.4).</summary>
public sealed record LayoutIndiceDto
{
    /// <summary>
    /// A versão publicada com <c>Estado == Vigente</c> — no máximo uma (garantida pelo índice
    /// único parcial <c>uq_codex_versoeslayout_estado_vigente</c>, não por checagem em código).
    /// <c>null</c> quando nenhuma versão vigente está publicada.
    /// </summary>
    public LayoutVersaoResumoDto? Vigente { get; init; }

    public required IReadOnlyCollection<LayoutVersaoResumoDto> Versoes { get; init; }
}

/// <summary>
/// Resumo de uma versão de layout, reutilizado nos três endpoints (índice, verbete de versão e
/// verbete de campo). <see cref="TotalCampos"/> é sempre a contagem de campos VISÍVEIS (§4.1) e
/// ATIVOS NAQUELA VERSÃO específica (fórmula M12, §4.2) — nunca o total bruto de linhas de
/// <c>camposlayout</c> vinculadas à versão.
/// </summary>
public sealed record LayoutVersaoResumoDto
{
    public required string Slug { get; init; }

    public required string Rotulo { get; init; }

    /// <summary>Enum mapeado para <c>string</c> explicitamente (<c>EstadoVersaoLayout</c>) — nunca enum cru na resposta.</summary>
    public required string Estado { get; init; }

    public required int Ordem { get; init; }

    public required int TotalCampos { get; init; }
}

/// <summary>Dicionário completo de uma versão publicada do layout (P11, §4.3).</summary>
public sealed record LayoutVersaoDto
{
    public required LayoutVersaoResumoDto Versao { get; init; }

    public string? Descricao { get; init; }

    /// <summary>A versão publicada com a maior <c>Ordem</c> MENOR que a de <see cref="Versao"/>. <c>null</c> se <see cref="Versao"/> for a mais antiga publicada.</summary>
    public LayoutVersaoResumoDto? Anterior { get; init; }

    public required int TotalCampos { get; init; }

    public required IReadOnlyCollection<GrupoCamposLayoutDto> CamposPorGrupo { get; init; }
}

public sealed record GrupoCamposLayoutDto
{
    public string? Grupo { get; init; }

    public string? GrupoSlug { get; init; }

    public required IReadOnlyCollection<CampoLayoutPublicoDto> Campos { get; init; }
}

/// <summary>
/// Verbete do campo dentro do dicionário de uma versão (P11). PROIBIDO conter tipo/tamanho/
/// máscara/obrigatoriedade (R3 — vocabulário canônico puro, isso é comportamento e vive só em
/// <c>CampoVersao</c>). SEM "onde é usado" — esse dado não faz parte do item do dicionário; é obtido
/// sob demanda no verbete do campo (P12).
/// </summary>
public sealed record CampoLayoutPublicoDto
{
    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public required string CaminhoXml { get; init; }

    public required string Significado { get; init; }

    public required int Ordem { get; init; }

    public required bool NovoNestaVersao { get; init; }

    public required bool DescontinuadoNestaVersao { get; init; }

    /// <summary>"/layout/campos/{slug}" — única fonte de link para o campo (A3).</summary>
    public required string UrlCanonica { get; init; }
}

/// <summary>Verbete de resolução do campo canônico (P12) — a URL estável "/layout/campos/{campoSlug}" (A3).</summary>
public sealed record CampoLayoutVerbeteDto
{
    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public required string CaminhoXml { get; init; }

    public required string Significado { get; init; }

    /// <summary>Enum mapeado para <c>string</c> explicitamente (<c>EstadoCampoLayout</c>).</summary>
    public required string Estado { get; init; }

    /// <summary>
    /// Onde o frontend deve abrir o campo (regra A3, no backend, um lugar só): a versão Vigente
    /// publicada quando o campo está ativo nela; senão a versão de origem, se publicada.
    /// </summary>
    public required LayoutVersaoResumoDto VersaoExibicao { get; init; }

    public required LayoutVersaoResumoDto VersaoOrigem { get; init; }

    public LayoutVersaoResumoDto? VersaoDescontinuado { get; init; }

    public required IReadOnlyCollection<UsoCampoLayoutDto> OndeEUsado { get; init; }
}

/// <summary>
/// Provedor/versão PUBLICADOS que usam este <c>CampoLayout</c> (via <c>CampoVersao</c>), com o
/// destino no XML e a obrigatoriedade DAQUELE provedor. Consumido pelo modal do layout.
/// </summary>
public sealed record UsoCampoLayoutDto
{
    public required string ProvedorSlug { get; init; }

    public required string ProvedorNome { get; init; }

    public required string VersaoSlug { get; init; }

    public required string VersaoRotulo { get; init; }

    /// <summary>
    /// Valor cru de <c>CampoVersao.DestinoXml</c>: vários destinos separados por <c>" | "</c>,
    /// <c>"…"</c> quando a carga os omitiu, <c>null</c> sem destino. A chave é sempre serializada.
    /// </summary>
    public string? DestinoXml { get; init; }

    /// <summary><c>EstadoPreenchimento.ToString()</c> (<c>Obrigatorio</c>/<c>Opcional</c>/<c>Condicional</c>) — nunca o enum cru.</summary>
    public required string EstadoPreenchimento { get; init; }
}

/// <summary>
/// Projeção BRUTA de leitura (repositório → serviço) de todo <c>CampoLayout</c> VISÍVEL (§4.1:
/// campo e sua versão de ORIGEM publicados e não desativados) — nunca exposta como contrato.
/// Carrega exatamente o que a fórmula M12 (§4.2) precisa para decidir, por versão-alvo, se o
/// campo está ativo nela.
/// </summary>
public sealed record CampoLayoutVisivelBrutoDto
{
    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public required string CaminhoXml { get; init; }

    public required string Significado { get; init; }

    public string? Grupo { get; init; }

    public required int Ordem { get; init; }

    public required int VersaoLayoutOrigemId { get; init; }

    public required int OrigemOrdem { get; init; }

    public int? VersaoLayoutDescontinuadoId { get; init; }

    public int? DescontinuadoOrdem { get; init; }
}
