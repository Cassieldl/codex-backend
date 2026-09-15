namespace Codex.Application.Common;

/// <summary>
/// Normalização única de paginação — piso **e teto** nos dois parâmetros.
/// </summary>
/// <remarks>
/// O teto de <see cref="NormalizarPagina"/> não é zelo: o offset que o EF calcula
/// (<c>(pagina - 1) * tamanhoPagina</c>) é aritmética <c>int</c> sem verificação de estouro.
/// Com <c>pagina=2000000000</c> e <c>tamanhoPagina=100</c> o produto transborda para um valor
/// negativo, o <c>Skip()</c> vira <c>OFFSET</c> negativo e o Postgres recusa com
/// <c>2201X: OFFSET must not be negative</c> — 500 devolvido a quem só mandou um número grande,
/// em rota anônima. Capar só <c>tamanhoPagina</c> (que era o que a regra de segurança pedia)
/// deixa o buraco aberto, porque o parâmetro que estoura é o outro.
/// </remarks>
public static class Paginacao
{
    /// <summary>Aplica o padrão quando não informado e prende entre 1 e <paramref name="maximo"/>.</summary>
    public static int NormalizarTamanho(int tamanhoPagina, int padrao, int maximo) =>
        Math.Clamp(tamanhoPagina <= 0 ? padrao : tamanhoPagina, 1, maximo);

    /// <summary>
    /// Prende a página entre 1 e a maior página cujo offset ainda cabe em <see cref="int"/>.
    /// Página acima do acervo continua sendo resultado válido — devolve lista vazia, nunca erro.
    /// </summary>
    public static int NormalizarPagina(int pagina, int tamanhoPaginaNormalizado) =>
        Math.Clamp(pagina, 1, int.MaxValue / tamanhoPaginaNormalizado);

    /// <summary>
    /// Normaliza os dois de uma vez, na ordem certa: o teto da página depende do tamanho já
    /// normalizado. É a forma que todo serviço de listagem usa — a conta existir num lugar só é
    /// o que impede que o próximo filtro nasça com piso e sem teto, como os oito anteriores.
    /// </summary>
    public static (int Pagina, int TamanhoPagina) Normalizar(int pagina, int tamanhoPagina, int padrao, int maximo)
    {
        var tamanhoNormalizado = NormalizarTamanho(tamanhoPagina, padrao, maximo);

        return (NormalizarPagina(pagina, tamanhoNormalizado), tamanhoNormalizado);
    }
}
