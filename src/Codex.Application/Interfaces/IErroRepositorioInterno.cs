using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Domain.Entities;

namespace Codex.Application.Interfaces;

/// <summary>
/// Repositório de leitura/escrita INTERNA de <see cref="Erro"/> — enxerga rascunho (I18-I20c).
/// <see cref="Erro.VersoesAplicaveisIds"/>/<see cref="Erro.CamposRelacionadosIds"/> NUNCA são
/// persistidos pelo EF (mapeados com <c>Ignore()</c> — as tabelas de junção reais são
/// <c>codex.errosversoes</c>/<c>codex.erroscampos</c>) — por isso este repositório expõe métodos
/// dedicados para ler/substituir essas duas coleções diretamente, em vez de confiar na
/// propriedade da entidade (sempre vazia ao carregar do banco).
/// </summary>
public interface IErroRepositorioInterno
{
    /// <summary><paramref name="rastreada"/> = true já inclui <see cref="Erro.Variantes"/> (necessário antes de <see cref="Erro.SubstituirVariantes"/>).</summary>
    Task<Erro?> ObterPorIdAsync(int id, bool rastreada, CancellationToken ct);

    Task<PaginaResultado<ErroAdminListaItemDto>> ListarAsync(FiltroErroInterno filtro, CancellationToken ct);

    /// <summary>Todos os erros ativos, com <see cref="Erro.Variantes"/> carregado — usado por I22 (reconstrução total do índice).</summary>
    Task<IReadOnlyCollection<Erro>> ListarTodosAtivosComVariantesAsync(CancellationToken ct);

    Task<IReadOnlyCollection<int>> ListarVersoesAplicaveisIdsAsync(int erroId, CancellationToken ct);

    Task<IReadOnlyCollection<int>> ListarCamposRelacionadosIdsAsync(int erroId, CancellationToken ct);

    /// <summary>Substitui a coleção inteira (PUT substitui por inteiro, §4.2) — não salva sozinho, entra no mesmo <see cref="SalvarAlteracoesAsync"/> do agregado.</summary>
    Task SubstituirVersoesAplicaveisAsync(int erroId, IReadOnlyCollection<int> versaoProvedorIds, CancellationToken ct);

    Task SubstituirCamposRelacionadosAsync(int erroId, IReadOnlyCollection<int> campoLayoutIds, CancellationToken ct);

    /// <summary>
    /// Remove explicitamente as <see cref="VarianteMensagem"/> existentes do <see cref="Erro"/> —
    /// chamar ANTES de <see cref="Erro.SubstituirVariantes"/> num <see cref="Erro"/> rastreado.
    /// <see cref="VarianteMensagem.ErroId"/> é obrigatória e a relação usa
    /// <c>OnDelete(DeleteBehavior.Restrict)</c>: limpar a coleção de navegação sozinha "rompe" o
    /// relacionamento das linhas antigas sem dizer ao EF o que fazer com elas, e o
    /// <c>SaveChanges</c> lança <c>InvalidOperationException</c> em vez de apagar o órfão. Remover
    /// pelo <c>DbSet</c> primeiro marca as linhas antigas como <c>Deleted</c> explicitamente — o
    /// mesmo padrão já usado nas tabelas de junção (<c>ArtigoRepositorioInterno.SubstituirCamposLayoutRelacionadosAsync</c>),
    /// só que aqui a FK não é opcional nem apagável via <c>RemoveRange</c> numa tabela de junção
    /// pura: é uma entidade-filho de verdade.
    /// </summary>
    Task RemoverVariantesAsync(int erroId, CancellationToken ct);

    Task AdicionarAsync(Erro erro, CancellationToken ct);

    Task SalvarAlteracoesAsync(CancellationToken ct);
}
