using Codex.Application.DTOs.Publico;

namespace Codex.Application.Interfaces;

/// <summary>Leitura pública de comunicados por contexto (provedor / versão).</summary>
public interface IComunicadoRepositorioPublico
{
    /// <summary>
    /// Comunicados no ar (<c>Comunicado.EhVigenteEm</c>) E visíveis (cascata do alvo, a mesma da
    /// Home) que valem no contexto: os globais sempre; os de provedor e de versão só dos
    /// provedores pedidos — e, com <see cref="FiltroComunicadoPublico.VersaoSlug"/>, só os da versão
    /// pedida. Ordem: severidade (crítico primeiro), mais recente, id. No máximo
    /// <paramref name="limite"/> itens.
    /// </summary>
    Task<IReadOnlyCollection<ComunicadoPublicoBrutoDto>> ListarVisiveisAsync(
        DateTimeOffset referencia, FiltroComunicadoPublico filtro, int limite, CancellationToken ct);
}
