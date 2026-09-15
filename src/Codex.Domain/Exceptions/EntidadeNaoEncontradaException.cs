namespace Codex.Domain.Exceptions;

/// <summary>Mapeada para 404 pelo middleware de erro (analise-backend.md §8).</summary>
public sealed class EntidadeNaoEncontradaException : Exception
{
    public EntidadeNaoEncontradaException(string mensagem)
        : base(mensagem)
    {
    }

    public EntidadeNaoEncontradaException(string tipoEntidade, object identificador)
        : base($"{tipoEntidade} '{identificador}' não encontrado(a).")
    {
    }
}
