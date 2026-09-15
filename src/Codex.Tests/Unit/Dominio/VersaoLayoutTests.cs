using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using FluentAssertions;

namespace Codex.Tests.Unit.Dominio;

public sealed class VersaoLayoutTests
{
    [Fact]
    public void Criar_NasceEmPrevistaENaoPublicada()
    {
        var versao = VersaoLayout.Criar("Layout 2026.1", 1);

        versao.Estado.Should().Be(EstadoVersaoLayout.Prevista);
        versao.Publicado.Should().BeFalse();
        versao.Ordem.Should().Be(1);
        versao.Slug.Should().Be("layout-2026-1");
    }

    [Fact]
    public void MarcarVigente_APartirDePrevista_Sucede()
    {
        var versao = VersaoLayout.Criar("Layout", 1);

        versao.MarcarVigente();

        versao.Estado.Should().Be(EstadoVersaoLayout.Vigente);
    }

    [Fact]
    public void MarcarVigente_APartirDeVigenteOuAnterior_Lanca()
    {
        var versao = VersaoLayout.Criar("Layout", 1);
        versao.MarcarVigente();

        var acao = () => versao.MarcarVigente();

        acao.Should().Throw<TransicaoDeEstadoInvalidaException>();
    }

    [Fact]
    public void MarcarAnterior_APartirDeVigente_Sucede()
    {
        var versao = VersaoLayout.Criar("Layout", 1);
        versao.MarcarVigente();

        versao.MarcarAnterior();

        versao.Estado.Should().Be(EstadoVersaoLayout.Anterior);
    }

    [Fact]
    public void MarcarAnterior_APartirDePrevista_Lanca()
    {
        var versao = VersaoLayout.Criar("Layout", 1);

        var acao = () => versao.MarcarAnterior();

        acao.Should().Throw<TransicaoDeEstadoInvalidaException>();
    }

    [Fact]
    public void MarcarAnterior_APartirDeAnterior_Lanca()
    {
        var versao = VersaoLayout.Criar("Layout", 1);
        versao.MarcarVigente();
        versao.MarcarAnterior();

        var acao = () => versao.MarcarAnterior();

        acao.Should().Throw<TransicaoDeEstadoInvalidaException>();
    }

    [Fact]
    public void PublicarEDespublicar_SaoIndependentesDeEstado()
    {
        var versao = VersaoLayout.Criar("Layout", 1);

        // Prevista + Publicado
        versao.Publicar();
        versao.Estado.Should().Be(EstadoVersaoLayout.Prevista);
        versao.Publicado.Should().BeTrue();

        // Avança Estado sem afetar Publicado
        versao.MarcarVigente();
        versao.Publicado.Should().BeTrue();
        versao.Estado.Should().Be(EstadoVersaoLayout.Vigente);

        // Despublica sem afetar Estado
        versao.Despublicar();
        versao.Estado.Should().Be(EstadoVersaoLayout.Vigente);
        versao.Publicado.Should().BeFalse();
    }

    [Fact]
    public void ReordenarPara_AtualizaOrdem()
    {
        var versao = VersaoLayout.Criar("Layout", 1);

        versao.ReordenarPara(5);

        versao.Ordem.Should().Be(5);
    }
}
