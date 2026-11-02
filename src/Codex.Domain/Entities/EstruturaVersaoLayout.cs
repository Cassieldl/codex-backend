namespace Codex.Domain.Entities;

/// <summary>
/// Estrutura do documento (árvores XML e JSON) de uma <see cref="VersaoLayout"/>: artefato da
/// versão, não do campo (R3), por isso valor-modelo e repetição vivem aqui e não em
/// <see cref="CampoLayout"/>. Mesma linha de <c>codex.versoeslayout</c> (coluna <c>estrutura</c>),
/// em entidade própria para que só seja lida quando pedida: a árvore tem centenas de KB e toda
/// leitura de versão a arrastaria junto.
/// </summary>
public class EstruturaVersaoLayout
{
    public int VersaoLayoutId { get; private set; }

    /// <summary>JSON das duas árvores, gravado em <c>jsonb</c>. Nulo enquanto a versão não tem estrutura.</summary>
    public string? Conteudo { get; private set; }

    /// <summary>O mesmo <c>xmin</c> da linha da versão.</summary>
    public uint Versao { get; private set; }

    protected EstruturaVersaoLayout()
    {
    }

    public void Substituir(string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new ArgumentException("Estrutura da versão de layout é obrigatória.", nameof(conteudo));

        Conteudo = conteudo;
    }
}
