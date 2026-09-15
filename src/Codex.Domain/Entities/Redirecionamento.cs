using Codex.Domain.Enums;
using Codex.Domain.ValueObjects;

namespace Codex.Domain.Entities;

/// <summary>
/// Slug antigo → novo, estabilidade de URL (modelo-dados.md §5.9). Resposta 308 no leitor.
///
/// <para>
/// <c>Destino</c> vira o header <c>Location</c> de um 308 na leitura pública, então é validado
/// aqui — na ORIGEM (achado <c>BAIXO-B07</c>). O guard dos controllers públicos protege os dois
/// sinks de hoje, mas quem escrever a troca de slug (P-10, adiado para a 002) não passa por eles:
/// entra por esta fábrica.
/// </para>
/// </summary>
public class Redirecionamento : EntidadeAuditada
{
    public int Id { get; private set; }

    public string Origem { get; private set; } = null!;

    public string Destino { get; private set; } = null!;

    public TipoItemIndexado TipoItem { get; private set; }

    protected Redirecionamento()
    {
    }

    public static Redirecionamento Criar(string origem, string destino, TipoItemIndexado tipoItem)
    {
        if (string.IsNullOrWhiteSpace(origem))
            throw new ArgumentException("Origem do redirecionamento é obrigatória.", nameof(origem));
        if (string.IsNullOrWhiteSpace(destino))
            throw new ArgumentException("Destino do redirecionamento é obrigatório.", nameof(destino));
        if (!CaminhoInternoSeguro.EhValido(destino))
            throw new ArgumentException("Destino do redirecionamento precisa ser um path interno seguro.", nameof(destino));

        return new Redirecionamento
        {
            Origem = origem,
            Destino = destino,
            TipoItem = tipoItem,
        };
    }

    /// <summary>Usado no colapso de cadeia (A→B, depois B→C: atualiza o destino de A para C).</summary>
    public void AtualizarDestino(string novoDestino)
    {
        if (string.IsNullOrWhiteSpace(novoDestino))
            throw new ArgumentException("Destino do redirecionamento é obrigatório.", nameof(novoDestino));
        if (!CaminhoInternoSeguro.EhValido(novoDestino))
            throw new ArgumentException("Destino do redirecionamento precisa ser um path interno seguro.", nameof(novoDestino));

        Destino = novoDestino;
    }
}
