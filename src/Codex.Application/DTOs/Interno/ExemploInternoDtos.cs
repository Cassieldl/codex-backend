namespace Codex.Application.DTOs.Interno;

/// <summary>Área interna de <see cref="Codex.Domain.Entities.Exemplo"/> (I38-I42, analise-backend.md §5.2).</summary>
public sealed record ExemploAdminDto
{
    public required int Id { get; init; }

    public required int VersaoProvedorId { get; init; }

    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public string? Descricao { get; init; }

    public required string Xml { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Criação (I39) — <c>slug</c> NUNCA no body (mass assignment): sempre derivado de <c>Nome</c> na criação.</summary>
public sealed record CriarExemploDto
{
    public required string Nome { get; init; }

    public string? Descricao { get; init; }

    public required string Xml { get; init; }
}

/// <summary>Atualização (I41) — sem <c>slug</c>: renomear nunca recalcula slug (modelo-dados.md §1.3).</summary>
public sealed record AtualizarExemploDto
{
    public required string Nome { get; init; }

    public string? Descricao { get; init; }

    public required string Xml { get; init; }
}
