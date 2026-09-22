using Codex.Domain.Enums;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Domain.Entities;

/// <summary>Conteúdo independente de provedor (modelo-dados.md §6.2). Migrada na 002.</summary>
public class Artigo : EntidadeAuditada
{
    public int Id { get; private set; }

    /// <summary>Token de concorrência otimista (xmin) — sustenta If-Match nas escritas internas (modelo-dados.md §5).</summary>
    public uint Versao { get; private set; }

    public TipoArtigo Tipo { get; private set; }

    public string Titulo { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public string Corpo { get; private set; } = null!;

    public bool Publicado { get; private set; }

    public DateTimeOffset? VerificadoEm { get; private set; }

    protected Artigo()
    {
    }

    public static Artigo Criar(TipoArtigo tipo, string titulo, string corpo)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("Título do artigo é obrigatório.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(corpo))
            throw new ArgumentException("Corpo do artigo é obrigatório.", nameof(corpo));

        return new Artigo
        {
            Tipo = tipo,
            Titulo = titulo,
            Slug = SlugVo.DeTexto(titulo),
            Corpo = corpo,
            Publicado = false,
        };
    }

    public void AtualizarConteudo(string titulo, string corpo)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("Título do artigo é obrigatório.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(corpo))
            throw new ArgumentException("Corpo do artigo é obrigatório.", nameof(corpo));

        Titulo = titulo;
        Corpo = corpo;
    }

    /// <summary>
    /// Método separado de <see cref="AtualizarConteudo"/> de propósito (decisoes.md B-2: o editor
    /// que escolheu o tipo errado é o consumidor da edição — trocar o tipo não pode exigir
    /// apagar/recriar o artigo, o que quebraria o <see cref="Slug"/> público). Reindexação (peso D
    /// depende do tipo) é responsabilidade de quem chama, no mesmo fluxo que já recalcula pesos a
    /// cada edição de conteúdo.
    /// </summary>
    public void AtualizarTipo(TipoArtigo tipo)
    {
        Tipo = tipo;
    }

    /// <summary>
    /// Troca explícita de slug (P-10, I44) — mesmo contrato de <see cref="Provedor.AlterarSlug"/>.
    /// A recusa de trocar o slug do artigo RESERVADO é do service
    /// (<c>ArtigoServicoInterno.AtualizarSlugAsync</c>, 422 <c>SLUG_RESERVADO</c>), e não daqui:
    /// é a mesma divisão já usada por I27/I28, onde a entidade não conhece a decisão de produto.
    /// </summary>
    public void AlterarSlug(SlugVo novoSlug)
    {
        ArgumentNullException.ThrowIfNull(novoSlug);
        Slug = novoSlug.Valor;
    }

    public void RegistrarVerificacao(DateTimeOffset momento)
    {
        VerificadoEm = momento;
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
