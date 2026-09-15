namespace Codex.Domain.Constantes;

/// <summary>
/// Códigos de erro que o cliente distingue, em <c>UPPER_SNAKE_CASE</c> (rules/api.md). Fonte
/// única: o código aparece no envelope de erro e nos testes de contrato, e literal repetido nos
/// dois lados é divergência esperando acontecer.
/// </summary>
/// <remarks>
/// Só entra aqui o código que o cliente precisa <b>distinguir</b> de uma violação de regra
/// qualquer. Violação sem tratamento próprio no cliente continua no genérico
/// <see cref="Exceptions.RegraDeNegocioException.CodigoGenerico"/>.
/// </remarks>
public static class CodigosErro
{
    /// <summary>Artigo obrigatório não pode ser removido nem despublicado (analise-backend.md §5.2, I27/I28).</summary>
    public const string ArtigoReservado = "ARTIGO_RESERVADO";

    /// <summary>Publicar comunicado já expirado (analise-backend.md §6.2, I35 — decisão B-4).</summary>
    public const string ComunicadoExpirado = "COMUNICADO_EXPIRADO";
}
