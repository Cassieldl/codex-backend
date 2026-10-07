using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>Implementação de <see cref="IVerificacaoVencidaRepositorioInterno"/>.</summary>
public sealed class VerificacaoVencidaRepositorioInterno : RepositorioBase<CodexDbContext>, IVerificacaoVencidaRepositorioInterno
{
    public VerificacaoVencidaRepositorioInterno(CodexDbContext contexto) : base(contexto)
    {
    }

    public async Task<PaginaResultado<VersaoVerificacaoVencidaBrutaDto>> ListarAsync(
        DateOnly dataLimite, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var vencidas =
            from versao in Contexto.VersoesProvedor.AsNoTracking()
            join provedor in Contexto.Provedores.AsNoTracking() on versao.ProvedorId equals provedor.Id
            where versao.DesativadoEm == null
                && provedor.DesativadoEm == null
                && (versao.VerificadoEm == null || versao.VerificadoEm <= dataLimite)
            select new { versao, provedor };

        var totalCount = await vencidas.CountAsync(ct);

        // Nunca verificada primeiro (é a mais vencida de todas), depois da mais antiga para a mais nova.
        // O id da versão fecha a ordem: sem chave única, o OFFSET/LIMIT repete uma linha em duas
        // páginas e some com outra quando várias versões compartilham a mesma data.
        var itens = await vencidas
            .OrderBy(x => x.versao.VerificadoEm == null ? 0 : 1)
            .ThenBy(x => x.versao.VerificadoEm)
            .ThenBy(x => x.versao.Id)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(x => new VersaoVerificacaoVencidaBrutaDto(
                x.versao.Id,
                x.versao.Rotulo,
                x.versao.Slug,
                x.versao.Publicado,
                x.versao.VerificadoEm,
                x.provedor.Id,
                x.provedor.Nome,
                x.provedor.Slug))
            .ToListAsync(ct);

        return new PaginaResultado<VersaoVerificacaoVencidaBrutaDto>(itens, totalCount, pagina, tamanhoPagina);
    }
}
