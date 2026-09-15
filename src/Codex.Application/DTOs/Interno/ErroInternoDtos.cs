using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Interno;

/// <summary>Filtro de listagem interna de erros (I18) — enxerga rascunho.</summary>
public sealed record FiltroErroInterno(
    string? Busca,
    string? ProvedorSlug,
    OrigemErro? Origem,
    bool? Publicado,
    int Pagina,
    int TamanhoPagina);

/// <summary>Item de listagem interna de erro (I18).</summary>
public sealed record ErroAdminListaItemDto
{
    public required int Id { get; init; }

    public required string Slug { get; init; }

    public required string Codigo { get; init; }

    public required string Origem { get; init; }

    public required string Titulo { get; init; }

    public required bool Publicado { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Detalhe interno de erro — agregado completo (variantes, versões aplicáveis, campos relacionados).</summary>
public sealed record ErroAdminDetalheDto
{
    public required int Id { get; init; }

    public required string Slug { get; init; }

    public required string Codigo { get; init; }

    public required string Origem { get; init; }

    public required string Titulo { get; init; }

    public string? CausaProvavel { get; init; }

    public string? SolucaoSugerida { get; init; }

    public required bool Publicado { get; init; }

    public required IReadOnlyCollection<string> Variantes { get; init; }

    public required IReadOnlyCollection<int> VersoesProvedorIds { get; init; }

    public required IReadOnlyCollection<int> CamposLayoutIds { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>
/// Criação (I19) — sem <c>publicado</c>. <see cref="VersoesProvedorIds"/> pode ser vazio (U9),
/// salvo quando <see cref="Origem"/> = <see cref="OrigemErro.Prefeitura"/> (validado no
/// Application, §5.1d).
/// </summary>
public sealed record CriarErroDto
{
    public required string Codigo { get; init; }

    public required OrigemErro Origem { get; init; }

    public required string Titulo { get; init; }

    public string? CausaProvavel { get; init; }

    public string? SolucaoSugerida { get; init; }

    public IReadOnlyCollection<string> Variantes { get; init; } = [];

    public IReadOnlyCollection<int> VersoesProvedorIds { get; init; } = [];

    public IReadOnlyCollection<int> CamposLayoutIds { get; init; } = [];
}

/// <summary>Atualização (I20a) — mesma regra de <see cref="OrigemErro.Prefeitura"/> de <see cref="CriarErroDto"/>.</summary>
public sealed record AtualizarErroDto
{
    public required OrigemErro Origem { get; init; }

    public required string Titulo { get; init; }

    public string? CausaProvavel { get; init; }

    public string? SolucaoSugerida { get; init; }

    public IReadOnlyCollection<string> Variantes { get; init; } = [];

    public IReadOnlyCollection<int> VersoesProvedorIds { get; init; } = [];

    public IReadOnlyCollection<int> CamposLayoutIds { get; init; } = [];
}
