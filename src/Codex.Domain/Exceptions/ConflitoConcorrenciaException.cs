namespace Codex.Domain.Exceptions;

/// <summary>
/// Mapeada para 409 pelo middleware de erro (código <c>CONFLITO_CONCORRENCIA</c>,
/// analise-backend.md §8) — mesma família de <c>DbUpdateConcurrencyException</c> (EF), mas
/// lançada pela camada de Application quando o <c>If-Match</c> enviado pelo cliente diverge do
/// <c>xmin</c> real da entidade recém-carregada, ANTES de qualquer tentativa de
/// <c>SaveChangesAsync</c> (falha rápida, evita round-trip de escrita para um conflito já
/// detectável na leitura). Nunca vaza qual coluna divergiu — só a mensagem genérica.
/// </summary>
public sealed class ConflitoConcorrenciaException : Exception
{
    public ConflitoConcorrenciaException(string mensagem)
        : base(mensagem)
    {
    }
}
