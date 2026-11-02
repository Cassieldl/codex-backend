using Codex.Application.DTOs.Interno;
using Codex.Application.DTOs.Publico;

namespace Codex.Application.Services;

/// <summary>
/// Recorta a árvore da estrutura do layout aos campos de um alcance do envio modelo. Fica o nó cujo
/// caminho canônico é de um campo do alcance e todos os ancestrais dele; o resto sai. Ordem,
/// valor-modelo, repetição, escolha e formato passam como estão. A raiz fica sempre: é o documento.
/// </summary>
public static class RecorteEnvioModelo
{
    /// <summary>Árvore recortada e os caminhos canônicos que tinham nó nela, antes do corte de escolha.</summary>
    public sealed record Resultado(NoEstruturaDto Raiz, IReadOnlySet<string> CaminhosComNo);

    /// <param name="slugPorCaminho">Caminho canônico de cada campo do alcance, com o slug que o nó recebe.</param>
    public static Resultado Recortar(
        NoEstruturaLayoutDto raiz, IReadOnlyDictionary<string, string> slugPorCaminho, AlcanceEnvioModelo alcance)
    {
        var caminhosComNo = new HashSet<string>(StringComparer.Ordinal);
        var recortada = Podar(raiz, slugPorCaminho, alcance, caminhosComNo)
            ?? Copiar(raiz, campoSlug: null, filhos: []);

        return new Resultado(recortada, caminhosComNo);
    }

    /// <summary>
    /// Nulo quando o nó sai. Grupo que é campo do alcance fica mesmo sem filho: o campo tem posição.
    /// </summary>
    private static NoEstruturaDto? Podar(
        NoEstruturaLayoutDto no,
        IReadOnlyDictionary<string, string> slugPorCaminho,
        AlcanceEnvioModelo alcance,
        HashSet<string> caminhosComNo)
    {
        string? campoSlug = null;
        if (no.CaminhoCampo is not null && slugPorCaminho.TryGetValue(no.CaminhoCampo, out var slug))
        {
            campoSlug = slug;
            caminhosComNo.Add(no.CaminhoCampo);
        }

        var filhos = no.Filhos
            .Select(filho => (Original: filho, Podado: Podar(filho, slugPorCaminho, alcance, caminhosComNo)))
            .ToArray();

        var mantidos = alcance == AlcanceEnvioModelo.Minimo
            ? SoPrimeiraAlternativaDeCadaEscolha(filhos)
            : filhos.Where(filho => filho.Podado is not null).Select(filho => filho.Podado!).ToArray();

        if (campoSlug is null && mantidos.Length == 0)
            return null;

        return Copiar(no, campoSlug, mantidos);
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

    private static NoEstruturaDto Copiar(NoEstruturaLayoutDto no, string? campoSlug, IReadOnlyList<NoEstruturaDto> filhos) => new()
    {
        Nome = no.Nome,
        Caminho = no.Caminho,
        EhGrupo = no.EhGrupo,
        Repete = no.Repete,
        Escolha = no.Escolha,
        ValorModelo = no.ValorModelo,
        CampoSlug = campoSlug,
        Formato = no.Formato,
        Filhos = filhos,
    };
}
