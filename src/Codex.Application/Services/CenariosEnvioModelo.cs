using Codex.Application.DTOs.Publico;

namespace Codex.Application.Services;

/// <summary>
/// Cenários do envio modelo, num lugar só e iguais para todo provedor. O cenário age sobre os campos
/// do alcance; o recorte da estrutura vem depois e não sabe de cenário. Cada cenário é a soma de
/// blocos: um bloco força os campos dele que a versão aceita, tira ramos que não cabem na situação e
/// troca o valor-modelo genérico por um exemplo plausível.
/// </summary>
/// <remarks>
/// No Layout InvoiCy o tomador não tem <c>xs:choice</c>: CNPJ, CPF, NIF e os dois endereços são
/// irmãos da mesma sequência. Os caminhos seguem o mapeamento do Emissor Nacional:
/// <c>toma/NIF</c> e <c>toma/cNaoNIF</c> na identificação, <c>end/endExt</c> no endereço,
/// <c>serv/comExt</c> no comércio exterior, <c>serv/obra</c> na obra e <c>tribFed</c> nas
/// retenções federais.
/// </remarks>
public static class CenariosEnvioModelo
{
    public const string Nacional = "nacional";

    public const string TomadorEstrangeiro = "tomador-estrangeiro";

    public const string ExportacaoServico = "exportacao-servico";

    public const string ConstrucaoCivil = "construcao-civil";

    public const string RetencoesFederais = "retencoes-federais";

    /// <summary>
    /// Parte de um cenário. Ele só está disponível quando a versão aceita algum caminho de
    /// <paramref name="Exige"/>. Das <paramref name="Exclusivas"/>, entra só a primeira que a versão
    /// aceita e as outras saem, mesmo no máximo.
    /// </summary>
    private sealed record BlocoCenario(
        IReadOnlyList<string> Exige,
        IReadOnlyList<string> Forcados,
        IReadOnlyList<string> Exclusivas,
        IReadOnlyList<string> Removidos,
        IReadOnlyDictionary<string, string> Valores);

    private sealed record DefinicaoCenario(CenarioEnvioModelo Cenario, string Texto, IReadOnlyList<BlocoCenario> Blocos);

