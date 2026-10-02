using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Infrastructure.Repositories;

/// <summary>
/// Implementação de <see cref="IVersaoProvedorRepositorioPublico"/> (P4) — todo acesso a
/// <see cref="Provedor"/>/<see cref="VersaoLayout"/> a partir de <see cref="VersaoProvedor"/> é
/// via join explícito no LINQ, porque não existe navegação mapeada entre essas entidades
/// (confirmado em <c>VersaoProvedorConfiguration</c>: <c>HasOne&lt;Provedor&gt;().WithMany()</c>
/// sem propriedade de navegação) — nunca <c>Include</c>.
/// </summary>
public sealed class VersaoProvedorRepositorioPublico : RepositorioBase<CodexDbContext>, IVersaoProvedorRepositorioPublico
{
    public VersaoProvedorRepositorioPublico(CodexDbContext contexto) : base(contexto)
    {
    }

    public async Task<ProvedorVersaoBrutaDto?> ObterPorSlugsAsync(string provedorSlug, string versaoSlug, CancellationToken ct)
    {
        var resultado = await (
            from vp in Contexto.VersoesProvedor.AsNoTracking()
            join p in Contexto.Provedores.AsNoTracking() on vp.ProvedorId equals p.Id
            join vl in Contexto.VersoesLayout.AsNoTracking() on vp.VersaoLayoutId equals vl.Id
            where p.Slug == provedorSlug && vp.Slug == versaoSlug
                && p.DesativadoEm == null && vp.DesativadoEm == null
            select new { Provedor = p, Versao = vp, VersaoLayoutRotulo = vl.Rotulo }
        ).FirstOrDefaultAsync(ct);

        return resultado is null
            ? null
            : new ProvedorVersaoBrutaDto(resultado.Provedor, resultado.Versao, resultado.VersaoLayoutRotulo);
    }

    public Task<int> ContarCamposAsync(int versaoProvedorId, CancellationToken ct) =>
        Contexto.CamposVersao.AsNoTracking()
            .CountAsync(c => c.VersaoProvedorId == versaoProvedorId && c.DesativadoEm == null, ct);

    public async Task<IReadOnlyCollection<CampoVersaoProjecaoDto>> ListarCamposAsync(int versaoProvedorId, CancellationToken ct)
    {
        // M23 (campolayout fora do índice público) é sobre a busca/página standalone do campo
        // canônico (demanda 002) — NÃO filtra a tabela exaustiva de P4, cujo propósito é mostrar
        // todo campo em uso naquela versão publicada, independentemente do CampoLayout.Publicado.
        var brutos = await (
            from cv in Contexto.CamposVersao.AsNoTracking()
            join cl in Contexto.CamposLayout.AsNoTracking() on cv.CampoLayoutId equals cl.Id
            where cv.VersaoProvedorId == versaoProvedorId && cv.DesativadoEm == null && cl.DesativadoEm == null
            orderby cl.Ordem
            select new
            {
                cl.Slug,
                cl.Nome,
                cl.CaminhoXml,
                cl.Significado,
                cl.Grupo,
                cl.Ordem,
                cv.Tipo,
                cv.Tamanho,
                cv.Mascara,
                cv.Dominio,
                cv.EstadoPreenchimento,
                cv.Condicao,
                cv.Exemplo,
                cv.NotaPreenchimento,
                cv.DestinoXml,
                cv.NotaTransformacao,
            }
        ).ToListAsync(ct);

        // Uma consulta para TODOS os campos, agrupada em memória — nunca uma subconsulta
        // correlacionada por linha, que numa tabela exaustiva vira uma ida ao banco por campo.
        //
        // O critério repete o de `ListarErrosAplicaveisPublicadosAsync` de propósito: o
        // indicador da linha leva a um erro que a MESMA página lista mais abaixo. Trazer aqui
        // erro geral (sem vínculo de versão) apontaria para fora do que está à vista.
        var errosPorCampo = await (
            from ec in Contexto.ErrosCampos.AsNoTracking()
            join e in Contexto.Erros.AsNoTracking() on ec.ErroId equals e.Id
            join cl in Contexto.CamposLayout.AsNoTracking() on ec.CampoLayoutId equals cl.Id
            where e.Publicado
                && e.DesativadoEm == null
                && ec.DesativadoEm == null
                && Contexto.ErrosVersoes.Any(ev =>
                    ev.ErroId == e.Id
                    && ev.VersaoProvedorId == versaoProvedorId
                    && ev.DesativadoEm == null)
            // Desempate pela chave única: o código empata quando dois erros nascem na mesma
            // transação, e sem ele a ordem dos indicadores muda a cada UPDATE no heap.
            orderby e.Codigo, e.Id
            select new { CampoSlug = cl.Slug, e.Slug, e.Codigo }
        ).ToListAsync(ct);

        var indicePorCampo = errosPorCampo
            .GroupBy(x => x.CampoSlug)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => (IReadOnlyCollection<ErroDoCampoPublicoDto>)grupo
                    .Select(x => new ErroDoCampoPublicoDto { Slug = x.Slug, Codigo = x.Codigo })
                    .ToArray());

