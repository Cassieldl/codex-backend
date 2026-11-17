using Codex.Application.DTOs.Publico;
using Codex.Application.Services;
using Codex.Domain.Enums;
using FluentAssertions;
using static Codex.Tests.Unit.Publico.EnvioModeloComEstruturaReal;

namespace Codex.Tests.Unit.Publico;

/// <summary>
/// Exportação de serviço, construção civil e retenções federais sobre a estrutura real dos layouts 8
/// e 9, com os campos que o ISS.NET 1.01 aceita no serviço, nos valores, na obra e no tomador.
/// </summary>
public sealed class CenariosNovosEnvioModeloTests
{
    private static readonly CampoEnvioModeloBrutoDto[] CamposIssNet =
    [
        new("natop", "//natOp", EstadoPreenchimento.Obrigatorio),
        new("servico-paipreservico", "//Servico/PaiPreServico", EstadoPreenchimento.Condicional),
        new("servico-valores-valservicos", "//Servico/Valores/ValServicos", EstadoPreenchimento.Obrigatorio),
        new("servico-valores-valdeducoes", "//Servico/Valores/ValDeducoes", EstadoPreenchimento.Condicional),
        new("servico-valores-valpis", "//Servico/Valores/ValPIS", EstadoPreenchimento.Opcional),
        new("servico-valores-valcofins", "//Servico/Valores/ValCOFINS", EstadoPreenchimento.Opcional),
        new("servico-valores-valinss", "//Servico/Valores/ValINSS", EstadoPreenchimento.Opcional),
        new("servico-valores-valir", "//Servico/Valores/ValIR", EstadoPreenchimento.Opcional),
        new("servico-valores-valcsll", "//Servico/Valores/ValCSLL", EstadoPreenchimento.Opcional),
        new("servico-valores-issretido", "//Servico/Valores/ISSRetido", EstadoPreenchimento.Obrigatorio),
        new("servico-valores-valretfederais", "//Servico/Valores/ValRetFederais", EstadoPreenchimento.Condicional),
        new("servico-valores-mdprestacao", "//Servico/Valores/mdPrestacao", EstadoPreenchimento.Condicional),
        new("servico-valores-vincprest", "//Servico/Valores/vincPrest", EstadoPreenchimento.Condicional),
        new("servico-valores-tpmoeda", "//Servico/Valores/tpMoeda", EstadoPreenchimento.Condicional),
        new("servico-valores-valservest", "//Servico/Valores/ValServEst", EstadoPreenchimento.Condicional),
        new("servico-valores-mecafcomexp", "//Servico/Valores/mecAFComexP", EstadoPreenchimento.Condicional),
        new("servico-valores-mecafcomext", "//Servico/Valores/mecAFComexT", EstadoPreenchimento.Condicional),
        new("servico-valores-movtempbens", "//Servico/Valores/movTempBens", EstadoPreenchimento.Condicional),
        new("servico-valores-ndi", "//Servico/Valores/nDI", EstadoPreenchimento.Opcional),
        new("servico-valores-nre", "//Servico/Valores/nRE", EstadoPreenchimento.Opcional),
        new("servico-valores-mdic", "//Servico/Valores/mdic", EstadoPreenchimento.Condicional),
        new("servico-ibscbs-tpretpiscofins", "//Servico/IBSCBS/tpRetPisCofins", EstadoPreenchimento.Opcional),
        new("constcivil-codobra", "//ConstCivil/CodObra", EstadoPreenchimento.Condicional),
        new("constcivil-obralog", "//ConstCivil/ObraLog", EstadoPreenchimento.Condicional),
        new("constcivil-obracompl", "//ConstCivil/ObraCompl", EstadoPreenchimento.Opcional),
        new("constcivil-obranumero", "//ConstCivil/ObraNumero", EstadoPreenchimento.Condicional),
        new("constcivil-obrabairro", "//ConstCivil/ObraBairro", EstadoPreenchimento.Condicional),
        new("constcivil-obracep", "//ConstCivil/ObraCEP", EstadoPreenchimento.Condicional),
        new("constcivil-obramun", "//ConstCivil/ObraMun", EstadoPreenchimento.Opcional),
        new("constcivil-obrauf", "//ConstCivil/ObraUF", EstadoPreenchimento.Condicional),
        new("constcivil-obrainscimobfis", "//ConstCivil/ObraInscImobFis", EstadoPreenchimento.Opcional),
        new("constcivil-obracib", "//ConstCivil/ObraCIB", EstadoPreenchimento.Condicional),
        new("tomador-tomacnpj", "//Tomador/TomaCNPJ", EstadoPreenchimento.Condicional),
        new("tomador-tomarazaosocial", "//Tomador/TomaRazaoSocial", EstadoPreenchimento.Condicional),
        new("tomador-tomauf", "//Tomador/TomaUF", EstadoPreenchimento.Condicional),
        new("tomador-tomapais", "//Tomador/TomaPais", EstadoPreenchimento.Condicional),
        new("tomador-tomacep", "//Tomador/TomaCEP", EstadoPreenchimento.Condicional),
        new("tomador-doctomadorestrangeiro", "//Tomador/DocTomadorEstrangeiro", EstadoPreenchimento.Condicional),
        new("tomador-tomaexsemnif", "//Tomador/TomaExSemNIF", EstadoPreenchimento.Condicional),
    ];

