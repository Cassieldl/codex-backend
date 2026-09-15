using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Publico;

/// <summary>
/// Filtro de listagem pública de provedores (P2, analise-backend.md §4.1). <c>OrdenarPor</c> é
/// validado por whitelist (switch/case) no repositório — nunca <c>OrderBy(string)</c> arbitrário
/// (rules/security.md, AP-SECURITY-005).
/// </summary>
public sealed record FiltroProvedorPublico(
    string? Busca,
    CategoriaProvedor? Categoria,
    string? OrdenarPor,
    string? Direcao,
    int Pagina,
    int TamanhoPagina);

/// <summary>Filtro de listagem pública de erros (P5) — <c>Codigo</c> é o atalho exato de M2.</summary>
public sealed record FiltroErroPublico(
    string? Codigo,
    string? ProvedorSlug,
    OrigemErro? Origem,
    int Pagina,
    int TamanhoPagina);
