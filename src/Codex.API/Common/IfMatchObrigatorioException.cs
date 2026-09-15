namespace Codex.API.Common;

/// <summary>
/// Mapeada para 400 pelo middleware de erro (código <c>IF_MATCH_OBRIGATORIO</c>,
/// analise-backend.md §4.4/§8) — lançada quando um <c>PUT</c>/<c>DELETE</c> que exige
/// concorrência otimista chega sem o header <c>If-Match</c> (ou com um valor que não é um
/// inteiro válido). É um erro de protocolo HTTP, não uma violação de invariante de domínio —
/// por isso vive em <c>Codex.API</c>, não em <c>Codex.Domain.Exceptions</c>.
/// </summary>
public sealed class IfMatchObrigatorioException : Exception
{
    public IfMatchObrigatorioException(string mensagem)
        : base(mensagem)
    {
    }
}
