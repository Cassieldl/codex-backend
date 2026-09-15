namespace Codex.Application.DTOs.Publico;

/// <summary>
/// Forma final do resultado agregado de busca (analise-backend.md §6, arquitetura.md §5.4).
/// Consumido integralmente pela tela 2 (pública) e pela tela interna de busca (I21, M15) — o
/// mesmo shape serve às duas, a diferença de comportamento é <c>incluirRascunho</c> na consulta,
/// nunca no contrato de saída.
/// </summary>
public sealed record ResultadoBuscaDto
{
    /// <summary>Exatamente o que o usuário digitou.</summary>
    public required string Termo { get; init; }

    /// <summary>O que a busca de fato executou — igual a <see cref="Termo"/>, exceto quando houve autocorreção (ver <see cref="Correcao"/>).</summary>
    public required string TermoUsado { get; init; }

    /// <summary>Não nulo só quando a cascata precisou de autocorreção (passo APROX., §6.4 da arquitetura). Nunca corrige em silêncio — a UI sempre mostra os dois termos.</summary>
    public BuscaCorrecaoDto? Correcao { get; init; }

    /// <summary>'exata' | 'parcial' | 'aproximada' | 'vazia' — aciona os estados da tela. 'vazia' é sempre 200, nunca 404.</summary>
    public required string Qualidade { get; init; }

    /// <summary>Total de itens encontrados dentro do filtro de provedor aplicado (ou geral, se nenhum filtro).</summary>
    public required int TotalGeral { get; init; }

    /// <summary>Grupos ordenados por <see cref="BuscaGrupoDto.Pontuacao"/> — nunca por ordem fixa de tipo.</summary>
    public required IReadOnlyCollection<BuscaGrupoDto> Grupos { get; init; }

    /// <summary>Preenchido só quando <see cref="FiltroProvedor"/> foi aplicado e existem resultados fora dele.</summary>
    public BuscaForaDoFiltroDto? ForaDoFiltro { get; init; }

    /// <summary>Preenchido quando <c>?provedor=</c> foi aplicado e o slug resolveu para um provedor real.</summary>
    public BuscaFiltroProvedorDto? FiltroProvedor { get; init; }
}

/// <summary>Bloco de autocorreção explícita (D-arquitetura §6.5) — nunca corrige em silêncio.</summary>
public sealed record BuscaCorrecaoDto
{
    public required string TermoOriginal { get; init; }

    public required string TermoCorrigido { get; init; }

    public required IReadOnlyCollection<BuscaTokenCorrecaoDto> Tokens { get; init; }
}

/// <summary>Um token individual corrigido dentro do termo (ex.: "aliqouta" → "aliquota").</summary>
public sealed record BuscaTokenCorrecaoDto
{
    public required string De { get; init; }

    public required string Para { get; init; }

    public required double Similaridade { get; init; }
}

/// <summary>Um grupo de resultados por tipo de item (erro, provedor, versaoProvedor, campoLayout, aviso).</summary>
public sealed record BuscaGrupoDto
{
    public required string Tipo { get; init; }

    /// <summary>Rótulo de exibição, ex.: "Erros conhecidos".</summary>
    public required string Rotulo { get; init; }

    /// <summary>Total de itens deste tipo dentro do filtro aplicado — pode ser maior que <see cref="Itens"/>.Count quando a página atual não cobre todos.</summary>
    public required int Total { get; init; }

    /// <summary>Maior pontuação dos itens do grupo na página atual — grupos são ordenados por ela.</summary>
    public required double Pontuacao { get; init; }

    public required IReadOnlyCollection<BuscaItemDto> Itens { get; init; }
}

/// <summary>Um item individual de resultado de busca.</summary>
public sealed record BuscaItemDto
{
    public required string Tipo { get; init; }

    /// <summary>Natureza da referência em maiúsculas, ex.: "REJEIÇÃO", "CAMPO DO LAYOUT".</summary>
    public required string Rotulo { get; init; }

    /// <summary>Código do erro, quando aplicável.</summary>
    public string? Chave { get; init; }

    public required string Titulo { get; init; }

    /// <summary>Relativa, já com âncora quando aplicável (ex.: <c>#avisos</c>).</summary>
    public required string Url { get; init; }

