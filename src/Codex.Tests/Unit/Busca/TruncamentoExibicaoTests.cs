using System.Text;
using Codex.Infrastructure.Busca;
using FluentAssertions;

namespace Codex.Tests.Unit.Busca;

/// <summary>
/// Demanda 008-campos-emissor-nacional (D3) — <see cref="TruncamentoExibicao"/> é o único ponto
/// que corta texto de EXIBIÇÃO (titulo/subtitulo de <c>codex.itensbusca</c>) para caber na coluna.
/// </summary>
public sealed class TruncamentoExibicaoTests
{
    [Fact]
    public void Truncar_TextoAcimaDoLimite_CortaNaUltimaPalavraInteira()
    {
        var texto = "Alíquota aplicável ao serviço prestado pelo contribuinte optante do regime simplificado";

        var resultado = TruncamentoExibicao.Truncar(texto, 40);

        resultado.Should().EndWith("…");
        resultado.Should().NotContain("prest…", because: "o corte não pode partir uma palavra ao meio — só a reticência vem colada");
        var semReticencias = resultado[..^1];
        texto.Should().StartWith(semReticencias,
            because: "o que sobra antes das reticências é um PREFIXO exato do texto original, nunca uma palavra cortada");
        semReticencias.Should().NotEndWith(" ", because: "a última palavra inteira não deixa espaço solto antes das reticências");
    }

    [Theory]
    [InlineData(300)]
    [InlineData(80)]
    [InlineData(10)]
    [InlineData(1)]
    public void Truncar_ResultadoNuncaExcedeOLimite_IndependenteDoTamanhoDoTexto(int limite)
    {
        var texto = string.Join(' ', Enumerable.Range(0, 200).Select(i => $"palavra{i}"));

        var resultado = TruncamentoExibicao.Truncar(texto, limite);

        resultado.Length.Should().BeLessOrEqualTo(limite);
    }

    [Fact]
    public void Truncar_TextoSemEspaco_CortaCruENuncaExcedeOLimite()
    {
        var textoDeUmaPalavraSo = new string('a', 500);

        var resultado = TruncamentoExibicao.Truncar(textoDeUmaPalavraSo, 300);

        resultado.Length.Should().Be(300, because: "sem espaço nenhum não há palavra inteira para preservar — o corte é cru, mas ainda cabe no limite");
        resultado.Should().EndWith("…");
    }

    [Fact]
    public void Truncar_TextoDentroDoLimite_DevolveIntactoSemReticencias()
    {
        var texto = new string('b', 300);

        var resultado = TruncamentoExibicao.Truncar(texto, 300);

        resultado.Should().Be(texto);
        resultado.Should().NotEndWith("…");
    }

    [Fact]
    public void Truncar_TextoUmCaractereAcimaDoLimite_Trunca()
    {
        var texto = new string('c', 301);

        var resultado = TruncamentoExibicao.Truncar(texto, 300);

        resultado.Length.Should().BeLessOrEqualTo(300);
        resultado.Should().NotBe(texto);
    }

    /// <summary>
    /// S-02 (auditoria de segurança, D3) — reprodutor exato do auditor: 298 'a' + 😀 (par
    /// substituto <c>😀</c>) + "bbbb", sem espaço nenhum. O corte cru por posição (índice
    /// 299 dos 300) cai bem na metade alta do emoji — sem o recuo, o resultado terminaria sozinho em
    /// <c>\uD83D</c>, que não é um caractere Unicode válido e faz o Npgsql (UTF-8 estrito) lançar
    /// <c>EncoderFallbackException</c> ao persistir.
    /// </summary>
    [Fact]
    public void Truncar_ComEmojiExatamenteNaFronteira_NuncaTerminaEmMetadeDeSurrogate()
    {
        var texto = new string('a', 298) + "😀" + "bbbb";

        var resultado = TruncamentoExibicao.Truncar(texto, 300);

        resultado.Length.Should().BeLessOrEqualTo(300);
        char.IsHighSurrogate(resultado[^1]).Should().BeFalse(
            because: "uma metade alta de par substituto sozinha no fim não é um caractere Unicode válido");

        var acao = () => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetBytes(resultado);
        acao.Should().NotThrow(because: "é exatamente o encoder estrito que o Npgsql usa para gravar — texto com metade de surrogate solta lança aqui");
    }
}
