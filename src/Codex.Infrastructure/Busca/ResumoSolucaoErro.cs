namespace Codex.Infrastructure.Busca;

/// <summary>
/// Resumo da solução sugerida de um erro para o card da busca: a primeira linha não vazia, cortada
/// por <see cref="TruncamentoExibicao"/>. A solução é texto puro (a tela de detalhe exibe com
/// quebras preservadas, sem Markdown), então nenhuma marcação é removida.
/// </summary>
public static class ResumoSolucaoErro
{
    public const int TamanhoMaximo = 160;

    public static string? Derivar(string? solucaoSugerida)
    {
        if (string.IsNullOrWhiteSpace(solucaoSugerida))
            return null;

        var primeiraLinha = solucaoSugerida
            .Split('\n')
            .Select(linha => linha.Trim())
            .First(linha => linha.Length > 0);

        return TruncamentoExibicao.Truncar(primeiraLinha, TamanhoMaximo);
    }
}
