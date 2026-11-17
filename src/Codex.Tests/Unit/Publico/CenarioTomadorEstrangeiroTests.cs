using Codex.Application.DTOs.Interno;
using Codex.Application.DTOs.Publico;
using Codex.Application.Services;
using Codex.Domain.Enums;
using FluentAssertions;
using static Codex.Tests.Unit.Publico.EnvioModeloComEstruturaReal;

namespace Codex.Tests.Unit.Publico;

/// <summary>
/// Cenário tomador estrangeiro do envio modelo sobre a estrutura real dos layouts 8 e 9, versionada
/// pela carga, com os campos do tomador que o ISS.NET 1.01 aceita: o tomador entra mesmo quando
/// tudo nele é condicional, com uma identificação estrangeira só, endereço no exterior com valores
/// de exemplo, e sem CNPJ, CPF, CAEPF e município IBGE.
/// </summary>
public sealed class CenarioTomadorEstrangeiroTests
{
    /// <summary>Campos da versão 1.01 do ISS.NET com caminho no tomador, como estão no acervo, mais o número do RPS.</summary>
    private static readonly CampoEnvioModeloBrutoDto[] CamposIssNet =
    [
        new("rps-numero", "//RPSNumero", EstadoPreenchimento.Obrigatorio),
        Condicional("TomaCNPJ"),
        Condicional("TomaCPF"),
        Opcional("TomaIM"),
        Condicional("TomaRazaoSocial"),
        Condicional("TomaEndereco"),
        Condicional("TomaNumero"),
        Opcional("TomaComplemento"),
        Condicional("TomaBairro"),
        Condicional("TomacMun"),
        Condicional("TomaxMun"),
        Condicional("TomaUF"),
        Condicional("TomaPais"),
        Condicional("TomaCEP"),
        Opcional("TomaTelefone"),
        Opcional("TomaEmail"),
        Condicional("DocTomadorEstrangeiro"),
        Opcional("TomaCAEPF"),
        Condicional("TomaExSemNIF"),
    ];

    [Theory]
    [InlineData("8", AlcanceEnvioModelo.Minimo)]
    [InlineData("8", AlcanceEnvioModelo.Maximo)]
    [InlineData("9", AlcanceEnvioModelo.Minimo)]
    [InlineData("9", AlcanceEnvioModelo.Maximo)]
    public async Task TomadorEstrangeiro_TrazNifEEnderecoNoExterior_SemOsRamosNacionais(string rotuloLayout, AlcanceEnvioModelo alcance)
    {
        var envio = await ObterAsync(rotuloLayout, CamposIssNet, alcance, CenarioEnvioModelo.TomadorEstrangeiro);

        envio.Cenario.Should().Be("tomador-estrangeiro");
        envio.CenariosDisponiveis.Should().Equal("nacional", "tomador-estrangeiro");

        // NIF e motivo de não ter NIF se excluem: mesmo no máximo, só a primeira que a versão aceita.
        string[] esperados = alcance == AlcanceEnvioModelo.Minimo
            ? ["TomaRazaoSocial", "TomaEndereco", "TomaNumero", "TomaBairro", "TomaxMun", "TomaUF", "TomaPais", "TomaCEP",
                "DocTomadorEstrangeiro"]
            : ["TomaIM", "TomaRazaoSocial", "TomaEndereco", "TomaNumero", "TomaComplemento", "TomaBairro", "TomaxMun", "TomaUF",
                "TomaPais", "TomaCEP", "TomaTelefone", "TomaEmail", "DocTomadorEstrangeiro"];

        foreach (var raiz in new[] { envio.Xml, envio.Json })
        {
            No(raiz, "RPS").Filhos.Should().Contain(no => no.Nome == "RPSNumero",
                because: "o cenário soma ao alcance, nunca o substitui");
            No(raiz, "RPS/Tomador").Filhos.Select(no => no.Nome).Should().Equal(esperados);
        }
    }

