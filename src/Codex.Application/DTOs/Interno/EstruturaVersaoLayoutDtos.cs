namespace Codex.Application.DTOs.Interno;

/// <summary>
/// Estrutura do documento de uma versão de layout (PUT interno): a árvore XML (SOAP) e a JSON
/// (REST), gravadas como vieram. É artefato da versão, não do campo (R3).
/// </summary>
public sealed record AtualizarEstruturaVersaoLayoutDto
{
    public required NoEstruturaLayoutDto Xml { get; init; }

    public required NoEstruturaLayoutDto Json { get; init; }
}

/// <summary>
/// Nó da árvore como a carga grava. <see cref="CaminhoCampo"/> é o caminho canônico do campo
/// (<c>//Tomador/TomaCNPJ</c>): o slug público é resolvido na leitura, nunca gravado, para que
/// campo despublicado não vaze.
/// </summary>
public sealed record NoEstruturaLayoutDto
{
    public required string Nome { get; init; }

    public required string Caminho { get; init; }

    public bool EhGrupo { get; init; }

    public bool Repete { get; init; }

    /// <summary>Alternativa de um <c>xs:choice</c>: entre irmãos consecutivos marcados, só um vai no documento.</summary>
    public bool Escolha { get; init; }

    public string? ValorModelo { get; init; }

    /// <summary><c>ambos</c>, <c>soXml</c> ou <c>soJson</c> (ver <see cref="FormatosNoEstrutura"/>).</summary>
    public required string Formato { get; init; }

    public string? CaminhoCampo { get; init; }

    public IReadOnlyList<NoEstruturaLayoutDto> Filhos { get; init; } = [];
}

/// <summary>Valores aceitos em <see cref="NoEstruturaLayoutDto.Formato"/>.</summary>
public static class FormatosNoEstrutura
{
    public const string Ambos = "ambos";

    public const string SoXml = "soXml";

    public const string SoJson = "soJson";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal) { Ambos, SoXml, SoJson };
}
