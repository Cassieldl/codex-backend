namespace Codex.Domain.Exceptions;

/// <summary>
/// Mapeada para 409 pelo middleware de erro. Cobre colisão de slug/código/ordem — inclui a
/// colisão de <c>ordem</c> em <c>versoeslayout</c> traduzida a partir de
/// <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/> pelos repositórios (§9,
/// <c>ReordenarVersaoLayoutTests</c>).
/// </summary>
public sealed class RegistroDuplicadoException : Exception
{
    /// <summary>Código genérico do envelope — o que toda colisão de unicidade produz por padrão.</summary>
    public const string CodigoGenerico = "REGISTRO_DUPLICADO";

    /// <summary>
    /// Código do envelope de erro. Default retrocompatível: quem lança sem especificar continua
    /// produzindo <see cref="CodigoGenerico"/>. Só colisão que o cliente precisa DISTINGUIR ganha
    /// código próprio (PQ-17, analise-backend.md §2.6/§4.6).
    /// </summary>
    public string Codigo { get; }

    public RegistroDuplicadoException(string mensagem, string codigo = CodigoGenerico)
        : base(mensagem)
    {
        Codigo = codigo;
    }

    public RegistroDuplicadoException(string mensagem, Exception innerException, string codigo = CodigoGenerico)
        : base(mensagem, innerException)
    {
        Codigo = codigo;
    }
}
