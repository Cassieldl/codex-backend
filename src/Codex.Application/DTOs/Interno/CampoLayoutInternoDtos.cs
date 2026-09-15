using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Interno;

/// <summary>
/// Vocabulário canônico do Layout InvoiCy (I16/I17, I11 <c>camposLayoutAusentes</c>) — R3:
/// PROIBIDO conter <c>Tipo</c>/<c>Tamanho</c>/<c>Mascara</c>/<c>Dominio</c>/
/// <c>EstadoPreenchimento</c>/<c>Condicao</c>/<c>Exemplo</c>/<c>NotaPreenchimento</c>/
/// <c>DestinoXml</c>/<c>NotaTransformacao</c> — isso é comportamento, vive só em
/// <see cref="CampoVersaoAdminDto"/>. Provado por reflexão em
/// <c>Dtos/CampoLayoutDtoNaoTemComportamentoTests.cs</c>.
/// </summary>
public sealed record CampoLayoutResponseDto
{
    public required int Id { get; init; }

    public required int VersaoLayoutId { get; init; }

    public int? VersaoLayoutDescontinuadoId { get; init; }

    public required string Nome { get; init; }

    public required string CaminhoXml { get; init; }

    public required string Significado { get; init; }

    public string? Grupo { get; init; }

    public required int Ordem { get; init; }

    public required string Slug { get; init; }

    public required string Estado { get; init; }

    public required bool Publicado { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Criação (I16 POST) — sem <c>publicado</c>.</summary>
public sealed record CriarCampoLayoutDto
{
    public required string Nome { get; init; }

    public required string CaminhoXml { get; init; }

    public required string Significado { get; init; }

    public string? Grupo { get; init; }

    public int Ordem { get; init; }
}

/// <summary>
/// Atualização (I17a) — todos opcionais. <c>versaoLayoutDescontinuadoId</c> obrigatório quando
/// <c>estado="Descontinuado"</c>, proibido caso contrário (§5.1b) — checagem de forma aqui;
/// checagem de <c>ordem</c> (risco #2 do DBA) é do domínio (<see cref="Codex.Domain.Entities.CampoLayout.Descontinuar"/>),
/// chamado pelo service.
/// </summary>
public sealed record AtualizarCampoLayoutDto
{
    public string? Nome { get; init; }

    public string? Significado { get; init; }

    public string? Grupo { get; init; }

    public int? Ordem { get; init; }

    public EstadoCampoLayout? Estado { get; init; }

    public int? VersaoLayoutDescontinuadoId { get; init; }
}
