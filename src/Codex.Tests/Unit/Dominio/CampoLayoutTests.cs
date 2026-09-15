using System.Reflection;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using FluentAssertions;

namespace Codex.Tests.Unit.Dominio;

public sealed class CampoLayoutTests
{
    [Fact]
    public void Criar_DerivaSlugDoCaminhoXml_NuncaDoNome()
    {
        var campo = CampoLayout.Criar(1, nome: "Número", caminhoXml: "tomador/numero", significado: "sig", ordem: 1);

        campo.Slug.Should().Be("tomador-numero");
    }

    [Fact]
    public void Criar_CaminhosDiferentesComMesmoNomeFolha_GeramSlugsDiferentes()
    {
        var campoTomador = CampoLayout.Criar(1, "Número", "tomador/numero", "sig", 1);
        var campoPrestador = CampoLayout.Criar(1, "Número", "prestador/numero", "sig", 2);

        campoTomador.Slug.Should().NotBe(campoPrestador.Slug);
    }

    [Fact]
    public void AtualizarNome_NuncaMudaSlug()
    {
        var campo = CampoLayout.Criar(1, "Número", "tomador/numero", "sig", 1);
        var slugOriginal = campo.Slug;

        campo.AtualizarNome("Número do Tomador");

        campo.Nome.Should().Be("Número do Tomador");
        campo.Slug.Should().Be(slugOriginal);
    }

    [Fact]
    public void Descontinuar_QuandoOrdemDaVersaoDescontinuacaoEMaior_Sucede()
    {
        var versaoOrigem = VersaoLayout.Criar("Origem", 1);
        var versaoDescontinuacao = VersaoLayout.Criar("Descontinuacao", 2);
        var campo = CriarComOrigemCarregada(versaoOrigem);

        campo.Descontinuar(versaoDescontinuacao);

        campo.Estado.Should().Be(EstadoCampoLayout.Descontinuado);
        campo.VersaoLayoutDescontinuadoId.Should().Be(versaoDescontinuacao.Id);
    }

    [Fact]
    public void Descontinuar_QuandoOrdemIgual_Lanca()
    {
        var versaoOrigem = VersaoLayout.Criar("Origem", 5);
        var versaoMesmaOrdem = VersaoLayout.Criar("Mesma ordem", 5);
        var campo = CriarComOrigemCarregada(versaoOrigem);

        var acao = () => campo.Descontinuar(versaoMesmaOrdem);

        acao.Should().Throw<RegraDeNegocioException>();
    }

    [Fact]
    public void Descontinuar_QuandoOrdemMenor_Lanca()
    {
        var versaoOrigem = VersaoLayout.Criar("Origem", 5);
        var versaoAnterior = VersaoLayout.Criar("Anterior", 3);
        var campo = CriarComOrigemCarregada(versaoOrigem);

        var acao = () => campo.Descontinuar(versaoAnterior);

        acao.Should().Throw<RegraDeNegocioException>();
    }

    [Fact]
    public void Descontinuar_SemVersaoLayoutOrigemCarregada_Lanca()
    {
        var campo = CampoLayout.Criar(1, "Número", "tomador/numero", "sig", 1);
        var versaoDescontinuacao = VersaoLayout.Criar("Descontinuacao", 2);

        var acao = () => campo.Descontinuar(versaoDescontinuacao);

        acao.Should().Throw<InvalidOperationException>(
            because: "VersaoLayoutOrigem precisa estar carregada (Include) antes de descontinuar");
    }

    /// <summary>
    /// Simula o que o EF faz via fixup de navegação ao usar <c>Include</c> — necessário aqui
    /// porque este é um teste de domínio puro, sem DbContext.
    /// </summary>
    private static CampoLayout CriarComOrigemCarregada(VersaoLayout origem)
    {
        var campo = CampoLayout.Criar(origem.Id, "Número", $"tomador/numero-{Guid.NewGuid():N}", "sig", 1);

        typeof(CampoLayout)
            .GetProperty(nameof(CampoLayout.VersaoLayoutOrigem))!
            .SetValue(campo, origem);

        return campo;
    }
}
