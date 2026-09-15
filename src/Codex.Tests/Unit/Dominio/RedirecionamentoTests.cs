using Codex.Domain.Entities;
using Codex.Domain.Enums;
using FluentAssertions;

namespace Codex.Tests.Unit.Dominio;

/// <summary>
/// Achado <c>BAIXO-B07</c>: <c>Redirecionamento.Destino</c> vira o header <c>Location</c> de um
/// 308 na leitura pública, e a validação existia SÓ no sink (controllers públicos) enquanto o
/// comentário afirmava que também existia na escrita. Estes testes provam a garantia na origem —
/// quem implementar a troca de slug (002) não passa pelos controllers, passa por aqui.
///
/// <para>
/// O caminho do 308 está inativo na 001 (nada grava <see cref="Redirecionamento"/> fora de teste),
/// então esta é a única prova viva de que a origem valida.
/// </para>
/// </summary>
public sealed class RedirecionamentoTests
{
    private const string DestinoValido = "/provedores/acme";

    [Theory]
    [InlineData("https://evil.com/x")]
    [InlineData("http://evil.com")]
    [InlineData("//evil.com/x")]
    [InlineData("/\\evil.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("provedores/acme")]
    [InlineData("/\t/evil.com")]
    public void Criar_ComDestinoQueNaoEhPathInternoSeguro_Lanca(string destino)
    {
        var act = () => Redirecionamento.Criar("/provedores/antigo", destino, TipoItemIndexado.provedor);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*path interno seguro*")
            .And.ParamName.Should().Be("destino");
    }

    [Theory]
    [InlineData("https://evil.com/x")]
    [InlineData("//evil.com/x")]
    [InlineData("/\t/evil.com")]
    public void AtualizarDestino_ComDestinoQueNaoEhPathInternoSeguro_Lanca(string novoDestino)
    {
        var redirecionamento = Redirecionamento.Criar("/provedores/antigo", DestinoValido, TipoItemIndexado.provedor);

        var act = () => redirecionamento.AtualizarDestino(novoDestino);

        act.Should().Throw<ArgumentException>().WithMessage("*path interno seguro*");
        redirecionamento.Destino.Should().Be(DestinoValido, because: "destino inválido nunca pode ser gravado nem parcialmente");
    }

    [Fact]
    public void Criar_ComPathInternoRelativo_Aceita()
    {
        var redirecionamento = Redirecionamento.Criar(
            "/provedores/antigo", "/provedores/acme/v1", TipoItemIndexado.versaoprovedor);

        redirecionamento.Origem.Should().Be("/provedores/antigo");
        redirecionamento.Destino.Should().Be("/provedores/acme/v1");
        redirecionamento.TipoItem.Should().Be(TipoItemIndexado.versaoprovedor);
    }

    [Fact]
    public void AtualizarDestino_ComPathInternoRelativo_Atualiza()
    {
        var redirecionamento = Redirecionamento.Criar("/erros/antigo", "/erros/intermediario", TipoItemIndexado.erro);

        redirecionamento.AtualizarDestino("/erros/final");

        redirecionamento.Destino.Should().Be("/erros/final");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComDestinoVazio_LancaMensagemDeObrigatoriedade(string destino)
    {
        var act = () => Redirecionamento.Criar("/provedores/antigo", destino, TipoItemIndexado.provedor);

        act.Should().Throw<ArgumentException>().WithMessage("*obrigatório*");
    }
}
