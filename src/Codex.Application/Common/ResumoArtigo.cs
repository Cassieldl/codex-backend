using System.Text.RegularExpressions;

namespace Codex.Application.Common;

/// <summary>
/// Deriva o <c>resumo</c> de P7 (analise-backend.md §4.2) a partir do corpo em Markdown — função
/// estática pura, testável isoladamente sem banco. Não usa <c>react-markdown</c>/parser completo
/// (é backend, sem dependência de renderização): pega o primeiro bloco não vazio até a primeira
/// linha em branco dupla, remove marcação inline básica e trunca em 200 caracteres.
/// </summary>
public static partial class ResumoArtigo
{
    private const int TamanhoMaximo = 200;

    public static string? Derivar(string? corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
            return null;

        var primeiroParagrafo = ExtrairPrimeiroParagrafo(corpo);
        if (string.IsNullOrWhiteSpace(primeiroParagrafo))
            return null;

        var textoPuro = RemoverMarcacaoInline(primeiroParagrafo).Trim();
        if (string.IsNullOrWhiteSpace(textoPuro))
            return null;

        return textoPuro.Length > TamanhoMaximo
            ? textoPuro[..TamanhoMaximo].TrimEnd() + "…"
            : textoPuro;
    }

    private static string ExtrairPrimeiroParagrafo(string corpo)
    {
        var normalizado = corpo.Replace("\r\n", "\n");
        var blocos = normalizado.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        return blocos.Select(bloco => bloco.Trim()).FirstOrDefault(bloco => !string.IsNullOrWhiteSpace(bloco)) ?? string.Empty;
    }

    private static string RemoverMarcacaoInline(string texto)
    {
        var semLinks = RegexLink().Replace(texto, "$1");
        var semNegrito = RegexNegrito().Replace(semLinks, "$2");
        var semItalico = RegexItalico().Replace(semNegrito, "$2");
        var semCodigo = RegexCodigo().Replace(semItalico, "$1");
        var semCabecalho = RegexCabecalho().Replace(semCodigo, string.Empty);
        var semQuebrasDeLinha = semCabecalho.Replace('\n', ' ');

        return semQuebrasDeLinha;
    }

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex RegexLink();

    [GeneratedRegex(@"(\*\*|__)(.+?)\1")]
    private static partial Regex RegexNegrito();

    [GeneratedRegex(@"(\*|_)(.+?)\1")]
    private static partial Regex RegexItalico();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex RegexCodigo();

    [GeneratedRegex(@"^\s*#{1,6}\s*")]
    private static partial Regex RegexCabecalho();
}
