namespace Codex.Domain.Exceptions;

/// <summary>
/// Mapeada para 409 pelo middleware de erro. Cobre colisão de slug/código/ordem — inclui a
/// colisão de <c>ordem</c> em <c>versoeslayout</c> traduzida a partir de
/// <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/> pelos repositórios (§9,
/// <c>ReordenarVersaoLayoutTests</c>).
/// </summary>
public sealed class RegistroDuplicadoException : Exception
{
    public RegistroDuplicadoException(string mensagem)
        : base(mensagem)
    {
    }

    public RegistroDuplicadoException(string mensagem, Exception innerException)
        : base(mensagem, innerException)
    {
    }
}
