using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
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
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;

    public CampoVersaoServicoInterno(
        ICampoVersaoRepositorioInterno repositorio,
        IVersaoProvedorRepositorioInterno versaoRepositorio,
        IUnidadeDeTrabalho unidadeDeTrabalho)
    {
        _repositorio = repositorio;
        _versaoRepositorio = versaoRepositorio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
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

        await _unidadeDeTrabalho.ExecutarAsync(async ctInterno =>
        {
            // Mesmo lock da troca de layout — serializa contra um PUT
            // concorrente de /interno/versoes/{id} que esteja trocando versaoLayoutId.
            await _versaoRepositorio.BloquearParaAlteracaoAsync(versaoProvedorId, ctInterno);

            // Revalida sob o lock: o FluentValidation leu o layout ANTES deste commit — uma troca
            // pode ter sido confirmada na janela entre a validação e aqui.
            if (!await _repositorio.CampoLayoutEstaAtivoNaVersaoDoProvedorAsync(dto.CampoLayoutId, versaoProvedorId, ctInterno))
                throw new RegraDeNegocioException(
                    CodigosErro.CampoLayoutOrigemAlterado,
                    "O layout de origem desta versão mudou durante a operação. Recarregue e tente novamente.");

            await _repositorio.AdicionarAsync(campoVersao, ctInterno);
            await _repositorio.SalvarAlteracoesAsync(ctInterno);
        }, ct);

        return await LerDtoOuFalharAsync(campoVersao.Id, ct);
    }

    public async Task<CampoVersaoAdminDto> AtualizarAsync(int id, AtualizarCampoVersaoDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var campoVersao = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        campoVersao.AtualizarComportamento(
            dto.Tipo, dto.EstadoPreenchimento, dto.Ordem,
            dto.Tamanho, dto.Mascara, dto.Dominio, dto.Condicao, dto.Exemplo,
            dto.NotaPreenchimento, dto.DestinoXml, dto.NotaTransformacao);

        await _repositorio.SalvarAlteracoesAsync(ct);

        return await LerDtoOuFalharAsync(campoVersao.Id, ct);
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

    /// <summary>
    /// Relê pela projeção do repositório em vez de remontar o DTO à mão: o nome do campo canônico
    /// mora em outra tabela, e a cópia manual devolvia criar/atualizar sem ele — diferente do que
    /// a mesma tela recebe ao listar.
    /// </summary>
    private async Task<CampoVersaoAdminDto> LerDtoOuFalharAsync(int id, CancellationToken ct) =>
        await _repositorio.ObterDtoPorIdAsync(id, ct)
            ?? throw new EntidadeNaoEncontradaException("CampoVersao", id);
}
