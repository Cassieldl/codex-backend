using Codex.Application.Common;
using FluentAssertions;

namespace Codex.Tests.Unit.Common;

/// <summary>Texto do corpo que vai para o índice de busca, sem a marcação visual.</summary>
public sealed class MarcacaoCorpoArtigoTests
{
    [Fact]
    public void TextoIndexavel_CorpoSemMarcacaoVisual_FicaIgual()
    {
        const string corpo = "## Envio\n\nO **InvoiCy** monta o [lote](/artigos/lote).";

        MarcacaoCorpoArtigo.TextoIndexavel(corpo).Should().Be(corpo);
    }

    [Fact]
    public void TextoIndexavel_TiraMarcadorEMantemOTituloDoDetalhes()
    {
        var corpo = "> [!DICA]\n> Confira o lote.\n\n> [!DETALHES] Quando o lote volta\n> Veja o protocolo.";

        var texto = MarcacaoCorpoArtigo.TextoIndexavel(corpo);

        texto.Should().Be("> \n> Confira o lote.\n\n>  Quando o lote volta\n> Veja o protocolo.");
    }

    [Fact]
    public void TextoIndexavel_TrocaImagemPorAltELegendaSemOCaminho()
    {
        var corpo = "Antes ![Tela do lote](/imagens/emissao-em-lote/tela.png \"Menu Lote\") e ![Sem legenda](/imagens/a/b.png).";

        var texto = MarcacaoCorpoArtigo.TextoIndexavel(corpo);

        texto.Should().Be("Antes Tela do lote Menu Lote e Sem legenda.");
    }

    [Fact]
    public void TextoIndexavel_TiraAsCercasEMantemAsEtapasDoFluxo()
    {
        var corpo = "Passos:\n\n```fluxo\nEnvio: o InvoiCy monta o lote\nRetorno: a prefeitura responde\n```";

        var texto = MarcacaoCorpoArtigo.TextoIndexavel(corpo);

        texto.Should().Be("Passos:\n\nEnvio: o InvoiCy monta o lote\nRetorno: a prefeitura responde");
    }

    [Fact]
    public void TextoIndexavel_TiraCercaDentroDoBlockquoteDeDetalhes()
    {
        var corpo = "> [!DETALHES] Código de exemplo\n> **C#**\n>\n> ```csharp\n> var hash = Md5(xml);\n> ```";

        var texto = MarcacaoCorpoArtigo.TextoIndexavel(corpo);

        texto.Should().Be(">  Código de exemplo\n> **C#**\n>\n> var hash = Md5(xml);");
    }

    [Fact]
    public async Task ImagemSemFecharParentese_TerminaRapido_NoResumoENoIndice()
    {
        var corpoPatologico = string.Concat(Enumerable.Repeat("![](a", 10_000));

        // Roda fora do teste para a regressão falhar no prazo em vez de travar a suíte.
        var processamento = Task.Run(() =>
        {
            ResumoArtigo.Derivar(corpoPatologico);
            MarcacaoCorpoArtigo.TextoIndexavel(corpoPatologico);
        });

        var primeiro = await Task.WhenAny(processamento, Task.Delay(TimeSpan.FromSeconds(2)));

        primeiro.Should().BeSameAs(processamento,
            because: "imagem sem o ) final não pode custar tempo cúbico num GET público");
    }
}
