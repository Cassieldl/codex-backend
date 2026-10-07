using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Application.Mappings;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services.Interno;

/// <summary>Área interna de <see cref="Aviso"/> (I14a/I14a2/I14b/I14c/I14d, U8, analise-backend.md §4.2/§3.4).</summary>
public sealed class AvisoServicoInterno : IAvisoServicoInterno
{
    private readonly IAvisoRepositorioInterno _repositorio;
    private readonly IProvedorRepositorioInterno _provedorRepositorio;
    private readonly IVersaoProvedorRepositorioInterno _versaoRepositorio;
    private readonly IIndiceBuscaWriter _indiceBuscaWriter;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;

    public AvisoServicoInterno(
        IAvisoRepositorioInterno repositorio,
        IProvedorRepositorioInterno provedorRepositorio,
        IVersaoProvedorRepositorioInterno versaoRepositorio,
        IIndiceBuscaWriter indiceBuscaWriter,
        IUnidadeDeTrabalho unidadeDeTrabalho)
    {
        _repositorio = repositorio;
        _provedorRepositorio = provedorRepositorio;
        _versaoRepositorio = versaoRepositorio;
        _indiceBuscaWriter = indiceBuscaWriter;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<AvisoAdminDto> CriarDeVersaoAsync(int versaoProvedorId, CriarAvisoDeVersaoDto dto, CancellationToken ct)
    {
        _ = await _versaoRepositorio.ObterPorIdAsync(versaoProvedorId, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoProvedor", versaoProvedorId);

        var campoVersaoIds = dto.CampoVersaoIds ?? [];
        await ValidarCamposAfetadosAsync(versaoProvedorId, campoVersaoIds, ct);

        var aviso = Aviso.CriarDeVersao(versaoProvedorId, dto.Severidade, dto.Titulo, dto.Corpo, dto.VigenciaInicio);
        aviso.SubstituirCamposAfetados(campoVersaoIds);

        await SalvarComIndiceAsync(aviso, ct);

        return AvisoAdminMapper.Mapear(aviso);
    }

    public async Task<AvisoAdminDto> CriarDeProvedorAsync(int provedorId, CriarAvisoDeProvedorDto dto, CancellationToken ct)
    {
        _ = await _provedorRepositorio.ObterPorIdAsync(provedorId, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("Provedor", provedorId);

        var aviso = Aviso.CriarDeProvedor(provedorId, dto.Severidade, dto.Titulo, dto.Corpo, dto.VigenciaInicio);

        await SalvarComIndiceAsync(aviso, ct);

        return AvisoAdminMapper.Mapear(aviso);
    }

    public async Task<AvisoAdminDto> AtualizarAsync(int id, AtualizarAvisoDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var aviso = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        aviso.AtualizarConteudo(dto.Severidade, dto.Titulo, dto.Corpo, dto.VigenciaInicio);

        // Ausente = não mexe nos campos; presente = substitui o conjunto inteiro.
        if (dto.CampoVersaoIds is not null)
        {
            if (aviso.VersaoProvedorId is { } versaoProvedorId)
                await ValidarCamposAfetadosAsync(versaoProvedorId, dto.CampoVersaoIds, ct);

            aviso.SubstituirCamposAfetados(dto.CampoVersaoIds); // domínio lança RegraDeNegocioException (422) se o escopo for provedor.
        }

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            if (dto.CampoVersaoIds is not null && aviso.VersaoProvedorId is not null)
            {
                await _repositorio.SubstituirCamposAfetadosAsync(aviso.Id, aviso.CamposAfetadosIds, ctInterno);

                // Os campos mudam sem tocar a linha do aviso: sem isto o xmin não anda e o If-Match
                // seguinte não percebe a edição anterior.
                _unidadeDeTrabalho.ForcarConferenciaDeConcorrencia(aviso);
            }

            await TocarVersaoDoAvisoAsync(aviso, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoAviso(aviso);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.aviso, aviso.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

        return AvisoAdminMapper.Mapear(aviso);
    }

    public async Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var aviso = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);
        aviso.Desativar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await TocarVersaoDoAvisoAsync(aviso, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.DesativarAsync(TipoItemIndexado.aviso, aviso.Id, ctInterno);
        }, ct);
    }

    public async Task<AvisoAdminDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct)
    {
        var aviso = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        if (publicado) aviso.Publicar(); else aviso.Despublicar();

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await TocarVersaoDoAvisoAsync(aviso, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
            await _indiceBuscaWriter.RecalcularVisibilidadeAsync(TipoItemBusca.Aviso, aviso.Id, ctInterno);
        }, ct);

        return AvisoAdminMapper.Mapear(aviso);
    }

    private async Task SalvarComIndiceAsync(Aviso aviso, CancellationToken ct) =>
        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            await _repositorio.AdicionarAsync(aviso, ctInterno);
            await TocarVersaoDoAvisoAsync(aviso, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno); // gera o Id real, necessário para a junção abaixo.

            if (aviso.CamposAfetadosIds.Count > 0)
            {
                await _repositorio.SubstituirCamposAfetadosAsync(aviso.Id, aviso.CamposAfetadosIds, ctInterno);
                await _repositorio.SalvarAlteracoesAsync(ctInterno);
            }

            var (pesoA, pesoB, pesoC, pesoD) = PesosBusca.DoAviso(aviso);
            await _indiceBuscaWriter.AtualizarPesosAsync(TipoItemIndexado.aviso, aviso.Id, pesoA, pesoB, pesoC, pesoD, ctInterno);
        }, ct);

    /// <summary>
    /// Aviso de versão é filho da página pública da versão, cuja <c>ETag</c> vem do <c>xmin</c> da
    /// <see cref="VersaoProvedor"/>. Toda escrita nele força um <c>UPDATE</c> na versão pai, no
    /// mesmo <c>SaveChanges</c> — sem isso, editar ou publicar o aviso deixa a página respondendo
    /// <c>304</c> com o conteúdo velho (mesmo mecanismo dos exemplos).
    /// </summary>
    private async Task TocarVersaoDoAvisoAsync(Aviso aviso, CancellationToken ct)
    {
        if (aviso.VersaoProvedorId is not { } versaoProvedorId)
            return;

        var versao = await _versaoRepositorio.ObterPorIdAsync(versaoProvedorId, rastreada: true, ct);
        if (versao is not null)
            _unidadeDeTrabalho.ForcarConferenciaDeConcorrencia(versao);
    }

    /// <summary>Cada campo afetado tem de ser um campo ativo da MESMA versão do aviso — senão o link do aviso não tem alvo na página.</summary>
    private async Task ValidarCamposAfetadosAsync(int versaoProvedorId, IReadOnlyCollection<int> campoVersaoIds, CancellationToken ct)
    {
        var pedidos = campoVersaoIds.Distinct().ToArray();
        if (pedidos.Length == 0)
            return;

        var validos = await _repositorio.ListarCamposAtivosDaVersaoAsync(versaoProvedorId, pedidos, ct);
        var invalidos = pedidos.Except(validos).Order().ToArray();

        if (invalidos.Length > 0)
            throw new RegraDeNegocioException(
                CodigosErro.CampoAfetadoInvalido,
                $"Campos afetados inexistentes ou de outra versão: {string.Join(", ", invalidos)}.");
    }

    private async Task<Aviso> CarregarRastreadaOuFalharAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var aviso = await _repositorio.ObterPorIdAsync(id, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("Aviso", id);

        if (aviso.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException("O aviso foi alterado por outra requisição. Recarregue e tente novamente.");

        return aviso;
    }
}
