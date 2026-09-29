using System.Reflection;

namespace Codex.API.Common;

/// <summary>
/// ETag de leitura pública: versão do DADO combinada com a versão do BINÁRIO que o serializou.
///
/// <para>
/// Só o dado não basta. O <c>xmin</c> muda quando o conteúdo muda, mas não quando muda o
/// <b>formato</b> da resposta — e um deploy que acrescenta campo ao DTO não toca em linha nenhuma
/// do banco. O cliente com a resposta anterior em cache revalida, recebe <c>304</c> e continua
/// servindo o corpo velho, sem o campo novo, até que o dado por acaso mude. Medido aqui: a página
/// de versão passou a quebrar inteira (<c>Cannot read properties of undefined</c>) depois de o
/// servidor ganhar um campo que ela já lia.
/// </para>
///
/// <para>
/// O identificador do módulo (MVID) é gerado pelo compilador a cada compilação e é estável
/// enquanto o binário for o mesmo — exatamente a semântica que falta: "a partir desta build, o
/// formato pode ter mudado". O custo é uma revalidação por cliente a cada deploy, que é o preço
/// correto por não servir corpo obsoleto.
/// </para>
/// </summary>
public static class ETagDeConteudo
{
    /// <summary>Identidade desta build — muda a cada compilação do assembly.</summary>
    public static readonly string VersaoDoBinario =
        typeof(ETagDeConteudo).Assembly.ManifestModule.ModuleVersionId.ToString("N")[..8];

    /// <summary>ETag fraca para um recurso cuja versão de dado é <paramref name="versaoDoDado"/>.</summary>
    public static string Montar(uint versaoDoDado) => Montar(VersaoDoBinario, versaoDoDado);

    /// <summary>
    /// A composição, com a versão de binário explícita. Só o defeito que este tipo fecha vive
    /// <b>entre</b> duas builds, e é esta sobrecarga que torna as duas observáveis de uma vez.
    /// </summary>
    public static string Montar(string versaoDoBinario, uint versaoDoDado) =>
        $"W/\"{versaoDoBinario}-{versaoDoDado}\"";
}
