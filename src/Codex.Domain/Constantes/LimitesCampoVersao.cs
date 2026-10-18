namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos das colunas de <c>codex.camposversao</c>: fonte única entre o schema
/// (<c>CampoVersaoConfiguration</c>) e os validators.
/// </summary>
public static class LimitesCampoVersao
{
    /// <summary><c>tipo</c>: <c>varchar(60)</c>.</summary>
    public const int Tipo = 60;

    /// <summary><c>tamanho</c>: <c>varchar(40)</c>.</summary>
    public const int Tamanho = 40;

    /// <summary><c>mascara</c>: <c>varchar(120)</c>.</summary>
    public const int Mascara = 120;

    /// <summary><c>destinoxml</c>: <c>varchar(400)</c>.</summary>
    public const int DestinoXml = 400;
}
