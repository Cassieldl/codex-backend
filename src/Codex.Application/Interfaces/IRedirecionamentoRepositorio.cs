using Codex.Domain.Entities;
using Codex.Domain.Enums;

namespace Codex.Application.Interfaces;

/// <summary>
/// Escrita de <see cref="Redirecionamento"/> — compartilhada pelos três services de troca de slug
/// (I37/I43/I44). A leitura continua em cada <c>*RepositorioPublico</c>
/// (<c>ObterRedirecionamentoAsync</c>), que resolve o 308 do lado público.
///
/// <para>
/// Os dois <c>UPDATE</c> abaixo são em LOTE, por <c>WHERE</c> — nunca <c>SELECT</c> + laço em
/// memória: o conjunto de linhas que apontam para um slug abandonado não tem teto (é o histórico
/// inteiro de renomeações daquele recurso), e carregá-lo para trocar uma coluna seria pagar
/// hidratação de entidade por nada.
/// </para>
/// </summary>
public interface IRedirecionamentoRepositorio
{
    /// <summary>
    /// B-4 — desativa todo redirect ATIVO cuja origem é o caminho que está voltando a ficar vivo.
    /// Chamado ANTES de <see cref="RepontarDestinosAsync"/>: sem isto, o ciclo A→B→A faz o colapso
    /// de cadeia encontrar a linha <c>{A→B}</c> e reescrevê-la como <c>{A→A}</c> — um redirect que
    /// nunca é alcançado (o slug vivo resolve antes) e que mente sobre o que é.
    /// </summary>
    Task DesativarPorOrigemAsync(string origem, TipoItemIndexado tipoItem, CancellationToken ct);

    /// <summary>Colapso de cadeia (E2): quem apontava para o slug abandonado passa a apontar para o novo.</summary>
    Task RepontarDestinosAsync(string destinoAntigo, string destinoNovo, TipoItemIndexado tipoItem, CancellationToken ct);

    /// <summary>Grava o redirect do slug antigo para o novo, já dentro da transação da troca.</summary>
    Task CriarAsync(Redirecionamento redirecionamento, CancellationToken ct);
}
