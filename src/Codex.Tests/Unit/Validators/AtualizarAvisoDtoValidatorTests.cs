using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Application.Validators;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Codex.Tests.Unit.Validators;

/// <summary>§9/risco #3 do DBA: <c>CampoVersaoIds</c> preenchido num aviso cujo <c>ProvedorId != null</c> (carregado via contexto mockado) → erro.</summary>
public sealed class AtualizarAvisoDtoValidatorTests
{
    private readonly Mock<IAvisoRepositorioInterno> _repositorio = new();
    private readonly Mock<IContextoRequisicaoHttp> _contextoRequisicao = new();

    private AtualizarAvisoDtoValidator CriarSut() => new(_repositorio.Object, _contextoRequisicao.Object);

    private static AtualizarAvisoDto DtoValido(IReadOnlyCollection<int>? campoVersaoIds = null) => new()
    {
        Severidade = Severidade.Alerta,
        Titulo = "Título",
        Corpo = "Corpo",
        CampoVersaoIds = campoVersaoIds,
    };

    [Fact]
    public async Task Validate_AvisoDeProvedor_ComCampoVersaoIds_RetornaErro()
    {
        var avisoDeProvedor = Aviso.CriarDeProvedor(10, Severidade.Critico, "T", "C");
        _contextoRequisicao.Setup(c => c.ObterIdDaRota(It.IsAny<string>())).Returns(1);
        _repositorio.Setup(r => r.ObterPorIdAsync(1, false, It.IsAny<CancellationToken>())).ReturnsAsync(avisoDeProvedor);

        var resultado = await CriarSut().ValidateAsync(DtoValido([1, 2, 3]));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarAvisoDto.CampoVersaoIds));
    }

    [Fact]
    public async Task Validate_AvisoDeVersao_ComCampoVersaoIds_EhValido()
    {
        var avisoDeVersao = Aviso.CriarDeVersao(20, Severidade.Critico, "T", "C");
        _contextoRequisicao.Setup(c => c.ObterIdDaRota(It.IsAny<string>())).Returns(1);
        _repositorio.Setup(r => r.ObterPorIdAsync(1, false, It.IsAny<CancellationToken>())).ReturnsAsync(avisoDeVersao);

        var resultado = await CriarSut().ValidateAsync(DtoValido([1, 2, 3]));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_SemCampoVersaoIds_NuncaConsultaRepositorio()
    {
        var resultado = await CriarSut().ValidateAsync(DtoValido(campoVersaoIds: null));

        resultado.IsValid.Should().BeTrue();
        _repositorio.Verify(r => r.ObterPorIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
