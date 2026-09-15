using Codex.Domain.Enums;

namespace Codex.Domain.Entities;

/// <summary>
/// Projeção materializada de busca — uma linha por item pesquisável (modelo-dados.md §5.6).
/// Construída SÓ pelo <c>IIndiceBuscaWriter</c> (Infrastructure/Busca, implementado pela
/// unidade BACK-2) — nunca por um service de domínio comum. <see cref="PesoA"/>–<see cref="PesoD"/>
/// são as únicas colunas graváveis por C#; <see cref="TextoBusca"/> e <see cref="VisivelPublico"/>
/// são somente leitura do lado C# (geradas pelo Postgres via coluna <c>GENERATED ALWAYS ... STORED</c>
/// — ver migration). A coluna <c>documento</c> (tsvector) não é mapeada aqui porque é tipo
/// específico do provedor Npgsql — Domain não referencia nada (rules/backend.md); ela é mapeada
/// como shadow property em <c>Codex.Infrastructure</c>.
/// </summary>
public class ItemBusca : EntidadeAuditada
{
    public int Id { get; private set; }

    public TipoItemIndexado TipoItem { get; private set; }

    public int ItemId { get; private set; }

    public string? Chave { get; private set; }

    public string Titulo { get; private set; } = null!;

    public string? Subtitulo { get; private set; }

    public string Url { get; private set; } = null!;

    public bool Publicado { get; private set; }

    public int? ProvedorId { get; private set; }

    public int? VersaoProvedorId { get; private set; }

    private readonly List<int> _provedorIds = [];

    /// <summary>Um erro (N:N via errosversoes) pode se relacionar com vários provedores — sustenta o filtro <c>?provedor=</c> de P1.</summary>
    public IReadOnlyCollection<int> ProvedorIds => _provedorIds.AsReadOnly();

    public string? PesoA { get; private set; }

    public string? PesoB { get; private set; }

    public string? PesoC { get; private set; }

    public string? PesoD { get; private set; }

    /// <summary>Gerada pelo Postgres (STORED) — nunca escrita pelo C#.</summary>
    public string? TextoBusca { get; private set; }

    /// <summary>Gerada pelo Postgres (STORED): publicado AND desativadoem IS NULL AND tipoitem &lt;&gt; 'campolayout' (M23) — nunca escrita pelo C#.</summary>
    public bool VisivelPublico { get; private set; }

    protected ItemBusca()
    {
    }

    /// <summary>
    /// Fábrica de uso exclusivo do writer de índice de busca (Infrastructure). Nenhum outro
    /// service de domínio deve chamar isto diretamente.
    /// </summary>
    public static ItemBusca Criar(
        TipoItemIndexado tipoItem,
        int itemId,
        string titulo,
        string url,
        string? chave = null,
        string? subtitulo = null,
        int? provedorId = null,
        int? versaoProvedorId = null,
        IEnumerable<int>? provedorIds = null)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("Título do item de busca é obrigatório.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL do item de busca é obrigatória.", nameof(url));

        var item = new ItemBusca
        {
            TipoItem = tipoItem,
            ItemId = itemId,
            Titulo = titulo,
            Url = url,
            Chave = chave,
            Subtitulo = subtitulo,
            ProvedorId = provedorId,
            VersaoProvedorId = versaoProvedorId,
            Publicado = false,
        };

        if (provedorIds is not null)
            item._provedorIds.AddRange(provedorIds);

        return item;
    }

    public void AtualizarPesos(string? pesoA, string? pesoB, string? pesoC, string? pesoD)
    {
        PesoA = pesoA;
        PesoB = pesoB;
        PesoC = pesoC;
        PesoD = pesoD;
    }

    /// <summary>
    /// Re-sincroniza os campos DERIVADOS da entidade de origem (o que o resultado de busca
    /// exibe e para onde ele aponta). Sem isto, editar o conteúdo atualizaria só os pesos: o
    /// texto indexado passaria a casar com o nome novo enquanto o título exibido continuaria o
    /// do momento da criação — a busca acha pelo nome atual e mostra o antigo.
    /// </summary>
    public void AtualizarProjecao(
        string titulo,
        string url,
        string? chave,
        string? subtitulo,
        int? provedorId,
        int? versaoProvedorId)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("Título do item de busca é obrigatório.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL do item de busca é obrigatória.", nameof(url));

        Titulo = titulo;
        Url = url;
        Chave = chave;
        Subtitulo = subtitulo;
        ProvedorId = provedorId;
        VersaoProvedorId = versaoProvedorId;
    }

    public void AtualizarProvedorIds(IEnumerable<int> provedorIds)
    {
        ArgumentNullException.ThrowIfNull(provedorIds);

        _provedorIds.Clear();
        _provedorIds.AddRange(provedorIds);
    }

    public void MarcarPublicado(bool publicado)
    {
        Publicado = publicado;
    }
}
