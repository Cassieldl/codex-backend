using Codex.Domain.Enums;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Domain.Entities;

/// <summary>
/// Catálogo de provedores (modelo-dados.md §4.3). Sem coluna livre de "observações" — o que
/// seria observação vira <see cref="Aviso"/> com <see cref="Severidade.Informativo"/> e escopo
/// de provedor (U8).
/// </summary>
public class Provedor : EntidadeAuditada
{
    public int Id { get; private set; }

    public string Nome { get; private set; } = null!;

    public string IdentificadorInvoicy { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public CategoriaProvedor Categoria { get; private set; }

    /// <summary>Estado de publicação próprio (M19) — permite redigir o provedor inteiro antes de publicar qualquer coisa.</summary>
    public bool Publicado { get; private set; }

    public uint Versao { get; private set; }

    private readonly List<AliasProvedor> _aliases = [];
    public IReadOnlyCollection<AliasProvedor> Aliases => _aliases.AsReadOnly();

    protected Provedor()
    {
    }

    public static Provedor Criar(string nome, string identificadorInvoicy, CategoriaProvedor categoria)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome do provedor é obrigatório.", nameof(nome));
        if (string.IsNullOrWhiteSpace(identificadorInvoicy))
            throw new ArgumentException("Identificador InvoiCy é obrigatório.", nameof(identificadorInvoicy));

        return new Provedor
        {
            Nome = nome,
            IdentificadorInvoicy = identificadorInvoicy,
            Slug = SlugVo.DeTexto(nome),
            Categoria = categoria,
            Publicado = false,
        };
    }

    /// <summary>Slug permanece intacto — renomear nunca recalcula slug.</summary>
    public void Renomear(string novoNome)
    {
        if (string.IsNullOrWhiteSpace(novoNome))
            throw new ArgumentException("Nome do provedor é obrigatório.", nameof(novoNome));

        Nome = novoNome;
    }

    /// <summary>
    /// Ação explícita de troca de slug. A entidade só troca o próprio valor — é o SERVICE quem
    /// grava o <see cref="Redirecionamento"/> correspondente na mesma transação (§7 do modelo de
    /// dados), nunca esta entidade.
    ///
    /// <para>
    /// <b>CAMINHO INATIVO NA 001</b> (P-10, adiado para a 002 por decisão do usuário): nenhum
    /// service chama este método e nada grava <see cref="Redirecionamento"/>, então o 308 de
    /// P3/P6 nunca dispara hoje. Quem ativar precisa gravar o <see cref="Redirecionamento"/> na
    /// MESMA transação — a validação do destino já está em
    /// <see cref="Redirecionamento.Criar"/>/<see cref="Redirecionamento.AtualizarDestino"/>.
    /// </para>
    /// </summary>
    public void AlterarSlug(SlugVo novoSlug)
    {
        ArgumentNullException.ThrowIfNull(novoSlug);
        Slug = novoSlug.Valor;
    }

    public void SubstituirAliases(IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        _aliases.Clear();
        foreach (var alias in aliases)
            _aliases.Add(AliasProvedor.Criar(Id, alias));
    }

    public void Publicar()
    {
        Publicado = true;
    }

    public void Despublicar()
    {
        Publicado = false;
    }
}
