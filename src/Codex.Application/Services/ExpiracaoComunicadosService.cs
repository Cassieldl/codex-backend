using Codex.Application.Interfaces;

namespace Codex.Application.Services;

/// <summary>
/// Regra da expiração de comunicados (analise-backend.md §10.2), testável sem host: recebe o
/// instante de referência e delega o <c>UPDATE</c> único ao repositório.
///
/// <para>
/// Não cascateia para <c>IIndiceBuscaWriter</c> — comunicado não é indexado (decisoes.md M-1), não
/// há nada a recalcular.
/// </para>
/// </summary>
public sealed class ExpiracaoComunicadosService : IExpiracaoComunicadosService
{
    private readonly IComunicadoRepositorioInterno _repositorio;

    public ExpiracaoComunicadosService(IComunicadoRepositorioInterno repositorio)
    {
        _repositorio = repositorio;
    }

    public Task<int> ExecutarAsync(DateTimeOffset referencia, CancellationToken ct) =>
        _repositorio.DespublicarVencidosAsync(referencia, ct);
}
