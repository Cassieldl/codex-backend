using System.Text.Json;
using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services;

/// <summary>
/// Leitura pública de provedor/versão (P2/P3/P4, analise-backend.md §4.1/§4.3). Cascata de
/// visibilidade em consultas ao vivo (§3.3): a versão só é considerada pública se ELA e o
/// PROVEDOR estiverem publicados — join explícito já resolvido pelo repositório, checagem
/// sequencial (provedor primeiro) para escolher o código de erro correto de M7/§4.4.
/// </summary>
public sealed class ProvedorServicoPublico : IProvedorServicoPublico
{
    private const int TamanhoPaginaPadrao = 25;
    private const int TamanhoPaginaMaximo = 100;

    /// <summary>M22 — cap de 2000 campos por versão, defesa também na leitura (a escrita, I12, é de BACK-4).</summary>
    private const int CapCamposPorVersao = 2000;

    private const string EscopoProvedor = "provedor";
    private const string EscopoVersao = "versao";

    private readonly IProvedorRepositorioPublico _provedorRepositorio;
    private readonly IVersaoProvedorRepositorioPublico _versaoRepositorio;

    public ProvedorServicoPublico(
        IProvedorRepositorioPublico provedorRepositorio,
        IVersaoProvedorRepositorioPublico versaoRepositorio)
    {
        _provedorRepositorio = provedorRepositorio;
        _versaoRepositorio = versaoRepositorio;
    }

    public Task<PaginaResultado<ProvedorListaItemDto>> ListarAsync(FiltroProvedorPublico filtro, CancellationToken ct)
    {
        var filtroNormalizado = NormalizarPaginacao(filtro);
        return _provedorRepositorio.ListarAsync(filtroNormalizado, ct);
    }

    public async Task<ProvedorDetalheDto> ObterDetalheAsync(string provedorSlug, CancellationToken ct)
    {
        var provedor = await _provedorRepositorio.ObterPorSlugAsync(provedorSlug, ct)
            ?? throw new EntidadeNaoEncontradaException("Provedor", provedorSlug);

        if (!provedor.Publicado)
            throw new ConteudoDespublicadoException(nameof(TipoItemIndexado.provedor), provedorSlug);

        var avisos = await _provedorRepositorio.ListarAvisosPublicadosDoProvedorAsync(provedor.Id, ct);
        var versoes = await _provedorRepositorio.ListarVersoesPublicadasAsync(provedor.Id, ct);

        return new ProvedorDetalheDto
        {
            Slug = provedor.Slug,
            Nome = provedor.Nome,
            Categoria = provedor.Categoria.ToString(),
            IdentificadorInvoicy = provedor.IdentificadorInvoicy,
            Avisos = OrdenarAvisos(avisos).Select(aviso => MapearAviso(aviso, EscopoProvedor, [])).ToArray(),
            Versoes = versoes
                .OrderByDescending(v => v.CriadoEm)
                .Select(v => new VersaoResumoPublicoDto
                {
                    Slug = v.Slug,
                    Rotulo = v.Rotulo,
                    VerificadoEm = v.VerificadoEm,
                    // Path interno relativo (nunca absoluto/com esquema/host — restrição de
                    // segurança: o frontend injeta este valor direto em <Link to>/<a href> sem
                    // validação própria, o backend é a última linha de defesa). É a rota do
                    // FRONTEND (arquitetura.md §3.2: provedores/$provedorSlug/$versaoSlug), não
                    // a rota da API pública. Garantia contra "//"/"/\\" por slug hostil: Slug
                    // (Codex.Domain.ValueObjects.Slug.DeTexto) só produz [a-z0-9-], nunca vazio,
                    // nunca contém '/' ou '\\' — os dois segmentos abaixo nunca colapsam.
                    Url = $"/provedores/{provedor.Slug}/{v.Slug}",
                })
                .ToArray(),
        };
    }

