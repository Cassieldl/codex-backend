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
    public void Derivar_CorpoSoComTituloDeSecao_RetornaNull()
    {
        var resultado = ResumoArtigo.Derivar("## Título da seção");

        resultado.Should().BeNull(because: "título de seção nunca vira resumo");
    }

    // Começos reais de artigos da carga: o resumo saía "Resumo", "O que é", "Mensagem" e "Fórmula".
    [Theory]
    [InlineData(
        "## Resumo\n\nO documento fica **em conflito** quando o InvoiCy **não consegue confirmar** se a prefeitura autorizou a nota.\n\n## Risco\n\nReenviar o RPS sem conferir.",
        "O documento fica em conflito quando o InvoiCy não consegue confirmar se a prefeitura autorizou a nota.")]
    [InlineData(
        "> Fonte: [Retenção de ISS](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/RetencaodeISS/) (13/02/2026).\n\n## O que é\n\nNa retenção, o **tomador** recolhe o ISS no lugar do prestador.",
        "Na retenção, o tomador recolhe o ISS no lugar do prestador.")]
    [InlineData(
        "## Mensagem\n\n> O código do município emissor informado na DPS é inexistente no cadastro de convênio municipal do sistema nacional.\n\n## Causa\n\nApesar da mensagem, o município **existe**.",
        "O código do município emissor informado na DPS é inexistente no cadastro de convênio municipal do sistema nacional.")]
    [InlineData(
        "## Fórmula\n\n```\nvLiq = vServ − vDescCond\n```\n\n## Campos usados\n\n| Parcela | Tag |\n| :--- | :--- |\n| Valor do serviço | `vServ` |\n\n**Valores retidos** = soma de:",
        "Valores retidos = soma de:")]
    [InlineData(
        "## O que é\nNa retenção, o tomador recolhe o ISS.",
        "Na retenção, o tomador recolhe o ISS.")]
    public void Derivar_SemResumo_PulaTituloDeSecaoETabela(string corpo, string esperado)
    {
        var resultado = ResumoArtigo.Derivar(corpo);

        resultado.Should().Be(esperado);
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

    [Theory]
    [InlineData("CARTOES", "> - **Nome do campo** | Significado e preenchimento. | [Ex.: TomaCNPJ](/busca?q=TomaCNPJ)")]
    [InlineData("COMPARATIVO", "> **Aviso**\n> - Trata de regra em vigor.")]
    [InlineData("CHECKLIST", "> - O erro não está documentado.")]
    [InlineData("SUCESSO", "> A nota foi autorizada.")]
    public void Derivar_BlocoVisualNovoAntesDoTexto_NaoViraResumo(string marcador, string conteudo)
    {
        var corpo = $"> [!{marcador}]\n{conteudo}\n\nO primeiro parágrafo de texto.";

        var resultado = ResumoArtigo.Derivar(corpo);

        resultado.Should().Be("O primeiro parágrafo de texto.");
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

    [Theory]
    [InlineData("Fonte: Webhook - Configurações (12/08/2026).")]
    [InlineData("**Fonte:** Webhook - Configurações (12/08/2026).")]
    [InlineData("> Fonte: Webhook - Configurações (12/08/2026).")]
    [InlineData("> Fontes: [obs] SMARAPDISIL e TIPLAN; [FAQ - Padrão Nacional](https://desenvolvedores.migrate.info/ModulosFiscais/Documentos/NFSe/FAQ/).")]
    [InlineData("**Fontes:** Impressão de NFS-e (17/07/2026) e Gerar Espelho do RPS e da NFS-e (13/02/2026).")]
    [InlineData("FONTES: Consulta de Municípios Integrados (16/02/2026).")]
    public void Derivar_SemResumo_PulaOParagrafoDeFonte(string fonte)
    {
        var resultado = ResumoArtigo.Derivar($"{fonte}\n\nO webhook avisa o sistema do parceiro a cada nota.");

        resultado.Should().Be("O webhook avisa o sistema do parceiro a cada nota.");
    }

    [Fact]
    public void Derivar_ResumoMarcado_VenceMesmoDepoisDaFonte()
    {
        var corpo = "> Fonte: Webhook.\n\nPrimeiro parágrafo.\n\n> [!RESUMO]\n> Resumo escolhido.";

        var resultado = ResumoArtigo.Derivar(corpo);

        resultado.Should().Be("Resumo escolhido.");
    }
}