    /// <summary>
    /// Nome do provedor do item. Para <c>erro</c>, só é preenchido quando TODAS as versões
    /// aplicáveis pertencem a um único provedor — com mais de um, o nome vem qualificado dentro
    /// de cada entrada de <see cref="Versoes"/>.
    /// </summary>
    public string? ProvedorNome { get; init; }

    /// <summary>Rótulo da versão quando o item pertence a exatamente uma — <c>null</c> quando são várias (aí valem as de <see cref="Versoes"/>) ou nenhuma.</summary>
    public string? VersaoRotulo { get; init; }

    /// <summary>
    /// Rótulos das versões aplicáveis a um <c>erro</c>. É <c>null</c> — nunca coleção vazia —
    /// quando o erro não tem nenhuma: <c>ProvedorNome</c>, <c>VersaoRotulo</c> e <c>Versoes</c>
    /// nulos ao mesmo tempo são o sinal de "erro geral" que a UI usa para o selo "Geral" (U9), e
    /// uma coleção vazia não dispararia esse sinal.
    /// </summary>
    public IReadOnlyCollection<string>? Versoes { get; init; }

    /// <summary>
    /// Estruturado (P-12) — a formatação de exibição (ex.: "verificado em 12/05/2026") passa a
    /// ser responsabilidade do frontend, que já usa <c>date-fns</c>/<c>Intl</c> em todo o resto.
    /// Preenchido para <c>versaoprovedor</c> a partir de <c>VersaoProvedor.VerificadoEm</c>, e
    /// para <c>artigo</c> a partir de <c>Artigo.VerificadoEm</c> (B-5) — <c>null</c> nos demais
    /// tipos e quando a entidade nunca foi verificada.
    /// </summary>
    public BuscaMetaDto? Meta { get; init; }

    /// <summary>
    /// Texto curto explicando a natureza do casamento (ex.: "casou com o código E-124", "casou
    /// com o título"). Migrou do grupo para o item (P-06): o casamento é por item, não por tipo.
    /// </summary>
    public required string DescricaoCasamento { get; init; }

    /// <summary>
    /// Sempre <c>true</c> na busca pública (P1, por construção — nenhuma linha rascunho chega
    /// até aqui). Reflete o estado real na busca interna (I21, <c>incluirRascunho=true</c>) — é o
    /// que permite a UI trocar "Copiar link" por "Copiar link interno" para item em rascunho
    /// (PQ-09).
    /// </summary>
    public required bool Publicado { get; init; }

    public required double Pontuacao { get; init; }

    /// <summary>Fragmentos estruturados de destaque (D11) — nunca HTML.</summary>
    public required IReadOnlyCollection<BuscaTrechoDto> Trechos { get; init; }
}

/// <summary>Meta estruturada de verificação (P-12) — <c>data</c> em ISO (<c>aaaa-MM-dd</c>), sem hora nem fuso.</summary>
public sealed record BuscaMetaDto
{
    public string Tipo => "verificacao";

    public required DateOnly Data { get; init; }
}

/// <summary>Um fragmento de texto de destaque — a UI decide como estilizar <see cref="Destacado"/>, nunca recebe HTML pronto.</summary>
public sealed record BuscaTrechoDto
{
    public required string Texto { get; init; }

    public required bool Destacado { get; init; }
}

/// <summary>Resumo do que existe fora do filtro de provedor aplicado.</summary>
public sealed record BuscaForaDoFiltroDto
{
    public required int Total { get; init; }

    public required IReadOnlyCollection<string> Tipos { get; init; }
}

/// <summary>Identifica o provedor quando <c>?provedor=</c> resolveu para um registro real.</summary>
public sealed record BuscaFiltroProvedorDto
{
    public required string Slug { get; init; }

    public required string Nome { get; init; }
}

/// <summary>
/// Entrada validada de uma consulta de busca — usada tanto pelo endpoint público (P1) quanto
/// pelo interno (I21, M15); o que muda entre os dois é <c>incluirRascunho</c>, passado à parte
/// para <see cref="Codex.Application.Interfaces.IBuscaService.BuscarAsync"/>.
/// </summary>
public sealed record BuscaConsultaEntrada
{
    public required string Termo { get; init; }

    public string? ProvedorSlug { get; init; }

    public IReadOnlyCollection<string>? Tipos { get; init; }

    public int Pagina { get; init; } = 1;

    public int? TamanhoPagina { get; init; }
}