        return brutos.Select(c => new CampoVersaoProjecaoDto
        {
            ErrosConhecidos = indicePorCampo.GetValueOrDefault(c.Slug, []),
            CampoLayoutSlug = c.Slug,
            Nome = c.Nome,
            CaminhoXml = c.CaminhoXml,
            Significado = c.Significado,
            Tipo = c.Tipo,
            Tamanho = c.Tamanho,
            Mascara = c.Mascara,
            Dominio = c.Dominio,
            EstadoPreenchimento = c.EstadoPreenchimento.ToString(),
            Condicao = c.Condicao,
            Exemplo = c.Exemplo,
            NotaPreenchimento = c.NotaPreenchimento,
            DestinoXml = c.DestinoXml,
            NotaTransformacao = c.NotaTransformacao,
            Ordem = c.Ordem,
            Grupo = c.Grupo,
            GrupoSlug = GerarGrupoSlug(c.Grupo),
        }).ToArray();
    }

    public async Task<IReadOnlyCollection<Aviso>> ListarAvisosPublicadosDaVersaoAsync(int versaoProvedorId, CancellationToken ct) =>
        await Contexto.Avisos.AsNoTracking()
            .Where(a => a.VersaoProvedorId == versaoProvedorId && a.Publicado && a.DesativadoEm == null)
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<ErroResumoPublicoDto>> ListarErrosAplicaveisPublicadosAsync(int versaoProvedorId, CancellationToken ct) =>
        await (
            from ev in Contexto.ErrosVersoes.AsNoTracking()
            join e in Contexto.Erros.AsNoTracking() on ev.ErroId equals e.Id
            where ev.VersaoProvedorId == versaoProvedorId && e.Publicado && e.DesativadoEm == null
            // Desempate pela chave única, como nos exemplos abaixo: erros criados na mesma
            // transação empatam em `criadoem`, e sem ele a ordem devolvida é a física do heap.
            orderby e.CriadoEm descending, e.Id descending
            select new ErroResumoPublicoDto
            {
                Slug = e.Slug,
                Codigo = e.Codigo,
                Titulo = e.Titulo,
                Variantes = Contexto.VariantesMensagem
                    .Where(v => v.ErroId == e.Id && v.DesativadoEm == null)
                    .OrderBy(v => v.Id)
                    .Select(v => v.Texto)
                    .ToList(),
            }
        ).ToListAsync(ct);

    /// <summary>
    /// Ordem por criação com desempate pela chave única: <c>criadoem</c> empata quando vários
    /// exemplos nascem na mesma transação, e sem o desempate a ordem devolvida é a física do heap
    /// (muda a cada UPDATE) — a âncora publicada exige ordem estável entre requisições.
    /// </summary>
    public async Task<IReadOnlyCollection<ExemploPublicoDto>> ListarExemplosAsync(int versaoProvedorId, CancellationToken ct) =>
        await Contexto.Exemplos.AsNoTracking()
            .Where(e => e.VersaoProvedorId == versaoProvedorId && e.DesativadoEm == null)
            .OrderBy(e => e.CriadoEm)
            .ThenBy(e => e.Id)
            .Select(e => new ExemploPublicoDto
            {
                Slug = e.Slug,
                Nome = e.Nome,
                Descricao = e.Descricao,
                Xml = e.Xml,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<VersaoProvedor>> ListarOutrasVersoesPublicadasAsync(
        int provedorId, int versaoProvedorIdAtual, CancellationToken ct) =>
        await Contexto.VersoesProvedor.AsNoTracking()
            .Where(v => v.ProvedorId == provedorId && v.Id != versaoProvedorIdAtual && v.Publicado && v.DesativadoEm == null)
            .ToListAsync(ct);

    /// <summary>Slug de exibição do grupo — tolera texto que colapsaria para vazio (ex.: só pontuação), tratando como "sem grupo".</summary>
    private static string? GerarGrupoSlug(string? grupo)
    {
        if (string.IsNullOrWhiteSpace(grupo))
            return null;

        try
        {
            return SlugVo.DeTexto(grupo);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