    public async Task<VersaoProvedorPaginaDto> ObterVersaoPaginaAsync(string provedorSlug, string versaoSlug, CancellationToken ct)
    {
        var encontrado = await _versaoRepositorio.ObterPorSlugsAsync(provedorSlug, versaoSlug, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoProvedor", $"{provedorSlug}/{versaoSlug}");

        var provedor = encontrado.Provedor;
        var versao = encontrado.Versao;

        // Ordem importa para o código de erro (M7/§4.4): provedor despublicado prevalece sobre
        // versão despublicada, porque é a causa raiz da cascata.
        if (!provedor.Publicado)
            throw new ConteudoDespublicadoException(nameof(TipoItemIndexado.provedor), provedorSlug);
        if (!versao.Publicado)
            throw new ConteudoDespublicadoException(nameof(TipoItemIndexado.versaoprovedor), versaoSlug);

        var totalCampos = await _versaoRepositorio.ContarCamposAsync(versao.Id, ct);
        if (totalCampos > CapCamposPorVersao)
            throw new LimiteExcedidoException(
                $"Versão possui {totalCampos} campos, acima do limite de {CapCamposPorVersao} suportado para exibição (M22).");

        var campos = await _versaoRepositorio.ListarCamposAsync(versao.Id, ct);
        var avisos = await _versaoRepositorio.ListarAvisosPublicadosDaVersaoAsync(versao.Id, ct);
        var camposAfetadosPorAviso = await _versaoRepositorio.ListarCamposAfetadosPorAvisoAsync(
            versao.Id, avisos.Select(a => a.Id).ToArray(), ct);
        var erros = await _versaoRepositorio.ListarErrosAplicaveisPublicadosAsync(versao.Id, ct);
        var outrasVersoes = await _versaoRepositorio.ListarOutrasVersoesPublicadasAsync(provedor.Id, versao.Id, ct);
        var exemplos = await _versaoRepositorio.ListarExemplosAsync(versao.Id, ct);

        var camposPorGrupo = campos
            .GroupBy(c => (c.Grupo, c.GrupoSlug))
            .OrderBy(g => g.Min(c => c.Ordem))
            .Select(g => new GrupoCamposPublicoDto
            {
                Grupo = g.Key.Grupo,
                GrupoSlug = g.Key.GrupoSlug,
                Campos = g.OrderBy(c => c.Ordem).Select(MapearCampo).ToArray(),
            })
            .ToArray();

        return new VersaoProvedorPaginaDto
        {
            Provedor = new ProvedorResumoPaginaDto
            {
                Slug = provedor.Slug,
                Nome = provedor.Nome,
                Categoria = provedor.Categoria.ToString(),
                IdentificadorInvoicy = provedor.IdentificadorInvoicy,
            },
            Versao = new VersaoResumoPaginaDto
            {
                Slug = versao.Slug,
                Rotulo = versao.Rotulo,
                VerificadoEm = versao.VerificadoEm,
                Introducao = versao.Introducao,
                VersaoLayoutRotulo = encontrado.VersaoLayoutRotulo,
                VersaoConcorrencia = versao.Versao,
            },
            CamposPorGrupo = camposPorGrupo,
            TotalCampos = totalCampos,
            Avisos = OrdenarAvisos(avisos)
                .Select(aviso => MapearAviso(aviso, EscopoVersao, camposAfetadosPorAviso.GetValueOrDefault(aviso.Id, [])))
                .ToArray(),
            ErrosResumo = new ErrosResumoPublicoDto { Total = erros.Count, Itens = erros },
            OutrasVersoes = outrasVersoes
                .OrderByDescending(v => v.CriadoEm)
                .Select(v => new VersaoResumoMinimaDto { Slug = v.Slug, Rotulo = v.Rotulo })
                .ToArray(),
            Exemplos = exemplos,
        };
    }

    public async Task<EnvioModeloDto> ObterEnvioModeloAsync(
        string provedorSlug, string versaoSlug, AlcanceEnvioModelo alcance, CancellationToken ct)
    {
        // Oculto responde como inexistente: o envio modelo não existe para o leitor.
        var encontrado = await _versaoRepositorio.ObterPorSlugsAsync(provedorSlug, versaoSlug, ct);
        if (encontrado is null || !encontrado.Provedor.Publicado || !encontrado.Versao.Publicado)
            throw new EntidadeNaoEncontradaException("VersaoProvedor", $"{provedorSlug}/{versaoSlug}");

        var conteudo = await _versaoRepositorio.ObterEstruturaDoLayoutPublicadoAsync(encontrado.Versao.VersaoLayoutId, ct);
        var estrutura = (conteudo is null
            ? null
            : JsonSerializer.Deserialize<AtualizarEstruturaVersaoLayoutDto>(conteudo, JsonSerializerOptions.Web))
            ?? throw new EntidadeNaoEncontradaException(
                $"O layout {encontrado.VersaoLayoutRotulo} da versão '{provedorSlug}/{versaoSlug}' não tem estrutura.")
            {
                Codigo = CodigosErro.EstruturaIndisponivel,
            };

        var campos = await _versaoRepositorio.ListarCamposDoEnvioModeloAsync(encontrado.Versao.Id, ct);
        var camposDoAlcance = campos
            .Where(campo => alcance == AlcanceEnvioModelo.Maximo || campo.EstadoPreenchimento == EstadoPreenchimento.Obrigatorio)
            .ToArray();
        var slugPorCaminho = camposDoAlcance
            .GroupBy(campo => campo.CaminhoXml, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.First().CampoLayoutSlug, StringComparer.Ordinal);

        var xml = RecorteEnvioModelo.Recortar(estrutura.Xml, slugPorCaminho, alcance);
        var json = RecorteEnvioModelo.Recortar(estrutura.Json, slugPorCaminho, alcance);

        return new EnvioModeloDto
        {
            Alcance = AlcancesEnvioModelo.ParaTexto(alcance),
            VersaoLayoutRotulo = encontrado.VersaoLayoutRotulo,
            Xml = xml.Raiz,
            Json = json.Raiz,
            CamposSemPosicao = camposDoAlcance
                .Where(campo => !xml.CaminhosComNo.Contains(campo.CaminhoXml) && !json.CaminhosComNo.Contains(campo.CaminhoXml))
                .Select(campo => campo.CampoLayoutSlug)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
        };
    }

    public Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct) =>
        _provedorRepositorio.ObterRedirecionamentoAsync(slugAntigo, ct);

