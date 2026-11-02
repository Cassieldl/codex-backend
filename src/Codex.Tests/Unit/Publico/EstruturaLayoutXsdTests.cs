using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Codex.Application.DTOs.Interno;
using FluentAssertions;

namespace Codex.Tests.Unit.Publico;

/// <summary>
/// A estrutura XML que a carga grava para cada versão precisa ser um documento que o InvoiCy aceita.
/// Lê o arquivo gerado e versionado pela carga (<c>layout-invoicy-v{rotulo}-estrutura.json</c>,
/// conferido contra o gerador pelo teste Node) e valida o XML montado dele contra o XSD da mesma
/// versão, versionado ao lado.
/// </summary>
public sealed class EstruturaLayoutXsdTests
{
    private static readonly string PastaDoLayout = Path.Combine(RaizDoRepositorio(), "database", "carga", "emissor-nacional");

    [Theory]
    [InlineData("8")]
    [InlineData("9")]
    public void XmlDaEstrutura_ValidaContraOXsdDaMesmaVersao(string rotulo)
    {
        var documento = MontarDocumento(LerEstrutura(rotulo).Xml);

        Validar(documento, rotulo).Should().BeEmpty();
    }

    [Fact]
    public void Validacao_ReprovaDocumentoSemElementoObrigatorio()
    {
        // Prova de que a validação acima não é inerte: sem RPSNumero, obrigatório no XSD, reprova.
        var documento = MontarDocumento(LerEstrutura("9").Xml);
        documento.Root!.Element("RPS")!.Element("RPSNumero")!.Remove();

        Validar(documento, "9").Should().ContainSingle().Which.Should().Contain("RPSNumero");
    }

    [Fact]
    public void JsonDaEstrutura9_SemOsGruposDeItemOmitidosNoRest_EAListaRepete()
    {
        var json = LerEstrutura("9").Json;
        var porCaminho = Nos(json).ToDictionary(no => no.Caminho, StringComparer.Ordinal);

        json.Nome.Should().Be("Documento");
        foreach (var (lista, grupoDeItem) in GruposOmitidosNoRest)
        {
            var caminhoDaLista = $"Documento/RPS/{lista}";
            porCaminho.Should().ContainKey(caminhoDaLista);
            porCaminho.Should().NotContainKey($"{caminhoDaLista}/{grupoDeItem}");

            var listaNoJson = porCaminho[caminhoDaLista];
            listaNoJson.Repete.Should().BeTrue(because: $"no REST {lista} recebe o array de {grupoDeItem} direto");
            listaNoJson.Filhos.Should().NotContain(filho => filho.Nome == grupoDeItem);
        }
    }

    /// <summary>Os sete grupos que a planilha 9 manda não informar em API Rest, com a lista que os contém.</summary>
    private static readonly (string Lista, string GrupoDeItem)[] GruposOmitidosNoRest =
    [
        ("ListaItens", "Item"),
        ("ListaParcelas", "Parcela"),
        ("ConstCivil/ListaMaterial", "Material"),
        ("ListaDed", "Ded"),
        ("Imovel/Locacao/ListaUnidadesImobiliarias", "UnidadeImobiliaria"),
        ("ListaDocumentos", "Documento"),
        ("ListaNFSeReferenciadas", "NFSeReferenciada"),
    ];

    private static AtualizarEstruturaVersaoLayoutDto LerEstrutura(string rotulo) =>
        JsonSerializer.Deserialize<AtualizarEstruturaVersaoLayoutDto>(
            File.ReadAllText(Path.Combine(PastaDoLayout, $"layout-invoicy-v{rotulo}-estrutura.json")),
            JsonSerializerOptions.Web)!;

    /// <summary>De cada sequência de alternativas de um <c>xs:choice</c>, só a primeira entra.</summary>
    private static XDocument MontarDocumento(NoEstruturaLayoutDto raiz) => new(MontarElemento(raiz));

    private static XElement MontarElemento(NoEstruturaLayoutDto no)
    {
        var elemento = new XElement(no.Nome);
        if (!no.EhGrupo)
        {
            elemento.Value = no.ValorModelo ?? string.Empty;
            return elemento;
        }

        var anteriorEraEscolha = false;
        foreach (var filho in no.Filhos)
        {
            if (!(filho.Escolha && anteriorEraEscolha))
                elemento.Add(MontarElemento(filho));
            anteriorEraEscolha = filho.Escolha;
        }

        return elemento;
    }

    private static List<string> Validar(XDocument documento, string rotulo)
    {
        var esquemas = new XmlSchemaSet();
        using (var leitor = XmlReader.Create(Path.Combine(PastaDoLayout, $"layout-invoicy-v{rotulo}.xsd")))
            esquemas.Add(null, leitor);

        var erros = new List<string>();
        documento.Validate(esquemas, (_, evento) => erros.Add(evento.Message));
        return erros;
    }

    private static IEnumerable<NoEstruturaLayoutDto> Nos(NoEstruturaLayoutDto raiz) =>
        new[] { raiz }.Concat(raiz.Filhos.SelectMany(Nos));

    private static string RaizDoRepositorio()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "Codex.slnx")))
            diretorio = diretorio.Parent;

        return diretorio?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
