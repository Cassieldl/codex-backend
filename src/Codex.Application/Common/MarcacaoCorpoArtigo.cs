using System.Text.RegularExpressions;

namespace Codex.Application.Common;

/// <summary>
/// Marcação visual do corpo de artigo (blocos <c>[!X]</c>, imagem, bloco cercado) e o texto que
/// vai para a busca. Os padrões ficam aqui para o resumo e o índice lerem o corpo do mesmo jeito.
/// </summary>
public static partial class MarcacaoCorpoArtigo
{
    /// <summary>
    /// Corpo pronto para indexar: sem marcador <c>[!X]</c> (o título que vem depois dele fica),
    /// imagem trocada por alt e legenda, sem o caminho, e sem as linhas de cerca, com o conteúdo
    /// do bloco (as etapas do fluxo) mantido. Item de lista sem pontuação final ganha ponto, para
    /// o trecho da busca não emendar um item no seguinte.
    /// </summary>
    public static string TextoIndexavel(string corpo)
    {
        var linhas = corpo.Replace("\r\n", "\n")
            .Split('\n')
            .Where(linha => !Cerca().IsMatch(linha));
        var semCercas = string.Join('\n', linhas);

        var imagensComoTexto = Imagem().Replace(semCercas, achado =>
            string.Join(' ', new[] { achado.Groups["alt"].Value, achado.Groups["legenda"].Value }
                .Where(parte => !string.IsNullOrWhiteSpace(parte))));

        // Depois da troca de imagem: a legenda pode trazer a pontuação final do item.
        var itensFechados = string.Join('\n', imagensComoTexto.Split('\n').Select(FecharItemDeLista));

        return Marcador().Replace(itensFechados, string.Empty);
    }

    private static string FecharItemDeLista(string linha)
    {
        var semEspacoFinal = linha.TrimEnd();

        return ItemDeLista().IsMatch(semEspacoFinal) && !PontuacaoFinal().IsMatch(semEspacoFinal)
            ? semEspacoFinal + "."
            : linha;
    }

    /// <summary>Item com marcador <c>-</c>, <c>*</c>, <c>+</c> ou número, inclusive dentro de blockquote.</summary>
    [GeneratedRegex(@"^\s*(?:>\s*)*(?:[-*+]|\d+[.)])\s+\S")]
    private static partial Regex ItemDeLista();

    [GeneratedRegex(@"[.!?:;,…]$")]
    private static partial Regex PontuacaoFinal();

    /// <summary>Linha de cerca, inclusive dentro de blockquote (<c>&gt; ```csharp</c>).</summary>
    [GeneratedRegex(@"^\s*(?:>\s*)*(```|~~~)")]
    internal static partial Regex Cerca();

    [GeneratedRegex(@"\[![A-Za-z]+\]")]
    internal static partial Regex Marcador();

    /// <summary>
    /// Caminho e resto do parêntese casam os mesmos caracteres: com backtracking, imagem sem o
    /// <c>)</c> final custa tempo cúbico, e o resumo roda em todo GET público.
    /// </summary>
    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\([^)\s]*(?:\s+""(?<legenda>[^""]*)"")?[^)]*\)", RegexOptions.NonBacktracking)]
    internal static partial Regex Imagem();
}
