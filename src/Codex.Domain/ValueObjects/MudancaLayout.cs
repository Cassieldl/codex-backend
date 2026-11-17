using Codex.Domain.Enums;

namespace Codex.Domain.ValueObjects;

/// <summary>
/// Uma mudança estruturada de uma versão do layout em relação à anterior. <see cref="Antes"/> e
/// <see cref="Depois"/> são caminhos canônicos, como <c>//Servico/Valores/nDI</c>.
/// </summary>
public sealed record MudancaLayout(TipoMudancaLayout Tipo, string? Antes, string? Depois, string Descricao)
{
    /// <summary>Tipos em que o caminho antigo identifica a mudança.</summary>
    public static bool ExigeAntes(TipoMudancaLayout tipo) =>
        tipo is not (TipoMudancaLayout.GrupoNovo or TipoMudancaLayout.CampoNovo);

    /// <summary>Tipos em que o caminho novo identifica a mudança.</summary>
    public static bool ExigeDepois(TipoMudancaLayout tipo) => tipo != TipoMudancaLayout.Saiu;
}
