using Codex.Domain.Enums;
using Codex.Domain.Exceptions;

namespace Codex.Domain.Entities;

/// <summary>
/// Particularidade permanente e documental (modelo-dados.md §4.7). Escopo duplo (U8): aponta
/// para um <see cref="Provedor"/> OU para uma <see cref="VersaoProvedor"/> — nunca os dois,
/// nunca nenhum. Garantido por construção: os dois únicos factory methods
/// (<see cref="CriarDeProvedor"/>/<see cref="CriarDeVersao"/>) tornam impossível, em C#, chegar
/// a um estado com os dois FKs preenchidos ou os dois nulos — não existe um construtor genérico
/// que aceite os dois como parâmetro opcional.
/// </summary>
public class Aviso : EntidadeAuditada
{
    public int Id { get; private set; }

    public int? ProvedorId { get; private set; }

    public int? VersaoProvedorId { get; private set; }

    public Severidade Severidade { get; private set; }

    public string Titulo { get; private set; } = null!;

    public string Corpo { get; private set; } = null!;

    public DateOnly VigenciaInicio { get; private set; }

    public bool Publicado { get; private set; }

    public uint Versao { get; private set; }

    private readonly List<int> _camposAfetadosIds = [];

    /// <summary>Ids de <see cref="CampoVersao"/> afetados — só populável quando o aviso é de escopo versão (ver <see cref="SubstituirCamposAfetados"/>).</summary>
    public IReadOnlyCollection<int> CamposAfetadosIds => _camposAfetadosIds.AsReadOnly();

    protected Aviso()
    {
    }

    /// <summary>Aviso de escopo PROVEDOR — <see cref="VersaoProvedorId"/> permanece sempre nulo.</summary>
    public static Aviso CriarDeProvedor(
        int provedorId,
        Severidade severidade,
        string titulo,
        string corpo,
        DateOnly? vigenciaInicio = null)
    {
        ValidarConteudo(titulo, corpo);

        return new Aviso
        {
            ProvedorId = provedorId,
            VersaoProvedorId = null,
            Severidade = severidade,
            Titulo = titulo,
            Corpo = corpo,
            VigenciaInicio = vigenciaInicio ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Publicado = false,
        };
    }

    /// <summary>Aviso de escopo VERSÃO — <see cref="ProvedorId"/> permanece sempre nulo.</summary>
    public static Aviso CriarDeVersao(
        int versaoProvedorId,
        Severidade severidade,
        string titulo,
        string corpo,
        DateOnly? vigenciaInicio = null)
    {
        ValidarConteudo(titulo, corpo);

        return new Aviso
        {
            ProvedorId = null,
            VersaoProvedorId = versaoProvedorId,
            Severidade = severidade,
            Titulo = titulo,
            Corpo = corpo,
            VigenciaInicio = vigenciaInicio ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Publicado = false,
        };
    }

    private static void ValidarConteudo(string titulo, string corpo)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("Título do aviso é obrigatório.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(corpo))
            throw new ArgumentException("Corpo do aviso é obrigatório.", nameof(corpo));
    }

    public void AtualizarConteudo(Severidade severidade, string titulo, string corpo, DateOnly? vigenciaInicio = null)
    {
        ValidarConteudo(titulo, corpo);

        Severidade = severidade;
        Titulo = titulo;
        Corpo = corpo;
        if (vigenciaInicio.HasValue)
            VigenciaInicio = vigenciaInicio.Value;
    }

    /// <summary>
    /// Só faz sentido para aviso de escopo VERSÃO — aviso de provedor não tem contexto de
    /// versão para apontar um <see cref="CampoVersao"/> (risco #3 do DBA, modelo-dados.md §4.8).
    /// </summary>
    public void SubstituirCamposAfetados(IEnumerable<int> campoVersaoIds)
    {
        ArgumentNullException.ThrowIfNull(campoVersaoIds);

        if (ProvedorId is not null)
            throw new RegraDeNegocioException("Aviso de escopo provedor não pode ter campos afetados.");

        _camposAfetadosIds.Clear();
        _camposAfetadosIds.AddRange(campoVersaoIds);
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
