namespace Codex.Domain.Exceptions;

/// <summary>Mapeada para 404 pelo middleware de erro (analise-backend.md §8).</summary>
public sealed class EntidadeNaoEncontradaException : Exception
{
    public const string CodigoGenerico = "NAO_ENCONTRADO";

    public EntidadeNaoEncontradaException(string mensagem)
        : base(mensagem)
    {
    }

    public EntidadeNaoEncontradaException(string tipoEntidade, object identificador)
        : base($"{tipoEntidade} '{identificador}' não encontrado(a).")
    {
    }

    /// <summary>Código do envelope de erro; o 404 que o cliente precisa distinguir traz o seu.</summary>
    public string Codigo { get; init; } = CodigoGenerico;
}
