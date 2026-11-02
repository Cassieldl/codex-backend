using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Publico;

/// <summary>Quanto do documento o envio modelo traz.</summary>
public enum AlcanceEnvioModelo
{
    /// <summary>Só os campos que a versão do provedor obriga.</summary>
    Minimo,

    /// <summary>Todo campo que a versão do provedor aceita, em qualquer estado.</summary>
    Maximo,
}

/// <summary>Valores aceitos no parâmetro <c>alcance</c> da rota, e o enum de cada um.</summary>
public static class AlcancesEnvioModelo
{
    public const string Minimo = "minimo";

    public const string Maximo = "maximo";

    public static AlcanceEnvioModelo DeTexto(string alcance) => alcance switch
    {
        Minimo => AlcanceEnvioModelo.Minimo,
        Maximo => AlcanceEnvioModelo.Maximo,
        _ => throw new ArgumentOutOfRangeException(nameof(alcance), alcance, "Alcance desconhecido."),
    };

    public static string ParaTexto(AlcanceEnvioModelo alcance) =>
        alcance == AlcanceEnvioModelo.Minimo ? Minimo : Maximo;
}

/// <summary>
/// Envio modelo InvoiCy de uma versão de provedor: a estrutura do layout da versão recortada aos
/// campos que o provedor aceita (máximo) ou obriga (mínimo), em XML (SOAP) e em JSON (REST).
/// </summary>
public sealed record EnvioModeloDto
{
    /// <summary><c>minimo</c> ou <c>maximo</c>.</summary>
    public required string Alcance { get; init; }

    public required string VersaoLayoutRotulo { get; init; }

    public required NoEstruturaDto Xml { get; init; }

    public required NoEstruturaDto Json { get; init; }

    /// <summary>Slugs dos campos considerados no alcance que não têm nó na estrutura, em ordem.</summary>
    public required IReadOnlyList<string> CamposSemPosicao { get; init; }
}

/// <summary>Campo da versão do provedor como o recorte precisa: slug e caminho canônicos e o estado.</summary>
public sealed record CampoEnvioModeloBrutoDto(string CampoLayoutSlug, string CaminhoXml, EstadoPreenchimento EstadoPreenchimento);
