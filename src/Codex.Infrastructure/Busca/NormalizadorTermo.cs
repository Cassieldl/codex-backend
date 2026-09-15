using System.Text.RegularExpressions;

namespace Codex.Infrastructure.Busca;

/// <summary>
/// Utilitários de preparação de parâmetros de consulta — NUNCA reimplementa
/// <c>codex.normalizar_busca</c> (modelo-dados.md §5.5): nenhum lower/unaccent acontece aqui, só
/// operações neutras de string (aparar, cortar por tamanho, separar em tokens usando os MESMOS
/// separadores da função de banco, para saber qual substring do termo ORIGINAL substituir quando
/// a autocorreção encontra um candidato). A normalização de acento/caixa continua 100%
/// responsabilidade do Postgres, em <see cref="ConsultaBuscaPostgres"/>.
/// </summary>
public static class NormalizadorTermo
{
    /// <summary>
    /// Mesma classe de separadores de <c>codex.normalizar_busca</c> (arquitetura.md §6.3):
    /// <c>/ \ - _ . : ; , | ( ) &lt; &gt; [ ]</c>, mais espaço em branco.
    /// </summary>
    private static readonly char[] Separadores =
        ['/', '\\', '-', '_', '.', ':', ';', ',', '|', '(', ')', '<', '>', '[', ']', ' ', '\t', '\n', '\r'];

    /// <summary>Apara espaços nas pontas e corta no tamanho máximo (proteção de custo, cap de 300 chars).</summary>
    public static string LimitarTamanho(string termo, int tamanhoMaximo = 300)
    {
        ArgumentNullException.ThrowIfNull(termo);

        var aparado = termo.Trim();
        return aparado.Length <= tamanhoMaximo ? aparado : aparado[..tamanhoMaximo];
    }

    /// <summary>
    /// Extrai tokens candidatos a autocorreção: comprimento mínimo (arquitetura.md §6.5 usa 4),
    /// distintos (case-insensitive), preservando a grafia ORIGINAL de cada um — é essa grafia
    /// original que <see cref="ConsultaBuscaPostgres"/> substitui no termo de trabalho quando
    /// encontra uma correção válida no léxico.
    /// </summary>
    public static IReadOnlyList<string> ExtrairTokensCandidatos(string termo, int tamanhoMinimo = 4)
    {
        ArgumentNullException.ThrowIfNull(termo);

        return termo
            .Split(Separadores, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= tamanhoMinimo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Troca todas as ocorrências de <paramref name="token"/> (comparação sem caixa) pelo
    /// <paramref name="substituto"/> LITERAL.
    ///
    /// <para>
    /// O substituto vem do léxico (<c>codex.termosbusca</c>), alimentado a partir de conteúdo
    /// editorial, e <c>codex.normalizar_busca</c> preserva <c>$</c>: um termo com <c>$&amp;</c> ou
    /// <c>$1</c> seria interpretado como padrão de substituição do <c>Regex</c> e devolveria ao
    /// leitor um termo que ninguém digitou. Escapar o PADRÃO não protege disso — o que protege é
    /// escapar o REPLACEMENT, e é por isso que esta troca é uma função nomeada em vez de uma
    /// chamada solta de <c>Regex.Replace</c>.
    /// </para>
    /// </summary>
    public static string SubstituirTokenPorLiteral(string termo, string token, string substituto)
    {
        ArgumentNullException.ThrowIfNull(termo);
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentNullException.ThrowIfNull(substituto);

        return Regex.Replace(
            termo,
            Regex.Escape(token),
            substituto.Replace("$", "$$", StringComparison.Ordinal),
            RegexOptions.IgnoreCase);
    }
}
