using Codex.Application.DTOs.Interno;
using Codex.Application.Validators;
using Codex.Domain.Enums;
using FluentAssertions;

namespace Codex.Tests.Unit.Validators;

public sealed class CriarArtigoDtoValidatorTests
{
    private readonly CriarArtigoDtoValidator _sut = new();

    private static CriarArtigoDto DtoValido() => new()
    {
        Tipo = TipoArtigo.Conceito,
        Titulo = "Título de exemplo",
        Corpo = "Corpo de exemplo.",
        CampoLayoutIds = [],
        ErroIds = [],
    };

    [Fact]
    public void Validate_DtoValido_EhValido()
    {
        var resultado = _sut.Validate(DtoValido());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TituloVazio_RetornaErro()
    {
        var dto = DtoValido() with { Titulo = "" };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarArtigoDto.Titulo));
    }

    [Fact]
    public void Validate_TituloAcimaDeDuzentosCaracteres_RetornaErro()
    {
        var dto = DtoValido() with { Titulo = new string('a', 201) };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarArtigoDto.Titulo));
    }

    [Fact]
    public void Validate_CorpoVazio_RetornaErro()
    {
        var dto = DtoValido() with { Corpo = "" };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarArtigoDto.Corpo));
    }

    [Fact]
    public void Validate_CampoLayoutIdsComDuplicata_RetornaErro()
    {
        var dto = DtoValido() with { CampoLayoutIds = [1, 1] };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarArtigoDto.CampoLayoutIds));
    }

    [Fact]
    public void Validate_ErroIdsComIdInvalido_RetornaErro()
    {
        var dto = DtoValido() with { ErroIds = [0] };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarArtigoDto.ErroIds));
    }
}

public sealed class AtualizarArtigoDtoValidatorTests
{
    private readonly AtualizarArtigoDtoValidator _sut = new();

    private static AtualizarArtigoDto DtoValido() => new()
    {
        Tipo = TipoArtigo.Procedimento,
        Titulo = "Título de exemplo",
        Corpo = "Corpo de exemplo.",
        CampoLayoutIds = [],
        ErroIds = [],
    };

    [Fact]
    public void Validate_DtoValido_EhValido()
    {
        var resultado = _sut.Validate(DtoValido());

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TituloVazio_RetornaErro()
    {
        var dto = DtoValido() with { Titulo = "" };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
    }
}

public sealed class AtualizarVerificacaoArtigoDtoValidatorTests
{
    private readonly AtualizarVerificacaoArtigoDtoValidator _sut = new();

    [Fact]
    public void Validate_DataDeHoje_EhValida()
    {
        var dto = new AtualizarVerificacaoArtigoDto { VerificadoEm = DateOnly.FromDateTime(DateTime.UtcNow) };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DataNoPassado_EhValida()
    {
        var dto = new AtualizarVerificacaoArtigoDto { VerificadoEm = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10) };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DataNoFuturo_RetornaErro()
    {
        var dto = new AtualizarVerificacaoArtigoDto { VerificadoEm = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarVerificacaoArtigoDto.VerificadoEm));
    }
}
