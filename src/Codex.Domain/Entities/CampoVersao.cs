using Codex.Domain.Enums;

namespace Codex.Domain.Entities;

/// <summary>
/// Comportamento do campo naquele provedor/versão (modelo-dados.md §4.6). Não tem
/// <c>Publicado</c> próprio — sua visibilidade é sempre a da <see cref="VersaoProvedor"/> pai
/// (não entra na lista de M19).
/// </summary>
public class CampoVersao : EntidadeAuditada
{
    public int Id { get; private set; }

    public int VersaoProvedorId { get; private set; }

    /// <summary>
    /// FK obrigatória, sem exceção (U4) — não existe overload de <see cref="Criar"/> que
    /// dispense este parâmetro. Se o provedor exige algo fora do vocabulário, a redação cria o
    /// campo canônico primeiro; nunca existe linha órfã aqui.
    /// </summary>
    public int CampoLayoutId { get; private set; }

    public string Tipo { get; private set; } = null!;

    public string? Tamanho { get; private set; }

    public string? Mascara { get; private set; }

    public string? Dominio { get; private set; }

    public EstadoPreenchimento EstadoPreenchimento { get; private set; }

    public string? Condicao { get; private set; }

    public string? Exemplo { get; private set; }

    public string? NotaPreenchimento { get; private set; }

    public string? DestinoXml { get; private set; }

    public string? NotaTransformacao { get; private set; }

    public int Ordem { get; private set; }

    public uint Versao { get; private set; }

    protected CampoVersao()
    {
    }

    public static CampoVersao Criar(
        int versaoProvedorId,
        int campoLayoutId,
        string tipo,
        EstadoPreenchimento estadoPreenchimento,
        int ordem = 0,
        string? tamanho = null,
        string? mascara = null,
        string? dominio = null,
        string? condicao = null,
        string? exemplo = null,
        string? notaPreenchimento = null,
        string? destinoXml = null,
        string? notaTransformacao = null)
    {
        if (string.IsNullOrWhiteSpace(tipo))
            throw new ArgumentException("Tipo do campo é obrigatório.", nameof(tipo));

        return new CampoVersao
        {
            VersaoProvedorId = versaoProvedorId,
            CampoLayoutId = campoLayoutId,
            Tipo = tipo,
            EstadoPreenchimento = estadoPreenchimento,
            Ordem = ordem,
            Tamanho = tamanho,
            Mascara = mascara,
            Dominio = dominio,
            Condicao = condicao,
            Exemplo = exemplo,
            NotaPreenchimento = notaPreenchimento,
            DestinoXml = destinoXml,
            NotaTransformacao = notaTransformacao,
        };
    }

    public void AtualizarComportamento(
        string tipo,
        EstadoPreenchimento estadoPreenchimento,
        int ordem,
        string? tamanho = null,
        string? mascara = null,
        string? dominio = null,
        string? condicao = null,
        string? exemplo = null,
        string? notaPreenchimento = null,
        string? destinoXml = null,
        string? notaTransformacao = null)
    {
        if (string.IsNullOrWhiteSpace(tipo))
            throw new ArgumentException("Tipo do campo é obrigatório.", nameof(tipo));

        Tipo = tipo;
        EstadoPreenchimento = estadoPreenchimento;
        Ordem = ordem;
        Tamanho = tamanho;
        Mascara = mascara;
        Dominio = dominio;
        Condicao = condicao;
        Exemplo = exemplo;
        NotaPreenchimento = notaPreenchimento;
        DestinoXml = destinoXml;
        NotaTransformacao = notaTransformacao;
    }
}
