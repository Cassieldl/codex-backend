using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services.Interno;

/// <summary>
/// Fila editorial (escopo funcional v3.1 §9.2): registra a busca interna sem resultado e deixa a
/// equipe resolver, descartar ou reabrir. Sem cascata de busca — lacuna nunca é indexada.
/// "Agora" vem do <see cref="TimeProvider"/> injetado.
/// </summary>
public sealed class LacunaBuscaServicoInterno : ILacunaBuscaServicoInterno
{
    private const int TamanhoPaginaPadrao = 25;
    private const int TamanhoPaginaMaximo = 200;

    private readonly ILacunaBuscaRepositorioInterno _repositorio;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly TimeProvider _relogio;

    public LacunaBuscaServicoInterno(
        ILacunaBuscaRepositorioInterno repositorio, IUnidadeDeTrabalho unidadeDeTrabalho, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _relogio = relogio;
    }

    public Task<PaginaResultado<LacunaBuscaDto>> ListarAsync(FiltroLacunaBusca filtro, CancellationToken ct)
    {
        var (pagina, tamanhoPagina) = Paginacao.Normalizar(
            filtro.Pagina, filtro.TamanhoPagina, TamanhoPaginaPadrao, TamanhoPaginaMaximo);

        return _repositorio.ListarAsync(filtro with { Pagina = pagina, TamanhoPagina = tamanhoPagina }, ct);
    }

    public Task<PaginaResultado<LacunaBuscaTermoDto>> ListarTermosAsync(FiltroLacunaBuscaTermo filtro, CancellationToken ct)
    {
        var (pagina, tamanhoPagina) = Paginacao.Normalizar(
            filtro.Pagina, filtro.TamanhoPagina, TamanhoPaginaPadrao, TamanhoPaginaMaximo);

        return _repositorio.ListarTermosAsync(filtro with { Pagina = pagina, TamanhoPagina = tamanhoPagina }, ct);
    }

    public async Task<LacunaBuscaDto> ObterAsync(int id, CancellationToken ct)
    {
        var lacuna = await _repositorio.ObterPorIdAsync(id, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("Lacuna de busca", id);

        return await MapearAsync(lacuna, ct);
    }

    public async Task<LacunaBuscaDto> RegistrarAsync(
        RegistrarLacunaBuscaDto dto, UsuarioDaRequisicao usuario, CancellationToken ct)
    {
        var lacuna = LacunaBusca.Registrar(dto.Termo, dto.Parceiro, dto.NumeroChamado, usuario.Id, usuario.Nome);

        await _repositorio.AdicionarAsync(lacuna, ct);
        await _repositorio.SalvarAlteracoesAsync(ct);

        return await MapearAsync(lacuna, ct);
    }

    public async Task<AlteracaoEstadoLacunaBuscaDto> AlterarEstadoAsync(
        int id, AlterarEstadoLacunaBuscaDto dto, uint versaoEsperada, UsuarioDaRequisicao usuario, CancellationToken ct)
    {
        var lacuna = await _repositorio.ObterPorIdAsync(id, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("Lacuna de busca", id);

        if (lacuna.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException("A lacuna foi alterada por outra requisição. Recarregue e tente novamente.");

        var agora = _relogio.GetUtcNow();
        lacuna.AlterarEstado(dto.Estado, dto.Nota, usuario.Id, usuario.Nome, agora);

        var mesmoTermoAlteradas = 0;
        if (dto.IncluirMesmoTermo)
        {
            // Duas escritas, uma transação: a ocorrência escolhida e as irmãs mudam juntas ou nenhuma muda.
            await _unidadeDeTrabalho.ExecutarAsync(async token =>
            {
                await _repositorio.SalvarAlteracoesAsync(token);
                mesmoTermoAlteradas = await _repositorio.AlterarAbertasDoMesmoTermoAsync(lacuna, token);
            }, ct);
        }
        else
        {
            await _repositorio.SalvarAlteracoesAsync(ct);
        }

        return new AlteracaoEstadoLacunaBuscaDto
        {
            Lacuna = await MapearAsync(lacuna, ct),
            MesmoTermoAlteradas = mesmoTermoAlteradas,
        };
    }

    private async Task<LacunaBuscaDto> MapearAsync(LacunaBusca lacuna, CancellationToken ct) => new()
    {
        Id = lacuna.Id,
        Termo = lacuna.Termo,
        Parceiro = lacuna.Parceiro,
        NumeroChamado = lacuna.NumeroChamado,
        Estado = lacuna.Estado.ToString(),
        Nota = lacuna.Nota,
        RegistradoPorNome = lacuna.RegistradoPorNome,
        RegistradoEm = lacuna.CriadoEm,
        ResolvidaEm = lacuna.ResolvidaEm,
        ResolvidoPorNome = lacuna.ResolvidoPorNome,
        OcorrenciasAbertas = await _repositorio.ContarAbertasDoTermoAsync(lacuna.TermoNormalizado, ct),
        VersaoConcorrencia = lacuna.Versao,
    };
}
