using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Codex.Domain.ValueObjects;

/// <summary>
/// Identificador amigável de URL, gerado uma única vez na criação da entidade (nunca
/// recalculado em edição — ver decisoes.md M6). Não confundir com
/// <c>codex.normalizar_busca</c>: aquela função serve à busca textual, esta serve à URL.
/// </summary>
public sealed record Slug
{
    public string Valor { get; }

    private Slug(string valor)
    {
        Valor = valor;
    }

    /// <summary>Gera um slug a partir de um texto livre: lowercase, ASCII sem acento, "-" como separador.</summary>
    public static Slug DeTexto(string textoOrigem)
    {
        if (string.IsNullOrWhiteSpace(textoOrigem))
            throw new ArgumentException("Não é possível gerar slug de texto vazio.", nameof(textoOrigem));

        var normalizado = Normalizar(textoOrigem);

        if (string.IsNullOrEmpty(normalizado))
            throw new ArgumentException("Slug resultante ficou vazio após normalização.", nameof(textoOrigem));

        return new Slug(normalizado);
    }

    /// <summary>
    /// Responde se <see cref="DeTexto"/> produziria um slug em vez de lançar — predicado para o
    /// validator de entrada devolver <c>422</c> em vez de deixar a <see cref="ArgumentException"/>
    /// virar <c>500</c> (texto só de pontuação, ex.: <c>"..."</c>, passa em <c>NotEmpty</c> e
    /// normaliza para vazio). Roda a MESMA normalização de <see cref="DeTexto"/> em vez de uma
    /// regex paralela: um predicado aproximado (ex.: exigir <c>[a-zA-Z0-9]</c>) reprovaria texto
    /// legítimo só com letras acentuadas, que a normalização converte para ASCII sem problema.
    /// </summary>
    public static bool PodeDerivarDe(string? textoOrigem) =>
        !string.IsNullOrWhiteSpace(textoOrigem) && Normalizar(textoOrigem).Length > 0;

    private static string Normalizar(string textoOrigem)
    {
        var semAcento = RemoverDiacriticos(textoOrigem.Trim().ToLowerInvariant());
        var comHifen = Regex.Replace(semAcento, "[^a-z0-9]+", "-");

        return Regex.Replace(comHifen, "-{2,}", "-").Trim('-');
    }

    /// <summary>Reidrata um slug já persistido — não valida formato (o banco já garante unicidade).</summary>
    public static Slug DeValorPersistido(string valorPersistido)
    {
        if (string.IsNullOrWhiteSpace(valorPersistido))
            throw new ArgumentException("Slug persistido não pode ser vazio.", nameof(valorPersistido));

        return new Slug(valorPersistido);
    }

    private static string RemoverDiacriticos(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var construtor = new StringBuilder();

        foreach (var caractere in normalizado)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(caractere);
            if (categoria != UnicodeCategory.NonSpacingMark)
                construtor.Append(caractere);
        }

        return construtor.ToString().Normalize(NormalizationForm.FormC);
    }

    public override string ToString() => Valor;

    public static implicit operator string(Slug slug) => slug.Valor;
}