    [Fact]
    public async Task CenariosDisponiveis_IssNet_TrazOsCinco()
    {
        var envio = await ObterAsync("8", CamposIssNet, AlcanceEnvioModelo.Minimo, CenarioEnvioModelo.Nacional);

        envio.CenariosDisponiveis.Should().Equal(
            "nacional", "tomador-estrangeiro", "exportacao-servico", "construcao-civil", "retencoes-federais");
    }

    [Theory]
    [InlineData(new[] { "//Tomador/DocTomadorEstrangeiro" }, new[] { "nacional", "tomador-estrangeiro" })]
    [InlineData(new[] { "//Servico/Valores/mdPrestacao" }, new[] { "nacional" })]
    [InlineData(new[] { "//Tomador/TomaExSemNIF", "//Servico/Valores/movTempBens" },
        new[] { "nacional", "tomador-estrangeiro", "exportacao-servico" })]
    [InlineData(new[] { "//ConstCivil/ObraCEI", "//Servico/Valores/ValIR" }, new[] { "nacional", "construcao-civil", "retencoes-federais" })]
    public void CenariosDisponiveis_SoQuandoAVersaoAceitaOsCamposDeCadaBloco(string[] caminhos, string[] esperados)
    {
        var campos = caminhos.Select(caminho => new CampoEnvioModeloBrutoDto(caminho, caminho, EstadoPreenchimento.Opcional)).ToArray();

        CenariosEnvioModelo.Disponiveis(campos).Select(CenariosEnvioModelo.ParaTexto).Should().Equal(esperados);
    }

    [Theory]
    [InlineData("8")]
    [InlineData("9")]
    public async Task ExportacaoServico_TrazComercioExteriorNaturezaETomadorNoExterior(string rotuloLayout)
    {
        var envio = await ObterAsync(rotuloLayout, CamposIssNet, AlcanceEnvioModelo.Minimo, CenarioEnvioModelo.ExportacaoServico);

        envio.Cenario.Should().Be("exportacao-servico");
        foreach (var raiz in new[] { envio.Xml, envio.Json })
        {
            No(raiz, "RPS/natOp").ValorModelo.Should().Be("9");
            No(raiz, "RPS/Servico/PaiPreServico").ValorModelo.Should().Be("MX");

            var valores = No(raiz, "RPS/Servico/Valores").Filhos.ToDictionary(no => no.Nome, no => no.ValorModelo);
            valores.Should().Contain(new Dictionary<string, string?>
            {
                ["mdPrestacao"] = "1",
                ["vincPrest"] = "0",
                ["tpMoeda"] = "220",
                ["mecAFComexP"] = "01",
                ["mecAFComexT"] = "01",
                ["movTempBens"] = "1",
                ["mdic"] = "0",
            });
            valores.Should().NotContainKeys(["nDI", "nRE"], because: "opcionais fora do bloco não entram no mínimo");

            var tomador = No(raiz, "RPS/Tomador").Filhos.Select(no => no.Nome);
            tomador.Should().Equal("TomaRazaoSocial", "TomaUF", "TomaPais", "TomaCEP", "DocTomadorEstrangeiro");
        }

        // ValServEst saiu na 9: no envio da 9 ele não tem posição.
        if (rotuloLayout == "8")
            No(envio.Xml, "RPS/Servico/Valores/ValServEst").ValorModelo.Should().Be("2000.00");
        else
            envio.CamposSemPosicao.Should().Contain("servico-valores-valservest");
    }

