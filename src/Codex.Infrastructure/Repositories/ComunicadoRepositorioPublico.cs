using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IComunicadoRepositorioPublico"/> sobre <see cref="ComunicadosVisiveis"/>.</summary>
public sealed class ComunicadoRepositorioPublico : RepositorioBase<CodexDbContext>, IComunicadoRepositorioPublico
{
    public ComunicadoRepositorioPublico(CodexDbContext contexto) : base(contexto)
    {
    }

    public async Task<IReadOnlyCollection<ComunicadoPublicoBrutoDto>> ListarVisiveisAsync(
        DateTimeOffset referencia, FiltroComunicadoPublico filtro, int limite, CancellationToken ct)
    {
        var provedorSlugs = filtro.ProvedorSlugs.ToArray();
        var versaoSlug = filtro.VersaoSlug;

        var consulta = ComunicadosVisiveis.Consultar(Contexto, referencia)
            .Where(c => c.Abrangencia == AbrangenciaComunicado.Global
                || (c.ProvedorSlug != null && provedorSlugs.Contains(c.ProvedorSlug)
                    && (versaoSlug == null || c.VersaoSlug == null || c.VersaoSlug == versaoSlug)));

        // A severidade é gravada como texto: ordenar pela coluna daria ordem alfabética
        // (Alerta, Critico, Informativo), então o posto é explícito. Id fecha a ordem total.
        return await consulta
            .OrderBy(c => c.Severidade == Severidade.Critico ? 0 : c.Severidade == Severidade.Alerta ? 1 : 2)
            .ThenByDescending(c => c.CriadoEm)
            .ThenByDescending(c => c.Id)
            .Take(limite)
            .Select(c => new ComunicadoPublicoBrutoDto
            {
                Id = c.Id,
                Titulo = c.Titulo,
                Corpo = c.Corpo,
                Severidade = c.Severidade,
                Abrangencia = c.Abrangencia,
                ExpiraEm = c.ExpiraEm,
                ProvedorNome = c.ProvedorNome,
                ProvedorSlug = c.ProvedorSlug,
                VersaoRotulo = c.VersaoRotulo,
                VersaoSlug = c.VersaoSlug,
            })
            .ToListAsync(ct);
    }
}
