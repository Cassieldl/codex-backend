namespace Codex.Domain.Exceptions;

/// <summary>Mapeada para 422 pelo middleware de erro. Transição de <c>Estado</c> fora do permitido.</summary>
public sealed class TransicaoDeEstadoInvalidaException : Exception
{
    public TransicaoDeEstadoInvalidaException(string mensagem)
        : base(mensagem)
    {
    }
}
