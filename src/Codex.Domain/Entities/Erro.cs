using Codex.Domain.Enums;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Domain.Entities;

/// <summary>
/// Erro conhecido, documentado (modelo-dados.md §4.9). <see cref="Codigo"/> não é único entre
/// provedores. Não exige nenhuma versão de provedor aplicável na criação (U9) — a obrigação de
/// ao menos uma versão para <see cref="OrigemErro.Prefeitura"/> é validação de
/// <c>Codex.Application</c> (FluentValidation), nunca regra de domínio.
/// </summary>
public class Erro : EntidadeAuditada
{
    public int Id { get; private set; }

    public string Codigo { get; private set; } = null!;

    public OrigemErro Origem { get; private set; }

    public string Titulo { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public string? CausaProvavel { get; private set; }

    public string? SolucaoSugerida { get; private set; }

    public bool Publicado { get; private set; }

    public uint Versao { get; private set; }

    private readonly List<VarianteMensagem> _variantes = [];
    public IReadOnlyCollection<VarianteMensagem> Variantes => _variantes.AsReadOnly();

    private readonly List<int> _versoesAplicaveisIds = [];
    public IReadOnlyCollection<int> VersoesAplicaveisIds => _versoesAplicaveisIds.AsReadOnly();

    private readonly List<int> _camposRelacionadosIds = [];
    public IReadOnlyCollection<int> CamposRelacionadosIds => _camposRelacionadosIds.AsReadOnly();

    protected Erro()
    {
    }

    public static Erro Criar(
        string codigo,
        OrigemErro origem,
        string titulo,
        string? causaProvavel = null,
        string? solucaoSugerida = null)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("Código do erro é obrigatório.", nameof(codigo));
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("Título do erro é obrigatório.", nameof(titulo));

        return new Erro
        {
            Codigo = codigo,
            Origem = origem,
            Titulo = titulo,
            Slug = SlugVo.DeTexto($"{codigo}-{titulo}"),
            CausaProvavel = causaProvavel,
            SolucaoSugerida = solucaoSugerida,
            Publicado = false,
        };
    }

    public void AtualizarConteudo(string titulo, string? causaProvavel, string? solucaoSugerida)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("Título do erro é obrigatório.", nameof(titulo));

        Titulo = titulo;
        CausaProvavel = causaProvavel;
        SolucaoSugerida = solucaoSugerida;
    }

    /// <summary>
    /// Troca explícita de slug (P-10, I43) — mesmo contrato de <see cref="Provedor.AlterarSlug"/>:
    /// a entidade só troca o próprio valor, e é o service quem grava o
    /// <see cref="Redirecionamento"/> na mesma transação.
    /// </summary>
    public void AlterarSlug(SlugVo novoSlug)
    {
        ArgumentNullException.ThrowIfNull(novoSlug);
        Slug = novoSlug.Valor;
    }

    public void SubstituirVariantes(IEnumerable<string> variantes)
    {
        ArgumentNullException.ThrowIfNull(variantes);

        _variantes.Clear();
        foreach (var texto in variantes)
            _variantes.Add(VarianteMensagem.Criar(Id, texto));
    }

    /// <summary>
    /// Aceita coleção vazia livremente (U9) — a regra "Prefeitura exige ao menos uma" é
    /// validada no Application, porque depende de cruzar <see cref="Origem"/> (já conhecido
    /// pela entidade) com o tamanho da coleção recebida, decisão de manter a regra concentrada
    /// no validator junto das demais regras do agregado.
    /// </summary>
    public void SubstituirVersoesAplicaveis(IEnumerable<int> versaoProvedorIds)
    {
        ArgumentNullException.ThrowIfNull(versaoProvedorIds);

        _versoesAplicaveisIds.Clear();
        _versoesAplicaveisIds.AddRange(versaoProvedorIds);
    }

    public void SubstituirCamposRelacionados(IEnumerable<int> campoLayoutIds)
    {
        ArgumentNullException.ThrowIfNull(campoLayoutIds);

        _camposRelacionadosIds.Clear();
        _camposRelacionadosIds.AddRange(campoLayoutIds);
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
