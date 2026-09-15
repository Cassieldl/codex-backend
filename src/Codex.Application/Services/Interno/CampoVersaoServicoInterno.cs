using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services.Interno;

/// <summary>
/// Área interna de <see cref="CampoVersao"/> (I11-I13b). Sem chamada a <see cref="IIndiceBuscaWriter"/> —
/// <c>CampoVersao</c> não tem linha própria em <c>codex.itensbusca</c> (M26).
/// </summary>
public sealed class CampoVersaoServicoInterno : ICampoVersaoServicoInterno
{
    /// <summary>M22 — cap de 2000 campos por versão, rejeitado na escrita com 422.</summary>
    private const int CapCamposPorVersao = 2000;

    private readonly ICampoVersaoRepositorioInterno _repositorio;
    private readonly IVersaoProvedorRepositorioInterno _versaoRepositorio;

    public CampoVersaoServicoInterno(
        ICampoVersaoRepositorioInterno repositorio, IVersaoProvedorRepositorioInterno versaoRepositorio)
    {
        _repositorio = repositorio;
        _versaoRepositorio = versaoRepositorio;
    }

    public async Task<CamposVersaoAgregadoDto> ListarAgregadoAsync(int versaoProvedorId, CancellationToken ct)
    {
        _ = await _versaoRepositorio.ObterPorIdAsync(versaoProvedorId, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoProvedor", versaoProvedorId);

        var campos = await _repositorio.ListarPorVersaoAsync(versaoProvedorId, ct);
        var ausentes = await _repositorio.ListarCamposLayoutAusentesAsync(versaoProvedorId, ct);

        return new CamposVersaoAgregadoDto { Campos = campos, CamposLayoutAusentes = ausentes };
    }

    public async Task<CampoVersaoAdminDto> CriarAsync(int versaoProvedorId, CriarCampoVersaoDto dto, CancellationToken ct)
    {
        _ = await _versaoRepositorio.ObterPorIdAsync(versaoProvedorId, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("VersaoProvedor", versaoProvedorId);

        var totalAtual = await _repositorio.ContarPorVersaoAsync(versaoProvedorId, ct);
        if (totalAtual >= CapCamposPorVersao)
            throw new LimiteExcedidoException($"A versão já possui {totalAtual} campos, no limite de {CapCamposPorVersao} (M22).");

        var campoVersao = CampoVersao.Criar(
            versaoProvedorId, dto.CampoLayoutId, dto.Tipo, dto.EstadoPreenchimento, dto.Ordem,
            dto.Tamanho, dto.Mascara, dto.Dominio, dto.Condicao, dto.Exemplo,
            dto.NotaPreenchimento, dto.DestinoXml, dto.NotaTransformacao);

        await _repositorio.AdicionarAsync(campoVersao, ct);
        await _repositorio.SalvarAlteracoesAsync(ct);

        return Mapear(campoVersao);
    }

    public async Task<CampoVersaoAdminDto> AtualizarAsync(int id, AtualizarCampoVersaoDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var campoVersao = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        campoVersao.AtualizarComportamento(
            dto.Tipo, dto.EstadoPreenchimento, dto.Ordem,
            dto.Tamanho, dto.Mascara, dto.Dominio, dto.Condicao, dto.Exemplo,
            dto.NotaPreenchimento, dto.DestinoXml, dto.NotaTransformacao);

        await _repositorio.SalvarAlteracoesAsync(ct);

        return Mapear(campoVersao);
    }

    public async Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var campoVersao = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);
        campoVersao.Desativar();
        await _repositorio.SalvarAlteracoesAsync(ct);
    }

    private async Task<CampoVersao> CarregarRastreadaOuFalharAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var campoVersao = await _repositorio.ObterPorIdAsync(id, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("CampoVersao", id);

        if (campoVersao.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException("O campo foi alterado por outra requisição. Recarregue e tente novamente.");

        return campoVersao;
    }

    private static CampoVersaoAdminDto Mapear(CampoVersao campoVersao) => new()
    {
        Id = campoVersao.Id,
        VersaoProvedorId = campoVersao.VersaoProvedorId,
        CampoLayoutId = campoVersao.CampoLayoutId,
        Tipo = campoVersao.Tipo,
        Tamanho = campoVersao.Tamanho,
        Mascara = campoVersao.Mascara,
        Dominio = campoVersao.Dominio,
        EstadoPreenchimento = campoVersao.EstadoPreenchimento.ToString(),
        Condicao = campoVersao.Condicao,
        Exemplo = campoVersao.Exemplo,
        NotaPreenchimento = campoVersao.NotaPreenchimento,
        DestinoXml = campoVersao.DestinoXml,
        NotaTransformacao = campoVersao.NotaTransformacao,
        Ordem = campoVersao.Ordem,
        VersaoConcorrencia = campoVersao.Versao,
    };
}
