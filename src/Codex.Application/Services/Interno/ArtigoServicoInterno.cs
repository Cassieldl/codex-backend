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
/// Área interna de <see cref="Artigo"/> (I23-I29, I36, analise-backend.md §5). Mesmo padrão de
/// <see cref="ErroServicoInterno"/>: <see cref="Artigo"/> não persiste as duas coleções de
/// referência cruzada via EF — este service sempre lê/grava via os métodos dedicados do
/// repositório.
/// </summary>
public sealed class ArtigoServicoInterno : IArtigoServicoInterno
{
    private readonly IArtigoRepositorioInterno _repositorio;
    private readonly IIndiceBuscaWriter _indiceBuscaWriter;
    private readonly IRedirecionamentoRepositorio _redirecionamentoRepositorio;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;

    public ArtigoServicoInterno(
        IArtigoRepositorioInterno repositorio,
        IIndiceBuscaWriter indiceBuscaWriter,
        IRedirecionamentoRepositorio redirecionamentoRepositorio,
        IUnidadeDeTrabalho unidadeDeTrabalho)
    {
        _repositorio = repositorio;
        _indiceBuscaWriter = indiceBuscaWriter;
        _redirecionamentoRepositorio = redirecionamentoRepositorio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public Task<PaginaResultado<ArtigoAdminListaItemDto>> ListarAsync(FiltroArtigoInterno filtro, CancellationToken ct) =>
        _repositorio.ListarAsync(NormalizarPaginacao(filtro), ct);

    public async Task<ArtigoAdminDto> ObterDetalheAsync(int id, CancellationToken ct)
    {
        var artigo = await _repositorio.ObterPorIdAsync(id, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("Artigo", id);

        return await MapearAsync(artigo, ct);
    }

    public async Task<ArtigoAdminDto> CriarAsync(CriarArtigoDto dto, CancellationToken ct)
    {
        var artigo = Artigo.Criar(dto.Tipo, dto.Titulo, dto.Corpo);

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.AdicionarAsync(artigo, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno); // gera o Id real, necessário para as tabelas de junção abaixo.

            await _repositorio.SubstituirCamposLayoutRelacionadosAsync(artigo.Id, dto.CampoLayoutIds, ctInterno);
            await _repositorio.SubstituirErrosRelacionadosAsync(artigo.Id, dto.ErroIds, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoArtigo(artigo);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.artigo, artigo.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return Mapear(artigo, dto.CampoLayoutIds, dto.ErroIds);
    }

    public async Task<ArtigoAdminDto> AtualizarAsync(int id, AtualizarArtigoDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var artigo = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        artigo.AtualizarConteudo(dto.Titulo, dto.Corpo);
        artigo.AtualizarTipo(dto.Tipo); // B-2 — tipo é editável; reindexação (peso D) roda no mesmo AtualizarPesosAsync abaixo.

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            // As junções mudam sem tocar a linha do artigo: sem isto o xmin não anda e o If-Match
            // seguinte não percebe a edição anterior.
            _unidadeDeTrabalho.ForcarConferenciaDeConcorrencia(artigo);

            await _repositorio.SubstituirCamposLayoutRelacionadosAsync(artigo.Id, dto.CampoLayoutIds, ctInterno);
            await _repositorio.SubstituirErrosRelacionadosAsync(artigo.Id, dto.ErroIds, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoArtigo(artigo);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.artigo, artigo.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return Mapear(artigo, dto.CampoLayoutIds, dto.ErroIds);
    }

    /// <summary>
    /// I44 (P-10) — troca de slug com redirecionamento. Diferença das outras duas entidades: o
    /// artigo RESERVADO não troca de slug (§0.2), porque <c>SlugsReservadosArtigo.EhReservado</c>
    /// decide pela comparação com o valor atual do campo — trocar deixaria a rota fixa
    /// <c>/como-funciona</c> órfã sem lançar nada.
    /// </summary>
    public async Task<ArtigoAdminDto> AtualizarSlugAsync(int id, AlterarSlugDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var artigo = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        if (SlugsReservadosArtigo.EhReservado(artigo.Slug))
            throw new RegraDeNegocioException(
                CodigosErro.SlugReservadoNaoPodeMudar, "O slug do artigo obrigatório não pode ser alterado.");

        var slugNovo = SlugVo.DeTexto(dto.Slug).Valor;
        if (slugNovo == artigo.Slug)
            throw new RegraDeNegocioException(CodigosErro.SlugIgualAoAtual, "O novo slug é igual ao atual.");

        // RotaCanonica é a fonte única da URL pública do artigo — a mesma que o writer de busca
        // usa. Interpolar "/artigos/{slug}" aqui criaria a segunda fórmula.
        var caminhoAntigo = SlugsReservadosArtigo.RotaCanonica(artigo.Slug);
        var caminhoNovo = SlugsReservadosArtigo.RotaCanonica(slugNovo);

        artigo.AlterarSlug(SlugVo.DeValorPersistido(slugNovo));

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);

            await _redirecionamentoRepositorio.DesativarPorOrigemAsync(caminhoNovo, TipoItemIndexado.artigo, ctInterno);
            await _redirecionamentoRepositorio.RepontarDestinosAsync(caminhoAntigo, caminhoNovo, TipoItemIndexado.artigo, ctInterno);
            await _redirecionamentoRepositorio.CriarAsync(
                Redirecionamento.Criar(caminhoAntigo, caminhoNovo, TipoItemIndexado.artigo), ctInterno);

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoArtigo(artigo);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.artigo, artigo.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return await MapearAsync(artigo, ct);
    }

    public async Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var artigo = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        if (SlugsReservadosArtigo.EhReservado(artigo.Slug))
            throw new RegraDeNegocioException(CodigosErro.ArtigoReservado, "O artigo obrigatório não pode ser removido.");

        artigo.Desativar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.DesativarAsync(TipoItemIndexado.artigo, artigo.Id, ctInterno);
        }, ct);
    }

