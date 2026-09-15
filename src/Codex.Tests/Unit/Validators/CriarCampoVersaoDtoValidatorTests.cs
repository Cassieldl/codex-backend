using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Application.Validators;
using Codex.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Codex.Tests.Unit.Validators;

/// <summary>§9: <c>EstadoPreenchimento</c> fora do enum → erro.</summary>
public sealed class CriarCampoVersaoDtoValidatorTests
{
    private readonly Mock<ICampoVersaoRepositorioInterno> _repositorio = new();
    private readonly Mock<IContextoRequisicaoHttp> _contextoRequisicao = new();
    private readonly CriarCampoVersaoDtoValidator _sut;

    public CriarCampoVersaoDtoValidatorTests()
    {
        _contextoRequisicao.Setup(c => c.ObterIdDaRota(It.IsAny<string>())).Returns(1);
        _repositorio.Setup(r => r.CampoLayoutEstaAtivoNaVersaoDoProvedorAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new CriarCampoVersaoDtoValidator(_repositorio.Object, _contextoRequisicao.Object);
    }

    private static CriarCampoVersaoDto DtoValido() => new()
    {
        CampoLayoutId = 1,
        Tipo = "texto",
        EstadoPreenchimento = EstadoPreenchimento.Obrigatorio,
        Ordem = 1,
    };

    [Fact]
    public async Task Validate_ComDtoValido_NaoRetornaErro()
    {
        var resultado = await _sut.ValidateAsync(DtoValido());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ComEstadoPreenchimentoForaDoEnum_RetornaErro()
    {
        var dto = DtoValido() with { EstadoPreenchimento = (EstadoPreenchimento)999 };

        var resultado = await _sut.ValidateAsync(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCampoVersaoDto.EstadoPreenchimento));
    }

    [Fact]
    public async Task Validate_SemCampoLayoutId_RetornaErro()
    {
        var dto = DtoValido() with { CampoLayoutId = 0 };

        var resultado = await _sut.ValidateAsync(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCampoVersaoDto.CampoLayoutId));
    }

    [Fact]
    public async Task Validate_QuandoCampoLayoutNaoEstaAtivoNaVersaoDoProvedor_RetornaErro()
    {
        _repositorio.Setup(r => r.CampoLayoutEstaAtivoNaVersaoDoProvedorAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var resultado = await _sut.ValidateAsync(DtoValido());

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCampoVersaoDto.CampoLayoutId));
    }
}
