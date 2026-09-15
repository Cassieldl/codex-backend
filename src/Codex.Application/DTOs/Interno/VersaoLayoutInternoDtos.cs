using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Interno;

/// <summary>Versão do vocabulário canônico do Layout InvoiCy (I15a/I15c).</summary>
public sealed record VersaoLayoutAdminDto
{
    public required int Id { get; init; }

    public required string Rotulo { get; init; }

    public required string Slug { get; init; }

    public required string Estado { get; init; }

    public required int Ordem { get; init; }

    public required bool Publicado { get; init; }

    public string? Descricao { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Criação (I15b) — sem <c>ordem</c> (auto-atribuída, max(ordem)+1) e sem <c>publicado</c>.</summary>
public sealed record CriarVersaoLayoutDto
{
    public required string Rotulo { get; init; }

    public string? Descricao { get; init; }
}

/// <summary>
/// Atualização (I15c) — todos os campos opcionais: <c>ordem</c> presente é reordenação editorial
/// explícita (409 se colidir); <c>estado</c> presente é transição de <see cref="EstadoVersaoLayout"/>
/// (422 se inválida); campos ausentes permanecem como estão.
/// </summary>
public sealed record AtualizarVersaoLayoutDto
{
    public string? Rotulo { get; init; }

    public string? Descricao { get; init; }

    public EstadoVersaoLayout? Estado { get; init; }

    public int? Ordem { get; init; }
}
