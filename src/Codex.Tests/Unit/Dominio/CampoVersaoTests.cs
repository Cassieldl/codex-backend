using System.Reflection;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using FluentAssertions;

namespace Codex.Tests.Unit.Dominio;

/// <summary>Prova de tipo de U4 — não existe overload de <see cref="CampoVersao.Criar"/> que dispense <c>campoLayoutId</c>.</summary>
public sealed class CampoVersaoTests
{
    [Fact]
    public void Criar_TodasAsSobrecargasExigemCampoLayoutIdObrigatorio()
    {
        var sobrecargas = typeof(CampoVersao)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(CampoVersao.Criar))
            .ToList();

        sobrecargas.Should().NotBeEmpty();

        foreach (var metodo in sobrecargas)
        {
            var parametro = metodo.GetParameters().FirstOrDefault(p => p.Name == "campoLayoutId");

            parametro.Should().NotBeNull(because: $"{metodo} precisa exigir campoLayoutId (U4)");
            parametro!.ParameterType.Should().Be(typeof(int));
            parametro.IsOptional.Should().BeFalse(because: "campoLayoutId não pode ter valor default — isso seria uma forma de omiti-lo (U4)");
            parametro.HasDefaultValue.Should().BeFalse();
        }
    }

    [Fact]
    public void Criar_DefineCampoLayoutIdInformado()
    {
        var campoVersao = CampoVersao.Criar(versaoProvedorId: 1, campoLayoutId: 42, tipo: "texto", estadoPreenchimento: EstadoPreenchimento.Obrigatorio);

        campoVersao.CampoLayoutId.Should().Be(42);
        campoVersao.VersaoProvedorId.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_LancaQuandoTipoInvalido(string? tipoInvalido)
    {
        var acao = () => CampoVersao.Criar(1, 42, tipoInvalido!, EstadoPreenchimento.Obrigatorio);

        acao.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(EstadoPreenchimento.Obrigatorio)]
    [InlineData(EstadoPreenchimento.Opcional)]
    [InlineData(EstadoPreenchimento.Condicional)]
    public void Criar_AceitaTodosOsEstadosDePreenchimento(EstadoPreenchimento estado)
    {
        var campoVersao = CampoVersao.Criar(1, 42, "texto", estado);

        campoVersao.EstadoPreenchimento.Should().Be(estado);
    }

    [Fact]
    public void AtualizarComportamento_AtualizaTodosOsCamposDeComportamento()
    {
        var campoVersao = CampoVersao.Criar(1, 42, "texto", EstadoPreenchimento.Opcional);

        campoVersao.AtualizarComportamento(
            tipo: "numero",
            estadoPreenchimento: EstadoPreenchimento.Obrigatorio,
            ordem: 5,
            tamanho: "10",
            mascara: "###",
            dominio: "1-10",
            condicao: "quando X",
            exemplo: "5",
            notaPreenchimento: "nota",
            destinoXml: "tomador/numero",
            notaTransformacao: "transformar");

        campoVersao.Tipo.Should().Be("numero");
        campoVersao.EstadoPreenchimento.Should().Be(EstadoPreenchimento.Obrigatorio);
        campoVersao.Ordem.Should().Be(5);
        campoVersao.Tamanho.Should().Be("10");
        campoVersao.Mascara.Should().Be("###");
        campoVersao.Dominio.Should().Be("1-10");
        campoVersao.Condicao.Should().Be("quando X");
        campoVersao.Exemplo.Should().Be("5");
        campoVersao.NotaPreenchimento.Should().Be("nota");
        campoVersao.DestinoXml.Should().Be("tomador/numero");
        campoVersao.NotaTransformacao.Should().Be("transformar");
    }
}
