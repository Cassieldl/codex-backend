using Codex.Domain.ValueObjects;

namespace Codex.Domain.Constantes;

/// <summary>
/// Traduz o caminho PÚBLICO guardado em <c>codex.redirecionamentos.destino</c>
/// (<c>/provedores/{slug}</c>) para a rota da API que serve aquele recurso
/// (<c>/api/v1/provedores/{slug}</c>). Irmã de <see cref="SlugsReservadosArtigo.RotaCanonica"/>:
/// fonte ÚNICA da tradução, para não existirem duas fórmulas divergindo entre si.
///
/// <para>
/// <b>Por que o <c>Location</c> do 308 aponta para a API e não para a rota de app</b>
/// (arquitetura.md §5.F, decisão F1): no navegador, <c>XMLHttpRequest</c> segue redirect de forma
/// transparente — não expõe o status 3xx nem o <c>Location</c> ao código do cliente. Apontar para
/// a rota de app faria o auto-follow refazer a requisição contra um endereço que a API não serve,
/// devolvendo <c>404</c>; apontando para a rota da API, o mesmo auto-follow entrega <c>200</c> com
/// o recurso correto e o cliente descobre a mudança comparando o slug canônico do payload.
/// </para>
///
/// <para>
/// O <c>destino</c> continua sendo gravado no formato público: é o valor certo para um redirect de
/// borda (nginx/CDN) no dia em que existir, e é o formato que <see cref="CaminhoInternoSeguro"/>
/// valida. A tradução acontece só aqui, na borda da resposta HTTP.
/// </para>
/// </summary>
public static class RotaApiPublica
{
    private const string PrefixoApi = "/api/v1";

    /// <summary>Raízes públicas que têm leitura de API 1:1 — as mesmas três entidades de E3 (provedor, erro, artigo).</summary>
    private static readonly string[] RaizesTraduziveis = ["provedores", "erros", "artigos"];

    /// <summary>
    /// Devolve a rota da API correspondente, ou <c>null</c> quando o caminho não é um destino
    /// traduzível (caminho inseguro, raiz desconhecida ou mais de um segmento além da raiz — o
    /// caso de <c>/provedores/{p}/{v}</c>, que não é destino de redirect nenhum porque slug de
    /// versão não muda, decisão E3). <c>null</c> significa "não redirecione": o chamador segue
    /// para o <c>404</c> honesto em vez de mandar o cliente a um endereço que não resolve.
    /// </summary>
    public static string? DeCaminhoPublico(string? caminhoPublico)
    {
        if (!CaminhoInternoSeguro.EhValido(caminhoPublico))
            return null;

        // O reservado não mora sob /artigos/ na superfície pública (RotaCanonica), mas é servido
        // pela mesma rota de API dos demais artigos.
        if (caminhoPublico == $"/{SlugsReservadosArtigo.ComoFunciona}")
            return $"{PrefixoApi}/artigos/{SlugsReservadosArtigo.ComoFunciona}";

        foreach (var raiz in RaizesTraduziveis)
        {
            var prefixoPublico = $"/{raiz}/";
            if (!caminhoPublico!.StartsWith(prefixoPublico, StringComparison.Ordinal))
                continue;

            var slug = caminhoPublico[prefixoPublico.Length..];
            if (slug.Length == 0 || slug.Contains('/', StringComparison.Ordinal))
                return null;

            return $"{PrefixoApi}/{raiz}/{slug}";
        }

        return null;
    }
}
