using Codex.Application.DTOs.Interno;
using Codex.Application.DTOs.Publico;

namespace Codex.Application.Services;

/// <summary>
/// Recorta a árvore da estrutura do layout aos campos de um alcance do envio modelo. Fica o nó cujo
/// caminho canônico é de um campo do alcance e todos os ancestrais dele; o resto sai. Ordem,
/// repetição, escolha e formato passam como estão; o valor-modelo também, salvo quando o cenário traz
/// um exemplo para aquele caminho. A raiz fica sempre: é o documento.
/// </summary>
public static class RecorteEnvioModelo
{
    /// <summary>Árvore recortada e os caminhos canônicos que tinham nó nela, antes do corte de escolha.</summary>
    public sealed record Resultado(NoEstruturaDto Raiz, IReadOnlySet<string> CaminhosComNo);

    /// <param name="slugPorCaminho">Caminho canônico de cada campo do alcance, com o slug que o nó recebe.</param>
    /// <param name="valoresDeExemplo">Valor que troca o valor-modelo do nó com aquele caminho canônico.</param>
    public static Resultado Recortar(
        NoEstruturaLayoutDto raiz,
        IReadOnlyDictionary<string, string> slugPorCaminho,
        AlcanceEnvioModelo alcance,
        IReadOnlyDictionary<string, string>? valoresDeExemplo = null)
    {
        var caminhosComNo = new HashSet<string>(StringComparer.Ordinal);
        var poda = new Poda(slugPorCaminho, alcance, valoresDeExemplo ?? new Dictionary<string, string>(), caminhosComNo);
        var recortada = Podar(raiz, poda)
            ?? Copiar(raiz, campoSlug: null, filhos: [], poda.ValoresDeExemplo);

        return new Resultado(recortada, caminhosComNo);
    }

    /// <summary>O que vale para a árvore toda durante um recorte, mais os caminhos que ganharam nó.</summary>
    private sealed record Poda(
        IReadOnlyDictionary<string, string> SlugPorCaminho,
        AlcanceEnvioModelo Alcance,
        IReadOnlyDictionary<string, string> ValoresDeExemplo,
        HashSet<string> CaminhosComNo);

    /// <summary>
    /// Nulo quando o nó sai. Grupo que é campo do alcance fica mesmo sem filho: o campo tem posição.
    /// </summary>
    private static NoEstruturaDto? Podar(NoEstruturaLayoutDto no, Poda poda)
    {
        string? campoSlug = null;
        if (no.CaminhoCampo is not null && poda.SlugPorCaminho.TryGetValue(no.CaminhoCampo, out var slug))
        {
            campoSlug = slug;
            poda.CaminhosComNo.Add(no.CaminhoCampo);
        }

        var filhos = no.Filhos
            .Select(filho => (Original: filho, Podado: Podar(filho, poda)))
            .ToArray();

        var mantidos = poda.Alcance == AlcanceEnvioModelo.Minimo
            ? SoPrimeiraAlternativaDeCadaEscolha(filhos)
            : filhos.Where(filho => filho.Podado is not null).Select(filho => filho.Podado!).ToArray();

        if (campoSlug is null && mantidos.Length == 0)
            return null;

        return Copiar(no, campoSlug, mantidos, poda.ValoresDeExemplo);
    }

    /// <summary>
    /// Uma escolha é a sequência de irmãos consecutivos marcados no XSD. De cada uma, o mínimo leva
    /// só a primeira alternativa que sobrou do recorte; a sequência é a original, para que duas
    /// escolhas separadas por um irmão que saiu não virem uma só.
    /// </summary>
    private static NoEstruturaDto[] SoPrimeiraAlternativaDeCadaEscolha(
        IReadOnlyList<(NoEstruturaLayoutDto Original, NoEstruturaDto? Podado)> filhos)
    {
        var mantidos = new List<NoEstruturaDto>();
        var escolhaJaAtendida = false;

        foreach (var (original, podado) in filhos)
        {
            // Irmão fora da escolha encerra a sequência, mesmo que ele próprio tenha saído.
            if (!original.Escolha)
                escolhaJaAtendida = false;

            if (podado is null)
                continue;

            if (original.Escolha)
            {
                if (escolhaJaAtendida)
                    continue;
                escolhaJaAtendida = true;
            }

            mantidos.Add(podado);
        }

        return mantidos.ToArray();
    }

    private static NoEstruturaDto Copiar(
        NoEstruturaLayoutDto no,
        string? campoSlug,
        IReadOnlyList<NoEstruturaDto> filhos,
        IReadOnlyDictionary<string, string> valoresDeExemplo) => new()
    {
        Nome = no.Nome,
        Caminho = no.Caminho,
        EhGrupo = no.EhGrupo,
        Repete = no.Repete,
        Escolha = no.Escolha,
        ValorModelo = no.CaminhoCampo is not null && valoresDeExemplo.TryGetValue(no.CaminhoCampo, out var exemplo)
            ? exemplo
            : no.ValorModelo,
        CampoSlug = campoSlug,
        Formato = no.Formato,
        Filhos = filhos,
    };
}
