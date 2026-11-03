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
    /// do bloco (as etapas do fluxo) mantido.
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

        return Marcador().Replace(imagensComoTexto, string.Empty);
    }

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
