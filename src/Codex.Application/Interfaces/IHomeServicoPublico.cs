using Codex.Application.DTOs.Publico;

namespace Codex.Application.Interfaces;

/// <summary>
/// Agregado da Home (P9, analise-backend.md §6.1). Nunca lança para "não há conteúdo": acervo
/// vazio é o estado inicial do produto e responde <c>200</c> com coleções vazias.
/// </summary>
public interface IHomeServicoPublico
{
    Task<HomeDto> ObterAsync(CancellationToken ct);
}
