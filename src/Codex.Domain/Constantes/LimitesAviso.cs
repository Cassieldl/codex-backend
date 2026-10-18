namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos de <c>codex.avisos</c>: fonte única entre o schema (<c>AvisoConfiguration</c>) e os
/// validators.
/// </summary>
public static class LimitesAviso
{
    /// <summary><c>titulo</c>: <c>varchar(200)</c>.</summary>
    public const int Titulo = 200;

    /// <summary>Mesmo teto de campos por versão que a página pública aceita exibir.</summary>
    public const int MaximoCamposAfetados = 2000;
}
