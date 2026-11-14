using Codex.Infrastructure.Busca;
using FluentAssertions;

namespace Codex.Tests.Unit.Busca;

/// <summary>Resumo da solução sugerida que o card de erro mostra na busca.</summary>
public sealed class ResumoSolucaoErroTests
{
    [Fact]
    public void Derivar_SolucaoComVariasLinhas_DevolveSoAPrimeiraNaoVazia()
    {
        var solucao = "\n   \r\n  Confira o CNPJ do prestador.  \r\nDepois reenvie o RPS.\nTerceira linha.";

        ResumoSolucaoErro.Derivar(solucao).Should().Be("Confira o CNPJ do prestador.");
    }

    [Fact]
    public void Derivar_PrimeiraLinhaAcimaDe160_CortaNaUltimaPalavraInteiraComReticencias()
    {
        var palavras = string.Join(' ', Enumerable.Range(0, 40).Select(i => $"passo{i}"));

        var resumo = ResumoSolucaoErro.Derivar($"{palavras}\nSegunda linha.");

        resumo.Should().NotBeNull();
        resumo!.Length.Should().BeLessOrEqualTo(160);
        resumo.Should().EndWith("…");
        resumo[..^1].Split(' ').Should().OnlyContain(palavra => palavras.Split(' ').Contains(palavra),
            because: "o corte não parte palavra ao meio");
        resumo.Should().NotContain("Segunda");
    }

    [Fact]
    public void Derivar_PrimeiraLinhaCom160Exatos_VoltaInteiraSemReticencias()
    {
        var linha = new string('a', 80) + " " + new string('b', 79);

        ResumoSolucaoErro.Derivar(linha).Should().Be(linha);
    }

    [Fact]
    public void Derivar_PrimeiraLinhaCom161_CortaComReticencias()
    {
        var primeiraPalavra = new string('a', 80);
        var linha = primeiraPalavra + " " + new string('b', 80);

        ResumoSolucaoErro.Derivar(linha).Should().Be(primeiraPalavra + "…");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n \r\n\t ")]
    public void Derivar_SemSolucao_DevolveNulo(string? solucao)
    {
        ResumoSolucaoErro.Derivar(solucao).Should().BeNull();
    }
}
