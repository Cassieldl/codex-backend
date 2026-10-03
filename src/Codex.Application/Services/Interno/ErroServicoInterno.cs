using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Application.Services.Interno;

/// <summary>
/// Área interna de <see cref="Erro"/> (I18-I20c, U9, analise-backend.md §4.2/§3.4).
/// <see cref="Erro.VersoesAplicaveisIds"/>/<see cref="Erro.CamposRelacionadosIds"/> não são
/// persistidos pelo EF (ver <see cref="IErroRepositorioInterno"/>) — este service sempre lê/grava
/// essas duas coleções via os métodos dedicados do repositório, nunca confiando na propriedade
/// da entidade para refletir o estado do banco.
/// </summary>
public sealed class ErroServicoInterno : IErroServicoInterno
{
    private readonly IErroRepositorioInterno _repositorio;
    private readonly IIndiceBuscaWriter _indiceBuscaWriter;
    private readonly IRedirecionamentoRepositorio _redirecionamentoRepositorio;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;

    public ErroServicoInterno(
        IErroRepositorioInterno repositorio,
        IIndiceBuscaWriter indiceBuscaWriter,
        IRedirecionamentoRepositorio redirecionamentoRepositorio,
        IUnidadeDeTrabalho unidadeDeTrabalho)
    {
        _repositorio = repositorio;
        _indiceBuscaWriter = indiceBuscaWriter;
        _redirecionamentoRepositorio = redirecionamentoRepositorio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public Task<PaginaResultado<ErroAdminListaItemDto>> ListarAsync(FiltroErroInterno filtro, CancellationToken ct) =>
        _repositorio.ListarAsync(NormalizarPaginacao(filtro), ct);

    public async Task<ErroAdminDetalheDto> ObterDetalheAsync(int id, CancellationToken ct)
    {
        var erro = await _repositorio.ObterPorIdAsync(id, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("Erro", id);

        return await MapearAsync(erro, ct);
    }

    public async Task<ErroAdminDetalheDto> CriarAsync(CriarErroDto dto, CancellationToken ct)
    {
        var erro = Erro.Criar(dto.Codigo, dto.Origem, dto.Titulo, dto.CausaProvavel, dto.SolucaoSugerida);
        erro.SubstituirVariantes(dto.Variantes);

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.AdicionarAsync(erro, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno); // gera o Id real, necessário para as tabelas de junção abaixo.

            await _repositorio.SubstituirVersoesAplicaveisAsync(erro.Id, dto.VersoesProvedorIds, ctInterno);
            await _repositorio.SubstituirCamposRelacionadosAsync(erro.Id, dto.CamposLayoutIds, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoErro(erro);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.erro, erro.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return Mapear(erro, dto.VersoesProvedorIds, dto.CamposLayoutIds);
    }

    public async Task<ErroAdminDetalheDto> AtualizarAsync(int id, AtualizarErroDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var erro = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        erro.AtualizarConteudo(dto.Titulo, dto.CausaProvavel, dto.SolucaoSugerida);

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            // QA-01 (demanda 008): remove as VarianteMensagem antigas ANTES de
            // SubstituirVariantes mutar a coleção de navegação — senão o EF tenta "severar" filhos
            // de FK obrigatória com OnDelete(Restrict) e o SaveChanges lança.
            await _repositorio.RemoverVariantesAsync(erro.Id, ctInterno);
            erro.SubstituirVariantes(dto.Variantes);

            await _repositorio.SubstituirVersoesAplicaveisAsync(erro.Id, dto.VersoesProvedorIds, ctInterno);
            await _repositorio.SubstituirCamposRelacionadosAsync(erro.Id, dto.CamposLayoutIds, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoErro(erro);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.erro, erro.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return Mapear(erro, dto.VersoesProvedorIds, dto.CamposLayoutIds);
    }

    /// <summary>I43 (P-10) — troca de slug com redirecionamento; mesmo template de <c>ProvedorServicoInterno.AtualizarSlugAsync</c>.</summary>
    public async Task<ErroAdminDetalheDto> AtualizarSlugAsync(int id, AlterarSlugDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var erro = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        var slugNovo = SlugVo.DeTexto(dto.Slug).Valor;
        if (slugNovo == erro.Slug)
            throw new RegraDeNegocioException(CodigosErro.SlugIgualAoAtual, "O novo slug é igual ao atual.");

        var caminhoAntigo = $"/erros/{erro.Slug}";
        var caminhoNovo = $"/erros/{slugNovo}";

        erro.AlterarSlug(SlugVo.DeValorPersistido(slugNovo));

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            await _redirecionamentoRepositorio.DesativarPorOrigemAsync(caminhoNovo, TipoItemIndexado.erro, ctInterno);
            await _redirecionamentoRepositorio.RepontarDestinosAsync(caminhoAntigo, caminhoNovo, TipoItemIndexado.erro, ctInterno);
            await _redirecionamentoRepositorio.CriarAsync(
                Redirecionamento.Criar(caminhoAntigo, caminhoNovo, TipoItemIndexado.erro), ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoErro(erro);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.erro, erro.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return await MapearAsync(erro, ct);
    }

    public async Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var erro = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);
        erro.Desativar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.DesativarAsync(TipoItemIndexado.erro, erro.Id, ctInterno);
        }, ct);
    }

    public async Task<ErroAdminDetalheDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct)
    {
        var erro = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        if (publicado) erro.Publicar(); else erro.Despublicar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.Erro, erro.Id, ctInterno);
        }, ct);

        return await MapearAsync(erro, ct);
    }

    private async Task<Erro> CarregarRastreadaOuFalharAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var erro = await _repositorio.ObterPorIdAsync(id, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("Erro", id);

        if (erro.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException("O erro foi alterado por outra requisição. Recarregue e tente novamente.");

        return erro;
    }

    private static FiltroErroInterno NormalizarPaginacao(FiltroErroInterno filtro)
    {
        const int tamanhoPaginaPadrao = 25;
        const int tamanhoPaginaMaximo = 200;

        var (paginaNormalizada, tamanhoNormalizado) = Paginacao.Normalizar(
            filtro.Pagina, filtro.TamanhoPagina, tamanhoPaginaPadrao, tamanhoPaginaMaximo);

        return filtro with
        {
            Pagina = paginaNormalizada,
            TamanhoPagina = tamanhoNormalizado,
        };
    }

    private async Task<ErroAdminDetalheDto> MapearAsync(Erro erro, CancellationToken ct)
    {
        var versoesAplicaveisIds = await _repositorio.ListarVersoesAplicaveisIdsAsync(erro.Id, ct);
        var camposRelacionadosIds = await _repositorio.ListarCamposRelacionadosIdsAsync(erro.Id, ct);

        return Mapear(erro, versoesAplicaveisIds, camposRelacionadosIds);
    }

    private static ErroAdminDetalheDto Mapear(
        Erro erro, IReadOnlyCollection<int> versoesAplicaveisIds, IReadOnlyCollection<int> camposRelacionadosIds) => new()
    {
        Id = erro.Id,
        Slug = erro.Slug,
        Codigo = erro.Codigo,
        Origem = erro.Origem.ToString(),
        Titulo = erro.Titulo,
        CausaProvavel = erro.CausaProvavel,
        SolucaoSugerida = erro.SolucaoSugerida,
        Publicado = erro.Publicado,
        Variantes = erro.Variantes.Select(v => v.Texto).ToArray(),
        VersoesProvedorIds = versoesAplicaveisIds,
        CamposLayoutIds = camposRelacionadosIds,
        VersaoConcorrencia = erro.Versao,
    };
}