    private static FiltroProvedorPublico NormalizarPaginacao(FiltroProvedorPublico filtro)
    {
        var (paginaNormalizada, tamanhoNormalizado) = Paginacao.Normalizar(
            filtro.Pagina, filtro.TamanhoPagina, TamanhoPaginaPadrao, TamanhoPaginaMaximo);

        return filtro with
        {
            Pagina = paginaNormalizada,
            TamanhoPagina = tamanhoNormalizado,
        };
    }

    /// <summary>Severidade Critico &gt; Alerta &gt; Informativo, depois VigenciaInicio DESC (§4.1).</summary>
    private static IEnumerable<Aviso> OrdenarAvisos(IEnumerable<Aviso> avisos) => avisos
        .OrderBy(a => a.Severidade switch
        {
            Severidade.Critico => 0,
            Severidade.Alerta => 1,
            _ => 2,
        })
        .ThenByDescending(a => a.VigenciaInicio);

    private static AvisoPublicoDto MapearAviso(
        Aviso aviso, string escopo, IReadOnlyCollection<CampoAfetadoPublicoDto> camposAfetados) => new()
    {
        Id = aviso.Id,
        Escopo = escopo,
        CamposAfetados = camposAfetados,
        Severidade = aviso.Severidade.ToString(),
        Titulo = aviso.Titulo,
        Corpo = aviso.Corpo,
        VigenciaInicio = aviso.VigenciaInicio,
    };

    private static CampoVersaoPublicoDto MapearCampo(CampoVersaoProjecaoDto campo) => new()
    {
        CampoLayoutSlug = campo.CampoLayoutSlug,
        Nome = campo.Nome,
        CaminhoXml = campo.CaminhoXml,
        Significado = campo.Significado,
        Tipo = campo.Tipo,
        Tamanho = campo.Tamanho,
        Mascara = campo.Mascara,
        Dominio = campo.Dominio,
        EstadoPreenchimento = campo.EstadoPreenchimento,
        Condicao = campo.Condicao,
        Exemplo = campo.Exemplo,
        NotaPreenchimento = campo.NotaPreenchimento,
        DestinoXml = campo.DestinoXml,
        NotaTransformacao = campo.NotaTransformacao,
        Ordem = campo.Ordem,
        ErrosConhecidos = campo.ErrosConhecidos,
    };
}
