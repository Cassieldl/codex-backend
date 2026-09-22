using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Domain.Entities;

/// <summary>
/// Cenário de XML por versão de provedor (modelo-dados.md §6.5, migrada na 003 — U-1/M-a).
/// Sem coluna <c>ambiente</c> (U3/M13). Slug persistido (não derivado em runtime): o artboard da
/// tela 5 copia <c>#exemplo-{slug}</c> como âncora — a PQ-13 da 002 já fixou o critério "âncora
/// copiável exige slug persistido" (modelo-dados.md §1.2).
/// </summary>
public class Exemplo : EntidadeAuditada
{
    public int Id { get; private set; }

    public int VersaoProvedorId { get; private set; }

    public string Nome { get; private set; } = null!;

    public string? Descricao { get; private set; }

    public string Xml { get; private set; } = null!;

    /// <summary>Gerado uma única vez na criação, a partir de <see cref="Nome"/>, e nunca recalculado (modelo-dados.md §1.3).</summary>
    public string Slug { get; private set; } = null!;

    /// <summary>Token de concorrência otimista (xmin) — sustenta If-Match nas escritas internas (decisão C2).</summary>
    public uint Versao { get; private set; }

    protected Exemplo()
    {
    }

    /// <param name="slugDesambiguado">
    /// Informado só pelo service quando a derivação de <paramref name="nome"/> colide com um slug
    /// já usado na mesma versão (modelo-dados.md §1.3: dois nomes distintos podem normalizar para
    /// o mesmo slug). Nulo = derivação padrão a partir do nome.
    /// </param>
    public static Exemplo Criar(
        int versaoProvedorId, string nome, string xml, string? descricao = null, string? slugDesambiguado = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome do exemplo é obrigatório.", nameof(nome));
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("XML do exemplo é obrigatório.", nameof(xml));

        return new Exemplo
        {
            VersaoProvedorId = versaoProvedorId,
            Nome = nome,
            Slug = slugDesambiguado ?? SlugVo.DeTexto(nome),
            Xml = xml,
            Descricao = descricao,
        };
    }

    /// <summary>Slug permanece intacto — atualizar conteúdo nunca recalcula slug (mesma regra de <see cref="Provedor.Renomear"/>).</summary>
    public void AtualizarConteudo(string nome, string xml, string? descricao)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome do exemplo é obrigatório.", nameof(nome));
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("XML do exemplo é obrigatório.", nameof(xml));

        Nome = nome;
        Xml = xml;
        Descricao = descricao;
    }
}
