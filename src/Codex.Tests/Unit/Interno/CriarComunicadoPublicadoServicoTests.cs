using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Application.Services.Interno;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Codex.Tests.Unit.Interno;

/// <summary>
/// D-1 no nível do serviço: o que a porta HTTP não consegue exercitar.
///
/// <list type="bullet">
/// <item><b>Atomicidade</b> — a entidade chega ao repositório JÁ publicada. Nenhum teste de HTTP
/// consegue observar o instante entre o <c>INSERT</c> e um <c>UPDATE</c> de publicação; aqui o
/// callback do mock observa exatamente esse instante.</item>
/// <item><b>Guarda de expiração</b> — <c>CriarComunicadoDtoValidator</c> já barra
/// <c>ExpiraEm</c> no passado antes do service, então pelo pipeline MVC o ramo
/// <c>COMUNICADO_EXPIRADO</c> nunca executa. Ele existe para chamadas diretas ao service (job,
/// carga, outro service) e é aqui que se prova que executa de fato — tratamento inalcançável
/// seria pior do que a ausência dele.</item>
/// </list>
/// </summary>
public sealed class CriarComunicadoPublicadoServicoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IComunicadoRepositorioInterno> _repositorio = new(MockBehavior.Strict);
    private readonly ComunicadoServicoInterno _servico;

    public CriarComunicadoPublicadoServicoTests()
    {
        _servico = new ComunicadoServicoInterno(_repositorio.Object, new RelogioParado(Agora));
    }

    [Fact]
    public async Task CriarAsync_ComPublicar_EntregaAEntidadeJaPublicadaAoRepositorio()
    {
        Comunicado? recebidoPeloRepositorio = null;
        var publicadoNoMomentoDoAdicionar = false;

        PrepararRepositorio(comunicado =>
        {
            recebidoPeloRepositorio = comunicado;
            publicadoNoMomentoDoAdicionar = comunicado.Publicado;
        });

        var resultado = await _servico.CriarAsync(Dto(publicar: true, expiraEm: Agora.AddDays(5)), CancellationToken.None);

        publicadoNoMomentoDoAdicionar.Should().BeTrue(
            because: "D-1 — a linha nunca existe em rascunho, nem por um instante: publicar acontece ANTES do INSERT");
        recebidoPeloRepositorio.Should().NotBeNull();
        resultado.Publicado.Should().BeTrue();

        // Uma transação só — dois SaveChanges reabririam a janela de rascunho que D-1 fecha.
        _repositorio.Verify(r => r.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_SemPublicar_EntregaAEntidadeEmRascunho()
    {
        var publicadoNoMomentoDoAdicionar = true;

        PrepararRepositorio(comunicado => publicadoNoMomentoDoAdicionar = comunicado.Publicado);

        var resultado = await _servico.CriarAsync(Dto(publicar: false, expiraEm: Agora.AddDays(5)), CancellationToken.None);

        publicadoNoMomentoDoAdicionar.Should().BeFalse();
        resultado.Publicado.Should().BeFalse();
    }

    [Fact]
    public async Task CriarAsync_ComPublicarEExpiracaoNoPassado_Lanca422ComunicadoExpirado_ESemPersistir()
    {
        // Sem Setup de AdicionarAsync/SalvarAlteracoesAsync: o mock é Strict, então qualquer
        // persistência aqui derruba o caso com "invocation failed" em vez de passar batido.
        var acao = async () => await _servico.CriarAsync(
            Dto(publicar: true, expiraEm: Agora.AddSeconds(-1)), CancellationToken.None);

        var excecao = await acao.Should().ThrowAsync<RegraDeNegocioException>();
        excecao.Which.Codigo.Should().Be(CodigosErro.ComunicadoExpirado);
        excecao.Which.Message.Should().Contain("expiração",
            because: "a mensagem diz o próximo passo ao editor, não só o problema (B-4)");

        _repositorio.Verify(r => r.AdicionarAsync(It.IsAny<Comunicado>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CriarAsync_SemPublicarEComExpiracaoNoPassado_NaoAcionaAGuarda()
    {
        // Controle negativo: a guarda é condicionada a `publicar`. Criar rascunho já vencido é
        // permitido — é o próprio gesto de "preparar agora, ajustar a data depois".
        PrepararRepositorio(_ => { });

        var resultado = await _servico.CriarAsync(
            Dto(publicar: false, expiraEm: Agora.AddSeconds(-1)), CancellationToken.None);

        resultado.Publicado.Should().BeFalse();
        resultado.Encerrado.Should().BeTrue();
    }

    private void PrepararRepositorio(Action<Comunicado> aoAdicionar)
    {
        _repositorio.Setup(r => r.AdicionarAsync(It.IsAny<Comunicado>(), It.IsAny<CancellationToken>()))
            .Callback<Comunicado, CancellationToken>((comunicado, _) => aoAdicionar(comunicado))
            .Returns(Task.CompletedTask);

        _repositorio.Setup(r => r.SalvarAlteracoesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repositorio.Setup(r => r.ObterRotulosEscopoAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RotulosEscopoComunicado(null, null));
    }

    private static CriarComunicadoDto Dto(bool publicar, DateTimeOffset expiraEm) => new()
    {
        Titulo = "Instabilidade generalizada",
        Corpo = "Emissão intermitente desde as 9h.",
        Severidade = Severidade.Critico,
        Abrangencia = AbrangenciaComunicado.Global,
        ExpiraEm = expiraEm,
        Publicar = publicar,
    };

    /// <summary>Relógio parado — a guarda de expiração compara contra "agora" injetado, nunca contra o relógio da máquina.</summary>
    private sealed class RelogioParado : TimeProvider
    {
        private readonly DateTimeOffset _agora;

        public RelogioParado(DateTimeOffset agora)
        {
            _agora = agora;
        }

        public override DateTimeOffset GetUtcNow() => _agora;
    }
}
