namespace Codex.Application.DTOs.Publico;

/// <summary>Item de listagem pública de provedores (P2) — só provedores com <c>Publicado = true</c>.</summary>
public sealed record ProvedorListaItemDto
{
    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public required string Categoria { get; init; }

    public required string IdentificadorInvoicy { get; init; }

    /// <summary>
    /// MAX(<c>VerificadoEm</c>) entre as versões publicadas e não desativadas do provedor —
    /// inferência necessária para sustentar <c>ordenarPor=verificadoEm</c> (P2), já que
    /// <c>Provedor</c> não tem coluna própria de verificação (só <c>VersaoProvedor</c> tem).
    /// <c>null</c> quando o provedor não tem nenhuma versão publicada com verificação registrada.
    /// </summary>
    public DateOnly? VerificadoEm { get; init; }
}

/// <summary>
/// Página do provedor (P3, U7): identidade + avisos DE PROVEDOR publicados + lista de versões
/// publicadas. Não existe "versão vigente" — <see cref="Versoes"/> é só apresentação, ordenada
/// por <c>CriadoEm DESC</c>.
/// </summary>
public sealed record ProvedorDetalheDto
{
    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public required string Categoria { get; init; }

    public required string IdentificadorInvoicy { get; init; }

    public required IReadOnlyCollection<AvisoPublicoDto> Avisos { get; init; }

    public required IReadOnlyCollection<VersaoResumoPublicoDto> Versoes { get; init; }
}

/// <summary>Item da lista "versões" de P3 — cada versão é uma aba com URL própria (U7).</summary>
public sealed record VersaoResumoPublicoDto
{
    public required string Slug { get; init; }

    public required string Rotulo { get; init; }

    public DateOnly? VerificadoEm { get; init; }

    public required string Url { get; init; }
}

/// <summary>Forma mínima usada em <c>outrasVersoes</c> de P4 (§4.3) — literal, sem <c>verificadoEm</c>/<c>url</c>.</summary>
public sealed record VersaoResumoMinimaDto
{
    public required string Slug { get; init; }

    public required string Rotulo { get; init; }
}

/// <summary>Aviso na visão pública — nunca expõe <c>Id</c>/FKs, só o conteúdo (compartilhado por P3 e P4).</summary>
public sealed record AvisoPublicoDto
{
    public required string Severidade { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public required DateOnly VigenciaInicio { get; init; }
}

/// <summary>
/// Achado bruto de <c>IVersaoProvedorRepositorioPublico.ObterPorSlugsAsync</c> — join explícito
/// provedor + versão + rótulo da versão de layout (analise-backend.md §3.3: nenhuma navegação
/// <c>VersaoProvedor.Provedor</c> existe no mapeamento real, então o repositório devolve os três
/// pedaços já resolvidos em vez de fingir uma entidade agregada). Não filtra <c>Publicado</c> —
/// M7 (distinguir 404 de 410) é responsabilidade do service, que recebe a entidade sempre que
/// ela existe e não está desativada.
/// </summary>
public sealed record ProvedorVersaoBrutaDto(
    Codex.Domain.Entities.Provedor Provedor,
    Codex.Domain.Entities.VersaoProvedor Versao,
    string VersaoLayoutRotulo);
