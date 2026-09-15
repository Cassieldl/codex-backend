using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Interno;

/// <summary>Filtro de listagem interna de provedores (I1) — enxerga rascunho, filtro de <c>publicado</c> opcional.</summary>
public sealed record FiltroProvedorInterno(
    string? Busca,
    CategoriaProvedor? Categoria,
    bool? Publicado,
    string? OrdenarPor,
    string? Direcao,
    int Pagina,
    int TamanhoPagina);

/// <summary>Item de listagem interna (I1) — inclui rascunho e o valor de concorrência para o <c>If-Match</c> de ações em lote na tela de lista.</summary>
public sealed record ProvedorAdminListaItemDto
{
    public required int Id { get; init; }

    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public required string Categoria { get; init; }

    public required string IdentificadorInvoicy { get; init; }

    public required bool Publicado { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Detalhe interno (I2) — inclui TODOS os avisos de provedor (publicados e rascunho, diferente de P3/U8).</summary>
public sealed record ProvedorAdminDetalheDto
{
    public required int Id { get; init; }

    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public required string Categoria { get; init; }

    public required string IdentificadorInvoicy { get; init; }

    public required bool Publicado { get; init; }

    public required IReadOnlyCollection<string> Aliases { get; init; }

    public required IReadOnlyCollection<AvisoAdminDto> Avisos { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Criação (I3) — nunca aceita <c>publicado</c> (publica-se só via I3b, M19).</summary>
public sealed record CriarProvedorDto
{
    public required string Nome { get; init; }

    public required string IdentificadorInvoicy { get; init; }

    public required CategoriaProvedor Categoria { get; init; }

    public IReadOnlyCollection<string> Aliases { get; init; } = [];
}

/// <summary>Atualização (I4) — sem <c>identificadorInvoicy</c> (imutável) nem <c>publicado</c>.</summary>
public sealed record AtualizarProvedorDto
{
    public required string Nome { get; init; }

    public required CategoriaProvedor Categoria { get; init; }

    public IReadOnlyCollection<string> Aliases { get; init; } = [];
}
