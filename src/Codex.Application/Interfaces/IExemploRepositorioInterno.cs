using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>Repositório de leitura/escrita INTERNA de <see cref="Exemplo"/> (I38-I42).</summary>
public interface IExemploRepositorioInterno
{
    Task<Exemplo?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    Task<IReadOnlyCollection<ExemploAdminDto>> ListarPorVersaoAsync(int versaoProvedorId, CancellationToken ct);

    Task<int> ContarPorVersaoAsync(int versaoProvedorId, CancellationToken ct);

    /// <summary>
    /// Slugs já ocupados na versão — sustenta o sufixo determinístico de desambiguação
    /// (modelo-dados.md §1.3). Inclui exemplos desativados: o <c>UNIQUE</c> do banco não conhece
    /// soft delete, então um slug de linha desativada continua ocupado.
    /// </summary>
    Task<IReadOnlyCollection<string>> ListarSlugsDaVersaoAsync(int versaoProvedorId, CancellationToken ct);

    Task AdicionarAsync(Exemplo exemplo, CancellationToken ct);

    /// <summary>
    /// Decisão C5 (analise-backend.md §5.3/§0.9, B-5) — marca a <see cref="VersaoProvedor"/> pai
    /// como modificada no change tracker do MESMO <c>DbContext</c> (escopo compartilhado com
    /// <see cref="IVersaoProvedorRepositorioInterno"/>), forçando o EF a emitir <c>UPDATE</c> de
    /// todas as colunas no próximo <see cref="SalvarAlteracoesAsync"/> mesmo sem mudança de valor
    /// — o que incrementa o <c>xmin</c> no Postgres. Fecha o buraco de cache de P4 (a página
    /// pública é cacheada pela <c>ETag</c> derivada do <c>xmin</c> da versão, e filhos não a
    /// invalidavam). Sem coluna nova; nunca toca <c>IVersaoProvedorRepositorioInterno</c> (fora do
    /// território desta unidade).
    /// </summary>
    void TocarVersaoProvedor(VersaoProvedor versaoProvedor);

    Task SalvarAlteracoesAsync(CancellationToken ct);
}
