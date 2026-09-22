using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services.Interno;

/// <summary>
/// Área interna de <see cref="Comunicado"/> (I30-I35, analise-backend.md §6). Sem cascata de
/// busca: comunicado NUNCA é indexado (decisoes.md M-1) — nenhuma transição daqui toca
/// <c>codex.itensbusca</c>, e por isso também não há <c>IUnidadeDeTrabalho</c> (não existe um
/// segundo comando a coordenar na mesma transação).
///
/// <para>
/// "Agora" vem sempre de um <see cref="TimeProvider"/> injetado, nunca de
/// <c>DateTimeOffset.UtcNow</c> lido aqui dentro: é o que torna o 422 de publicação expirada
/// (B-4) e o campo derivado <c>encerrado</c> testáveis sem depender do relógio da máquina.
/// </para>
/// </summary>
public sealed class ComunicadoServicoInterno : IComunicadoServicoInterno
{
    private readonly IComunicadoRepositorioInterno _repositorio;
    private readonly TimeProvider _relogio;

    public ComunicadoServicoInterno(IComunicadoRepositorioInterno repositorio, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _relogio = relogio;
    }

    public Task<PaginaResultado<ComunicadoAdminListaItemDto>> ListarAsync(FiltroComunicadoInterno filtro, CancellationToken ct) =>
        _repositorio.ListarAsync(NormalizarPaginacao(filtro), _relogio.GetUtcNow(), ct);

    public async Task<ComunicadoAdminDto> ObterDetalheAsync(int id, CancellationToken ct)
    {
        var comunicado = await _repositorio.ObterPorIdAsync(id, rastreada: false, ct)
            ?? throw new EntidadeNaoEncontradaException("Comunicado", id);

        return await MapearAsync(comunicado, ct);
    }

    public async Task<ComunicadoAdminDto> CriarAsync(CriarComunicadoDto dto, CancellationToken ct)
    {
        var comunicado = CriarPelaAbrangencia(dto);

        if (dto.Publicar)
        {
            // Mesma regra de I35 (B-4): publicar algo que a leitura já trataria como encerrado é
            // ação sem efeito observável, que o job desfaria no tique seguinte.
            //
            // Pela porta HTTP este ramo não chega a executar — CriarComunicadoDtoValidator já
            // exige ExpiraEm no futuro e devolve 422 VALIDATION_ERROR antes do service. A guarda
            // fica porque o service é chamável fora do pipeline MVC (job, script de carga, outro
            // service), e ali é o único ponto que impede um comunicado nascer publicado e vencido.
            if (comunicado.Expirado(_relogio.GetUtcNow()))
                throw new RegraDeNegocioException(
                    CodigosErro.ComunicadoExpirado,
                    "Não é possível publicar um comunicado cuja expiração já passou. "
                    + "Ajuste a data de expiração para o futuro antes de publicar.");

            comunicado.Publicar();
        }

        // UMA transação, UM INSERT: a linha nunca existe em rascunho, nem por um instante (D-1).
        await _repositorio.AdicionarAsync(comunicado, ct);
        await _repositorio.SalvarAlteracoesAsync(ct);

        return await MapearAsync(comunicado, ct);
    }

    public async Task<ComunicadoAdminDto> AtualizarAsync(int id, AtualizarComunicadoDto dto, uint versaoEsperada, CancellationToken ct)
    {
        var comunicado = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        AplicarAtualizacaoPelaAbrangencia(comunicado, dto);

        await _repositorio.SalvarAlteracoesAsync(ct);

        return await MapearAsync(comunicado, ct);
    }

    public async Task RemoverAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var comunicado = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        comunicado.Desativar();

