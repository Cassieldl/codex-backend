using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Interno;

/// <summary>Filtro de listagem interna de artigos (I23) — enxerga rascunho.</summary>
public sealed record FiltroArtigoInterno(
    string? Busca,
    TipoArtigo? Tipo,
    bool? Publicado,
    int Pagina,
    int TamanhoPagina);

/// <summary>
/// Item de listagem interna de artigo (I23) — compacto, SEM <c>Corpo</c> nem as coleções de
/// referência cruzada (§0.2: mesmo padrão de <c>ErroAdminListaItemDto</c> — listagem nunca carrega
/// corpo de texto/coleções pesadas em toda linha da página).
/// </summary>
public sealed record ArtigoAdminListaItemDto
{
    public required int Id { get; init; }

    public required string Slug { get; init; }

    /// <summary>Enum mapeado para <c>string</c> explicitamente — nunca enum cru na resposta.</summary>
    public required string Tipo { get; init; }

    public required string Titulo { get; init; }

    public required bool Publicado { get; init; }

    public required bool Reservado { get; init; }

    public DateOnly? VerificadoEm { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Detalhe interno de artigo — agregado completo (I24 e resposta de I25/I26/I28/I29).</summary>
public sealed record ArtigoAdminDto
{
    public required int Id { get; init; }

    public required string Slug { get; init; }

    public required string Tipo { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public required bool Publicado { get; init; }

    public required bool Reservado { get; init; }

    public DateOnly? VerificadoEm { get; init; }

    public required IReadOnlyCollection<int> CampoLayoutIds { get; init; }

    public required IReadOnlyCollection<int> ErroIds { get; init; }

    /// <summary>Os mesmos vínculos de <see cref="ErroIds"/>, rotulados e ordenados por código e id.</summary>
    public required IReadOnlyCollection<ErroVinculadoArtigoDto> Erros { get; init; }

    /// <summary>Os mesmos vínculos de <see cref="CampoLayoutIds"/>, rotulados e ordenados por ordem da versão do layout, nome e id.</summary>
    public required IReadOnlyCollection<CampoLayoutVinculadoArtigoDto> CamposLayout { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Erro vinculado ao artigo. <see cref="Desativado"/> marca o erro removido, que segue vinculado até o editor tirá-lo.</summary>
public sealed record ErroVinculadoArtigoDto
{
    public required int Id { get; init; }

    public required string Codigo { get; init; }

    public required string Titulo { get; init; }

    public required bool Desativado { get; init; }
}

/// <summary>Campo de layout vinculado ao artigo. <see cref="Desativado"/> marca o campo removido, que segue vinculado até o editor tirá-lo.</summary>
public sealed record CampoLayoutVinculadoArtigoDto
{
    public required int Id { get; init; }

    public required string Nome { get; init; }

    public required string CaminhoXml { get; init; }

    public required int VersaoLayoutId { get; init; }

    public required string VersaoLayoutRotulo { get; init; }

    public required bool Desativado { get; init; }
}

/// <summary>Criação (I25) — <c>slug</c> nunca é campo de request (mass assignment): sempre derivado de <see cref="Titulo"/>.</summary>
public sealed record CriarArtigoDto
{
    public required TipoArtigo Tipo { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public IReadOnlyCollection<int> CampoLayoutIds { get; init; } = [];

    public IReadOnlyCollection<int> ErroIds { get; init; } = [];
}

/// <summary>
/// Atualização (I26). <see cref="Tipo"/> É editável (decisoes.md B-2, rejeitando a proposta
/// original de imutabilidade) — o editor que escolheu o tipo errado é o consumidor da mudança;
/// trocar exigiria apagar/recriar, quebrando o slug público.
/// </summary>
public sealed record AtualizarArtigoDto
{
    public required TipoArtigo Tipo { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public IReadOnlyCollection<int> CampoLayoutIds { get; init; } = [];

    public IReadOnlyCollection<int> ErroIds { get; init; } = [];
}

/// <summary>Registro de verificação (I29) — data pura, nunca no futuro.</summary>
public sealed record AtualizarVerificacaoArtigoDto
{
    public required DateOnly VerificadoEm { get; init; }
}

/// <summary>Resposta de I36 — resolução de F-2 (endpoint dedicado, nunca varredura de página).</summary>
public sealed record ArtigoExisteDto
{
    public required string Slug { get; init; }

    public required bool Existe { get; init; }

    public int? ArtigoId { get; init; }
}
