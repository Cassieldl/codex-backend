using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Enums;

namespace Codex.Application.Services;

/// <summary>
/// Agregador da Home (P9, analise-backend.md §6). É a porta de entrada anônima do portal: sem
/// paginação (nenhum dos conjuntos é paginável de verdade — arquitetura.md §5.D), sem aritmética
/// de offset, e sem nenhum caminho que devolva 404/500 por acervo vazio.
///
/// <para>
/// "Agora" vem do <see cref="TimeProvider"/> injetado — o mesmo relógio do módulo de comunicados,
/// para que a vigência lida aqui e a escrita por I35 nunca divirjam por conta da fonte de tempo.
/// </para>
/// </summary>
public sealed class HomeServicoPublico : IHomeServicoPublico
{
    /// <summary>Três artigos, como o artboard da tela 1 pede — equivalente a P7 com <c>tamanhoPagina=3</c>.</summary>
    private const int ArtigosRecentes = 3;

    private readonly IHomeRepositorioPublico _repositorio;
    private readonly TimeProvider _relogio;

    public HomeServicoPublico(IHomeRepositorioPublico repositorio, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _relogio = relogio;
    }

    public async Task<HomeDto> ObterAsync(CancellationToken ct)
    {
        var comunicados = await _repositorio.ListarComunicadosVisiveisAsync(_relogio.GetUtcNow(), ct);
        var provedores = await _repositorio.ListarProvedoresPublicadosAsync(ct);
        var versoes = await _repositorio.ListarVersoesPublicadasAsync(ct);
        var layout = await _repositorio.ObterResumoLayoutAsync(ct);
        var artigos = await _repositorio.ListarArtigosRecentesAsync(ArtigosRecentes, ct);

        return new HomeDto
        {
            Comunicados = comunicados.Select(MapearComunicado).ToArray(),
            ProvedoresPorCategoria = AgruparPorCategoria(provedores, versoes, comunicados),
            Layout = layout is null ? null : MapearLayout(layout),
            ArtigosRecentes = artigos,
        };
    }

    private static ComunicadoVigenteDto MapearComunicado(ComunicadoHomeBrutoDto comunicado) => new()
    {
        Titulo = comunicado.Titulo,
        Corpo = comunicado.Corpo,
        Severidade = comunicado.Severidade.ToString(),
        Abrangencia = comunicado.Abrangencia.ToString(),
        ExpiraEm = comunicado.ExpiraEm,
        ProvedorNome = comunicado.ProvedorNome,
        Url = MontarUrl(comunicado),
    };

    /// <summary>
    /// <c>Global</c> não tem alvo e por isso não tem URL. Nas demais, o slug do provedor só chega
    /// aqui preenchido quando a cascata do alvo é pública (§6.3) — a ausência dele é tratada como
    /// "sem link", nunca como uma URL pela metade.
    /// </summary>
    private static string? MontarUrl(ComunicadoHomeBrutoDto comunicado)
    {
        if (comunicado.Abrangencia == AbrangenciaComunicado.Global || string.IsNullOrWhiteSpace(comunicado.ProvedorSlug))
            return null;

        return string.IsNullOrWhiteSpace(comunicado.VersaoSlug)
            ? $"/provedores/{comunicado.ProvedorSlug}"
            : $"/provedores/{comunicado.ProvedorSlug}/{comunicado.VersaoSlug}";
    }

    private static IReadOnlyCollection<CategoriaProvedoresDto> AgruparPorCategoria(
        IReadOnlyCollection<ProvedorHomeBrutoDto> provedores,
        IReadOnlyCollection<VersaoProvedorHomeBrutoDto> versoes,
        IReadOnlyCollection<ComunicadoHomeBrutoDto> comunicados)
    {
        // O repositório já devolve as versões ordenadas (§6.4) — GroupBy preserva a ordem de
        // chegada dentro de cada grupo, então o primeiro item é a "última versão".
        var versoesPorProvedor = versoes
            .GroupBy(v => v.ProvedorId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var provedoresComComunicado = comunicados
            .Where(c => c.ProvedorId.HasValue)
            .Select(c => c.ProvedorId!.Value)
            .ToHashSet();

        return provedores
            .GroupBy(p => p.Categoria)
            .OrderBy(g => g.Key) // ordem do enum — determinística, sem depender de rótulo de exibição
            .Select(grupo => new CategoriaProvedoresDto
            {
                Categoria = grupo.Key.ToString(),
                Total = grupo.Count(),
                Provedores = grupo
                    .Select(p => MapearProvedor(p, versoesPorProvedor, provedoresComComunicado))
                    .ToArray(),
            })
            .ToArray();
    }

    private static ProvedorHomeDto MapearProvedor(
        ProvedorHomeBrutoDto provedor,
        IReadOnlyDictionary<int, VersaoProvedorHomeBrutoDto[]> versoesPorProvedor,
        IReadOnlySet<int> provedoresComComunicado)
    {
        var versoes = versoesPorProvedor.TryGetValue(provedor.Id, out var doProvedor) ? doProvedor : [];
        var resumos = versoes.Select(v => new VersaoResumoMinimaDto { Slug = v.Slug, Rotulo = v.Rotulo }).ToArray();

        return new ProvedorHomeDto
        {
            Slug = provedor.Slug,
            Nome = provedor.Nome,
            UltimaVersao = resumos.FirstOrDefault(),
            Versoes = resumos,
            VerificadoEm = versoes.Max(v => v.VerificadoEm),
            TemComunicado = provedoresComComunicado.Contains(provedor.Id),
        };
    }

    private static LayoutResumoHomeDto MapearLayout(LayoutHomeBrutoDto layout) => new()
    {
        Vigente = layout.Vigente,
        Prevista = layout.Prevista,
        Descricao = layout.Descricao,
        CamposNovos = layout.CamposNovos,
        CamposDescontinuados = layout.CamposDescontinuados,
    };
}
