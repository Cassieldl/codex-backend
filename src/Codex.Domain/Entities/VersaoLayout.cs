using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using Codex.Domain.ValueObjects;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Domain.Entities;

/// <summary>
/// Vocabulário canônico do Layout InvoiCy — versão própria (modelo-dados.md §4.1). Só uma
/// <see cref="EstadoVersaoLayout.Vigente"/> por vez (garantido por índice único parcial no
/// banco, não aqui). <see cref="Ordem"/> sustenta a fórmula de M12 para "o campo X estava ativo
/// na versão Y do layout".
/// </summary>
public class VersaoLayout : EntidadeAuditada
{
    public int Id { get; private set; }

    public string Rotulo { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public EstadoVersaoLayout Estado { get; private set; }

    /// <summary>
    /// Sequência explícita e editável, atribuída pelo service (max(ordem)+1 na criação) — nunca
    /// escolhida livremente pelo cliente. Não é derivada de <see cref="Id"/> nem de
    /// <see cref="Estado"/> (modelo-dados.md §4.1).
    /// </summary>
    public int Ordem { get; private set; }

    /// <summary>
    /// Estado de publicação próprio e explícito (M19) — independente de <see cref="Estado"/>.
    /// Um layout pode estar <see cref="EstadoVersaoLayout.Prevista"/> e já publicado, ou
    /// <see cref="EstadoVersaoLayout.Vigente"/> e ainda rascunho.
    /// </summary>
    public bool Publicado { get; private set; }

    public string? Descricao { get; private set; }

    /// <summary>Norma que deu origem à versão, como "NT 009 v1.01".</summary>
    public string? NormaOrigem { get; private set; }

    /// <summary>Dia em que a versão passou a valer em homologação.</summary>
    public DateOnly? HomologacaoDesde { get; private set; }

    public DateOnly? LiberadaNoInvoicyEm { get; private set; }

    /// <summary>Dia em que a versão passa a ser obrigatória.</summary>
    public DateOnly? ObrigatoriaEm { get; private set; }

    /// <summary>Mudanças estruturadas em relação à versão anterior; vazia quando não há.</summary>
    public IReadOnlyList<MudancaLayout> Mudancas { get; private set; } = [];

    /// <summary>Concorrência otimista via xmin do Postgres — nunca coluna própria (analise-backend.md §3.1).</summary>
    public uint Versao { get; private set; }

    private readonly List<CampoLayout> _camposOrigem = [];
    public IReadOnlyCollection<CampoLayout> CamposOrigem => _camposOrigem.AsReadOnly();

    protected VersaoLayout()
    {
    }

    public static VersaoLayout Criar(string rotulo, int ordem, string? descricao = null)
    {
        if (string.IsNullOrWhiteSpace(rotulo))
            throw new ArgumentException("Rótulo da versão de layout é obrigatório.", nameof(rotulo));

        return new VersaoLayout
        {
            Rotulo = rotulo,
            Slug = SlugVo.DeTexto(rotulo),
            Estado = EstadoVersaoLayout.Prevista,
            Ordem = ordem,
            Publicado = false,
            Descricao = descricao,
        };
    }

    public void MarcarVigente()
    {
        if (Estado != EstadoVersaoLayout.Prevista)
            throw new TransicaoDeEstadoInvalidaException(
                $"Só é possível marcar como Vigente a partir de Prevista (estado atual: {Estado}).");

        Estado = EstadoVersaoLayout.Vigente;
    }

    public void MarcarAnterior()
    {
        if (Estado != EstadoVersaoLayout.Vigente)
            throw new TransicaoDeEstadoInvalidaException(
                $"Só é possível marcar como Anterior a partir de Vigente (estado atual: {Estado}).");

        Estado = EstadoVersaoLayout.Anterior;
    }

    /// <summary>
    /// Reordenação editorial explícita. Unicidade de <see cref="Ordem"/> é garantida pelo banco
    /// (uq_codex_versoeslayout_ordem) — colisão vira <see cref="RegistroDuplicadoException"/> no
    /// repositório, nunca validada aqui.
    /// </summary>
    public void ReordenarPara(int novaOrdem)
    {
        if (novaOrdem <= 0)
            throw new ArgumentOutOfRangeException(nameof(novaOrdem), "Ordem precisa ser positiva.");

        Ordem = novaOrdem;
    }

    public void AtualizarRotulo(string rotulo)
    {
        if (string.IsNullOrWhiteSpace(rotulo))
            throw new ArgumentException("Rótulo da versão de layout é obrigatório.", nameof(rotulo));

        Rotulo = rotulo;
    }

    public void AtualizarDescricao(string? descricao)
    {
        Descricao = descricao;
    }

    /// <summary>Norma em branco vira nula. As datas não precisam estar em ordem.</summary>
    public void AtualizarCalendario(
        string? normaOrigem, DateOnly? homologacaoDesde, DateOnly? liberadaNoInvoicyEm, DateOnly? obrigatoriaEm)
    {
        NormaOrigem = string.IsNullOrWhiteSpace(normaOrigem) ? null : normaOrigem.Trim();
        HomologacaoDesde = homologacaoDesde;
        LiberadaNoInvoicyEm = liberadaNoInvoicyEm;
        ObrigatoriaEm = obrigatoriaEm;
    }

    /// <summary>Substitui a lista inteira, na ordem recebida.</summary>
    public void AtualizarMudancas(IEnumerable<MudancaLayout> mudancas)
    {
        Mudancas = mudancas.ToArray();
    }

    /// <summary>Independente de <see cref="Estado"/> — M19.</summary>
    public void Publicar()
    {
        Publicado = true;
    }

    public void Despublicar()
    {
        Publicado = false;
    }
}
