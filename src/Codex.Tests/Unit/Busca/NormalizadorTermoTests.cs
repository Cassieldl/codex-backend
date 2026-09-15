using Codex.Infrastructure.Busca;
using FluentAssertions;

namespace Codex.Tests.Unit.Busca;

/// <summary>
/// Prova que <see cref="NormalizadorTermo"/> monta parâmetros de query (tokens candidatos,
/// termo aparado/cortado) sem reimplementar <c>codex.normalizar_busca</c> em C# — nenhum teste
/// aqui verifica lower/unaccent, porque essa responsabilidade nunca sai do Postgres
/// (modelo-dados.md §5.5).
/// </summary>
public sealed class NormalizadorTermoTests
{
    [Fact]
    public void ExtrairTokensCandidatos_PreservaGrafiaOriginal_NuncaAplicaLowerOuUnaccent()
    {
        var tokens = NormalizadorTermo.ExtrairTokensCandidatos("Aliquota/ISS rejeicao");

        tokens.Should().Contain("Aliquota", because: "a grafia original é o que a consulta substitui depois — não é papel deste utilitário normalizar acento/caixa");
        tokens.Should().Contain("rejeicao");
    }

    [Fact]
    public void ExtrairTokensCandidatos_RespeitaTamanhoMinimo_ExcluiTokensCurtos()
    {
        var tokens = NormalizadorTermo.ExtrairTokensCandidatos("E-124 ISS ab", tamanhoMinimo: 4);

        tokens.Should().NotContain("ISS", because: "tem 3 caracteres, abaixo do mínimo padrão de 4 (arquitetura.md §6.5)");
        tokens.Should().NotContain("E", because: "separado por hífen, sobra com 1 caractere");
        tokens.Should().NotContain("124", because: "tem 3 caracteres, abaixo do mínimo");
        tokens.Should().NotContain("ab");
    }

    [Fact]
    public void ExtrairTokensCandidatos_SeparaPorSeparadoresColados_MesmaClasseDoNormalizarBusca()
    {
        var tokens = NormalizadorTermo.ExtrairTokensCandidatos("Aliquota/ISS-Retido", tamanhoMinimo: 4);

        tokens.Should().Contain("Aliquota");
        tokens.Should().Contain("Retido");
        tokens.Should().NotContain("Aliquota/ISS-Retido", because: "o separador colado precisa quebrar em tokens distintos, senão a busca por 'aliquota' isolado nunca acharia correção");
    }

    [Fact]
    public void ExtrairTokensCandidatos_RemoveDuplicatasCaseInsensitive()
    {
        var tokens = NormalizadorTermo.ExtrairTokensCandidatos("Rejeicao rejeicao REJEICAO");

        tokens.Should().HaveCount(1);
    }

    [Fact]
    public void ExtrairTokensCandidatos_SemCandidatos_RetornaListaVazia()
    {
        var tokens = NormalizadorTermo.ExtrairTokensCandidatos("ab cd ef");

        tokens.Should().BeEmpty();
    }

    [Fact]
    public void LimitarTamanho_AparaEspacosNasPontas()
    {
        var resultado = NormalizadorTermo.LimitarTamanho("   termo com espaco   ");

        resultado.Should().Be("termo com espaco");
    }

    [Fact]
    public void LimitarTamanho_CortaNoTamanhoMaximo_ProtecaoDeCusto()
    {
        var termoLongo = new string('a', 400);

        var resultado = NormalizadorTermo.LimitarTamanho(termoLongo, tamanhoMaximo: 300);

        resultado.Should().HaveLength(300);
    }

    [Fact]
    public void LimitarTamanho_TermoDentroDoLimite_NaoAltera()
    {
        var resultado = NormalizadorTermo.LimitarTamanho("termo curto", tamanhoMaximo: 300);

        resultado.Should().Be("termo curto");
    }

    /// <summary>
    /// O substituto vem do léxico, que é alimentado com conteúdo editorial e admite <c>$</c>
    /// (<c>codex.normalizar_busca</c> não o remove). Sem escapar o REPLACEMENT, <c>$&amp;</c> e
    /// <c>$1</c> são interpretados pelo <c>Regex</c> e o leitor recebe um termo que ninguém
    /// digitou — nem o dele, nem o do léxico.
    /// </summary>
    [Theory]
    [InlineData("$&", "erro $&")]
    [InlineData("$&$&$&", "erro $&$&$&")]
    [InlineData("$1abc", "erro $1abc")]
    [InlineData("$$", "erro $$")]
    [InlineData("$`", "erro $`")]
    public void SubstituirTokenPorLiteral_SubstitutoComSintaxeDeRegex_EntraLiteral(string substituto, string esperado)
    {
        var resultado = NormalizadorTermo.SubstituirTokenPorLiteral("erro fatura", "fatura", substituto);

        resultado.Should().Be(esperado);
    }

    [Fact]
    public void SubstituirTokenPorLiteral_TokenComMetacaractere_NaoEInterpretadoComoPadrao()
    {
        var resultado = NormalizadorTermo.SubstituirTokenPorLiteral("codigo e.124 aqui", "e.124", "e-124");

        resultado.Should().Be("codigo e-124 aqui",
            because: "o ponto do token tem de casar literalmente — como padrão de regex ele casaria 'e5124' também");
    }

    [Fact]
    public void SubstituirTokenPorLiteral_IgnoraCaixaDoToken_ETrocaTodasAsOcorrencias()
    {
        var resultado = NormalizadorTermo.SubstituirTokenPorLiteral("Fatura e fatura", "fatura", "nota");

        resultado.Should().Be("nota e nota");
    }
}
