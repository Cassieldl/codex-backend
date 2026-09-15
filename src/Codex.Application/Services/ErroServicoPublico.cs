using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services;

/// <summary>
/// Leitura pública de erro (P5/P6, analise-backend.md §4.1). U9 — erro sem versão aplicável é
/// permitido: <see cref="ErroDetalheDto.VersoesAplicaveis"/> pode vir vazio, nunca é tratado como
/// exceção aqui.
/// </summary>
public sealed class ErroServicoPublico : IErroServicoPublico
{
    private const int TamanhoPaginaPadrao = 25;
    private const int TamanhoPaginaMaximo = 100;

    private readonly IErroRepositorioPublico _repositorio;
    private readonly IArtigoRepositorioPublico _artigoRepositorio;

    /// <summary>
    /// <paramref name="artigoRepositorio"/> — bloco aditivo da 002 (P6, §4.4): artigos relacionados
    /// só publicados (B-3). Vive em <c>IArtigoRepositorioPublico</c> em vez de
    /// <c>IErroRepositorioPublico</c> para não editar essa interface fora do escopo desta unidade.
    /// </summary>
    public ErroServicoPublico(IErroRepositorioPublico repositorio, IArtigoRepositorioPublico artigoRepositorio)
    {
        _repositorio = repositorio;
        _artigoRepositorio = artigoRepositorio;
    }

    public Task<PaginaResultado<ErroListaItemDto>> ListarAsync(FiltroErroPublico filtro, CancellationToken ct)
    {
        var (paginaNormalizada, tamanhoNormalizado) = Paginacao.Normalizar(
            filtro.Pagina, filtro.TamanhoPagina, TamanhoPaginaPadrao, TamanhoPaginaMaximo);

        var filtroNormalizado = filtro with
        {
            Pagina = paginaNormalizada,
            TamanhoPagina = tamanhoNormalizado,
        };

        return _repositorio.ListarAsync(filtroNormalizado, ct);
    }

    public async Task<ErroDetalheDto> ObterDetalheAsync(string erroSlug, CancellationToken ct)
    {
        var erro = await _repositorio.ObterPorSlugAsync(erroSlug, ct)
            ?? throw new EntidadeNaoEncontradaException("Erro", erroSlug);

        if (!erro.Publicado)
            throw new ConteudoDespublicadoException(nameof(TipoItemIndexado.erro), erroSlug);

        var variantes = await _repositorio.ListarVariantesAsync(erro.Id, ct);

        // U9 — pode vir vazio (erro InvoiCy/Schema sem versão aplicável, ou nenhuma versão
        // aplicável está publicada no momento). É um resultado válido, nunca um erro de negócio.
        var versoesAplicaveis = await _repositorio.ListarVersoesAplicaveisPublicadasAsync(erro.Id, ct);

        var camposRelacionados = await _repositorio.ListarCamposRelacionadosAsync(erro.Id, versoesAplicaveis, ct);

        // Bloco aditivo da 002 (P6, §4.4) — só artigos publicados (B-3).
        var artigosRelacionados = await _artigoRepositorio.ListarRelacionadosAoErroPublicadosAsync(erro.Id, ct);

        return new ErroDetalheDto
        {
            Slug = erro.Slug,
            Codigo = erro.Codigo,
            Origem = erro.Origem.ToString(),
            Titulo = erro.Titulo,
            CausaProvavel = erro.CausaProvavel,
            SolucaoSugerida = erro.SolucaoSugerida,
            Variantes = variantes,
            VersoesAplicaveis = versoesAplicaveis
                .Select(v => new VersaoAplicavelPublicaDto
                {
                    ProvedorSlug = v.ProvedorSlug,
                    ProvedorNome = v.ProvedorNome,
                    VersaoSlug = v.VersaoSlug,
                    VersaoRotulo = v.VersaoRotulo,
                })
                .ToArray(),
            CamposRelacionados = camposRelacionados,
            ArtigosRelacionados = artigosRelacionados,
        };
    }

    public Task<string?> ObterRedirecionamentoAsync(string slugAntigo, CancellationToken ct) =>
        _repositorio.ObterRedirecionamentoAsync(slugAntigo, ct);
}