    public async Task<ArtigoAdminDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct)
    {
        var artigo = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        if (!publicado && SlugsReservadosArtigo.EhReservado(artigo.Slug))
            throw new RegraDeNegocioException(CodigosErro.ArtigoReservado, "O artigo obrigatório não pode ser despublicado.");

        if (publicado) artigo.Publicar(); else artigo.Despublicar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.Artigo, artigo.Id, ctInterno);
        }, ct);

        return await MapearAsync(artigo, ct);
    }

    public async Task<ArtigoAdminDto> AtualizarVerificacaoAsync(int id, AtualizarVerificacaoArtigoDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var artigo = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        // §0.4/§5.2 — conversão na borda: o domínio guarda DateTimeOffset, o contrato é DateOnly.
        artigo.RegistrarVerificacao(new DateTimeOffset(dto.VerificadoEm.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        await _unidadeDeTrabalho.ExecutarAsync(
            ctInterno => _repositorio.SalvarAlteracoesAsync(ctInterno), ct);

        return await MapearAsync(artigo, ct);
    }

    public async Task<ArtigoExisteDto> ExisteAsync(string slug, CancellationToken ct)
    {
        var artigo = await _repositorio.ObterPorSlugAsync(slug, ct);

        return new ArtigoExisteDto
        {
            Slug = slug,
            Existe = artigo is not null,
            ArtigoId = artigo?.Id,
        };
    }

    private async Task<Artigo> CarregarRastreadaOuFalharAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var artigo = await _repositorio.ObterPorIdAsync(id, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("Artigo", id);

        if (artigo.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException("O artigo foi alterado por outra requisição. Recarregue e tente novamente.");

        return artigo;
    }

    private static FiltroArtigoInterno NormalizarPaginacao(FiltroArtigoInterno filtro)
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

    private async Task<ArtigoAdminDto> MapearAsync(Artigo artigo, CancellationToken ct)
    {
        var campoLayoutIds = await _repositorio.ListarCamposLayoutRelacionadosIdsAsync(artigo.Id, ct);
        var erroIds = await _repositorio.ListarErrosRelacionadosIdsAsync(artigo.Id, ct);

        return Mapear(artigo, campoLayoutIds, erroIds);
    }

    private static ArtigoAdminDto Mapear(Artigo artigo, IReadOnlyCollection<int> campoLayoutIds, IReadOnlyCollection<int> erroIds) => new()
    {
        Id = artigo.Id,
        Slug = artigo.Slug,
        Tipo = artigo.Tipo.ToString(),
        Titulo = artigo.Titulo,
        Corpo = artigo.Corpo,
        Publicado = artigo.Publicado,
        Reservado = SlugsReservadosArtigo.EhReservado(artigo.Slug),
        VerificadoEm = artigo.VerificadoEm.HasValue ? DateOnly.FromDateTime(artigo.VerificadoEm.Value.UtcDateTime) : null,
        CampoLayoutIds = campoLayoutIds,
        ErroIds = erroIds,
        VersaoConcorrencia = artigo.Versao,
    };
}
