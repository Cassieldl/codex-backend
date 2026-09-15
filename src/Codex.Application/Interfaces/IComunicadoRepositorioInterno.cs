using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Rótulos de exibição do escopo do comunicado, resolvidos por join — nunca denormalizados na tabela.</summary>
public sealed record RotulosEscopoComunicado(string? ProvedorNome, string? VersaoRotulo);

/// <summary>
/// Repositório de leitura/escrita INTERNA de <see cref="Comunicado"/> — enxerga rascunho e
/// encerrado (I30-I35). Comunicado não é indexado na busca (decisoes.md M-1), então nenhuma
/// operação daqui cascateia para <c>codex.itensbusca</c>.
/// </summary>
public interface IComunicadoRepositorioInterno
{
    Task<Comunicado?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    Task<PaginaResultado<ComunicadoAdminListaItemDto>> ListarAsync(
        FiltroComunicadoInterno filtro, DateTimeOffset referencia, CancellationToken ct);

    /// <summary>Resolve <c>provedorNome</c>/<c>versaoRotulo</c> do detalhe — ambos <c>null</c> quando a abrangência é global.</summary>
    Task<RotulosEscopoComunicado> ObterRotulosEscopoAsync(int? provedorId, int? versaoProvedorId, CancellationToken ct);

    /// <summary>
    /// Despublica, num único <c>UPDATE</c>, todo comunicado publicado e ativo cuja expiração já
    /// passou. É a metade de acesso a dados do job de expiração (§10) — a regra e a semântica
    /// ficam em <see cref="IExpiracaoComunicadosService"/>, o SQL fica aqui, onde a camada de
    /// dados vive.
    /// </summary>
    /// <returns>Linhas afetadas — <c>0</c> quando nada venceu desde o último tique.</returns>
    Task<int> DespublicarVencidosAsync(DateTimeOffset referencia, CancellationToken ct);

    Task AdicionarAsync(Comunicado comunicado, CancellationToken ct);

    Task SalvarAlteracoesAsync(CancellationToken ct);
}
