using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Qa;

/// <summary>
/// ISSUE-2 do QA da 002: listagem paginada sem desempate repete um item numa página e esconde
/// outro.
///
/// <para>
/// O cenário estático — semear, ler as páginas em sequência, conferir — **passa com o defeito
/// presente**, porque nada reescreve a tupla entre as leituras. O que expõe o bug é o gesto real
/// do editor: ler a página 1, **editar** um item dela e ler a página 2. O `UPDATE` reescreve a
/// linha no fim do heap e, sem ordem total, ela reaparece na página seguinte enquanto outra some.
/// </para>
///
/// <para>Foi o que quase fez o achado ser engolido: o teste que não muta o heap entre as páginas
/// dá falso verde.</para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class PaginacaoComEmpateNaoRepeteNemPulaTests : IAsyncLifetime
{
    private const string RolesDeArtigo = "artigos-buscar,artigos-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public PaginacaoComEmpateNaoRepeteNemPulaTests(PostgresContainerFixture fixture)
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

    [Fact]
    public async Task I23_EditarUmArtigoDaPaginaUm_NaoOFazReaparecerNaPaginaDois()
    {
        var sufixo = await SemearSeisArtigosNoMesmoInstanteAsync();

        var pagina1 = await ListarAsync(sufixo, pagina: 1);
        await EditarAsync(pagina1[0]);
        var pagina2 = await ListarAsync(sufixo, pagina: 2);

        pagina1.Concat(pagina2).Should().OnlyHaveUniqueItems(
            because: "as duas páginas juntas são o acervo — item repetido significa que outro ficou invisível para o editor");
        pagina1.Concat(pagina2).Should().HaveCount(6,
            because: "os seis artigos semeados têm de aparecer exatamente uma vez cada");
    }

    [Fact]
    public async Task P7_EditarUmArtigoDaPaginaUm_NaoOFazReaparecerNaPaginaDois()
    {
        var sufixo = await SemearSeisArtigosNoMesmoInstanteAsync(publicados: true);

        var pagina1 = await ListarPublicoAsync(sufixo, pagina: 1);
        await EditarPorBancoAsync(pagina1[0]);
        var pagina2 = await ListarPublicoAsync(sufixo, pagina: 2);

        pagina1.Concat(pagina2).Should().OnlyHaveUniqueItems(
            because: "no índice público o efeito é pior: o artigo some da vista do leitor sem nenhum sinal");
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>
    /// Os seis entram na mesma transação, então compartilham `criadoem` — que é justamente a
    /// coluna de ordenação. Sem isso não há empate e o cenário não exercita nada.
    /// </summary>
    private async Task<string> SemearSeisArtigosNoMesmoInstanteAsync(bool publicados = false)
    {
        var sufixo = $"empate{Guid.NewGuid():N}"[..16];
        await using var contexto = _fixture.CriarContexto();

        for (var indice = 1; indice <= 6; indice++)
        {
            var artigo = Artigo.Criar(TipoArtigo.Conceito, $"Artigo {indice} {sufixo}", $"Corpo {indice} {sufixo}.");
            if (publicados) artigo.Publicar();
            contexto.Artigos.Add(artigo);
        }

        await contexto.SaveChangesAsync();
        return sufixo;
    }

    private async Task<IReadOnlyList<string>> ListarAsync(string sufixo, int pagina)
    {
        using var requisicao = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/interno/artigos?q={sufixo}&pagina={pagina}&tamanhoPagina=3");
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeArtigo);

        using var resposta = await _cliente.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        return corpo.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("slug").GetString()!)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> ListarPublicoAsync(string sufixo, int pagina)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/artigos?q={sufixo}&pagina={pagina}&tamanhoPagina=3");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        return corpo.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("slug").GetString()!)
            .ToList();
    }

    /// <summary>Edita pelo endpoint real — é o gesto do editor que reescreve a tupla.</summary>
    private async Task EditarAsync(string slug)
    {
        var (id, versao, titulo, corpoTexto) = await CarregarPorSlugAsync(slug);

        using var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/artigos/{id}")
        {
            Content = JsonContent.Create(new { tipo = "Conceito", titulo = $"{titulo} editado", corpo = corpoTexto }),
        };
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RolesDeArtigo);
        requisicao.Headers.TryAddWithoutValidation("If-Match", versao.ToString());

        using var resposta = await _cliente.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: "a edição precisa acontecer de verdade para reescrever a linha");
    }

    /// <summary>
    /// No caso público não há endpoint de escrita — o `UPDATE` vai direto ao banco, que é o que
    /// importa: o efeito é do heap, não do caminho que provocou a escrita.
    /// </summary>
    private async Task EditarPorBancoAsync(string slug)
    {
        await using var contexto = _fixture.CriarContexto();

        var artigo = contexto.Artigos.Single(a => a.Slug == slug);
        artigo.AtualizarConteudo($"{artigo.Titulo} editado", artigo.Corpo);
        await contexto.SaveChangesAsync();
    }

    private async Task<(int Id, uint Versao, string Titulo, string Corpo)> CarregarPorSlugAsync(string slug)
    {
        await using var contexto = _fixture.CriarContexto();
        var artigo = contexto.Artigos.Single(a => a.Slug == slug);

        return (artigo.Id, artigo.Versao, artigo.Titulo, artigo.Corpo);
    }
}
