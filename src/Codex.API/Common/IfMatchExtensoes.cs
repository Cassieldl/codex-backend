namespace Codex.API.Common;

/// <summary>
/// As duas pontas do protocolo de concorrência otimista sobre HTTP: ler o <c>If-Match</c> que o
/// cliente manda e publicar o <c>ETag</c> que ele precisa ter recebido para poder mandar.
///
/// <para>
/// Leitura do header <c>If-Match</c> como o valor de concorrência otimista (<c>xmin</c>,
/// espelhado em <c>EntidadeAuditada.Versao</c> — <c>uint</c>) esperado pelo cliente. Obrigatório
/// em todo <c>PUT</c>/<c>DELETE</c> das entidades com <c>xmin</c> (analise-backend.md §4.2, nota
/// "If-Match obrigatório") — inclusive nos 6 endpoints de publicação, que são <c>PUT</c>s como
/// qualquer outro.
/// </para>
/// </summary>
public static class IfMatchExtensoes
{
    /// <summary>
    /// Aceita <c>If-Match: 123</c>, <c>If-Match: "123"</c> ou <c>If-Match: W/"123"</c> (o mesmo
    /// formato de ETag fraca emitido pela leitura pública, ver <c>ProvedoresController</c> P4).
    /// Ausente ou não-numérico → <see cref="IfMatchObrigatorioException"/> (400).
    /// </summary>
    public static uint ExigirIfMatch(this HttpRequest requisicao)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var valorBruto = requisicao.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(valorBruto))
            throw new IfMatchObrigatorioException("Header 'If-Match' é obrigatório para esta operação.");

        var semPrefixoFraco = valorBruto.Trim().TrimStart('W', '/').Trim('"');
        if (!uint.TryParse(semPrefixoFraco, out var versao))
            throw new IfMatchObrigatorioException("Header 'If-Match' inválido — esperado o valor de concorrência do recurso.");

        return versao;
    }

    /// <summary>
    /// Publica o validador de concorrência no header <c>ETag</c> da resposta, no mesmo formato de
    /// ETag fraca que <see cref="ExigirIfMatch"/> aceita de volta e que P4 já emitia.
    ///
    /// <para>
    /// Por que existe: a área interna EXIGIA <c>If-Match</c> em todo <c>PUT</c>/<c>DELETE</c> e não
    /// anunciava o validador em nenhuma resposta — nenhum endpoint interno emitia <c>ETag</c>. Um
    /// cliente que siga o protocolo HTTP (ler o <c>ETag</c> da resposta e devolvê-lo em
    /// <c>If-Match</c>) não tinha de onde tirar o valor e recebia <c>400</c> em toda escrita.
    /// </para>
    ///
    /// <para>
    /// O header complementa, e não substitui, o <c>versaoConcorrencia</c> do CORPO: header é um por
    /// resposta, então só serve para detalhe/mutação. ITEM DE LISTA continua tendo o corpo como
    /// única fonte possível do token (AP-API-002), e é por isso que os dois coexistem.
    /// </para>
    /// </summary>
    public static void PublicarETagConcorrencia(this HttpResponse resposta, uint versaoConcorrencia)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        resposta.Headers.ETag = FormatarETagConcorrencia(versaoConcorrencia);
    }

    /// <summary>Formato único da ETag de concorrência — fraca, porque o corpo pode variar em representação sem que o <c>xmin</c> mude.</summary>
    public static string FormatarETagConcorrencia(uint versaoConcorrencia) => $"W/\"{versaoConcorrencia}\"";
}
