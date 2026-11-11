using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Domain.Entities;

/// <summary>
/// Campo canônico do vocabulário (modelo-dados.md §4.2). Proibido conter <c>tipo</c>,
/// <c>tamanho</c>, <c>mascara</c> ou <c>obrigatoriedade</c> — isso é comportamento, vive só em
/// <see cref="CampoVersao"/> (R3). Linha viva, nunca duplicada por versão do layout (M12):
/// <see cref="VersaoLayoutId"/> é a versão de ORIGEM, <see cref="VersaoLayoutDescontinuadoId"/>
/// registra em qual versão o campo deixou de ser exigido.
/// </summary>
public class CampoLayout : EntidadeAuditada
{
    public int Id { get; private set; }

    public int VersaoLayoutId { get; private set; }

    /// <summary>Navegação da versão de ORIGEM — precisa estar carregada (Include) antes de chamar <see cref="Descontinuar"/>.</summary>
    public VersaoLayout VersaoLayoutOrigem { get; private set; } = null!;

    public int? VersaoLayoutDescontinuadoId { get; private set; }

    public string Nome { get; private set; } = null!;

    public string CaminhoXml { get; private set; } = null!;

    public string Significado { get; private set; } = null!;

    public string? Grupo { get; private set; }

    public int Ordem { get; private set; }

    /// <summary>Campo criado pela reforma tributária (IBS/CBS), marcado pelo InvoiCy na planilha do layout.</summary>
    public bool ReformaTributaria { get; private set; }

    public string Slug { get; private set; } = null!;

    public EstadoCampoLayout Estado { get; private set; }

    public bool Publicado { get; private set; }

    public uint Versao { get; private set; }

    protected CampoLayout()
    {
    }

    /// <summary>Slug é gerado uma única vez a partir do <paramref name="caminhoXml"/> — nunca do <paramref name="nome"/> (§4.2).</summary>
    public static CampoLayout Criar(
        int versaoLayoutId,
        string nome,
        string caminhoXml,
        string significado,
        int ordem,
        string? grupo = null,
        bool reformaTributaria = false)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome do campo é obrigatório.", nameof(nome));
        if (string.IsNullOrWhiteSpace(caminhoXml))
            throw new ArgumentException("Caminho XML do campo é obrigatório.", nameof(caminhoXml));
        if (string.IsNullOrWhiteSpace(significado))
            throw new ArgumentException("Significado do campo é obrigatório.", nameof(significado));

        return new CampoLayout
        {
            VersaoLayoutId = versaoLayoutId,
            Nome = nome,
            CaminhoXml = caminhoXml,
            Significado = significado,
            Grupo = grupo,
            Ordem = ordem,
            ReformaTributaria = reformaTributaria,
            Slug = SlugVo.DeTexto(caminhoXml),
            Estado = EstadoCampoLayout.Ativo,
            VersaoLayoutDescontinuadoId = null,
            Publicado = false,
        };
    }

    /// <summary>
    /// Só aceita quando <paramref name="versaoDescontinuacao"/>.Ordem é MAIOR que a Ordem da
    /// versão de origem do próprio campo (M12, risco #2 do DBA) — não há CHECK de banco
    /// garantindo isso, a garantia é 100% deste método. <see cref="VersaoLayoutOrigem"/> precisa
    /// estar carregada (Include) pelo chamador antes desta chamada.
    /// </summary>
    public void Descontinuar(VersaoLayout versaoDescontinuacao)
    {
        ArgumentNullException.ThrowIfNull(versaoDescontinuacao);

        if (VersaoLayoutOrigem is null)
            throw new InvalidOperationException(
                "VersaoLayoutOrigem precisa estar carregada (Include) antes de descontinuar o campo.");

        if (versaoDescontinuacao.Ordem <= VersaoLayoutOrigem.Ordem)
            throw new RegraDeNegocioException(
                "A versão de descontinuação precisa ser posterior à versão de origem do campo.");

        Estado = EstadoCampoLayout.Descontinuado;
        VersaoLayoutDescontinuadoId = versaoDescontinuacao.Id;
    }

    /// <summary>Nunca toca <see cref="Slug"/> — renomear não recalcula slug (§7 do modelo de dados).</summary>
    public void AtualizarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome do campo é obrigatório.", nameof(nome));

        Nome = nome;
    }

    public void AtualizarSignificado(string significado)
    {
        if (string.IsNullOrWhiteSpace(significado))
            throw new ArgumentException("Significado do campo é obrigatório.", nameof(significado));

        Significado = significado;
    }

    public void AtualizarGrupo(string? grupo)
    {
        Grupo = grupo;
    }

    public void AtualizarOrdem(int ordem)
    {
        Ordem = ordem;
    }

    public void AtualizarReformaTributaria(bool reformaTributaria)
    {
        ReformaTributaria = reformaTributaria;
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
