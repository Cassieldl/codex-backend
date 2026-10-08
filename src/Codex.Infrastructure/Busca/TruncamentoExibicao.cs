namespace Codex.Infrastructure.Busca;

/// <summary>
/// Trunca texto de EXIBIÇÃO — nunca o texto indexado para busca — no teto de uma coluna, cortando
/// na última palavra INTEIRA antes do limite e acrescentando "…". O resultado nunca excede
/// <paramref name="limiteCaracteres"/>, mesmo quando o texto não tem espaço nenhum (um "corte cru"
/// — não há palavra para preservar nesse caso).
/// </summary>
public static class TruncamentoExibicao
{
    private const string Reticencias = "…";

    public static string Truncar(string texto, int limiteCaracteres)
    {
        ArgumentNullException.ThrowIfNull(texto);
        if (limiteCaracteres <= 0)
            throw new ArgumentOutOfRangeException(nameof(limiteCaracteres), "Limite precisa ser positivo.");

        if (texto.Length <= limiteCaracteres)
            return texto;

        // Reserva espaço para as reticências — "corte + …" nunca pode passar do limite.
        var limiteSemReticencias = limiteCaracteres - Reticencias.Length;
        if (limiteSemReticencias <= 0)
            return Reticencias[..limiteCaracteres];

        var corte = RemoverSurrogateAltoNoFinal(texto[..limiteSemReticencias]);
        var ultimoEspaco = corte.LastIndexOf(' ');

        var textoCortado = ultimoEspaco > 0 ? RemoverSurrogateAltoNoFinal(corte[..ultimoEspaco]) : corte;

        return textoCortado + Reticencias;
    }

    /// <summary>
    /// Um corte por posição (índice UTF-16) pode
    /// terminar bem no meio de um par substituto — emoji e boa parte do plano suplementar Unicode
    /// exigem dois <c>char</c> (dois code units para representar um único emoji, ex. 😀), e a
    /// metade ALTA sozinha não é um caractere Unicode válido. O Npgsql grava UTF-8 estrito
    /// (fallback que lança, não substitui) —
    /// persistir essa metade solta derruba a requisição com <c>EncoderFallbackException</c> (500),
    /// exatamente a mesma classe de defeito que o truncamento inteiro existe para evitar.
    ///
    /// <para>
    /// Recuar uma posição descarta a metade alta solta: o emoji inteiro não cabia no limite mesmo
    /// (senão o corte não teria caído no meio dele), então ele some do texto truncado em vez de
    /// aparecer corrompido. Aplicado nos DOIS pontos de corte — o cru e o que procura a última
    /// palavra — porque o segundo fatia um prefixo do primeiro e herda o mesmo risco.
    /// </para>
    /// </summary>
    private static string RemoverSurrogateAltoNoFinal(string texto) =>
        texto.Length > 0 && char.IsHighSurrogate(texto[^1]) ? texto[..^1] : texto;
}
