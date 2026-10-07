namespace Codex.Application.Common;

/// <summary>
/// Quem está fazendo a requisição, lido do token pela camada de API. <see cref="Nome"/> é <c>null</c>
/// quando o token não traz nome. Nunca vem do corpo da requisição.
/// </summary>
public sealed record UsuarioDaRequisicao(string Id, string? Nome);
