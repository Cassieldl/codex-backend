using System.Net;
using System.Text.Json;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Busca;

/// <summary>
/// P1 muda de forma BREAKING (PQ-07, P-06/P-12 de arquitetura.md §5.E): <c>descricaoCasamento</c>
/// sai do grupo e entra no item; <c>meta</c> deixa de ser string pt-BR e vira objeto estruturado;
/// <c>publicado</c> é novo campo obrigatório. Teste em JSON LITERAL contra a API real via HTTP —
/// teste em C# que só serializa o DTO não prova contrato HTTP (rules/api.md, lição do projeto).
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class BuscaPublicaContratoJsonTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public BuscaPublicaContratoJsonTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static string NovoSufixo() => Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task GetBusca_ItemVerificado_TemDescricaoCasamentoNoItemEMetaComoObjeto()
    {
        var sufixo = NovoSufixo();
        var termo = $"contratojson{sufixo}";

        await using (var contexto = _fixture.CriarContexto())
        {
            var artigo = Artigo.Criar(TipoArtigo.Conceito, $"Artigo Contrato {sufixo}", "Corpo.");
            artigo.Publicar();
            artigo.RegistrarVerificacao(new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero));
            contexto.Artigos.Add(artigo);
            await contexto.SaveChangesAsync();

            var item = ItemBusca.Criar(
                TipoItemIndexado.artigo, artigo.Id, artigo.Titulo, SlugsReservadosArtigo.RotaCanonica(artigo.Slug),
                provedorIds: []);
            item.AtualizarPesos(termo, null, null, null);
            item.MarcarPublicado(true);
            contexto.ItensBusca.Add(item);
            await contexto.SaveChangesAsync();
        }

        using var resposta = await _cliente.GetAsync($"/api/v1/busca?q={termo}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var texto = await resposta.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(texto);
        var dados = documento.RootElement.GetProperty("data");

        var grupo = dados.GetProperty("grupos").EnumerateArray().Should().ContainSingle().Subject;

        grupo.TryGetProperty("descricaoCasamento", out _).Should().BeFalse(
            because: "P-06: descricaoCasamento saiu do grupo — manter os dois seria duplicar a fonte");

        var elementoItem = grupo.GetProperty("itens").EnumerateArray().Should().ContainSingle().Subject;

        elementoItem.GetProperty("descricaoCasamento").GetString().Should().NotBeNullOrWhiteSpace(
            because: "P-06: descricaoCasamento agora é obrigatório no ITEM");

        elementoItem.GetProperty("publicado").GetBoolean().Should().BeTrue(
            because: "PQ-09: P1 nunca devolve rascunho — publicado é sempre true por construção");

        var meta = elementoItem.GetProperty("meta");
        meta.ValueKind.Should().Be(JsonValueKind.Object,
            because: "P-12: meta é objeto estruturado, nunca string pt-BR pronta");
        meta.GetProperty("tipo").GetString().Should().Be("verificacao");
        meta.GetProperty("data").GetString().Should().Be("2026-05-12",
            because: "P-12: data em ISO (aaaa-MM-dd), sem hora e sem fuso — formatação pt-BR migrou para o frontend");
    }

    [Fact]
    public async Task GetBusca_ItemSemVerificacao_TemMetaComoJsonNuloNuncaOmitidoNuncaString()
    {
        var sufixo = NovoSufixo();
        var termo = $"contratojsonsemmeta{sufixo}";

        await using (var contexto = _fixture.CriarContexto())
        {
            var erro = Erro.Criar($"E-CTR-{sufixo}", OrigemErro.InvoiCy, $"Erro contrato {sufixo}");
            erro.Publicar();
            contexto.Erros.Add(erro);
            await contexto.SaveChangesAsync();

            var item = ItemBusca.Criar(TipoItemIndexado.erro, erro.Id, erro.Titulo, $"/erros/{erro.Slug}", chave: erro.Codigo);
            item.AtualizarPesos(termo, null, null, null);
            item.MarcarPublicado(true);
            contexto.ItensBusca.Add(item);
            await contexto.SaveChangesAsync();
        }

        using var resposta = await _cliente.GetAsync($"/api/v1/busca?q={termo}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var texto = await resposta.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(texto);
        var elementoItem = documento.RootElement.GetProperty("data").GetProperty("grupos")
            .EnumerateArray().Should().ContainSingle().Subject
            .GetProperty("itens").EnumerateArray().Should().ContainSingle().Subject;

        elementoItem.TryGetProperty("meta", out var meta).Should().BeTrue(because: "meta nunca é omitida do payload");
        meta.ValueKind.Should().Be(JsonValueKind.Null, because: "erro geral sem versão aplicável não tem meta de verificação");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetBusca_PorE157_SolucaoResumoEStringNoErroComSolucaoENuloNoRestante(bool interno)
    {
        var sufixo = NovoSufixo();
        var tituloComVersao = $"Erro com solucao e versao {sufixo}";
        var tituloGeral = $"Erro com solucao geral {sufixo}";
        var tituloSemSolucao = $"Erro sem solucao {sufixo}";
        var tituloArtigo = $"Artigo sobre E157 {sufixo}";

        await using (var contexto = _fixture.CriarContexto())
        {
            var versaoProvedorId = await CriarVersaoPublicadaAsync(contexto, sufixo);

            await IndexarErroAsync(contexto, tituloComVersao, "\n  \nConfira o CNPJ do prestador.  \nDepois reenvie o RPS.", versaoProvedorId);
            await IndexarErroAsync(contexto, tituloGeral, "Solicite a liberação na prefeitura.", versaoProvedorId: null);
            await IndexarErroAsync(contexto, tituloSemSolucao, solucaoSugerida: null, versaoProvedorId: null);

            var artigo = Artigo.Criar(TipoArtigo.Conceito, tituloArtigo, "Corpo.");
            artigo.Publicar();
            contexto.Artigos.Add(artigo);
            await contexto.SaveChangesAsync();

            var itemArtigo = ItemBusca.Criar(
                TipoItemIndexado.artigo, artigo.Id, artigo.Titulo, SlugsReservadosArtigo.RotaCanonica(artigo.Slug),
                provedorIds: []);
            itemArtigo.AtualizarPesos($"E157 {tituloArtigo}", null, null, null);
            itemArtigo.MarcarPublicado(true);
            contexto.ItensBusca.Add(itemArtigo);
            await contexto.SaveChangesAsync();
        }

        var rota = interno ? "/api/v1/interno/busca" : "/api/v1/busca";
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"{rota}?q=E157&tamanhoPagina=50");
        if (interno)
            requisicao.Headers.Add(TestAuthHandler.HeaderRoles, "indicebusca-buscar");

        using var resposta = await _cliente.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: texto);

        using var documento = JsonDocument.Parse(texto);
        var itensDoTeste = documento.RootElement.GetProperty("data").GetProperty("grupos").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("itens").EnumerateArray())
            .Where(item => item.GetProperty("titulo").GetString()!.EndsWith(sufixo, StringComparison.Ordinal))
            .ToDictionary(item => item.GetProperty("titulo").GetString()!, item => item.Clone());

        itensDoTeste.Keys.Should().BeEquivalentTo([tituloComVersao, tituloGeral, tituloSemSolucao, tituloArtigo]);

        var comVersao = itensDoTeste[tituloComVersao].GetProperty("solucaoResumo");
        comVersao.ValueKind.Should().Be(JsonValueKind.String);
        comVersao.GetString().Should().Be("Confira o CNPJ do prestador.",
            because: "vale a primeira linha não vazia, sem os espaços das bordas");

        itensDoTeste[tituloGeral].GetProperty("solucaoResumo").GetString().Should().Be(
            "Solicite a liberação na prefeitura.", because: "erro geral (sem versão) também traz o resumo");

        itensDoTeste[tituloSemSolucao].TryGetProperty("solucaoResumo", out var semSolucao).Should().BeTrue(
            because: "a chave nunca é omitida do payload");
        semSolucao.ValueKind.Should().Be(JsonValueKind.Null);

        itensDoTeste[tituloArtigo].GetProperty("tipo").GetString().Should().Be("artigo");
        itensDoTeste[tituloArtigo].GetProperty("solucaoResumo").ValueKind.Should().Be(JsonValueKind.Null,
            because: "só item do tipo erro tem resumo de solução");
    }

    private static async Task<int> CriarVersaoPublicadaAsync(CodexDbContext contexto, string sufixo)
    {
        var maiorOrdemAtual = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;

        var versaoLayout = VersaoLayout.Criar($"Layout solucao {sufixo}", maiorOrdemAtual + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor solucao {sufixo}", $"INV-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao solucao {sufixo}");
        versao.Publicar();
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        return versao.Id;
    }

    private static async Task IndexarErroAsync(
        CodexDbContext contexto, string titulo, string? solucaoSugerida, int? versaoProvedorId)
    {
        var erro = Erro.Criar("E157", OrigemErro.InvoiCy, titulo, solucaoSugerida: solucaoSugerida);
        erro.Publicar();
        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        if (versaoProvedorId is not null)
            contexto.ErrosVersoes.Add(ErroVersao.Criar(erro.Id, versaoProvedorId.Value));

        var item = ItemBusca.Criar(TipoItemIndexado.erro, erro.Id, titulo, $"/erros/{erro.Slug}", chave: erro.Codigo);
        item.AtualizarPesos($"E157 {titulo}", null, null, null);
        item.MarcarPublicado(true);
        contexto.ItensBusca.Add(item);
        await contexto.SaveChangesAsync();
    }
}
