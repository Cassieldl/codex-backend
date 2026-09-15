using Codex.Application.DTOs.Interno;
using Codex.Application.Validators;
using Codex.Domain.Enums;
using FluentAssertions;

namespace Codex.Tests.Unit.Validators;

/// <summary>§9/U9: <c>Variantes</c> vazio → erro (toda origem); <c>VersoesProvedorIds</c> vazio + <c>Origem=Prefeitura</c> → erro; outras origens → válido.</summary>
public sealed class CriarErroDtoValidatorTests
{
    private readonly CriarErroDtoValidator _sut = new();

    private static CriarErroDto DtoValido(OrigemErro origem = OrigemErro.InvoiCy) => new()
    {
        Codigo = "E-124",
        Origem = origem,
        Titulo = "Erro de exemplo",
        Variantes = ["mensagem observada"],
        VersoesProvedorIds = [],
    };

    [Theory]
    [InlineData(OrigemErro.Prefeitura)]
    [InlineData(OrigemErro.InvoiCy)]
    [InlineData(OrigemErro.Schema)]
    public void Validate_ComVariantesVazio_RetornaErro_ParaQualquerOrigem(OrigemErro origem)
    {
        var dto = DtoValido(origem) with { Variantes = [] };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarErroDto.Variantes));
    }

    [Fact]
    public void Validate_OrigemPrefeitura_SemVersoesProvedorIds_RetornaErro()
    {
        var dto = DtoValido(OrigemErro.Prefeitura) with { VersoesProvedorIds = [] };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarErroDto.VersoesProvedorIds));
    }

    [Theory]
    [InlineData(OrigemErro.InvoiCy)]
    [InlineData(OrigemErro.Schema)]
    public void Validate_OrigemNaoPrefeitura_SemVersoesProvedorIds_EhValido(OrigemErro origem)
    {
        var dto = DtoValido(origem) with { VersoesProvedorIds = [] };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_OrigemPrefeitura_ComVersoesProvedorIds_EhValido()
    {
        var dto = DtoValido(OrigemErro.Prefeitura) with { VersoesProvedorIds = [10] };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeTrue();
    }
}
