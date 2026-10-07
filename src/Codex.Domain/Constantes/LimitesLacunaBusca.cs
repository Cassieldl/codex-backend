namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos das colunas de <c>codex.lacunasbusca</c> — fonte ÚNICA entre o schema
/// (<c>LacunaBuscaConfiguration</c>), o validator e a entidade. Número repetido nas três camadas
/// diverge em silêncio: coluna mais estreita que o validator vira <c>500</c> (22001) em vez de 422.
/// </summary>
public static class LimitesLacunaBusca
{
    /// <summary>Mínimo do termo é o da própria busca (<c>BuscaService</c>): o que ela aceita, a lacuna registra.</summary>
    public const int TermoMinimo = 2;

    /// <summary><c>termo</c> e <c>termonormalizado</c> — <c>varchar(300)</c>, o mesmo máximo da busca.</summary>
    public const int Termo = 300;

    /// <summary><c>parceiro</c> — <c>varchar(160)</c>.</summary>
    public const int Parceiro = 160;

    /// <summary><c>numerochamado</c> — <c>varchar(60)</c>.</summary>
    public const int NumeroChamado = 60;

    /// <summary><c>nota</c> — <c>varchar(500)</c>: o que foi feito para resolver, ou por que foi descartada.</summary>
    public const int Nota = 500;

    /// <summary><c>registradoporid</c> e <c>resolvidoporid</c> — <c>varchar(100)</c> (o <c>sub</c> do token).</summary>
    public const int UsuarioId = 100;

    /// <summary><c>registradopornome</c> e <c>resolvidopornome</c> — <c>varchar(160)</c>. Vem do token, então é truncado, nunca recusado.</summary>
    public const int UsuarioNome = 160;

    /// <summary>Quantos parceiros (e quantos chamados) a fila agrupada lista por termo; o total sai à parte.</summary>
    public const int ContextosPorTermo = 10;
}
