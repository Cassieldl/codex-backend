using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Domain.Entities;

/// <summary>
/// Conteúdo canônico por versão de provedor (modelo-dados.md §4.5). Nenhuma coluna
/// <c>ambiente</c> (U3). Não existe "versão vigente" (U7) — cada versão tem URL própria, sem
/// resolução nenhuma no banco.
/// </summary>
public class VersaoProvedor : EntidadeAuditada
{
    public int Id { get; private set; }

    public int ProvedorId { get; private set; }

    public int VersaoLayoutId { get; private set; }

    public string Rotulo { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public bool Publicado { get; private set; }

    public DateOnly? VerificadoEm { get; private set; }

    public string? Introducao { get; private set; }

    public uint Versao { get; private set; }

    protected VersaoProvedor()
    {
    }

    public static VersaoProvedor Criar(int provedorId, int versaoLayoutId, string rotulo, string? introducao = null)
    {
        if (string.IsNullOrWhiteSpace(rotulo))
            throw new ArgumentException("Rótulo da versão de provedor é obrigatório.", nameof(rotulo));

        return new VersaoProvedor
        {
            ProvedorId = provedorId,
            VersaoLayoutId = versaoLayoutId,
            Rotulo = rotulo,
            Slug = SlugVo.DeTexto(rotulo),
            Introducao = introducao,
            Publicado = false,
        };
    }

    public void AtualizarRotulo(string rotulo)
    {
        if (string.IsNullOrWhiteSpace(rotulo))
            throw new ArgumentException("Rótulo da versão de provedor é obrigatório.", nameof(rotulo));

        Rotulo = rotulo;
    }

    public void AtualizarIntroducao(string? introducao)
    {
        Introducao = introducao;
    }

    /// <summary>Sem parâmetro de ambiente (U3).</summary>
    public void RegistrarVerificacao(DateOnly data)
    {
        VerificadoEm = data;
    }

    /// <summary>Idempotente.</summary>
    public void Publicar()
    {
        Publicado = true;
    }

    public void Despublicar()
    {
        Publicado = false;
    }
}