    /// <summary>
    /// NIF ou, sem ele, o motivo de não ter NIF; nome e endereço no exterior. TomaxMun é a cidade,
    /// TomaUF leva "EX" e TomaPais a sigla ISO do país, como no artigo do tomador estrangeiro.
    /// </summary>
    private static readonly BlocoCenario TomadorNoExterior = new(
        Exige: ["//Tomador/DocTomadorEstrangeiro", "//Tomador/TomaExSemNIF"],
        Forcados:
        [
            "//Tomador/TomaRazaoSocial",
            "//Tomador/TomaEndereco",
            "//Tomador/TomaNumero",
            "//Tomador/TomaBairro",
            "//Tomador/TomaxMun",
            "//Tomador/TomaUF",
            "//Tomador/TomaPais",
            "//Tomador/TomaCEP",
        ],
        Exclusivas: ["//Tomador/DocTomadorEstrangeiro", "//Tomador/TomaExSemNIF"],
        Removidos: ["//Tomador/TomaCNPJ", "//Tomador/TomaCPF", "//Tomador/TomaCAEPF", "//Tomador/TomacMun"],
        Valores: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["//Tomador/TomaxMun"] = "Cidade do México",
            ["//Tomador/TomaUF"] = "EX",
            ["//Tomador/TomaPais"] = "MX",
            // Código postal mexicano: só dígitos, porque o layout 8 ainda tipa o CEP como número.
            ["//Tomador/TomaCEP"] = "06600",
            ["//Tomador/DocTomadorEstrangeiro"] = "XEXX010101000",
            ["//Tomador/TomaExSemNIF"] = "1",
        });

    /// <summary>Grupo de comércio exterior (E0330), natureza de exportação e país do resultado.</summary>
    private static readonly BlocoCenario ComercioExterior = new(
        Exige:
        [
            "//Servico/Valores/mdPrestacao",
            "//Servico/Valores/vincPrest",
            "//Servico/Valores/movTempBens",
            "//Servico/Valores/mecAFComexP",
            "//Servico/Valores/mecAFComexT",
        ],
        Forcados:
        [
            "//natOp",
            "//Servico/PaiPreServico",
            "//Servico/Valores/ValServicos",
            "//Servico/Valores/mdPrestacao",
            "//Servico/Valores/vincPrest",
            "//Servico/Valores/tpMoeda",
            "//Servico/Valores/ValServEst",
            "//Servico/Valores/mecAFComexP",
            "//Servico/Valores/mecAFComexT",
            "//Servico/Valores/movTempBens",
            "//Servico/Valores/mdic",
            "//Servico/Valores/nroContCambio",
        ],
        Exclusivas: [],
        Removidos: [],
        Valores: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["//natOp"] = "9",
            ["//Servico/PaiPreServico"] = "MX",
            ["//Servico/Valores/ValServicos"] = "10000.00",
            ["//Servico/Valores/mdPrestacao"] = "1",
            ["//Servico/Valores/vincPrest"] = "0",
            ["//Servico/Valores/tpMoeda"] = "220",
            ["//Servico/Valores/ValServEst"] = "2000.00",
            ["//Servico/Valores/mecAFComexP"] = "01",
            ["//Servico/Valores/mecAFComexT"] = "01",
            ["//Servico/Valores/movTempBens"] = "1",
            ["//Servico/Valores/mdic"] = "0",
            ["//Servico/Valores/nroContCambio"] = "123456789",
        });

    /// <summary>Identificação da obra (CNO ou CEI, ART), endereço dela e dedução de materiais.</summary>
    private static readonly BlocoCenario Obra = new(
        Exige: ["//ConstCivil/CodObra", "//ConstCivil/ObraCEI", "//ConstCivil/Art"],
        Forcados:
        [
            "//ConstCivil/CodObra",
            "//ConstCivil/Art",
            "//ConstCivil/ObraCEI",
            "//ConstCivil/ObraLog",
            "//ConstCivil/ObraNumero",
            "//ConstCivil/ObraBairro",
            "//ConstCivil/ObraCEP",
            "//ConstCivil/ObraMun",
            "//ConstCivil/ObraXMun",
            "//ConstCivil/ObraUF",
            "//Servico/Valores/ValServicos",
            "//Servico/Valores/ValDeducoes",
            "//ConstCivil/ListaMaterial/Material/MatDescricao",
            "//ConstCivil/ListaMaterial/Material/MatQuantidade",
            "//ConstCivil/ListaMaterial/Material/MatValUni",
            "//ConstCivil/ListaMaterial/Material/MatVlrTotal",
        ],
        Exclusivas: [],
        Removidos: [],
        Valores: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["//ConstCivil/CodObra"] = "900061234571",
            ["//ConstCivil/Art"] = "SP2026001234",
            ["//ConstCivil/ObraCEI"] = "510012345678",
            ["//ConstCivil/ObraLog"] = "Avenida Paulista",
            ["//ConstCivil/ObraNumero"] = "1000",
            ["//ConstCivil/ObraBairro"] = "Bela Vista",
            ["//ConstCivil/ObraCEP"] = "01310100",
            ["//ConstCivil/ObraMun"] = "3550308",
            ["//ConstCivil/ObraXMun"] = "São Paulo",
            ["//ConstCivil/ObraUF"] = "SP",
            ["//ConstCivil/ObraPais"] = "BR",
            ["//Servico/Valores/ValServicos"] = "50000.00",
            ["//Servico/Valores/ValDeducoes"] = "12000.00",
            ["//ConstCivil/ListaMaterial/Material/MatDescricao"] = "Cimento CP II 50 kg",
            ["//ConstCivil/ListaMaterial/Material/MatQuantidade"] = "300",
            ["//ConstCivil/ListaMaterial/Material/MatValUni"] = "40.00",
            ["//ConstCivil/ListaMaterial/Material/MatVlrTotal"] = "12000.00",
        });

    /// <summary>
    /// ISS retido pelo tomador e retenções de PIS, COFINS, CSLL, IR e INSS sobre um serviço de
    /// R$ 1.000,00; o total federal é a soma das cinco.
    /// </summary>
    private static readonly BlocoCenario Retencoes = new(
        Exige:
        [
            "//Servico/Valores/ValPIS",
            "//Servico/Valores/ValCOFINS",
            "//Servico/Valores/ValCSLL",
            "//Servico/Valores/ValIR",
            "//Servico/Valores/ValINSS",
        ],
        Forcados:
        [
            "//Servico/Valores/ValServicos",
            "//Servico/Valores/ISSRetido",
            "//Servico/Valores/RespRetencao",
            "//Servico/Valores/ValISSRetido",
            "//Servico/Valores/ValPIS",
            "//Servico/Valores/PISRetido",
            "//Servico/Valores/ValPISRetido",
            "//Servico/Valores/ValCOFINS",
            "//Servico/Valores/COFINSRetido",
            "//Servico/Valores/ValCOFINSRetido",
            "//Servico/Valores/ValCSLL",
            "//Servico/Valores/CSLLRetido",
            "//Servico/Valores/ValIR",
            "//Servico/Valores/IRRetido",
            "//Servico/Valores/ValINSS",
            "//Servico/Valores/INSSRetido",
            "//Servico/Valores/CppRetido",
            "//Servico/Valores/ValRetFederais",
            "//Servico/IBSCBS/tpRetPisCofins",
        ],
        Exclusivas: [],
        Removidos: [],
        Valores: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["//Servico/Valores/ValServicos"] = "1000.00",
            ["//Servico/Valores/ISSRetido"] = "1",
            ["//Servico/Valores/RespRetencao"] = "1",
            ["//Servico/Valores/ValISSRetido"] = "50.00",
            ["//Servico/Valores/ValPIS"] = "6.50",
            ["//Servico/Valores/PISRetido"] = "1",
            ["//Servico/Valores/ValPISRetido"] = "6.50",
            ["//Servico/Valores/ValCOFINS"] = "30.00",
            ["//Servico/Valores/COFINSRetido"] = "1",
            ["//Servico/Valores/ValCOFINSRetido"] = "30.00",
            ["//Servico/Valores/ValCSLL"] = "10.00",
            ["//Servico/Valores/CSLLRetido"] = "1",
            ["//Servico/Valores/ValIR"] = "15.00",
            ["//Servico/Valores/IRRetido"] = "1",
            ["//Servico/Valores/ValINSS"] = "110.00",
            ["//Servico/Valores/INSSRetido"] = "1",
            ["//Servico/Valores/CppRetido"] = "1",
            ["//Servico/Valores/ValRetFederais"] = "171.50",
            ["//Servico/IBSCBS/tpRetPisCofins"] = "3",
        });

    /// <summary>Na ordem em que aparecem em <c>cenariosDisponiveis</c>.</summary>
    private static readonly DefinicaoCenario[] Definicoes =
    [
        new(CenarioEnvioModelo.Nacional, Nacional, []),
        new(CenarioEnvioModelo.TomadorEstrangeiro, TomadorEstrangeiro, [TomadorNoExterior]),
        new(CenarioEnvioModelo.ExportacaoServico, ExportacaoServico, [TomadorNoExterior, ComercioExterior]),
        new(CenarioEnvioModelo.ConstrucaoCivil, ConstrucaoCivil, [Obra]),
        new(CenarioEnvioModelo.RetencoesFederais, RetencoesFederais, [Retencoes]),
    ];

    /// <summary>Sem o parâmetro, o cenário é o nacional.</summary>
    public static CenarioEnvioModelo DeTexto(string? cenario) =>
        cenario is null
            ? CenarioEnvioModelo.Nacional
            : Definicoes.FirstOrDefault(definicao => definicao.Texto == cenario)?.Cenario
                ?? throw new ArgumentOutOfRangeException(nameof(cenario), cenario, "Cenário desconhecido.");

    public static string ParaTexto(CenarioEnvioModelo cenario) => Definir(cenario).Texto;

    /// <summary>Nacional sempre; os demais quando a versão aceita algum campo exigido por cada bloco deles.</summary>
    public static IReadOnlyList<CenarioEnvioModelo> Disponiveis(IReadOnlyCollection<CampoEnvioModeloBrutoDto> camposDaVersao)
    {
        var caminhosDaVersao = Caminhos(camposDaVersao);

        return Definicoes
            .Where(definicao => definicao.Blocos.All(bloco => bloco.Exige.Any(caminhosDaVersao.Contains)))
            .Select(definicao => definicao.Cenario)
            .ToArray();
    }

    /// <summary>
    /// Nacional devolve o alcance como veio. Os demais somam ao alcance os campos forçados dos blocos
    /// que a versão aceita e uma só alternativa exclusiva, e tiram os ramos removidos.
    /// </summary>
    public static IReadOnlyCollection<CampoEnvioModeloBrutoDto> Aplicar(
        CenarioEnvioModelo cenario,
        IReadOnlyCollection<CampoEnvioModeloBrutoDto> camposDaVersao,
        IReadOnlyCollection<CampoEnvioModeloBrutoDto> camposDoAlcance)
    {
        var blocos = Definir(cenario).Blocos;
        if (blocos.Count == 0)
            return camposDoAlcance;

        var caminhosDaVersao = Caminhos(camposDaVersao);
        var forcados = new HashSet<string>(StringComparer.Ordinal);
        var removidos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var bloco in blocos)
        {
            forcados.UnionWith(bloco.Forcados);
            removidos.UnionWith(bloco.Removidos);

            var escolhida = bloco.Exclusivas.FirstOrDefault(caminhosDaVersao.Contains);
            if (escolhida is not null)
                forcados.Add(escolhida);
            removidos.UnionWith(bloco.Exclusivas.Where(caminho => caminho != escolhida));
        }

        return camposDoAlcance
            .Concat(camposDaVersao.Where(campo => forcados.Contains(campo.CaminhoXml)))
            .Where(campo => !removidos.Contains(campo.CaminhoXml))
            .Distinct()
            .ToArray();
    }

    /// <summary>Valor de exemplo por caminho canônico, no lugar do valor-modelo genérico; vazio no nacional.</summary>
    public static IReadOnlyDictionary<string, string> ValoresDeExemplo(CenarioEnvioModelo cenario)
    {
        var valores = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var bloco in Definir(cenario).Blocos)
            foreach (var (caminho, valor) in bloco.Valores)
                valores[caminho] = valor;

        return valores;
    }

    private static DefinicaoCenario Definir(CenarioEnvioModelo cenario) =>
        Definicoes.FirstOrDefault(definicao => definicao.Cenario == cenario)
            ?? throw new ArgumentOutOfRangeException(nameof(cenario), cenario, "Cenário desconhecido.");

    private static HashSet<string> Caminhos(IReadOnlyCollection<CampoEnvioModeloBrutoDto> campos) =>
        new(campos.Select(campo => campo.CaminhoXml), StringComparer.Ordinal);
}
