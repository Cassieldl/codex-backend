using System.Text.Json;
using Codex.Application.DTOs.Interno;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Application.Services;

/// <summary>
/// Leitura pública do vocabulário canônico do Layout InvoiCy (P10/P11/P12, analise-backend.md
/// §4.3). Predicado de visibilidade é o mesmo da cascata de busca (§4.1, decisão A1) — sem regra
/// derivada, sem coluna nova: <c>campo.Publicado &amp;&amp; campo.DesativadoEm IS NULL &amp;&amp;
/// campoOrigem.Publicado &amp;&amp; campoOrigem.DesativadoEm IS NULL</c>.
/// </summary>
public sealed class LayoutServicoPublico : ILayoutServicoPublico
{
    /// <summary>M22 — mesmo cap de P4 (<c>ProvedorServicoPublico</c>), reusado sem nome próprio (analise-backend.md §4.3).</summary>
    private const int CapCamposPorVersao = 2000;

    private readonly ILayoutRepositorioPublico _repositorio;

    public LayoutServicoPublico(ILayoutRepositorioPublico repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<LayoutIndiceDto> ObterIndiceAsync(CancellationToken ct)
    {
        var (versoesPublicadas, camposVisiveis) = await CarregarContextoAsync(ct);

        var versoesOrdenadas = versoesPublicadas.OrderByDescending(v => v.Ordem).ToArray();
        var vigente = versoesOrdenadas.FirstOrDefault(v => v.Estado == EstadoVersaoLayout.Vigente);

        return new LayoutIndiceDto
        {
            Vigente = vigente is null ? null : MontarResumo(vigente, camposVisiveis),
            Versoes = versoesOrdenadas.Select(v => MontarResumo(v, camposVisiveis)).ToArray(),
        };
    }

    public async Task<LayoutVersaoDto> ObterVersaoAsync(string versaoSlug, CancellationToken ct)
    {
        var versao = await _repositorio.ObterVersaoPorSlugAsync(versaoSlug, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoLayout", versaoSlug);

        if (!versao.Publicado)
            throw new ConteudoDespublicadoException("versaolayout", versaoSlug);

        var (versoesPublicadas, camposVisiveis) = await CarregarContextoAsync(ct);

        var ativosNestaVersao = camposVisiveis
            .Where(c => CampoAtivoNaVersao(c.OrigemOrdem, c.DescontinuadoOrdem, versao.Ordem))
            .ToArray();

        if (ativosNestaVersao.Length > CapCamposPorVersao)
            throw new LimiteExcedidoException(
                $"Versão possui {ativosNestaVersao.Length} campos, acima do limite de {CapCamposPorVersao} suportado para exibição (M22).");

        var anterior = versoesPublicadas
            .Where(v => v.Ordem < versao.Ordem)
            .OrderByDescending(v => v.Ordem)
            .FirstOrDefault();

        var camposPorGrupo = ativosNestaVersao
            .GroupBy(c => c.Grupo)
            .OrderBy(g => g.Min(c => c.Ordem))
            .Select(g => new GrupoCamposLayoutDto
            {
                Grupo = g.Key,
                GrupoSlug = GerarGrupoSlug(g.Key),
                Campos = g.OrderBy(c => c.Ordem).Select(c => MapearCampo(c, versao, existeAnterior: anterior is not null)).ToArray(),
            })
            .ToArray();

        return new LayoutVersaoDto
        {
            Versao = MontarResumo(versao, camposVisiveis),
            Descricao = versao.Descricao,
            Anterior = anterior is null ? null : MontarResumo(anterior, camposVisiveis),
            TotalCampos = ativosNestaVersao.Length,
            CamposPorGrupo = camposPorGrupo,
        };
    }

    public async Task<LayoutEstruturaDto> ObterEstruturaAsync(string versaoSlug, CancellationToken ct)
    {
        // Despublicada responde 404 como a inexistente: a estrutura não existe para o leitor.
        var versao = await _repositorio.ObterVersaoPorSlugAsync(versaoSlug, ct);
        if (versao is null || !versao.Publicado)
            throw new EntidadeNaoEncontradaException("VersaoLayout", versaoSlug);

        var conteudo = await _repositorio.ObterEstruturaAsync(versao.Id, ct);
        var estrutura = conteudo is null
            ? null
            : JsonSerializer.Deserialize<AtualizarEstruturaVersaoLayoutDto>(conteudo, JsonSerializerOptions.Web);
        if (estrutura is null)
            throw new EntidadeNaoEncontradaException("EstruturaVersaoLayout", versaoSlug);

        var camposVisiveis = await _repositorio.ListarCamposVisiveisAsync(ct);
        var slugPorCaminho = camposVisiveis
            .Where(c => CampoEmVigorNaVersao(c.OrigemOrdem, c.DescontinuadoOrdem, versao.Ordem))
            .ToDictionary(c => c.CaminhoXml, c => c.Slug, StringComparer.Ordinal);

        return new LayoutEstruturaDto
        {
            Versao = MontarResumo(versao, camposVisiveis),
            Xml = MapearNo(estrutura.Xml, slugPorCaminho),
            Json = MapearNo(estrutura.Json, slugPorCaminho),
        };
    }

    public async Task<CampoLayoutVerbeteDto> ObterCampoAsync(string campoSlug, CancellationToken ct)
    {
        var campo = await _repositorio.ObterCampoPorSlugAsync(campoSlug, ct)
            ?? throw new EntidadeNaoEncontradaException("CampoLayout", campoSlug);

        var origem = campo.VersaoLayoutOrigem;
        var visivel = campo.Publicado && origem.Publicado && origem.DesativadoEm is null;

        if (!visivel)
            throw new ConteudoDespublicadoException("campolayout", campoSlug);

        var (versoesPublicadas, camposVisiveis) = await CarregarContextoAsync(ct);

        VersaoLayout? versaoDescontinuado = null;
        if (campo.VersaoLayoutDescontinuadoId.HasValue)
            versaoDescontinuado = await _repositorio.ObterVersaoPorIdAsync(campo.VersaoLayoutDescontinuadoId.Value, ct);

        var vigentePublicada = versoesPublicadas.FirstOrDefault(v => v.Estado == EstadoVersaoLayout.Vigente);
        var ativoNaVigente = vigentePublicada is not null
            && CampoAtivoNaVersao(origem.Ordem, versaoDescontinuado?.Ordem, vigentePublicada.Ordem);

        // A origem é necessariamente publicada aqui: o predicado A1 acima já barrou o contrário.
        // Um terceiro ramo de `410` seria inalcançável — a análise §4.1 previa três, mas o A1,
        // aplicado antes, resolve o caso da origem despublicada.
        var versaoExibicao = ativoNaVigente ? vigentePublicada! : origem;

        var usos = await _repositorio.ListarUsosPublicosAsync(campo.Id, ct);

        return new CampoLayoutVerbeteDto
        {
            Slug = campo.Slug,
            Nome = campo.Nome,
            CaminhoXml = campo.CaminhoXml,
            Significado = campo.Significado,
            Estado = campo.Estado.ToString(),
            VersaoExibicao = MontarResumo(versaoExibicao, camposVisiveis),
            VersaoOrigem = MontarResumo(origem, camposVisiveis),
            VersaoDescontinuado = versaoDescontinuado is null ? null : MontarResumo(versaoDescontinuado, camposVisiveis),
            OndeEUsado = usos,
        };
    }

    private async Task<(IReadOnlyCollection<VersaoLayout> VersoesPublicadas, IReadOnlyCollection<CampoLayoutVisivelBrutoDto> CamposVisiveis)> CarregarContextoAsync(CancellationToken ct)
    {
        var versoesPublicadas = await _repositorio.ListarVersoesPublicadasAsync(ct);
        var camposVisiveis = await _repositorio.ListarCamposVisiveisAsync(ct);
        return (versoesPublicadas, camposVisiveis);
    }

    /// <summary>
    /// Fórmula M12 (§4.2) — "o campo X estava ativo na versão Y do layout": primeira
    /// implementação real desta consulta (antes só validada em <c>CampoLayout.Descontinuar</c>,
    /// nunca consultada). Os dois limites abaixo são exatamente as flags
    /// <c>NovoNestaVersao</c>/<c>DescontinuadoNestaVersao</c> do DTO público.
    /// </summary>
    private static bool CampoAtivoNaVersao(int origemOrdem, int? descontinuadoOrdem, int ordemAlvo) =>
        origemOrdem <= ordemAlvo && (descontinuadoOrdem is null || descontinuadoOrdem >= ordemAlvo);

    /// <summary>
    /// Mais estrito que <see cref="CampoAtivoNaVersao"/>: o campo descontinuado NA versão aparece no
    /// dicionário dela com o selo, mas já não é campo do documento daquela versão.
    /// </summary>
    private static bool CampoEmVigorNaVersao(int origemOrdem, int? descontinuadoOrdem, int ordemAlvo) =>
        origemOrdem <= ordemAlvo && (descontinuadoOrdem is null || descontinuadoOrdem > ordemAlvo);

    private static NoEstruturaDto MapearNo(NoEstruturaLayoutDto no, IReadOnlyDictionary<string, string> slugPorCaminho) => new()
    {
        Nome = no.Nome,
        Caminho = no.Caminho,
        EhGrupo = no.EhGrupo,
        Repete = no.Repete,
        Escolha = no.Escolha,
        ValorModelo = no.ValorModelo,
        CampoSlug = no.CaminhoCampo is not null && slugPorCaminho.TryGetValue(no.CaminhoCampo, out var slug) ? slug : null,
        Formato = no.Formato,
        Filhos = no.Filhos.Select(filho => MapearNo(filho, slugPorCaminho)).ToArray(),
    };

    private static LayoutVersaoResumoDto MontarResumo(VersaoLayout versao, IReadOnlyCollection<CampoLayoutVisivelBrutoDto> camposVisiveis) => new()
    {
        Slug = versao.Slug,
        Rotulo = versao.Rotulo,
        Estado = versao.Estado.ToString(),
        Ordem = versao.Ordem,
        TotalCampos = camposVisiveis.Count(c => CampoAtivoNaVersao(c.OrigemOrdem, c.DescontinuadoOrdem, versao.Ordem)),
    };

    /// <param name="existeAnterior">
    /// "Novo" compara com a versão publicada anterior; sem ela, na primeira versão do acervo, todo campo
    /// seria novo e o selo deixaria de dizer qualquer coisa.
    /// </param>
    private static CampoLayoutPublicoDto MapearCampo(CampoLayoutVisivelBrutoDto campo, VersaoLayout versaoAlvo, bool existeAnterior) => new()
    {
        Slug = campo.Slug,
        Nome = campo.Nome,
        CaminhoXml = campo.CaminhoXml,
        Significado = campo.Significado,
        Ordem = campo.Ordem,
        NovoNestaVersao = existeAnterior && campo.VersaoLayoutOrigemId == versaoAlvo.Id,
        DescontinuadoNestaVersao = campo.VersaoLayoutDescontinuadoId == versaoAlvo.Id,
        UrlCanonica = $"/layout/campos/{campo.Slug}",
    };

    /// <summary>
    /// Slug de exibição do grupo — duplicado de <c>VersaoProvedorRepositorioPublico.GerarGrupoSlug</c>
    /// de propósito (analise-backend.md §4.3): evita que esta unidade precise tocar um arquivo de
    /// outra unidade só por causa de uma função pura de 8 linhas. Tolera texto que colapsaria para
    /// vazio (ex.: só pontuação), tratando como "sem grupo".
    /// </summary>
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
