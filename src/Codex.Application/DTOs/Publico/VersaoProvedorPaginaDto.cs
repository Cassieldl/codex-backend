using System.Text.Json.Serialization;

namespace Codex.Application.DTOs.Publico;

/// <summary>
/// Agregado da página de versão de provedor (P4, analise-backend.md §4.3) — a tabela exaustiva
/// de campos com âncora por campo. <see cref="Avisos"/> aqui é só de escopo VERSÃO (avisos de
/// provedor ficam em <see cref="ProvedorDetalheDto"/>, nunca duplicados — U8).
/// </summary>
public sealed record VersaoProvedorPaginaDto
{
    public required ProvedorResumoPaginaDto Provedor { get; init; }

    public required VersaoResumoPaginaDto Versao { get; init; }

    public required IReadOnlyCollection<GrupoCamposPublicoDto> CamposPorGrupo { get; init; }

    public required int TotalCampos { get; init; }

    public required IReadOnlyCollection<AvisoPublicoDto> Avisos { get; init; }

    public required ErrosResumoPublicoDto ErrosResumo { get; init; }

    public required IReadOnlyCollection<VersaoResumoMinimaDto> OutrasVersoes { get; init; }

    /// <summary>
    /// Cenários de XML da versão (003/§5.5). Sempre presente — <c>[]</c> quando a versão não tem
    /// exemplos (U9): o cliente nunca precisa distinguir ausente de vazio.
    /// </summary>
    public required IReadOnlyCollection<ExemploPublicoDto> Exemplos { get; init; }
}

/// <summary>
/// Cenário de XML na página pública da versão. O <c>slug</c> é o que sustenta a âncora copiável
/// <c>#exemplo-{slug}</c> do artboard da tela 5 — por isso ele é persistido e nunca recalculado.
/// Sem campo de publicação: exemplo não tem publicação própria (C1), a visibilidade cascateia
/// 100% da versão/provedor.
/// </summary>
public sealed record ExemploPublicoDto
{
    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public string? Descricao { get; init; }

    public required string Xml { get; init; }
}

public sealed record ProvedorResumoPaginaDto
{
    public required string Slug { get; init; }

    public required string Nome { get; init; }

    public required string Categoria { get; init; }

    public required string IdentificadorInvoicy { get; init; }
}

public sealed record VersaoResumoPaginaDto
{
    public required string Slug { get; init; }

    public required string Rotulo { get; init; }

    public DateOnly? VerificadoEm { get; init; }

    public string? Introducao { get; init; }

    public required string VersaoLayoutRotulo { get; init; }

    /// <summary>
    /// xmin (concorrência otimista) da <c>VersaoProvedor</c> — usado só para compor o header
    /// <c>ETag</c> em <c>ProvedoresController</c> (P4). Nunca serializado: não faz parte do
    /// contrato público (§4.3), é detalhe de infraestrutura de cache.
    /// </summary>
    [JsonIgnore]
    public uint VersaoConcorrencia { get; init; }
}

/// <summary>Grupo de campos ordenado pela menor <c>Ordem</c> de <c>CampoLayout</c> entre seus campos (§4.3).</summary>
public sealed record GrupoCamposPublicoDto
{
    public string? Grupo { get; init; }

    public string? GrupoSlug { get; init; }

    public required IReadOnlyCollection<CampoVersaoPublicoDto> Campos { get; init; }
}

/// <summary>
/// Linha da tabela exaustiva de campos (§4.3). Vocabulário canônico (<c>Nome</c>,
/// <c>CaminhoXml</c>, <c>Significado</c>) vem de <c>CampoLayout</c>; comportamento (<c>Tipo</c>,
/// <c>Tamanho</c>, <c>Mascara</c>, <c>Dominio</c>, <c>EstadoPreenchimento</c>, <c>Condicao</c>,
/// <c>Exemplo</c>, <c>NotaPreenchimento</c>, <c>DestinoXml</c>, <c>NotaTransformacao</c>) vem de
/// <c>CampoVersao</c>. Isto NÃO viola R3 (analise-backend.md §4.3): R3 proíbe essas propriedades
/// de comportamento na família <c>CampoLayout*Dto</c> (vocabulário canônico puro); este é um DTO
/// composto de exibição, não pertence a essa família.
/// </summary>
public sealed record CampoVersaoPublicoDto
{
    public required string CampoLayoutSlug { get; init; }

    public required string Nome { get; init; }

    public required string CaminhoXml { get; init; }

    public required string Significado { get; init; }

    public required string Tipo { get; init; }

    public string? Tamanho { get; init; }

    public string? Mascara { get; init; }

    public string? Dominio { get; init; }

    public required string EstadoPreenchimento { get; init; }

    public string? Condicao { get; init; }

    public string? Exemplo { get; init; }

    public string? NotaPreenchimento { get; init; }

    public string? DestinoXml { get; init; }

    public string? NotaTransformacao { get; init; }

    /// <summary>
    /// Ordem de exibição — é a <c>Ordem</c> de <c>CampoLayout</c> (canônica), não a de
    /// <c>CampoVersao</c>: a spec (§4.3) diz explicitamente "camposPorGrupo ordenado por ordem
    /// do CampoLayout" e não menciona a ordem própria de CampoVersao para este fim.
    /// </summary>
    public required int Ordem { get; init; }
}

/// <summary>
/// Projeção interna do repositório para <c>ListarCamposAsync</c> — inclui <c>Grupo</c>/
/// <c>GrupoSlug</c>, usados só pelo service para agrupar em <see cref="GrupoCamposPublicoDto"/>.
/// Nunca serializada diretamente (por isso não é <see cref="CampoVersaoPublicoDto"/>).
/// </summary>
public sealed record CampoVersaoProjecaoDto
{
    public required string CampoLayoutSlug { get; init; }

    public required string Nome { get; init; }

    public required string CaminhoXml { get; init; }

    public required string Significado { get; init; }

    public required string Tipo { get; init; }

    public string? Tamanho { get; init; }

    public string? Mascara { get; init; }

    public string? Dominio { get; init; }

    public required string EstadoPreenchimento { get; init; }

    public string? Condicao { get; init; }

    public string? Exemplo { get; init; }

    public string? NotaPreenchimento { get; init; }

    public string? DestinoXml { get; init; }

    public string? NotaTransformacao { get; init; }

    public required int Ordem { get; init; }

    public string? Grupo { get; init; }

    public string? GrupoSlug { get; init; }
}

public sealed record ErrosResumoPublicoDto
{
    public required int Total { get; init; }

    public required IReadOnlyCollection<ErroResumoPublicoDto> Itens { get; init; }
}

public sealed record ErroResumoPublicoDto
{
    public required string Slug { get; init; }

    public required string Codigo { get; init; }

    public required string Titulo { get; init; }

    /// <summary>
    /// Mensagens observadas do erro, como o provedor as devolve.
    ///
    /// <para>
    /// É por elas que o integrador reconhece o próprio caso: quem abre esta página tem em mãos o
    /// texto que a prefeitura retornou, não o código interno nem o título que damos a ele. Sem
    /// isto a lista só respondia a quem já sabia o código — e o texto ficava a um clique de
    /// distância, na página do erro.
    /// </para>
    /// </summary>
    public required IReadOnlyCollection<string> Variantes { get; init; }
}
