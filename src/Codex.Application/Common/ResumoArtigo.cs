using System.Text.RegularExpressions;

namespace Codex.Application.Common;

/// <summary>
/// Deriva o resumo de um artigo (listagem e home) a partir do corpo em Markdown, sem parser
/// completo. O bloco <c>&gt; [!RESUMO]</c> vence quando existe; sem ele, vale o primeiro parágrafo
/// de texto, pulando bloco cercado, linha só de imagem e blockquote de marcador (<c>[!DICA]</c> e
/// afins). A marcação inline sai e o texto é truncado em 200 caracteres.
/// </summary>
public static partial class ResumoArtigo
{
    private const int TamanhoMaximo = 200;
    private const string MarcadorResumo = "RESUMO";

    public static string? Derivar(string? corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
            return null;

        var paragrafos = SepararParagrafos(corpo);
        var textoEscolhido = TextoDoResumo(paragrafos) ?? TextoDoPrimeiroParagrafo(paragrafos);
        if (textoEscolhido is null)
            return null;

        return textoEscolhido.Length > TamanhoMaximo
            ? textoEscolhido[..TamanhoMaximo].TrimEnd() + "…"
            : textoEscolhido;
    }

    /// <summary>Linhas não vazias seguidas, fora de bloco cercado (que nunca vira resumo).</summary>
    private static List<List<string>> SepararParagrafos(string corpo)
    {
        var paragrafos = new List<List<string>>();
        var atual = new List<string>();
        var dentroDeBlocoCercado = false;

        void Fechar()
        {
            if (atual.Count > 0)
                paragrafos.Add(atual);
            atual = [];
        }

        foreach (var linha in corpo.Replace("\r\n", "\n").Split('\n'))
        {
            if (MarcacaoCorpoArtigo.Cerca().IsMatch(linha))
            {
                // Cerca dentro de blockquote não encerra o blockquote, que segue um parágrafo só.
                if (!linha.TrimStart().StartsWith('>'))
                    Fechar();
                dentroDeBlocoCercado = !dentroDeBlocoCercado;
                continue;
            }

            if (dentroDeBlocoCercado)
                continue;

            if (string.IsNullOrWhiteSpace(linha))
            {
                Fechar();
                continue;
            }

            atual.Add(linha);
        }

        Fechar();
        return paragrafos;
    }

    private static string? TextoDoResumo(List<List<string>> paragrafos)
    {
        var bloco = paragrafos.FirstOrDefault(p => MarcadorDaPrimeiraLinha(p) == MarcadorResumo);
        if (bloco is null)
            return null;

        var linhas = bloco.Select(RemoverPrefixoDeCitacao).ToList();
        linhas[0] = MarcacaoCorpoArtigo.Marcador().Replace(linhas[0], string.Empty, 1);
        return Limpar(linhas);
    }

    private static string? TextoDoPrimeiroParagrafo(List<List<string>> paragrafos)
    {
        foreach (var paragrafo in paragrafos)
        {
            if (MarcadorDaPrimeiraLinha(paragrafo) is not null)
                continue;

            var texto = Limpar(paragrafo
                .Where(linha => !EhLinhaSoDeImagem(linha))
                .Select(RemoverPrefixoDeCitacao));
            if (texto is not null)
                return texto;
        }

        return null;
    }

    private static string? MarcadorDaPrimeiraLinha(List<string> paragrafo)
    {
        var achado = RegexCitacaoComMarcador().Match(paragrafo[0]);
        return achado.Success ? achado.Groups["marcador"].Value.ToUpperInvariant() : null;
    }

    private static bool EhLinhaSoDeImagem(string linha) =>
        MarcacaoCorpoArtigo.Imagem().IsMatch(linha)
        && string.IsNullOrWhiteSpace(MarcacaoCorpoArtigo.Imagem().Replace(linha, string.Empty));

    private static string RemoverPrefixoDeCitacao(string linha) => RegexPrefixoDeCitacao().Replace(linha, string.Empty);

    private static string? Limpar(IEnumerable<string> linhas)
    {
        var texto = RemoverMarcacaoInline(string.Join('\n', linhas)).Trim();
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    private static string RemoverMarcacaoInline(string texto)
    {
        var semImagens = MarcacaoCorpoArtigo.Imagem().Replace(texto, string.Empty);
        var semMarcadores = MarcacaoCorpoArtigo.Marcador().Replace(semImagens, string.Empty);
        var semLinks = RegexLink().Replace(semMarcadores, "$1");
        var semNegrito = RegexNegrito().Replace(semLinks, "$2");
        var semItalico = RegexItalico().Replace(semNegrito, "$2");
        var semCodigo = RegexCodigo().Replace(semItalico, "$1");
        var semCabecalho = RegexCabecalho().Replace(semCodigo, string.Empty);

        return RegexEspacos().Replace(semCabecalho, " ");
    }

    [GeneratedRegex(@"^\s*>\s*\[!(?<marcador>[A-Za-z]+)\]")]
    private static partial Regex RegexCitacaoComMarcador();

    [GeneratedRegex(@"^\s*(>\s?)+")]
    private static partial Regex RegexPrefixoDeCitacao();

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

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacos();
}
