namespace Codex.Application.Interfaces;

/// <summary>
/// Convergência de comunicados vencidos (decisoes.md U-5 / analise-backend.md §10): a rotina é a
/// SEMÂNTICA da expiração — transição de estado real e persistida, visível na tela 12 como
/// "Encerrado". O predicado de leitura é a guarda, não a fonte: o teste que prova o predicado roda
/// com este serviço desligado.
/// </summary>
public interface IExpiracaoComunicadosService
{
    /// <param name="referencia">
    /// "Agora" é parâmetro explícito, nunca lido de <c>DateTimeOffset.UtcNow</c> dentro do método —
    /// é o que permite ao teste fixar o instante sem <c>Thread.Sleep</c>.
    /// </param>
    /// <returns>
    /// Quantidade de comunicados despublicados nesta execução. <c>0</c> é resultado normal (nada
    /// venceu desde o último tique), não erro.
    /// </returns>
    Task<int> ExecutarAsync(DateTimeOffset referencia, CancellationToken ct);
}
