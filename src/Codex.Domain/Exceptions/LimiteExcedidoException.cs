namespace Codex.Domain.Exceptions;

/// <summary>Mapeada para 422. Cobre o cap de 2000 campos por versão (M22).</summary>
public sealed class LimiteExcedidoException : Exception
{
    public LimiteExcedidoException(string mensagem)
        : base(mensagem)
    {
    }
}
