using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Publico;

/// <summary>
/// Comunicado no ar para um contexto de leitura (<c>GET /comunicados</c>): globais, mais os do
/// provedor e os da versão que o contexto pede. Mesma regra de visibilidade da Home — só chega
/// aqui o que está publicado, ativo, não vencido e com o alvo público.
/// </summary>
public sealed record ComunicadoPublicoDto
{
    public required int Id { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    /// <summary>Enum mapeado para <c>string</c> explicitamente — nunca enum cru na resposta.</summary>
    public required string Severidade { get; init; }

    public required string Abrangencia { get; init; }

    public required DateTimeOffset ExpiraEm { get; init; }

    /// <summary>Preenchido em <c>Provedor</c> e <c>ProvedorVersao</c>; <c>null</c> em <c>Global</c>.</summary>
    public ProvedorDoComunicadoDto? Provedor { get; init; }

    /// <summary>Preenchido só em <c>ProvedorVersao</c>.</summary>
    public VersaoDoComunicadoDto? Versao { get; init; }

    /// <summary><c>/provedores/{slug}</c> ou <c>/provedores/{slug}/{versao}</c>; <c>null</c> em <c>Global</c>.</summary>
    public string? Url { get; init; }
}

public sealed record ProvedorDoComunicadoDto
{
    public required string Nome { get; init; }

    public required string Slug { get; init; }
}

public sealed record VersaoDoComunicadoDto
{
    public required string Rotulo { get; init; }

    public required string Slug { get; init; }
}

/// <summary>
/// Contexto de leitura já validado. <see cref="VersaoSlug"/> só existe com exatamente um
/// provedor — o slug de versão é único por provedor, não no acervo.
/// </summary>
public sealed record FiltroComunicadoPublico(IReadOnlyCollection<string> ProvedorSlugs, string? VersaoSlug);

/// <summary>Achado do repositório, com os rótulos do alvo já resolvidos por join.</summary>
public sealed record ComunicadoPublicoBrutoDto
{
    public required int Id { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public required Severidade Severidade { get; init; }

    public required AbrangenciaComunicado Abrangencia { get; init; }

    public required DateTimeOffset ExpiraEm { get; init; }

    public string? ProvedorNome { get; init; }

    public string? ProvedorSlug { get; init; }

    public string? VersaoRotulo { get; init; }

    public string? VersaoSlug { get; init; }
}