        await _repositorio.SalvarAlteracoesAsync(ct);
    }

    public async Task<ComunicadoAdminDto> AtualizarPublicacaoAsync(int id, bool publicado, uint versaoEsperada, CancellationToken ct)
    {
        var comunicado = await CarregarRastreadaOuFalharAsync(id, versaoEsperada, ct);

        // B-4 — publicar algo que a leitura já trataria como encerrado no mesmo instante é uma
        // ação sem efeito observável, que o job desfaria no tique seguinte. A mensagem diz o
        // PRÓXIMO PASSO, não só o problema: é requisito, não cortesia.
        if (publicado && comunicado.Expirado(_relogio.GetUtcNow()))
            throw new RegraDeNegocioException(
                CodigosErro.ComunicadoExpirado,
                "Não é possível publicar um comunicado cuja expiração já passou. "
                + "Edite o comunicado para estender a data de expiração para o futuro e publique em seguida.");

        if (publicado)
            comunicado.Publicar();
        else
            comunicado.Despublicar();

        await _repositorio.SalvarAlteracoesAsync(ct);

        return await MapearAsync(comunicado, ct);
    }

    private static Comunicado CriarPelaAbrangencia(CriarComunicadoDto dto) => dto.Abrangencia switch
    {
        AbrangenciaComunicado.Global =>
            Comunicado.CriarGlobal(dto.Titulo, dto.Corpo, dto.Severidade, dto.ExpiraEm),

        AbrangenciaComunicado.Provedor =>
            Comunicado.CriarDeProvedor(
                ExigirId(dto.ProvedorId, nameof(dto.ProvedorId)), dto.Titulo, dto.Corpo, dto.Severidade, dto.ExpiraEm),

        AbrangenciaComunicado.ProvedorVersao =>
            Comunicado.CriarDeVersaoProvedor(
                ExigirId(dto.ProvedorId, nameof(dto.ProvedorId)),
                ExigirId(dto.VersaoProvedorId, nameof(dto.VersaoProvedorId)),
                dto.Titulo, dto.Corpo, dto.Severidade, dto.ExpiraEm),

        _ => throw new RegraDeNegocioException("Abrangência de comunicado inválida."),
    };

    private static void AplicarAtualizacaoPelaAbrangencia(Comunicado comunicado, AtualizarComunicadoDto dto)
    {
        switch (dto.Abrangencia)
        {
            case AbrangenciaComunicado.Global:
                comunicado.AtualizarComoGlobal(dto.Titulo, dto.Corpo, dto.Severidade, dto.ExpiraEm);
                break;

            case AbrangenciaComunicado.Provedor:
                comunicado.AtualizarComoDeProvedor(
                    ExigirId(dto.ProvedorId, nameof(dto.ProvedorId)), dto.Titulo, dto.Corpo, dto.Severidade, dto.ExpiraEm);
                break;

            case AbrangenciaComunicado.ProvedorVersao:
                comunicado.AtualizarComoDeVersaoProvedor(
                    ExigirId(dto.ProvedorId, nameof(dto.ProvedorId)),
                    ExigirId(dto.VersaoProvedorId, nameof(dto.VersaoProvedorId)),
                    dto.Titulo, dto.Corpo, dto.Severidade, dto.ExpiraEm);
                break;

            default:
                throw new RegraDeNegocioException("Abrangência de comunicado inválida.");
        }
    }

    /// <summary>
    /// Segunda camada da mesma regra que o <c>CHECK</c> do banco e o validator já impõem — nunca
    /// deixar um id ausente virar violação de constraint (500) em vez de 422.
    /// </summary>
    private static int ExigirId(int? id, string campo) =>
        id ?? throw new RegraDeNegocioException($"O campo '{campo}' é obrigatório para a abrangência informada.");

    private async Task<Comunicado> CarregarRastreadaOuFalharAsync(int id, uint versaoEsperada, CancellationToken ct)
    {
        var comunicado = await _repositorio.ObterPorIdAsync(id, rastreada: true, ct)
            ?? throw new EntidadeNaoEncontradaException("Comunicado", id);

        if (comunicado.Versao != versaoEsperada)
            throw new ConflitoConcorrenciaException("O comunicado foi alterado por outra requisição. Recarregue e tente novamente.");

        return comunicado;
    }

    private static FiltroComunicadoInterno NormalizarPaginacao(FiltroComunicadoInterno filtro)
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

    private async Task<ComunicadoAdminDto> MapearAsync(Comunicado comunicado, CancellationToken ct)
    {
        var rotulos = await _repositorio.ObterRotulosEscopoAsync(comunicado.ProvedorId, comunicado.VersaoProvedorId, ct);

        return new ComunicadoAdminDto
        {
            Id = comunicado.Id,
            Titulo = comunicado.Titulo,
            Corpo = comunicado.Corpo,
            Severidade = comunicado.Severidade.ToString(),
            Abrangencia = comunicado.Abrangencia.ToString(),
            ProvedorId = comunicado.ProvedorId,
            ProvedorNome = rotulos.ProvedorNome,
            VersaoProvedorId = comunicado.VersaoProvedorId,
            VersaoRotulo = rotulos.VersaoRotulo,
            ExpiraEm = comunicado.ExpiraEm,
            Publicado = comunicado.Publicado,
            Encerrado = comunicado.Expirado(_relogio.GetUtcNow()),
            VersaoConcorrencia = comunicado.Versao,
        };
    }
}
