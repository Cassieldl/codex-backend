using System.Linq.Expressions;
using Codex.Domain.Enums;

namespace Codex.Domain.Entities;

/// <summary>
/// Temporário/operacional, distinto de <see cref="Aviso"/> (R5) — expiração obrigatória.
/// Migrada na 002 (decisoes.md U-1/U-2) — <see cref="Publicado"/> é publicação explícita, e a
/// expiração converge por rotina agendada (job de expiração), nunca por soft delete.
/// </summary>
public class Comunicado : EntidadeAuditada
{
    public int Id { get; private set; }

    /// <summary>Token de concorrência otimista (xmin) — sustenta If-Match nas escritas internas (modelo-dados.md §5).</summary>
    public uint Versao { get; private set; }

    public bool Publicado { get; private set; }

    public string Titulo { get; private set; } = null!;

    public string Corpo { get; private set; } = null!;

    public Severidade Severidade { get; private set; }

    public AbrangenciaComunicado Abrangencia { get; private set; }

    public int? ProvedorId { get; private set; }

    public int? VersaoProvedorId { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    protected Comunicado()
    {
    }

    public static Comunicado CriarGlobal(string titulo, string corpo, Severidade severidade, DateTimeOffset expiraEm)
    {
        ValidarConteudo(titulo, corpo);

        return new Comunicado
        {
            Titulo = titulo,
            Corpo = corpo,
            Severidade = severidade,
            Abrangencia = AbrangenciaComunicado.Global,
            ExpiraEm = expiraEm,
        };
    }

    public static Comunicado CriarDeProvedor(int provedorId, string titulo, string corpo, Severidade severidade, DateTimeOffset expiraEm)
    {
        ValidarConteudo(titulo, corpo);

        return new Comunicado
        {
            ProvedorId = provedorId,
            Titulo = titulo,
            Corpo = corpo,
            Severidade = severidade,
            Abrangencia = AbrangenciaComunicado.Provedor,
            ExpiraEm = expiraEm,
        };
    }

    public static Comunicado CriarDeVersaoProvedor(int provedorId, int versaoProvedorId, string titulo, string corpo, Severidade severidade, DateTimeOffset expiraEm)
    {
        ValidarConteudo(titulo, corpo);

        return new Comunicado
        {
            ProvedorId = provedorId,
            VersaoProvedorId = versaoProvedorId,
            Titulo = titulo,
            Corpo = corpo,
            Severidade = severidade,
            Abrangencia = AbrangenciaComunicado.ProvedorVersao,
            ExpiraEm = expiraEm,
        };
    }

    private static void ValidarConteudo(string titulo, string corpo)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("Título do comunicado é obrigatório.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(corpo))
            throw new ArgumentException("Corpo do comunicado é obrigatório.", nameof(corpo));
    }

    /// <summary>
    /// Atualização com escopo GLOBAL (I33) — zera os dois ids, mantendo a consistência que
    /// <c>ck_codex_comunicados_abrangencia_consistente</c> exige. Há uma sobrecarga por
    /// abrangência, espelhando os três factories: trocar de escopo sem trocar os ids na mesma
    /// operação deixaria a linha inválida para o CHECK.
    /// </summary>
    public void AtualizarComoGlobal(string titulo, string corpo, Severidade severidade, DateTimeOffset expiraEm)
    {
        AtualizarConteudo(titulo, corpo, severidade, expiraEm);

        Abrangencia = AbrangenciaComunicado.Global;
        ProvedorId = null;
        VersaoProvedorId = null;
    }

    /// <summary>Atualização com escopo de PROVEDOR (I33).</summary>
    public void AtualizarComoDeProvedor(int provedorId, string titulo, string corpo, Severidade severidade, DateTimeOffset expiraEm)
    {
        AtualizarConteudo(titulo, corpo, severidade, expiraEm);

        Abrangencia = AbrangenciaComunicado.Provedor;
        ProvedorId = provedorId;
        VersaoProvedorId = null;
    }

    /// <summary>Atualização com escopo de VERSÃO de provedor (I33).</summary>
    public void AtualizarComoDeVersaoProvedor(
        int provedorId, int versaoProvedorId, string titulo, string corpo, Severidade severidade, DateTimeOffset expiraEm)
    {
        AtualizarConteudo(titulo, corpo, severidade, expiraEm);

        Abrangencia = AbrangenciaComunicado.ProvedorVersao;
        ProvedorId = provedorId;
        VersaoProvedorId = versaoProvedorId;
    }

    private void AtualizarConteudo(string titulo, string corpo, Severidade severidade, DateTimeOffset expiraEm)
    {
        ValidarConteudo(titulo, corpo);

        Titulo = titulo;
        Corpo = corpo;
        Severidade = severidade;
        ExpiraEm = expiraEm;
    }

    public bool Expirado(DateTimeOffset referencia) => ExpiraEm <= referencia;

    /// <summary>
    /// Predicado ÚNICO de "está no ar agora" — publicado, ativo e ainda não vencido, as MESMAS
    /// três condições do índice parcial <c>idx_codex_comunicados_vigentes</c>. Usado pela leitura
    /// pública (Home, P9) e pela interna (<c>ComunicadoRepositorioInterno.QueryVigentes</c>, que
    /// delega aqui em vez de repetir a expressão): reimplementar o predicado na leitura pública é
    /// o caminho direto para reabrir incidente encerrado (PQ-14).
    ///
    /// <para>
    /// É <see cref="Expression{TDelegate}"/>, não <c>Func</c>, porque precisa ser traduzível para
    /// SQL — avaliar em memória traria toda a tabela para o processo.
    /// </para>
    /// </summary>
    public static Expression<Func<Comunicado, bool>> EhVigenteEm(DateTimeOffset referencia) =>
        c => c.Publicado && c.DesativadoEm == null && c.ExpiraEm > referencia;

    /// <summary>Sempre permitido pelo domínio — a regra "não publicar já expirado" é do Application (I35, §6.2 de analise-backend.md), porque depende de "agora" injetável (TimeProvider), não de um valor fixo na entidade.</summary>
    public void Publicar()
    {
        Publicado = true;
    }

    public void Despublicar()
    {
        Publicado = false;
    }
}
