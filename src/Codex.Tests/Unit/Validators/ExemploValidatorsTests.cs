using System.Text;
using Codex.Application.DTOs.Interno;
using Codex.Application.Validators;
using FluentAssertions;

namespace Codex.Tests.Unit.Validators;

/// <summary>
/// C3 — o teto do XML do exemplo é de <b>64 KiB em CARACTERES</b> (UTF-16 de uma <c>string</c>
/// .NET), deliberadamente diferente do <c>CHECK</c> de 256 KiB em BYTES do banco
/// (modelo-dados.md §1.5). A fronteira é medida dos dois lados: um a menos passa, um a mais
/// reprova — teste de um lado só não distingue "teto certo" de "teto errado por um".
/// </summary>
public sealed class ExemploValidatorsTests
{
    private const int TetoCaracteres = 64 * 1024;

    private readonly CriarExemploDtoValidator _criarValidator = new();
    private readonly AtualizarExemploDtoValidator _atualizarValidator = new();

    [Theory]
    [InlineData(TetoCaracteres, true)]
    [InlineData(TetoCaracteres + 1, false)]
    [InlineData(1, true)]
    public void CriarExemplo_TetoDoXml_ValidaNosDoisLadosDaFronteira(int caracteres, bool deveSerValido)
    {
        var resultado = _criarValidator.Validate(new CriarExemploDto
        {
            Nome = "Cenário",
            Xml = new string('x', caracteres),
        });

        resultado.IsValid.Should().Be(deveSerValido);
    }

    [Theory]
    [InlineData(TetoCaracteres, true)]
    [InlineData(TetoCaracteres + 1, false)]
    public void AtualizarExemplo_TetoDoXml_ValidaNosDoisLadosDaFronteira(int caracteres, bool deveSerValido)
    {
        var resultado = _atualizarValidator.Validate(new AtualizarExemploDto
        {
            Nome = "Cenário",
            Xml = new string('x', caracteres),
        });

        resultado.IsValid.Should().Be(deveSerValido);
    }

    /// <summary>
    /// A razão de o teto ser em caracteres e o CHECK do banco em bytes: XML acentuado de 64 KiB em
    /// caracteres ocupa MAIS de 64 KiB em UTF-8. Se alguém "uniformizar" as unidades trocando
    /// <c>Length</c> por <c>GetByteCount</c>, este caso passa a reprovar um conteúdo legítimo.
    /// </summary>
    [Fact]
    public void CriarExemplo_XmlAcentuadoNoTetoDeCaracteres_EValido_AindaQuePasseDoTetoEmBytes()
    {
        var xml = new string('ç', TetoCaracteres);
        Encoding.UTF8.GetByteCount(xml).Should().BeGreaterThan(TetoCaracteres);

        var resultado = _criarValidator.Validate(new CriarExemploDto { Nome = "Cenário", Xml = xml });

        resultado.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("-–—")]
    public void CriarExemplo_NomeQueNaoGeraSlug_EInvalido(string nome)
    {
        var resultado = _criarValidator.Validate(new CriarExemploDto { Nome = nome, Xml = "<x/>" });

        resultado.IsValid.Should().BeFalse(
            because: "o slug é derivado do nome — nome sem letra nem dígito normalizaria para vazio e viraria 500");
    }

    [Fact]
    public void CriarExemplo_NomeAcimaDe160Caracteres_EInvalido()
    {
        var resultado = _criarValidator.Validate(new CriarExemploDto
        {
            Nome = new string('a', 161),
            Xml = "<x/>",
        });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CriarExemplo_XmlVazio_EInvalido()
    {
        var resultado = _criarValidator.Validate(new CriarExemploDto { Nome = "Cenário", Xml = "  " });

        resultado.IsValid.Should().BeFalse();
    }
}
