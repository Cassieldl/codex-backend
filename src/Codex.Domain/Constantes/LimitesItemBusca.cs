namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos das colunas de exibição de <c>codex.itensbusca</c> (modelo-dados.md §5.6) — fonte ÚNICA
/// entre o schema (<c>ItemBuscaConfiguration</c>, <c>Codex.Infrastructure</c>) e o truncamento de
/// exibição (<c>IndiceBuscaWriter.AplicarTetoDeExibicao</c>). Duplicar o número nas duas camadas é
/// exatamente o tipo de contrato que diverge em silêncio quando uma muda e a outra não — mesma
/// família do gotcha de DTO compartilhado registrado em rules/api.md.
/// </summary>
public static class LimitesItemBusca
{
    /// <summary><c>itensbusca.titulo</c> — <c>varchar(300)</c>, texto de EXIBIÇÃO, truncável.</summary>
    public const int Titulo = 300;

    /// <summary><c>itensbusca.subtitulo</c> — <c>varchar(300)</c>, texto de EXIBIÇÃO, truncável.</summary>
    public const int Subtitulo = 300;

    /// <summary><c>itensbusca.chave</c> — <c>varchar(80)</c>. NUNCA truncada (é identificador, não texto).</summary>
    public const int Chave = 80;

    /// <summary><c>itensbusca.url</c> — <c>varchar(400)</c>. NUNCA truncada — cortar a URL quebraria o link.</summary>
    public const int Url = 400;
}
