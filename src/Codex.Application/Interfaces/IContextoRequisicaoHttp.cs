namespace Codex.Application.Interfaces;

/// <summary>
/// Abstração mínima de acesso a dado de rota HTTP, usada por validators que precisam checar uma
/// invariante contra a entidade já existente identificada pela rota (ex.: <c>{id}</c> de
/// <c>PUT /interno/avisos/{id}</c>), sem acoplar <c>Codex.Application</c> a tipos do ASP.NET Core
/// (Clean Architecture — Application não referencia Microsoft.AspNetCore.*). Implementada em
/// <c>Codex.API</c> sobre <c>IHttpContextAccessor</c>.
/// </summary>
public interface IContextoRequisicaoHttp
{
    /// <summary>Valor de rota nomeado <paramref name="chave"/> (default <c>"id"</c>) convertido para <see cref="int"/>, ou <c>null</c> se ausente/inválido.</summary>
    int? ObterIdDaRota(string chave = "id");
}
