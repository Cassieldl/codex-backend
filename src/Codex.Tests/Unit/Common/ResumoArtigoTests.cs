using Codex.Application.Common;
using FluentAssertions;

namespace Codex.Tests.Unit.Common;

/// <summary>Derivação pura do resumo de artigo, sem banco.</summary>
public sealed class ResumoArtigoTests
{
    [Fact]
    public void Derivar_ParagrafoSimples_RetornaOTextoInteiro()
    {
        var resultado = ResumoArtigo.Derivar("Este é o primeiro parágrafo do artigo.");

        resultado.Should().Be("Este é o primeiro parágrafo do artigo.");
    }

    [Fact]
    public void Derivar_PegaSoOPrimeiroParagrafo_IgnoraOsSeguintes()
    {
        var resultado = ResumoArtigo.Derivar("Primeiro parágrafo.\n\nSegundo parágrafo, nunca deveria aparecer.");

        resultado.Should().Be("Primeiro parágrafo.");
    }

    [Theory]
    [InlineData("Texto com **negrito** no meio.", "Texto com negrito no meio.")]
    [InlineData("Texto com __negrito alternativo__ no meio.", "Texto com negrito alternativo no meio.")]
    [InlineData("Texto com *itálico* no meio.", "Texto com itálico no meio.")]
    [InlineData("Texto com _itálico alternativo_ no meio.", "Texto com itálico alternativo no meio.")]
    [InlineData("Texto com `código` no meio.", "Texto com código no meio.")]
    [InlineData("Texto com [um link](https://exemplo.com) no meio.", "Texto com um link no meio.")]
    public void Derivar_RemoveMarcacaoInlineBasica(string corpo, string esperado)
    {
        var resultado = ResumoArtigo.Derivar(corpo);

        resultado.Should().Be(esperado);
    }

    [Fact]
    public void Derivar_TruncaEmDuzentosCaracteres_ComReticencias()
    {
        var paragrafoLongo = new string('a', 250);

        var resultado = ResumoArtigo.Derivar(paragrafoLongo);

        resultado.Should().NotBeNull();
        resultado!.Length.Should().Be(201, because: "200 caracteres + reticências");
        resultado.Should().EndWith("…");
    }

    [Fact]
    public void Derivar_TextoComDuzentosCaracteresOuMenos_NaoTrunca()
    {
        var paragrafoExato = new string('a', 200);

        var resultado = ResumoArtigo.Derivar(paragrafoExato);

        resultado.Should().Be(paragrafoExato);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Derivar_CorpoVazioOuEmBranco_RetornaNull(string? corpo)
    {
        var resultado = ResumoArtigo.Derivar(corpo);

        resultado.Should().BeNull();
    }

    [Fact]
    public void Derivar_ApenasCabecalhoSemTexto_RetornaNull()
    {
        var resultado = ResumoArtigo.Derivar("#");

        resultado.Should().BeNull();
    }

    [Fact]
    public void Derivar_RemoveMarcacaoDeCabecalho()
    {
        var resultado = ResumoArtigo.Derivar("## Título da seção");

        resultado.Should().Be("Título da seção");
    }

    [Fact]
    public void Derivar_ComBlocoResumo_UsaOTextoDeleSemMarcacao()
    {
        var corpo = string.Join('\n',
            "![Tela](/imagens/geral/tela.png)",
            "",
            "Introdução que perde para o resumo.",
            "",
            "> [!RESUMO]",
            "> O **InvoiCy** valida o RPS",
            "> antes de [enviar](/artigos/envio).",
            "",
            "Resto.");

        var resultado = ResumoArtigo.Derivar(corpo);

        resultado.Should().Be("O InvoiCy valida o RPS antes de enviar.");
    }

    [Fact]
    public void Derivar_SemBlocoResumo_PulaImagemFluxoEBlockquoteDeMarcador()
    {
        var corpo = string.Join('\n',
            "![Fluxo do RPS](/imagens/geral/fluxo.png \"Legenda\")",
            "",
            "> [!DICA]",
            "> Confira o lote antes.",
            "",
            "```fluxo",
            "Envio: o RPS sai do InvoiCy",
            "```",
            "",
            "Primeiro parágrafo de verdade, com ![ícone](/imagens/geral/icone.png) no meio.");

        var resultado = ResumoArtigo.Derivar(corpo);

        resultado.Should().Be("Primeiro parágrafo de verdade, com no meio.");
    }

    [Fact]
    public void Derivar_CercaDentroDoDetalhes_NaoSoltaOFimDoBlocoComoResumo()
    {
        var corpo = string.Join('\n',
            "> [!DETALHES] Código de exemplo",
            "> ```csharp",
            "> var hash = Md5(xml);",
            "> ```",
            "> **Java**",
            "",
            "Texto do artigo.");

        var resultado = ResumoArtigo.Derivar(corpo);

        resultado.Should().Be("Texto do artigo.");
    }

    [Fact]
    public void Derivar_CorpoSoComBlocosVisuais_RetornaNull()
    {
        var corpo = "> [!ATENCAO]\n> Só aviso.\n\n![Tela](/imagens/geral/tela.png)";

        var resultado = ResumoArtigo.Derivar(corpo);

        resultado.Should().BeNull();
    }

    [Fact]
    public void Derivar_ResumoLongo_TruncaEmDuzentosCaracteres()
    {
        var corpo = "> [!RESUMO]\n> " + new string('a', 250);

        var resultado = ResumoArtigo.Derivar(corpo);

        resultado.Should().Be(new string('a', 200) + "…");
    }
}
