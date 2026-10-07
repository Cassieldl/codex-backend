using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>
/// Dono ÚNICO da regra "comunicado que um anônimo pode ver": vigência vem de
/// <see cref="Comunicado.EhVigenteEm"/> e a visibilidade é a cascata do ALVO — comunicado de
/// escopo provedor/versão cujo provedor (ou versão) ainda é rascunho nunca chega ao público,
/// porque vazaria o nome de um provedor pré-lançamento (PQ-04). Global não tem alvo e passa sempre.
/// A Home e <c>GET /comunicados</c> partem desta consulta; reescrever o predicado num dos dois é o
/// caminho direto para os dois divergirem.
/// </summary>
internal static class ComunicadosVisiveis
{
    public static IQueryable<ComunicadoVisivelLinha> Consultar(CodexDbContext contexto, DateTimeOffset referencia) =>
        from c in contexto.Comunicados.AsNoTracking().Where(Comunicado.EhVigenteEm(referencia))
        join provedor in contexto.Provedores.AsNoTracking() on c.ProvedorId equals (int?)provedor.Id into provedorGroup
        from provedor in provedorGroup.DefaultIfEmpty()
        join versao in contexto.VersoesProvedor.AsNoTracking() on c.VersaoProvedorId equals (int?)versao.Id into versaoGroup
        from versao in versaoGroup.DefaultIfEmpty()
        where c.Abrangencia == AbrangenciaComunicado.Global
            || (provedor != null && provedor.Publicado && provedor.DesativadoEm == null
                && (c.VersaoProvedorId == null
                    || (versao != null && versao.Publicado && versao.DesativadoEm == null)))
        select new ComunicadoVisivelLinha
        {
            Id = c.Id,
            Titulo = c.Titulo,
            Corpo = c.Corpo,
            Severidade = c.Severidade,
            Abrangencia = c.Abrangencia,
            ExpiraEm = c.ExpiraEm,
            CriadoEm = c.CriadoEm,
            ProvedorId = c.ProvedorId,
            ProvedorNome = provedor != null ? provedor.Nome : null,
            ProvedorSlug = provedor != null ? provedor.Slug : null,
            VersaoRotulo = versao != null ? versao.Rotulo : null,
            VersaoSlug = versao != null ? versao.Slug : null,
        };
}

internal sealed record ComunicadoVisivelLinha
{
    public required int Id { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public required Severidade Severidade { get; init; }

    public required AbrangenciaComunicado Abrangencia { get; init; }

    public required DateTimeOffset ExpiraEm { get; init; }

    public required DateTimeOffset CriadoEm { get; init; }

    public int? ProvedorId { get; init; }

    public string? ProvedorNome { get; init; }

    public string? ProvedorSlug { get; init; }

    public string? VersaoRotulo { get; init; }

    public string? VersaoSlug { get; init; }
}
