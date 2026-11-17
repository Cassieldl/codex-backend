using System.Text.Json.Serialization;
using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
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

    /// <summary>Norma que deu origem à versão, como "NT 009 v1.01".</summary>
    public string? NormaOrigem { get; init; }

    public DateOnly? HomologacaoDesde { get; init; }

    public DateOnly? LiberadaNoInvoicyEm { get; init; }

    public DateOnly? ObrigatoriaEm { get; init; }

    public required IReadOnlyList<MudancaLayoutDto> Mudancas { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Criação (I15b) - sem <c>ordem</c> (auto-atribuída, max(ordem)+1) e sem <c>publicado</c>.</summary>
public sealed record CriarVersaoLayoutDto
{
    public required string Rotulo { get; init; }

    public string? Descricao { get; init; }

    public string? NormaOrigem { get; init; }

    public DateOnly? HomologacaoDesde { get; init; }

    public DateOnly? LiberadaNoInvoicyEm { get; init; }

    public DateOnly? ObrigatoriaEm { get; init; }

    /// <summary>Ausente, a versão nasce sem mudanças.</summary>
    public IReadOnlyList<MudancaLayoutInternoDto>? Mudancas { get; init; }
}

/// <summary>
/// Atualização (I15c) - todos os campos opcionais: <c>ordem</c> presente é reordenação editorial
/// explícita (409 se colidir); <c>estado</c> presente é transição de <see cref="EstadoVersaoLayout"/>
/// (422 se inválida); campos ausentes permanecem como estão. Norma e datas do calendário substituem
/// o valor atual quando vêm no corpo, e <c>null</c> (ou norma vazia) apaga.
/// </summary>
public sealed record AtualizarVersaoLayoutDto
{
    public string? Rotulo { get; init; }

    public string? Descricao { get; init; }

    public EstadoVersaoLayout? Estado { get; init; }

    public int? Ordem { get; init; }

    // O init marca presença: o desserializador chama o setter para "null" explícito e não chama
    // para chave ausente, que é o que separa "apagar" de "manter".
    public string? NormaOrigem
    {
        get;
        init
        {
            field = value;
            NormaOrigemInformada = true;
        }
    }

    public DateOnly? HomologacaoDesde
    {
        get;
        init
        {
            field = value;
            HomologacaoDesdeInformada = true;
        }
    }

    public DateOnly? LiberadaNoInvoicyEm
    {
        get;
        init
        {
            field = value;
            LiberadaNoInvoicyEmInformada = true;
        }
    }

    public DateOnly? ObrigatoriaEm
    {
        get;
        init
        {
            field = value;
            ObrigatoriaEmInformada = true;
        }
    }

    [JsonIgnore]
    public bool NormaOrigemInformada { get; private init; }

    [JsonIgnore]
    public bool HomologacaoDesdeInformada { get; private init; }

    [JsonIgnore]
    public bool LiberadaNoInvoicyEmInformada { get; private init; }

    [JsonIgnore]
    public bool ObrigatoriaEmInformada { get; private init; }

    /// <summary>Presente, substitui a lista inteira; lista vazia limpa.</summary>
    public IReadOnlyList<MudancaLayoutInternoDto>? Mudancas { get; init; }
}

/// <summary>
/// Mudança da versão em relação à anterior, na entrada. <c>tipo</c> viaja como texto camelCase; <c>antes</c> e
/// <c>depois</c> são caminhos canônicos, como <c>//Servico/Valores/nDI</c>.
/// </summary>
public sealed record MudancaLayoutInternoDto
{
    [JsonConverter(typeof(TipoMudancaLayoutJsonConverter))]
    public required TipoMudancaLayout Tipo { get; init; }

    public string? Antes { get; init; }

    public string? Depois { get; init; }

    public required string Descricao { get; init; }
}
