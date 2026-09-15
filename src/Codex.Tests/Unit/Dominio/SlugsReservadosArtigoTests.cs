using Codex.Domain.Constantes;
using FluentAssertions;

namespace Codex.Tests.Unit.Dominio;

/// <summary>
/// A noção de "artigo reservado" (D2/§5.C de arquitetura.md) mora em código, nunca em coluna de
/// banco nem em <c>appsettings</c> — este é o teste que fecha esse contrato. Ver
/// <c>ArtigoIndexacaoBuscaTests</c> para a prova de que <see cref="SlugsReservadosArtigo.RotaCanonica"/>
/// é de fato a URL que chega até <c>codex.itensbusca</c>.
/// </summary>
public sealed class SlugsReservadosArtigoTests
{
    [Fact]
    public void EhReservado_SlugComoFunciona_RetornaTrue()
    {
        SlugsReservadosArtigo.EhReservado(SlugsReservadosArtigo.ComoFunciona).Should().BeTrue();
    }

    [Theory]
    [InlineData("como-funciona-para-provedores")]
    [InlineData("outro-artigo-qualquer")]
    [InlineData("")]
    public void EhReservado_SlugDiferente_RetornaFalse(string slug)
    {
        SlugsReservadosArtigo.EhReservado(slug).Should().BeFalse();
    }

    [Fact]
    public void RotaCanonica_SlugReservado_RetornaRotaFixaSemPrefixoArtigos()
    {
        SlugsReservadosArtigo.RotaCanonica(SlugsReservadosArtigo.ComoFunciona).Should().Be("/como-funciona");
    }

    [Fact]
    public void RotaCanonica_SlugNaoReservado_RetornaPrefixoArtigos()
    {
        SlugsReservadosArtigo.RotaCanonica("guia-de-erros").Should().Be("/artigos/guia-de-erros");
    }
}
