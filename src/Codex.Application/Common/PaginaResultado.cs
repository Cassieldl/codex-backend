namespace Codex.Application.Common;

/// <summary>
/// Resultado de página de repositório (itens já projetados + total antes da paginação) — tipo
/// compartilhado entre leitura pública (BACK-3) e interna (BACK-4), evitando que cada unidade
/// declare sua própria versão do mesmo conceito (analise-backend.md §3.3 já previa
/// <c>PaginaResultado&lt;Provedor&gt;</c> como padrão de referência). Não confundir com
/// <c>Codex.API.Common.PaginatedResponse&lt;T&gt;</c> — este é o resultado de repositório/service
/// (camada Application); aquele é o envelope HTTP (camada API, rules/api.md).
/// </summary>
/// <remarks>
/// <see cref="Pagina"/> e <see cref="TamanhoPagina"/> são os valores <c>efetivos</c> — os que o
/// serviço normalizou, não os que o cliente pediu. O envelope HTTP precisa ecoar o que foi de
/// fato servido: antes, cada controller refazia a normalização por conta própria, com os limites
/// repetidos à mão, e o envelope passou a afirmar uma página que o serviço nunca serviu no dia em
/// que a normalização ganhou teto.
/// </remarks>
public sealed record PaginaResultado<T>(
    IReadOnlyCollection<T> Itens,
    int TotalCount,
    int Pagina,
    int TamanhoPagina);
