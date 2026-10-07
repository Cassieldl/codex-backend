namespace Codex.Application.Common;

/// <summary>
/// Parâmetro de consulta pública malformado — mapeada pelo controller para
/// <c>400 PARAMETRO_INVALIDO</c>. A mensagem é sempre texto nosso, nunca eco do valor recebido.
/// Tipo próprio (e não <see cref="ArgumentException"/> genérica) pelo mesmo motivo de
/// <see cref="TermoBuscaInvalidoException"/>: o <c>catch</c> do controller é específico, e
/// qualquer argumento inválido inesperado cai no middleware global como 500 genérico.
/// </summary>
public sealed class ParametroConsultaInvalidoException : ArgumentException
{
    public ParametroConsultaInvalidoException(string mensagem)
        : base(mensagem)
    {
    }
}
