using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Application.Mappings;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Application.Services.Interno;

/// <summary>Área interna de <see cref="Provedor"/> (I1-I5, I3b, I37, analise-backend.md §4.2/§3.4).</summary>
public sealed class ProvedorServicoInterno : IProvedorServicoInterno
{
    private readonly IProvedorRepositorioInterno _repositorio;
    private readonly IAvisoRepositorioInterno _avisoRepositorio;
    private readonly IIndiceBuscaWriter _indiceBuscaWriter;
    private readonly IRedirecionamentoRepositorio _redirecionamentoRepositorio;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;

    public ProvedorServicoInterno(
        IProvedorRepositorioInterno repositorio,
        IAvisoRepositorioInterno avisoRepositorio,
        IIndiceBuscaWriter indiceBuscaWriter,
        IRedirecionamentoRepositorio redirecionamentoRepositorio,
        IUnidadeDeTrabalho unidadeDeTrabalho)
    {
        _repositorio = repositorio;
        _avisoRepositorio = avisoRepositorio;
        _indiceBuscaWriter = indiceBuscaWriter;
        _redirecionamentoRepositorio = redirecionamentoRepositorio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public Task<PaginaResultado<ProvedorAdminListaItemDto>> ListarAsync(FiltroProvedorInterno filtro, CancellationToken ct) =>
        _repositorio.ListarAsync(NormalizarPaginacao(filtro), ct);

    public async Task<ProvedorAdminDetalheDto> ObterDetalheAsync(int id, CancellationToken ct)
    {
        var provedor = await _repositorio.ObterPorIdAsync(id, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("Provedor", id);

        return await MapearAsync(provedor, ct);
    }

    public async Task<ProvedorAdminDetalheDto> CriarAsync(CriarProvedorDto dto, CancellationToken ct)
    {
        var provedor = Provedor.Criar(dto.Nome, dto.IdentificadorInvoicy, dto.Categoria);
        provedor.SubstituirAliases(dto.Aliases);

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.AdicionarAsync(provedor, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoProvedor(provedor);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.provedor, provedor.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return await MapearAsync(provedor, ct);
    }

    public async Task<ProvedorAdminDetalheDto> AtualizarAsync(int id, AtualizarProvedorDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var provedor = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        provedor.Renomear(dto.Nome);
        provedor.SubstituirAliases(dto.Aliases);

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoProvedor(provedor);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.provedor, provedor.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return await MapearAsync(provedor, ct);
    }

    /// <summary>
    /// I37 (P-10) — troca de slug com redirecionamento, tudo na mesma transação: a linha do
    /// provedor, a desativação do redirect que partia do slug que volta a ficar vivo (B-4), o
    /// colapso de cadeia (E2), o redirect novo e a reindexação da própria linha de busca.
    ///
    /// <para>
    /// As linhas FILHAS (versões, avisos) não precisam de reindexação: a URL delas é derivada na
    /// consulta a partir do slug corrente do provedor (decisão B), não lida da coluna
    /// <c>itensbusca.url</c>.
    /// </para>
    /// </summary>
    public async Task<ProvedorAdminDetalheDto> AtualizarSlugAsync(int id, AlterarSlugDto dto, uint versaoEsperada, CancellationToken ct)
    {
        // Com aliases: a reindexação abaixo não passa por SubstituirAliases, então a coleção
        // precisa vir do banco — senão o peso B sai só com o nome e o índice perde os apelidos.
        var provedor = await _repositorio.ObterPorIdComAliasesAsync(id, ct)
            ?? throw new EntidadeNaoEncontradaException("Provedor", id);
        GarantirVersaoEsperada(provedor, versaoEsperada);

        // Normaliza no backend, nunca confiando no que o formulário mandou (rules/security.md) —
        // é a MESMA derivação que Provedor.Criar aplica ao nome.
        var slugNovo = SlugVo.DeTexto(dto.Slug).Valor;
        if (slugNovo == provedor.Slug)
            throw new RegraDeNegocioException(CodigosErro.SlugIgualAoAtual, "O novo slug é igual ao atual.");

        var caminhoAntigo = $"/provedores/{provedor.Slug}";
        var caminhoNovo = $"/provedores/{slugNovo}";

        provedor.AlterarSlug(SlugVo.DeValorPersistido(slugNovo));

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            await _redirecionamentoRepositorio.DesativarPorOrigemAsync(caminhoNovo, TipoItemIndexado.provedor, ctInterno);
            await _redirecionamentoRepositorio.RepontarDestinosAsync(caminhoAntigo, caminhoNovo, TipoItemIndexado.provedor, ctInterno);
            await _redirecionamentoRepositorio.CriarAsync(
                Redirecionamento.Criar(caminhoAntigo, caminhoNovo, TipoItemIndexado.provedor), ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoProvedor(provedor);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.provedor, provedor.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return await MapearAsync(provedor, ct);
    }

    public async Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var provedor = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);
        provedor.Desativar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.DesativarAsync(TipoItemIndexado.provedor, provedor.Id, ctInterno);
        }, ct);
    }

    /// <summary>Template de publicação de §3.4 — mesma transação para a mudança de estado e a cascata de <c>codex.itensbusca</c>.</summary>
    public async Task<ProvedorAdminDetalheDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct)
    {
        var provedor = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        if (publicado) provedor.Publicar(); else provedor.Despublicar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.Provedor, provedor.Id, ctInterno);
        }, ct);

        return await MapearAsync(provedor, ct);
    }

    private async Task<Provedor> CarregarRastreadaOuFalharAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var provedor = await _repositorio.ObterPorIdAsync(id, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("Provedor", id);

        GarantirVersaoEsperada(provedor, versaoEsperada);

        return provedor;
    }

    private static void GarantirVersaoEsperada(Provedor provedor, uint versaoEsperada)
    {
        if (provedor.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException("O provedor foi alterado por outra requisição. Recarregue e tente novamente.");
    }

    private static FiltroProvedorInterno NormalizarPaginacao(FiltroProvedorInterno filtro)
    {
        const int tamanhoPaginaPadrao = 25;
        const int tamanhoPaginaMaximo = 200; // cap — rules/security.md AP-SECURITY-005.

        var (paginaNormalizada, tamanhoNormalizado) = Paginacao.Normalizar(
            filtro.Pagina, filtro.TamanhoPagina, tamanhoPaginaPadrao, tamanhoPaginaMaximo);

        return filtro with
        {
            Pagina = paginaNormalizada,
            TamanhoPagina = tamanhoNormalizado,
        };
    }

    /// <summary>I2: inclui TODOS os avisos de provedor (publicados e rascunho, diferente de P3/U8).</summary>
    private async Task<ProvedorAdminDetalheDto> MapearAsync(Provedor provedor, CancellationToken ct)
    {
        var avisos = provedor.Id == 0
            ? []
            : await _avisoRepositorio.ListarPorProvedorAsync(provedor.Id, ct);

        return new ProvedorAdminDetalheDto
        {
            Id = provedor.Id,
            Slug = provedor.Slug,
            Nome = provedor.Nome,
            Categoria = provedor.Categoria.ToString(),
            IdentificadorInvoicy = provedor.IdentificadorInvoicy,
            Publicado = provedor.Publicado,
            Aliases = provedor.Aliases.Select(a => a.Alias).ToArray(),
            Avisos = avisos.Select(AvisoAdminMapper.Mapear).ToArray(),
            VersaoConcorrencia = provedor.Versao,
        };
    }
}
