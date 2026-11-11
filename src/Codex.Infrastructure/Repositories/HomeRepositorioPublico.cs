using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>
/// Implementação de <see cref="IHomeRepositorioPublico"/> (P9, analise-backend.md §6). Cinco
/// consultas curtas sobre índices já existentes, todas <c>AsNoTracking</c> e projetadas — nunca
/// entidade inteira, nunca <c>Include</c> de navegação que a Home não usa.
/// </summary>
public sealed class HomeRepositorioPublico : RepositorioBase<CodexDbContext>, IHomeRepositorioPublico
{
    public HomeRepositorioPublico(CodexDbContext contexto) : base(contexto)
    {
    }

    /// <summary>
    /// A regra de visibilidade (vigência + cascata do alvo, §6.2/§6.3) vive em
    /// <see cref="ComunicadosVisiveis"/>, a mesma consulta de <c>GET /comunicados</c>; aqui ficam
    /// só a ordem e a projeção da Home.
    /// </summary>
    public async Task<IReadOnlyCollection<ComunicadoHomeBrutoDto>> ListarComunicadosVisiveisAsync(
        DateTimeOffset referencia, CancellationToken ct) =>
        await ComunicadosVisiveis.Consultar(Contexto, referencia)
            // Mais urgente primeiro; desempate por chave única, que a projeção não carrega para
            // fora mas a ordenação precisa ter.
            .OrderBy(c => c.ExpiraEm)
            .ThenBy(c => c.Id)
            .Select(c => new ComunicadoHomeBrutoDto
            {
                Titulo = c.Titulo,
                Corpo = c.Corpo,
                Severidade = c.Severidade,
                Abrangencia = c.Abrangencia,
                ExpiraEm = c.ExpiraEm,
                ProvedorId = c.ProvedorId,
                ProvedorNome = c.ProvedorNome,
                ProvedorSlug = c.ProvedorSlug,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<ProvedorHomeBrutoDto>> ListarProvedoresPublicadosAsync(CancellationToken ct) =>
        await Contexto.Provedores.AsNoTracking()
            .Where(p => p.Publicado && p.DesativadoEm == null)
            .OrderBy(p => p.Nome)
            .ThenBy(p => p.Id)
            .Select(p => new ProvedorHomeBrutoDto
            {
                Id = p.Id,
                Slug = p.Slug,
                Nome = p.Nome,
                Categoria = p.Categoria,
            })
            .ToListAsync(ct);

    /// <summary>
    /// Ordenação de §6.4, aplicada de uma vez para todos os provedores: <c>VersaoLayout.Ordem</c>
    /// DESC com desempate por id DESC. O primeiro item do grupo de um provedor é a "última versão"
    /// (que NÃO é "versão vigente" — U7); o grupo inteiro é <c>versoes</c>. Uma consulta só
    /// sustenta os dois campos.
    /// </summary>
    public async Task<IReadOnlyCollection<VersaoProvedorHomeBrutoDto>> ListarVersoesPublicadasAsync(CancellationToken ct) =>
        await (
            from vp in Contexto.VersoesProvedor.AsNoTracking()
            join vl in Contexto.VersoesLayout.AsNoTracking() on vp.VersaoLayoutId equals vl.Id
            join p in Contexto.Provedores.AsNoTracking() on vp.ProvedorId equals p.Id
            where vp.Publicado && vp.DesativadoEm == null
                && p.Publicado && p.DesativadoEm == null
            orderby vl.Ordem descending, vp.Id descending
            select new VersaoProvedorHomeBrutoDto
            {
                ProvedorId = vp.ProvedorId,
                Slug = vp.Slug,
                Rotulo = vp.Rotulo,
                VerificadoEm = vp.VerificadoEm,
            }
        ).ToListAsync(ct);

    /// <summary>
    /// <c>null</c> quando não existe NENHUMA versão de layout publicada — a Home distingue "não há
    /// layout público" de "há layout sem campos novos". Sem <c>PublicadaEm</c> (B-6): a coluna não
    /// existe, e <c>criadoem</c> sob esse rótulo seria a tela afirmando o que não é verdade.
    /// </summary>
    public async Task<LayoutHomeBrutoDto?> ObterResumoLayoutAsync(CancellationToken ct)
    {
        var publicadas = await Contexto.VersoesLayout.AsNoTracking()
            .Where(v => v.Publicado && v.DesativadoEm == null)
            .Select(v => new { v.Id, v.Slug, v.Rotulo, v.Estado, v.Ordem, v.Descricao })
            .ToListAsync(ct);

        if (publicadas.Count == 0)
            return null;

        var vigente = publicadas.SingleOrDefault(v => v.Estado == EstadoVersaoLayout.Vigente);

        // "Próxima prevista": a publicada de menor ordem entre as previstas. Desempate por id para
        // não depender da ordem física quando duas nascem com a mesma ordem.
        var prevista = publicadas
            .Where(v => v.Estado == EstadoVersaoLayout.Prevista)
            .OrderBy(v => v.Ordem)
            .ThenBy(v => v.Id)
            .FirstOrDefault();

        var camposNovos = 0;
        var camposDescontinuados = 0;

        if (vigente is not null)
        {
            // Predicado A1: campo publicado E versão de ORIGEM publicada. Contar campo cuja origem
            // é rascunho anunciaria na Home uma mudança que ninguém consegue abrir.
            var camposVisiveisDaVigente =
                from cl in Contexto.CamposLayout.AsNoTracking()
                join origem in Contexto.VersoesLayout.AsNoTracking() on cl.VersaoLayoutId equals origem.Id
                where cl.Publicado && cl.DesativadoEm == null
                    && origem.Publicado && origem.DesativadoEm == null
                select cl;

            camposNovos = await camposVisiveisDaVigente.CountAsync(cl => cl.VersaoLayoutId == vigente.Id, ct);
            camposDescontinuados = await camposVisiveisDaVigente
                .CountAsync(cl => cl.VersaoLayoutDescontinuadoId == vigente.Id, ct);
        }

        return new LayoutHomeBrutoDto
        {
            Vigente = vigente is null ? null : new VersaoResumoMinimaDto { Slug = vigente.Slug, Rotulo = vigente.Rotulo },
            Prevista = prevista is null ? null : new VersaoResumoMinimaDto { Slug = prevista.Slug, Rotulo = prevista.Rotulo },
            Descricao = vigente?.Descricao,
            CamposNovos = camposNovos,
            CamposDescontinuados = camposDescontinuados,
        };
    }

    /// <summary>
    /// Mesmas regras de P7 (<c>ArtigoRepositorioPublico.ListarAsync</c>): publicado, ativo, sem o
    /// slug reservado, mais recente primeiro com desempate por id — e o MESMO
    /// <see cref="ArtigoListaItemDto"/>, nunca um gêmeo divergente.
    /// </summary>
    public async Task<IReadOnlyCollection<ArtigoListaItemDto>> ListarArtigosRecentesAsync(int quantidade, CancellationToken ct)
    {
        var brutos = await Contexto.Artigos.AsNoTracking()
            .Where(a => a.Publicado && a.DesativadoEm == null && a.Slug != SlugsReservadosArtigo.ComoFunciona)
            .OrderByDescending(a => a.CriadoEm)
            .ThenByDescending(a => a.Id)
            .Take(quantidade)
            .Select(a => new { a.Slug, a.Tipo, a.Titulo, a.Corpo, a.VerificadoEm })
            .ToListAsync(ct);

        return brutos.Select(a => new ArtigoListaItemDto
        {
            Slug = a.Slug,
            Tipo = a.Tipo.ToString(),
            Titulo = a.Titulo,
            Resumo = ResumoArtigo.Derivar(a.Corpo),
            VerificadoEm = a.VerificadoEm.HasValue ? DateOnly.FromDateTime(a.VerificadoEm.Value.UtcDateTime) : null,
        }).ToArray();
    }
}
