using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Interno;

/// <summary>Comportamento do campo naquele provedor/versão (I11/I12/I13a) — nunca compartilhado com <c>CampoLayoutResponseDto</c> (R3).</summary>
public sealed record CampoVersaoAdminDto
{
    public required int Id { get; init; }

    public required int VersaoProvedorId { get; init; }

    public required int CampoLayoutId { get; init; }

    /// <summary>
    /// Nome do campo canônico, para a tela não precisar cruzar o id em outra requisição. Sem ele a
    /// coluna "Campo" da tabela ficava vazia e os 16 botões de ação anunciavam "campo undefined"
    /// ao leitor de tela — o cliente já lia este nome, o DTO é que não o tinha.
    /// </summary>
    public required string CampoLayoutNome { get; init; }

    public required string Tipo { get; init; }

    public string? Tamanho { get; init; }

    public string? Mascara { get; init; }

    public string? Dominio { get; init; }

    public required string EstadoPreenchimento { get; init; }

    public string? Condicao { get; init; }

    public string? Exemplo { get; init; }

    public string? NotaPreenchimento { get; init; }

    public string? DestinoXml { get; init; }

    public string? NotaTransformacao { get; init; }

    public required int Ordem { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Resposta agregada de I11 — campos preenchidos + campos canônicos da versão de layout ainda sem entrada nesta versão de provedor.</summary>
public sealed record CamposVersaoAgregadoDto
{
    public required IReadOnlyCollection<CampoVersaoAdminDto> Campos { get; init; }

    public required IReadOnlyCollection<CampoLayoutResponseDto> CamposLayoutAusentes { get; init; }
}

/// <summary>Criação (I12) — <c>campoLayoutId</c> obrigatório sem exceção (U4).</summary>
public sealed record CriarCampoVersaoDto
{
    public required int CampoLayoutId { get; init; }

    public required string Tipo { get; init; }

    public string? Tamanho { get; init; }

    public string? Mascara { get; init; }

    public string? Dominio { get; init; }

    public required EstadoPreenchimento EstadoPreenchimento { get; init; }

    public string? Condicao { get; init; }

    public string? Exemplo { get; init; }

    public string? NotaPreenchimento { get; init; }

    public string? DestinoXml { get; init; }

    public string? NotaTransformacao { get; init; }

    public int Ordem { get; init; }
}

/// <summary>Atualização (I13a) — sem <c>campoLayoutId</c> (o vínculo com o vocabulário canônico não muda; para isso, remove e cria de novo).</summary>
public sealed record AtualizarCampoVersaoDto
{
    public required string Tipo { get; init; }

    public string? Tamanho { get; init; }

    public string? Mascara { get; init; }

    public string? Dominio { get; init; }

    public required EstadoPreenchimento EstadoPreenchimento { get; init; }

    public string? Condicao { get; init; }

    public string? Exemplo { get; init; }

    public string? NotaPreenchimento { get; init; }

    public string? DestinoXml { get; init; }

    public string? NotaTransformacao { get; init; }

    public int Ordem { get; init; }
}
