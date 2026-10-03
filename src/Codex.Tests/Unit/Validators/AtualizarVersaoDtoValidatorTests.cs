using Codex.Application.DTOs.Interno;
using Codex.Application.Validators;
using FluentAssertions;

namespace Codex.Tests.Unit.Validators;

/// <summary>
/// Demanda 008-campos-emissor-nacional (D1) — <c>VersaoLayoutId</c> é opcional em
/// <see cref="AtualizarVersaoDto"/>: <c>null</c> (o que o frontend atual envia) precisa continuar
/// válido, e só reprova quando INFORMADO e fora do domínio de um id (<c>&lt;= 0</c>).
/// </summary>
public sealed class AtualizarVersaoDtoValidatorTests
{
    private readonly AtualizarVersaoDtoValidator _sut = new();

    private static AtualizarVersaoDto DtoValido(int? versaoLayoutId = null) => new()
    {
        Rotulo = "Rotulo válido",
        VersaoLayoutId = versaoLayoutId,
    };

    [Fact]
    public void Validate_SemVersaoLayoutId_NaoRetornaErro()
    {
        var resultado = _sut.Validate(DtoValido(versaoLayoutId: null));

        resultado.IsValid.Should().BeTrue(because: "é o payload que o frontend atual envia — sem o campo");
    }

    [Fact]
    public void Validate_ComVersaoLayoutIdPositivo_NaoRetornaErro()
    {
        var resultado = _sut.Validate(DtoValido(versaoLayoutId: 1));

        resultado.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ComVersaoLayoutIdInvalido_RetornaErro(int versaoLayoutIdInvalido)
    {
        var resultado = _sut.Validate(DtoValido(versaoLayoutId: versaoLayoutIdInvalido));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarVersaoDto.VersaoLayoutId));
    }
}
