using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Repositório da fila editorial (buscas internas sem resultado).</summary>
public interface ILacunaBuscaRepositorioInterno
{
    Task<LacunaBusca?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    Task<PaginaResultado<LacunaBuscaDto>> ListarAsync(FiltroLacunaBusca filtro, CancellationToken ct);

    /// <summary>
    /// Fila agrupada: um item por <c>termonormalizado</c> no estado filtrado. O agrupamento, a
    /// contagem de grupos e a paginação acontecem no banco — a página é de GRUPOS, não de linhas.
    /// </summary>
    Task<PaginaResultado<LacunaBuscaTermoDto>> ListarTermosAsync(FiltroLacunaBuscaTermo filtro, CancellationToken ct);

    /// <summary>Ocorrências abertas e ativas do termo normalizado — a medida de prioridade da fila.</summary>
    Task<int> ContarAbertasDoTermoAsync(string termoNormalizado, CancellationToken ct);

    /// <summary>
    /// Copia a resolução de <paramref name="referencia"/> (estado, nota, data e usuário, já
    /// aplicados e validados pela entidade), num único <c>UPDATE</c>, para as demais ocorrências
    /// ABERTAS do mesmo termo normalizado. A própria <paramref name="referencia"/> fica de fora.
    /// </summary>
    /// <returns>Linhas afetadas.</returns>
    Task<int> AlterarAbertasDoMesmoTermoAsync(LacunaBusca referencia, CancellationToken ct);

    Task AdicionarAsync(LacunaBusca lacuna, CancellationToken ct);

    Task SalvarAlteracoesAsync(CancellationToken ct);
}