    [Theory]
    [InlineData("8")]
    [InlineData("9")]
    public async Task ConstrucaoCivil_TrazObraComEnderecoEDeducaoDeMateriais(string rotuloLayout)
    {
        var envio = await ObterAsync(rotuloLayout, CamposIssNet, AlcanceEnvioModelo.Minimo, CenarioEnvioModelo.ConstrucaoCivil);

        envio.Cenario.Should().Be("construcao-civil");
        var obra = No(envio.Xml, "RPS/ConstCivil").Filhos.ToDictionary(no => no.Nome, no => no.ValorModelo);
        obra.Should().Equal(new Dictionary<string, string?>
        {
            ["CodObra"] = "900061234571",
            ["ObraLog"] = "Avenida Paulista",
            ["ObraNumero"] = "1000",
            ["ObraBairro"] = "Bela Vista",
            ["ObraCEP"] = "01310100",
            ["ObraMun"] = "3550308",
            ["ObraUF"] = "SP",
        });
        No(envio.Xml, "RPS/Servico/Valores/ValDeducoes").ValorModelo.Should().Be("12000.00");
        Nos(envio.Xml).Should().NotContain(no => no.Nome == "Tomador", because: "a obra não mexe no tomador");
    }

    [Theory]
    [InlineData("8")]
    [InlineData("9")]
    public async Task RetencoesFederais_TrazIssRetidoERetencoesComTotalCoerente(string rotuloLayout)
    {
        var envio = await ObterAsync(rotuloLayout, CamposIssNet, AlcanceEnvioModelo.Minimo, CenarioEnvioModelo.RetencoesFederais);

        envio.Cenario.Should().Be("retencoes-federais");
        foreach (var raiz in new[] { envio.Xml, envio.Json })
        {
            var valores = No(raiz, "RPS/Servico/Valores").Filhos.ToDictionary(no => no.Nome, no => no.ValorModelo);
            valores.Should().Contain(new Dictionary<string, string?>
            {
                ["ValServicos"] = "1000.00",
                ["ISSRetido"] = "1",
                ["ValPIS"] = "6.50",
                ["ValCOFINS"] = "30.00",
                ["ValCSLL"] = "10.00",
                ["ValIR"] = "15.00",
                ["ValINSS"] = "110.00",
                ["ValRetFederais"] = "171.50",
            });
            No(raiz, "RPS/Servico/IBSCBS/tpRetPisCofins").ValorModelo.Should().Be("3");
        }
    }

    [Fact]
    public async Task Nacional_MantemOValorModeloGenerico()
    {
        var envio = await ObterAsync("8", CamposIssNet, AlcanceEnvioModelo.Maximo, CenarioEnvioModelo.Nacional);

        No(envio.Xml, "RPS/Tomador/TomaUF").ValorModelo.Should().Be("XX");
        No(envio.Xml, "RPS/natOp").ValorModelo.Should().Be("0");
        CenariosEnvioModelo.ValoresDeExemplo(CenarioEnvioModelo.Nacional).Should().BeEmpty();
    }
}
