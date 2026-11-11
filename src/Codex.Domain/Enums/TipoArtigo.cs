namespace Codex.Domain.Enums;

/// <summary>
/// Espelha ck_codex_artigos_tipo. Os valores são ASCII (<see cref="Mudanca"/>,
/// <see cref="Configuracao"/>): o rótulo com acento vive só no frontend, no ponto de exibição.
/// <see cref="Configuracao"/> é referência de uma tela ou parâmetro do InvoiCy; passo a passo para
/// resolver uma situação é <see cref="Procedimento"/>.
/// </summary>
public enum TipoArtigo
{
    Conceito,
    Procedimento,
    Mudanca,
    Configuracao,
}
