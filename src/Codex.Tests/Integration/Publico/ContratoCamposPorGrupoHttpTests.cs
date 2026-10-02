using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// Forma do <c>camposPorGrupo</c> de P4 — o conteúdo principal da tela mais densa do portal —
/// afirmada sobre o JSON literal da resposta.
///
/// <para>
/// Por que não bastava o que existia: o teste de service
/// (<c>ProvedorServicoPublicoTests</c>) prova o agrupamento em objetos C#, e o de HTTP
/// (<c>ContratoPublicoHttpTests</c>) exercita P4 com um cenário que <b>não semeia campo nenhum</b>
/// — então a coleção chegava vazia e a forma serializada nunca foi observada. Objeto C# correto
/// não é contrato HTTP correto: nome de propriedade, caixa, enum virando número e nulo omitido
/// em vez de emitido só aparecem no JSON.
/// </para>
///
/// <para>
/// A lista de propriedades afirmada abaixo é a declarada pelo cliente em
/// <c>codex-frontend/src/features/provedores/types/versaoProvedorPagina.ts</c>. Divergir dela é
/// quebrar a tela, ainda que o C# compile dos dois lados.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ContratoCamposPorGrupoHttpTests : IAsyncLifetime
{
    /// <summary>Propriedades de <c>GrupoCampoVersao</c> no cliente — nem uma a mais, nem a menos.</summary>
    private static readonly string[] PropriedadesDoGrupo = ["grupo", "grupoSlug", "campos"];

    /// <summary>Propriedades de <c>CampoVersaoPublico</c> no cliente.</summary>
    private static readonly string[] PropriedadesDoCampo =
    [
        "campoLayoutSlug", "nome", "caminhoXml", "significado", "tipo", "tamanho", "mascara",
        "dominio", "estadoPreenchimento", "condicao", "exemplo", "notaPreenchimento",
        "destinoXml", "notaTransformacao", "ordem", "errosConhecidos",
    ];

    /// <summary>União <c>EstadoPreenchimentoCampo</c> do cliente — string, nunca número.</summary>
    private static readonly string[] EstadosAceitosPeloCliente = ["Obrigatorio", "Opcional", "Condicional"];

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ContratoCamposPorGrupoHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task P4_CamposPorGrupo_TemExatamenteAsPropriedadesQueOClienteDeclara()
    {
        var pagina = await ObterPaginaAsync();
        var grupos = pagina.GetProperty("camposPorGrupo").EnumerateArray().ToList();

        grupos.Should().HaveCount(2, because: "o cenário semeia um campo sem grupo e dois no mesmo grupo");

        foreach (var grupo in grupos)
        {
            NomesDe(grupo).Should().BeEquivalentTo(PropriedadesDoGrupo);

            var campos = grupo.GetProperty("campos").EnumerateArray().ToList();
            campos.Should().NotBeEmpty(because: "grupo sem campo não deveria existir na projeção");

            foreach (var campo in campos)
                NomesDe(campo).Should().BeEquivalentTo(PropriedadesDoCampo);
        }
    }

    [Fact]
    public async Task P4_CampoSemValorOpcional_VemComoNuloExplicito_NuncaOmitido()
    {
        var pagina = await ObterPaginaAsync();
        var campoEnxuto = LocalizarCampo(pagina, "campo-enxuto");

        // `tamanho: string | null` no cliente: propriedade ausente chega como `undefined` e
        // qualquer verificação `=== null` passa despercebida.
        foreach (var opcional in new[] { "tamanho", "mascara", "dominio", "condicao", "exemplo", "notaPreenchimento", "destinoXml", "notaTransformacao" })
        {
            campoEnxuto.TryGetProperty(opcional, out var valor).Should().BeTrue(
                because: $"'{opcional}' é declarado como nullable no cliente, e nullable exige a chave presente");
            valor.ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task P4_EstadoPreenchimento_ESerializadoComoStringDaUniaoDoCliente()
    {
        var pagina = await ObterPaginaAsync();

        var estados = pagina.GetProperty("camposPorGrupo").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("campos").EnumerateArray())
            .Select(campo => campo.GetProperty("estadoPreenchimento"))
            .ToList();

        estados.Should().HaveCount(3);
        estados.Should().OnlyContain(estado => estado.ValueKind == JsonValueKind.String,
            because: "enum serializado como número binda no C# e quebra a união de literais do cliente");
        estados.Select(estado => estado.GetString()).Should().BeSubsetOf(EstadosAceitosPeloCliente);
    }

    [Fact]
    public async Task P4_GrupoSemNome_VemComoNuloEOrdenaPelaMenorOrdemDoCampoLayout()
    {
        var pagina = await ObterPaginaAsync();
        var grupos = pagina.GetProperty("camposPorGrupo").EnumerateArray().ToList();

        // O campo sem grupo tem ordem 5; o grupo "Financeiro" tem 10 e 20. Menor ordem primeiro.
        grupos[0].GetProperty("grupo").ValueKind.Should().Be(JsonValueKind.Null);
        grupos[0].GetProperty("grupoSlug").ValueKind.Should().Be(JsonValueKind.Null);
        grupos[1].GetProperty("grupo").GetString().Should().Be("Financeiro");
        grupos[1].GetProperty("grupoSlug").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task P4_OrdemDoCampo_EADoCampoLayout_NuncaADoCampoVersao()
    {
        var pagina = await ObterPaginaAsync();

        // O cenário grava ordens propositalmente invertidas em CampoVersao (900/901/902) para que
        // reaproveitar a ordem errada seja visível: a spec §4.3 manda usar a do CampoLayout.
        LocalizarCampo(pagina, "campo-sem-grupo").GetProperty("ordem").GetInt32().Should().Be(5);
        LocalizarCampo(pagina, "campo-enxuto").GetProperty("ordem").GetInt32().Should().Be(10);
        LocalizarCampo(pagina, "campo-completo").GetProperty("ordem").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task P4_TotalCampos_BateComOSomatorioDosGrupos()
    {
        var pagina = await ObterPaginaAsync();

        var somaDosGrupos = pagina.GetProperty("camposPorGrupo").EnumerateArray()
            .Sum(grupo => grupo.GetProperty("campos").GetArrayLength());

        pagina.GetProperty("totalCampos").GetInt32().Should().Be(somaDosGrupos).And.Be(3);
    }

    [Fact]
    public async Task P4_VersaoConcorrencia_NuncaVazaNoCorpo()
    {
        var pagina = await ObterPaginaAsync();

        NomesDe(pagina.GetProperty("versao")).Should().NotContain("versaoConcorrencia",
            because: "é detalhe de infraestrutura de cache — viaja no header ETag, não no contrato público");
    }

    private static IEnumerable<string> NomesDe(JsonElement objeto) =>
        objeto.EnumerateObject().Select(propriedade => propriedade.Name);

    /// <summary>
    /// Localiza pelo <c>nome</c>, e não pelo <c>campoLayoutSlug</c>: o slug é derivado do
    /// <c>caminhoxml</c> (caminho canônico), nunca do nome de exibição — <c>modelo-dados.md</c>
    /// §"Campo canônico".
    /// </summary>
    private static JsonElement LocalizarCampo(JsonElement pagina, string prefixoDoNome)
    {
        var campo = pagina.GetProperty("camposPorGrupo").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("campos").EnumerateArray())
            .FirstOrDefault(c => c.GetProperty("nome").GetString()!.StartsWith(prefixoDoNome, StringComparison.Ordinal));

        campo.ValueKind.Should().Be(JsonValueKind.Object, because: $"o cenário semeia um campo '{prefixoDoNome}'");
        return campo;
    }

    /// <summary>Semeia a versão com três campos e devolve o <c>data</c> de P4 já parseado.</summary>
    private async Task<JsonElement> ObterPaginaAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        string provedorSlug;
        string versaoSlug;

        await using (var contexto = _fixture.CriarContexto())
        {
            var maiorOrdem = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
            var layout = VersaoLayout.Criar($"Layout campos {sufixo}", maiorOrdem + 1);
            contexto.VersoesLayout.Add(layout);
            await contexto.SaveChangesAsync();

            var provedor = Provedor.Criar($"Campos Probe {sufixo}", $"CMP-{sufixo}", CategoriaProvedor.Nacional);
            provedor.Publicar();
            contexto.Provedores.Add(provedor);
            await contexto.SaveChangesAsync();

            var versao = VersaoProvedor.Criar(provedor.Id, layout.Id, $"Versao Campos {sufixo}");
            versao.Publicar();
            contexto.VersoesProvedor.Add(versao);
            await contexto.SaveChangesAsync();

            var semGrupo = CampoLayout.Criar(layout.Id, $"campo-sem-grupo {sufixo}", $"//raiz/SemGrupo{sufixo}", "Campo fora de grupo", ordem: 5);
            var enxuto = CampoLayout.Criar(layout.Id, $"campo-enxuto {sufixo}", $"//raiz/Enxuto{sufixo}", "Campo sem opcionais", ordem: 10, grupo: "Financeiro");
            var completo = CampoLayout.Criar(layout.Id, $"campo-completo {sufixo}", $"//raiz/Completo{sufixo}", "Campo com todos os opcionais", ordem: 20, grupo: "Financeiro");
            contexto.CamposLayout.AddRange(semGrupo, enxuto, completo);
            await contexto.SaveChangesAsync();

            // Ordens de CampoVersao invertidas de propósito: se a projeção usar a ordem errada,
            // o teste de ordenação denuncia.
            contexto.CamposVersao.AddRange(
                CampoVersao.Criar(versao.Id, semGrupo.Id, "N", EstadoPreenchimento.Opcional, ordem: 902),
                CampoVersao.Criar(versao.Id, enxuto.Id, "C", EstadoPreenchimento.Obrigatorio, ordem: 901),
                CampoVersao.Criar(
                    versao.Id, completo.Id, "C", EstadoPreenchimento.Condicional, ordem: 900,
                    tamanho: "1-14", mascara: "999.999.999-99", dominio: "CPF|CNPJ",
                    condicao: "Quando pessoa juridica", exemplo: "12345678000199",
                    notaPreenchimento: "Sem pontuacao", destinoXml: "//destino/Doc",
                    notaTransformacao: "Remover mascara"));
            await contexto.SaveChangesAsync();

            provedorSlug = provedor.Slug;
            versaoSlug = versao.Slug;
        }

        using var resposta = await _cliente.GetAsync($"/api/v1/provedores/{provedorSlug}/versoes/{versaoSlug}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var texto = await resposta.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.GetProperty("data").Clone();
    }
}
