namespace Codex.Infrastructure.Repositories;

/// <summary>
/// Monta o padrão de <c>ILIKE</c> a partir de um termo digitado pelo usuário.
/// </summary>
/// <remarks>
/// Sem escapar, <c>%</c> e <c>_</c> chegam ao Postgres como **operadores**, não como texto:
/// buscar "50%" casa o acervo inteiro e "campo_nome" casa qualquer caractere no lugar do
/// sublinhado. Não é injeção de SQL — a query é parametrizada —, é resultado errado, e a busca
/// por <c>%</c> ainda varre a tabela toda de graça.
/// </remarks>
public static class PadraoBusca
{
    /// <summary>Barra invertida, declarada aqui para viajar junto do padrão que a usa.</summary>
    public const string CaractereDeEscape = "\\";

    /// <summary>Padrão "contém", com os curingas do termo neutralizados.</summary>
    public static string Contendo(string termo)
    {
        var literal = termo
            .Replace(CaractereDeEscape, CaractereDeEscape + CaractereDeEscape)
            .Replace("%", CaractereDeEscape + "%")
            .Replace("_", CaractereDeEscape + "_");

        return $"%{literal}%";
    }
}
