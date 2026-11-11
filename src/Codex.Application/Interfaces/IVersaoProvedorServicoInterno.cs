using Codex.Application.DTOs.Interno;

namespace Codex.Application.Interfaces;

/// <summary>Regras de negócio da área interna de <see cref="Codex.Domain.Entities.VersaoProvedor"/> (I6-I10).</summary>
public interface IVersaoProvedorServicoInterno
{
    Task<IReadOnlyCollection<VersaoAdminListaItemDto>> ListarPorProvedorAsync(int provedorId, CancellationToken ct);

    Task<VersaoAdminDetalheDto> ObterDetalheAsync(int id, CancellationToken ct);

    Task<VersaoAdminDetalheDto> CriarAsync(int provedorId, CriarVersaoDto dto, CancellationToken ct);

    Task<VersaoAdminDetalheDto> AtualizarAsync(int id, AtualizarVersaoDto dto, uint versaoEsperada, CancellationToken ct);

    Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct);

    Task<VersaoAdminDetalheDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct);

    Task<VersaoAdminDetalheDto> RegistrarVerificacaoAsync(int id, AtualizarVerificacaoDto dto, uint versaoEsperada, CancellationToken ct);

    Task<IReadOnlyCollection<ItemNaturezaOperacaoDto>> SubstituirNaturezasOperacaoAsync(
        int id, AtualizarNaturezasOperacaoDto dto, uint versaoEsperada, CancellationToken ct);
}