    [Theory]
    [InlineData("8")]
    [InlineData("9")]
    public async Task TomadorEstrangeiro_EnderecoComValoresDoExterior_NuncaOsGenericos(string rotuloLayout)
    {
        var envio = await ObterAsync(rotuloLayout, CamposIssNet, AlcanceEnvioModelo.Minimo, CenarioEnvioModelo.TomadorEstrangeiro);

        foreach (var raiz in new[] { envio.Xml, envio.Json })
        {
            var valores = No(raiz, "RPS/Tomador").Filhos.ToDictionary(no => no.Nome, no => no.ValorModelo);
            valores["TomaUF"].Should().Be("EX");
            valores["TomaPais"].Should().Be("MX");
            valores["TomaCEP"].Should().Be("06600");
            valores["TomaxMun"].Should().Be("Cidade do México");
            valores["DocTomadorEstrangeiro"].Should().Be("XEXX010101000");
        }
    }

    [Fact]
    public async Task TomadorEstrangeiro_VersaoSoComMotivoSemNif_UsaOMotivo()
    {
        var campos = CamposIssNet.Where(campo => campo.CampoLayoutSlug != "DocTomadorEstrangeiro").ToArray();

        var envio = await ObterAsync("9", campos, AlcanceEnvioModelo.Maximo, CenarioEnvioModelo.TomadorEstrangeiro);

        var tomador = No(envio.Xml, "RPS/Tomador");
        tomador.Filhos.Should().ContainSingle(no => no.Nome == "TomaExSemNIF").Which.ValorModelo.Should().Be("1");
        tomador.Filhos.Should().NotContain(no => no.Nome == "DocTomadorEstrangeiro");
    }

    [Fact]
    public async Task Nacional_SegueSemTomador_QuandoNadaNeleEObrigatorio()
    {
        var envio = await ObterAsync("8", CamposIssNet, AlcanceEnvioModelo.Minimo, CenarioEnvioModelo.Nacional);

        envio.Cenario.Should().Be("nacional");
        envio.CenariosDisponiveis.Should().Equal("nacional", "tomador-estrangeiro");
        Nos(envio.Xml).Should().NotContain(no => no.Nome == "Tomador");
    }

    [Fact]
    public void Minimo_EscolhaNoTomador_FicaComAAlternativaEstrangeira()
    {
        // Os layouts 8 e 9 não têm escolha no tomador; uma versão futura com escolha não pode cair no CNPJ.
        var campos = new[] { Condicional("TomaCNPJ"), Condicional("TomaCPF"), Condicional("DocTomadorEstrangeiro") };
        var raiz = NoLayout("Envio", false, null,
            NoLayout("Tomador", false, null,
                NoLayout("TomaCNPJ", true, "//Tomador/TomaCNPJ"),
                NoLayout("TomaCPF", true, "//Tomador/TomaCPF"),
                NoLayout("DocTomadorEstrangeiro", true, "//Tomador/DocTomadorEstrangeiro")));

        var doCenario = CenariosEnvioModelo.Aplicar(CenarioEnvioModelo.TomadorEstrangeiro, campos, []);
        var recorte = RecorteEnvioModelo.Recortar(
            raiz, doCenario.ToDictionary(campo => campo.CaminhoXml, campo => campo.CampoLayoutSlug), AlcanceEnvioModelo.Minimo);

        recorte.Raiz.Filhos.Single().Filhos.Select(no => no.Nome).Should().Equal("DocTomadorEstrangeiro");
    }

    // ---------------------------------------------------------------------------------- apoio

    private static CampoEnvioModeloBrutoDto Condicional(string nome) => new(nome, $"//Tomador/{nome}", EstadoPreenchimento.Condicional);

    private static CampoEnvioModeloBrutoDto Opcional(string nome) => new(nome, $"//Tomador/{nome}", EstadoPreenchimento.Opcional);

    private static NoEstruturaLayoutDto NoLayout(string nome, bool escolha, string? caminhoCampo, params NoEstruturaLayoutDto[] filhos) => new()
    {
        Nome = nome,
        Caminho = nome,
        EhGrupo = filhos.Length > 0,
        Escolha = escolha,
        Formato = "ambos",
        CaminhoCampo = caminhoCampo,
        Filhos = filhos,
    };
}
