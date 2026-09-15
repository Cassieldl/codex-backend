namespace Codex.Domain.Enums;

/// <summary>
/// Espelha ck_codex_artigos_tipo. Migrada na 002 (decisoes.md U-3/M-5) — o terceiro valor,
/// <see cref="Mudanca"/>, é grafado em ASCII (sem cedilha/acento) por decisão explícita: os 9
/// enums de domínio da 001 são todos ASCII, e o rótulo "Mudança" vive só no frontend, no ponto
/// de exibição.
/// </summary>
public enum TipoArtigo
{
    Conceito,
    Procedimento,
    Mudanca,
}
