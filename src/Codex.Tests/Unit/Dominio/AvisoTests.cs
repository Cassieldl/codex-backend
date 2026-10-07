using System.Reflection;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using FluentAssertions;

namespace Codex.Tests.Unit.Dominio;

/// <summary>Prova do XOR de escopo de U8: impossível, por construção em C#, ter os dois FKs preenchidos ou os dois nulos.</summary>
public sealed class AvisoTests
{
    [Fact]
    public void CriarDeProvedor_NuncaPreencheVersaoProvedorId()
    {
        var aviso = Aviso.CriarDeProvedor(10, Severidade.Informativo, "Título", "Corpo");

        aviso.ProvedorId.Should().Be(10);
        aviso.VersaoProvedorId.Should().BeNull();
    }

    [Fact]
    public void CriarDeVersao_NuncaPreencheProvedorId()
    {
        var aviso = Aviso.CriarDeVersao(20, Severidade.Alerta, "Título", "Corpo");

        aviso.VersaoProvedorId.Should().Be(20);
        aviso.ProvedorId.Should().BeNull();
    }

    [Fact]
    public void NaoExisteConstrutorPublico_UnicaSuperficieDeCriacaoSaoOsDoisFactoryMethods()
    {
        var construtoresPublicos = typeof(Aviso).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        construtoresPublicos.Should().BeEmpty(
            because: "a única forma de criar um Aviso precisa ser CriarDeProvedor/CriarDeVersao — nenhum construtor genérico que aceite os dois FKs opcionais");
    }

    [Fact]
    public void FactoryMethods_SaoExatamenteDois()
    {
        var metodosCriar = typeof(Aviso)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name.StartsWith("CriarDe"))
            .ToList();

        metodosCriar.Should().HaveCount(2);
        metodosCriar.Select(m => m.Name).Should().BeEquivalentTo(nameof(Aviso.CriarDeProvedor), nameof(Aviso.CriarDeVersao));
    }

    [Fact]
    public void SubstituirCamposAfetados_EmAvisoDeProvedor_Lanca()
    {
        var aviso = Aviso.CriarDeProvedor(10, Severidade.Critico, "Título", "Corpo");

        var acao = () => aviso.SubstituirCamposAfetados([1, 2, 3]);

        acao.Should().Throw<RegraDeNegocioException>();
    }

    [Fact]
    public void SubstituirCamposAfetados_EmAvisoDeVersao_Sucede()
    {
        var aviso = Aviso.CriarDeVersao(20, Severidade.Critico, "Título", "Corpo");

        aviso.SubstituirCamposAfetados([1, 2, 3]);

        aviso.CamposAfetadosIds.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public void SubstituirCamposAfetados_EmAvisoDeVersao_DescartaRepetidosEOrdenaPorId()
    {
        var aviso = Aviso.CriarDeVersao(20, Severidade.Critico, "Título", "Corpo");

        aviso.SubstituirCamposAfetados([9, 3, 9, 5, 3]);

        aviso.CamposAfetadosIds.Should().Equal(3, 5, 9);
    }

    [Fact]
    public void SubstituirCamposAfetados_EmAvisoDeProvedor_ComListaVazia_Sucede()
    {
        var aviso = Aviso.CriarDeProvedor(10, Severidade.Critico, "Título", "Corpo");

        var acao = () => aviso.SubstituirCamposAfetados([]);

        acao.Should().NotThrow(because: "o formulário de aviso de provedor envia lista vazia ao editar");
        aviso.CamposAfetadosIds.Should().BeEmpty();
    }

    [Fact]
    public void Criar_ComVigenciaInicioNula_UsaDataCorrente()
    {
        var aviso = Aviso.CriarDeProvedor(1, Severidade.Informativo, "T", "C");

        aviso.VigenciaInicio.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_LancaQuandoTituloOuCorpoInvalido(string invalido)
    {
        var acaoTitulo = () => Aviso.CriarDeProvedor(1, Severidade.Informativo, invalido, "corpo válido");
        var acaoCorpo = () => Aviso.CriarDeProvedor(1, Severidade.Informativo, "título válido", invalido);

        acaoTitulo.Should().Throw<ArgumentException>();
        acaoCorpo.Should().Throw<ArgumentException>();
    }
}
