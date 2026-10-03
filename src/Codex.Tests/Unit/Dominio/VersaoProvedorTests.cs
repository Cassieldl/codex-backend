using Codex.Domain.Entities;
using FluentAssertions;

namespace Codex.Tests.Unit.Dominio;

/// <summary>
/// Demanda 008-campos-emissor-nacional (D1) — <see cref="VersaoProvedor.TrocarVersaoLayout"/>. A
/// entidade só troca o próprio valor: ela não conhece <c>CampoVersao</c> nenhum, então a regra "só
/// pode trocar se a versão não tiver campos cadastrados" vive no service
/// (<c>VersaoProvedorServicoInterno.TrocarVersaoLayoutAsync</c>), nunca aqui.
/// </summary>
public sealed class VersaoProvedorTests
{
    [Fact]
    public void TrocarVersaoLayout_ComIdValido_AtualizaOValor()
    {
        var versao = VersaoProvedor.Criar(provedorId: 1, versaoLayoutId: 10, rotulo: "Versao 1");

        versao.TrocarVersaoLayout(20);

        versao.VersaoLayoutId.Should().Be(20);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TrocarVersaoLayout_ComIdInvalido_Lanca(int versaoLayoutIdInvalido)
    {
        var versao = VersaoProvedor.Criar(provedorId: 1, versaoLayoutId: 10, rotulo: "Versao 1");

        var acao = () => versao.TrocarVersaoLayout(versaoLayoutIdInvalido);

        acao.Should().Throw<ArgumentOutOfRangeException>();
        versao.VersaoLayoutId.Should().Be(10, because: "a troca inválida não pode ter efeito parcial");
    }
}
