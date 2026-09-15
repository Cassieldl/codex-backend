namespace Codex.Domain.Entities;

/// <summary>
/// Cenário de XML por versão de provedor (modelo-dados.md §6.5). Sem coluna <c>ambiente</c>
/// (U3/M13). Modelada, sem migration na 001.
/// </summary>
public class Exemplo : EntidadeAuditada
{
    public int Id { get; private set; }

    public int VersaoProvedorId { get; private set; }

    public string Nome { get; private set; } = null!;

    public string? Descricao { get; private set; }

    public string Xml { get; private set; } = null!;

    protected Exemplo()
    {
    }

    public static Exemplo Criar(int versaoProvedorId, string nome, string xml, string? descricao = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome do exemplo é obrigatório.", nameof(nome));
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("XML do exemplo é obrigatório.", nameof(xml));

        return new Exemplo
        {
            VersaoProvedorId = versaoProvedorId,
            Nome = nome,
            Xml = xml,
            Descricao = descricao,
        };
    }

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
