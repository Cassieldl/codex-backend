using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using Codex.Tests.Integration.Interno;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>De-para da natureza da operação: gravado pelo PUT interno e lido na página pública da versão.</summary>
[Collection(PostgresCollection.Nome)]
public sealed class NaturezasOperacaoNaPaginaVersaoHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private SemeadorInternoHttp _semeador = null!;

    public NaturezasOperacaoNaPaginaVersaoHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        _semeador = new SemeadorInternoHttp(_cliente);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private sealed record Cenario(int VersaoId, string RotaPublica);

    [Fact]
    public async Task VersaoSemTabela_TrazListaVaziaNuncaAusente()
    {
        var cenario = await SemearVersaoPublicadaAsync();

        var naturezas = await LerNaturezasDaPaginaAsync(cenario.RotaPublica);

        naturezas.ValueKind.Should().Be(JsonValueKind.Array);
        naturezas.EnumerateArray().Should().BeEmpty();
    }

    /// <summary>Ordem enviada diferente da numérica: a página segue a ordem gravada, não o código.</summary>
    [Fact]
    public async Task Substituir_GravaNaOrdemEnviada_EAPaginaPublicaMostraTodosOsCampos()
    {
        var cenario = await SemearVersaoPublicadaAsync();

        var (status, texto) = await SubstituirAsync(cenario.VersaoId,
            Item(7, "Exigível", "1"),
            Item(4, "Imunidade", "5", observacao: "Este provedor não recebe o tipo de imunidade."),
            Item(16, "Não Tributada", "14", aConfirmar: true));

        status.Should().Be(HttpStatusCode.OK, because: texto);

        var naturezas = (await LerNaturezasDaPaginaAsync(cenario.RotaPublica)).EnumerateArray().ToList();
        naturezas.Select(n => n.GetProperty("codigoInvoicy").GetInt32()).Should().Equal(7, 4, 16);

        var imunidade = naturezas[1];
        imunidade.GetProperty("descricao").GetString().Should().Be("Imunidade");
        imunidade.GetProperty("valorProvedor").GetString().Should().Be("5");
        imunidade.GetProperty("observacao").GetString().Should().Be("Este provedor não recebe o tipo de imunidade.");
        imunidade.GetProperty("aConfirmar").GetBoolean().Should().BeFalse();

        naturezas[2].GetProperty("aConfirmar").GetBoolean().Should().BeTrue();
        naturezas[0].GetProperty("observacao").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task SubstituirDeNovo_TrocaAListaInteira()
    {
        var cenario = await SemearVersaoPublicadaAsync();
        await SubstituirAsync(cenario.VersaoId, Item(1, "Operação tributável", "1"), Item(9, "Exportação de serviço", "3"));

        var (status, texto) = await SubstituirAsync(cenario.VersaoId, Item(8, "Não incidência", "4"));

        status.Should().Be(HttpStatusCode.OK, because: texto);
        (await LerNaturezasDaPaginaAsync(cenario.RotaPublica)).EnumerateArray()
            .Select(n => n.GetProperty("codigoInvoicy").GetInt32())
            .Should().Equal(8);
    }

    [Fact]
    public async Task CodigoRepetido_Devolve422_ENaoTocaAListaAtual()
    {
        var cenario = await SemearVersaoPublicadaAsync();
        await SubstituirAsync(cenario.VersaoId, Item(1, "Operação tributável", "1"));

        var (status, _) = await SubstituirAsync(cenario.VersaoId, Item(4, "Imunidade", "2"), Item(4, "Imune", "5"));

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerNaturezasDaPaginaAsync(cenario.RotaPublica)).EnumerateArray()
            .Select(n => n.GetProperty("codigoInvoicy").GetInt32())
            .Should().Equal(1);
    }

    private static object Item(int codigo, string descricao, string valor, string? observacao = null, bool aConfirmar = false) =>
        new { codigoInvoicy = codigo, descricao, valorProvedor = valor, observacao, aConfirmar };

    private async Task<(HttpStatusCode Status, string Texto)> SubstituirAsync(int versaoId, params object[] itens)
    {
        await using var contexto = _fixture.CriarContexto();
        var versaoConcorrencia = await contexto.VersoesProvedor
            .Where(v => v.Id == versaoId)
            .Select(v => v.Versao)
            .SingleAsync();

        return await _semeador.EnviarAsync(
            HttpMethod.Put, $"/api/v1/interno/versoes/{versaoId}/naturezas-operacao", new { itens }, $"\"{versaoConcorrencia}\"");
    }

    private async Task<JsonElement> LerNaturezasDaPaginaAsync(string rotaPublica)
    {
        using var resposta = await _cliente.GetAsync(rotaPublica);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("data").GetProperty("naturezasOperacao").Clone();
    }

    private async Task<Cenario> SemearVersaoPublicadaAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdemAtual = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout natop {sufixo}", maiorOrdemAtual + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor natop {sufixo}", $"NAT-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versão natop {sufixo}");
        versao.Publicar();
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return new Cenario(versao.Id, $"/api/v1/provedores/{provedor.Slug}/versoes/{versao.Slug}");
    }
}
