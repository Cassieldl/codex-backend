using Codex.Application.DTOs.Interno;
using Codex.Application.Validators;
using Codex.Domain.Enums;
using FluentAssertions;

namespace Codex.Tests.Unit.Validators;

/// <summary>
/// Os números são literais de propósito: eles são o contrato com as colunas do banco
/// (<c>varchar(300)</c>, <c>160</c>, <c>60</c>, <c>500</c>), e uma constante importada do produto
/// subiria junto com o produto sem derrubar o teste.
/// </summary>
public sealed class LacunaBuscaValidatorsTests
{
    private readonly RegistrarLacunaBuscaDtoValidator _registrar = new();
    private readonly AlterarEstadoLacunaBuscaDtoValidator _alterar = new();

    [Theory]
    [InlineData(2, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void Termo_RespeitaAsFronteirasDeDoisATrezentos(int tamanho, bool deveriaPassar)
    {
        var resultado = _registrar.Validate(new RegistrarLacunaBuscaDto { Termo = new string('a', tamanho) });

        resultado.IsValid.Should().Be(deveriaPassar);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData(" a ")]
    public void Termo_VazioOuCurtoDepoisDeAparar_Reprova(string termo)
    {
        _registrar.Validate(new RegistrarLacunaBuscaDto { Termo = termo }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(160, true)]
    [InlineData(161, false)]
    public void Parceiro_RespeitaAFronteiraDeCentoESessenta(int tamanho, bool deveriaPassar)
    {
        var dto = new RegistrarLacunaBuscaDto { Termo = "termo", Parceiro = new string('p', tamanho) };

        _registrar.Validate(dto).IsValid.Should().Be(deveriaPassar);
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void NumeroChamado_RespeitaAFronteiraDeSessenta(int tamanho, bool deveriaPassar)
    {
        var dto = new RegistrarLacunaBuscaDto { Termo = "termo", NumeroChamado = new string('c', tamanho) };

        _registrar.Validate(dto).IsValid.Should().Be(deveriaPassar);
    }

    [Fact]
    public void ParceiroEChamadoAusentes_Passam()
    {
        _registrar.Validate(new RegistrarLacunaBuscaDto { Termo = "termo" }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Nota_RespeitaAFronteiraDeQuinhentos(int tamanho, bool deveriaPassar)
    {
        var dto = new AlterarEstadoLacunaBuscaDto { Estado = EstadoLacunaBusca.Resolvida, Nota = new string('n', tamanho) };

        _alterar.Validate(dto).IsValid.Should().Be(deveriaPassar);
    }

    [Fact]
    public void Estado_ForaDoEnum_Reprova()
    {
        _alterar.Validate(new AlterarEstadoLacunaBuscaDto { Estado = (EstadoLacunaBusca)99 }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(EstadoLacunaBusca.Resolvida, true)]
    [InlineData(EstadoLacunaBusca.Descartada, true)]
    [InlineData(EstadoLacunaBusca.Aberta, false)]
    public void IncluirMesmoTermo_SoValeAoResolverOuDescartar(EstadoLacunaBusca estado, bool deveriaPassar)
    {
        var dto = new AlterarEstadoLacunaBuscaDto { Estado = estado, IncluirMesmoTermo = true };

        _alterar.Validate(dto).IsValid.Should().Be(deveriaPassar);
    }
}
