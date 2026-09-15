namespace Codex.Application.Common;

/// <summary>
/// Termo de busca fora do intervalo aceito (2-300 caracteres, §6.3) — mapeada pelos controllers
/// de P1/I21 para <c>400 PARAMETRO_INVALIDO</c>. O texto da mensagem é montado uma única vez, em
/// <see cref="UseCases.Busca.BuscaService"/>, a partir das constantes do cap.
///
/// <para>
/// <b>Existe por causa do achado BAIXO-B08.</b> A validação lançava
/// <c>ArgumentException(mensagem, nameof(entrada))</c> e os controllers devolviam
/// <c>ex.Message</c> cru, então a resposta pública trazia <c>(Parameter 'entrada')</c> — nome de
/// parâmetro C# vazado a usuário anônimo. Esta classe fecha o vetor por construção, de dois
/// jeitos: (a) só expõe construtor de mensagem, nunca o de <c>paramName</c>, então não há como
/// reintroduzir o sufixo; (b) sendo um tipo próprio, permite que o <c>catch</c> dos controllers
/// seja específico — antes era <c>catch (ArgumentException)</c>, que capturava QUALQUER
/// <c>ArgumentException</c>/<c>ArgumentNullException</c> nascida em qualquer ponto da pilha
/// abaixo do service e devolvia a mensagem dela ao anônimo como se fosse validação de entrada.
/// Com o catch específico, o inesperado cai no middleware global e sai como
/// <c>500 ERRO_INTERNO</c> com mensagem genérica.
/// </para>
///
/// <para>
/// Herda de <see cref="ArgumentException"/> porque é isso que ela é semanticamente — argumento
/// inválido vindo da borda. O defeito nunca foi o tipo: era o <c>paramName</c> e a largura do
/// <c>catch</c>.
/// </para>
/// </summary>
public sealed class TermoBuscaInvalidoException : ArgumentException
{
    public TermoBuscaInvalidoException(string mensagem)
        : base(mensagem)
    {
    }
}
