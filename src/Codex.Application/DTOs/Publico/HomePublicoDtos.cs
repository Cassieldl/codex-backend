using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Publico;

/// <summary>
/// Agregado da Home (P9, analise-backend.md §6.1) — um round-trip, sem paginação. As quatro
/// coleções são sempre presentes (<c>[]</c> quando vazias); só <see cref="Layout"/> é anulável,
/// porque "não há versão de layout publicada" é um estado distinto de "há uma versão sem campos".
/// </summary>
public sealed record HomeDto
{
    public required IReadOnlyCollection<ComunicadoVigenteDto> Comunicados { get; init; }

    public required IReadOnlyCollection<CategoriaProvedoresDto> ProvedoresPorCategoria { get; init; }

    public LayoutResumoHomeDto? Layout { get; init; }

    /// <summary>MESMO DTO de P7 — nunca um gêmeo divergente (arquitetura.md §4.3).</summary>
    public required IReadOnlyCollection<ArtigoListaItemDto> ArtigosRecentes { get; init; }
}

/// <summary>
/// Comunicado no ar (U-5: todas as abrangências, não só global — a UI distribui por severidade e
/// escopo). <see cref="ProvedorNome"/>/<see cref="Url"/> só são preenchidos quando a abrangência
/// tem alvo E a cascata do alvo é pública (§6.3) — comunicado de provedor em rascunho nem chega
/// aqui.
/// </summary>
public sealed record ComunicadoVigenteDto
{
    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    /// <summary>Enum mapeado para <c>string</c> explicitamente — nunca enum cru na resposta.</summary>
    public required string Severidade { get; init; }

    public required string Abrangencia { get; init; }

    public required DateTimeOffset ExpiraEm { get; init; }

    public string? ProvedorNome { get; init; }

    /// <summary><c>/provedores/{slug}</c>, também em comunicado de versão; <c>null</c> em <c>Global</c>.</summary>
    public string? Url { get; init; }
}

/// <summary>Provedores agrupados por categoria — só categorias com pelo menos um provedor publicado.</summary>
public sealed record CategoriaProvedoresDto
{
    /// <summary><see cref="CategoriaProvedor"/> como nome do enum, nunca rótulo de exibição.</summary>
    public required string Categoria { get; init; }

    public required int Total { get; init; }

    public required IReadOnlyCollection<ProvedorHomeDto> Provedores { get; init; }
}

/// <summary>
/// Card de provedor da Home. <see cref="Versoes"/> é a lista COMPLETA das versões publicadas
/// (decisoes.md rodada 4): a contagem antiga não sustentava o widget "escolha o provedor → escolha
/// a versão" do artboard. <see cref="UltimaVersao"/> continua à parte, para o rótulo de destaque
/// do card, e é sempre <c>Versoes[0]</c> — mesma ordenação, mesma consulta, custo adicional zero.
/// </summary>
public sealed record ProvedorHomeDto
{
    public required string Slug { get; init; }

    public required string Nome { get; init; }

    /// <summary>NÃO é "versão vigente" (U7): é a publicada cuja versão de layout tem a maior ordem.</summary>
    public VersaoResumoMinimaDto? UltimaVersao { get; init; }

    public required IReadOnlyCollection<VersaoResumoMinimaDto> Versoes { get; init; }

    /// <summary>MAX(<c>VerificadoEm</c>) entre as versões publicadas — <c>Provedor</c> não tem coluna própria.</summary>
    public DateOnly? VerificadoEm { get; init; }

    /// <summary>Há comunicado vigente e visível com este provedor como alvo (o ícone de alerta da linha).</summary>
    public required bool TemComunicado { get; init; }
}

/// <summary>
/// Bloco "Layout InvoiCy" da Home. SEM <c>PublicadaEm</c> (decisoes.md B-6): não existe coluna
/// equivalente em <c>versoeslayout</c>, e exibir <c>criadoem</c> sob rótulo de publicação seria a
/// tela afirmando o que não é verdade. Se o produto quiser a data, é coluna nova e decisão do
/// usuário.
/// </summary>
public sealed record LayoutResumoHomeDto
{
    public VersaoResumoMinimaDto? Vigente { get; init; }

    public VersaoResumoMinimaDto? Prevista { get; init; }

    /// <summary>Nota livre da versão vigente; NÃO há coluna de "o que mudou".</summary>
    public string? Descricao { get; init; }

    /// <summary>Campos visíveis cuja versão de ORIGEM é a vigente.</summary>
    public required int CamposNovos { get; init; }

    /// <summary>Campos visíveis descontinuados NA vigente.</summary>
    public required int CamposDescontinuados { get; init; }
}

/// <summary>
/// Achado bruto de comunicado vigente e visível (§6.3) — os rótulos do alvo já resolvidos por
/// join, para o service não precisar de uma segunda consulta por linha.
/// </summary>
public sealed record ComunicadoHomeBrutoDto
{
    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public required Severidade Severidade { get; init; }

    public required AbrangenciaComunicado Abrangencia { get; init; }

    public required DateTimeOffset ExpiraEm { get; init; }

    public int? ProvedorId { get; init; }

    public string? ProvedorNome { get; init; }

    public string? ProvedorSlug { get; init; }
}

/// <summary>Provedor publicado, cru — o agrupamento por categoria acontece no service.</summary>
public sealed record ProvedorHomeBrutoDto
{
    public required int Id { get; init; }

    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public required CategoriaProvedor Categoria { get; init; }
}

/// <summary>
/// Versão publicada de provedor, já na ordenação de "última versão" (§6.4): <c>VersaoLayout.Ordem</c>
/// DESC, desempate por id DESC. O primeiro item do grupo é a última versão; o grupo inteiro é
/// <c>Versoes</c>.
/// </summary>
public sealed record VersaoProvedorHomeBrutoDto
{
    public required int ProvedorId { get; init; }

    public required string Slug { get; init; }

    public required string Rotulo { get; init; }

    public DateOnly? VerificadoEm { get; init; }
}

/// <summary>Bloco de layout já resolvido no banco — o service só mapeia para o DTO público.</summary>
public sealed record LayoutHomeBrutoDto
{
    public VersaoResumoMinimaDto? Vigente { get; init; }

    public VersaoResumoMinimaDto? Prevista { get; init; }

    public string? Descricao { get; init; }

    public required int CamposNovos { get; init; }

    public required int CamposDescontinuados { get; init; }
}
