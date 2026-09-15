namespace Codex.Domain.Exceptions;

/// <summary>
/// Catch-all de invariante de domínio violada — mapeada para 422. Cobre, entre outras, o XOR
/// de escopo do aviso (U8) e a consistência de <c>ordem</c> do campo descontinuado (M12).
/// </summary>
/// <remarks>
/// <see cref="Codigo"/> é o código que o cliente recebe no envelope de erro. Sem ele, toda
/// violação de regra chega como <c>VALIDACAO_NEGOCIO</c> e o cliente não distingue "artigo
/// obrigatório" de "abrangência inválida" — foi o que aconteceu com o <c>ARTIGO_RESERVADO</c> que
/// analise-backend.md §5.2 exige. Quem precisa de código próprio usa a sobrecarga de dois
/// argumentos; o resto continua no código genérico.
/// </remarks>
public sealed class RegraDeNegocioException : Exception
{
    public const string CodigoGenerico = "VALIDACAO_NEGOCIO";

    public RegraDeNegocioException(string mensagem)
        : this(CodigoGenerico, mensagem)
    {
    }

    public RegraDeNegocioException(string codigo, string mensagem)
        : base(mensagem)
    {
        Codigo = codigo;
    }

    /// <summary>Código do envelope de erro, em <c>UPPER_SNAKE_CASE</c>.</summary>
    public string Codigo { get; }
}
