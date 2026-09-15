using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// P5 <c>GET /erros?provedor=</c> — o lado da INCLUSÃO do filtro publicação-aware.
///
/// <para>
/// O lado da exclusão (vínculo só por versão em rascunho ⇒ 0 itens) está em
/// <see cref="P5FiltroProvedorProbeTests"/>. Sem este par, aquele teste ficaria verde também se o
/// filtro passasse a excluir TODO erro — que é a forma clássica de "corrigir" um filtro tornando-o
/// inútil. Aqui a publicação está em ordem e o erro TEM de aparecer.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class P5FiltroProvedorPublicacaoHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public P5FiltroProvedorPublicacaoHttpTests(PostgresContainerFixture fixture)
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

    private sealed record Cenario(string ProvedorSlug, string ErroCodigo);

    private async Task<Cenario> MontarAsync(bool provedorPublicado, bool versaoPublicada)
    {
        var sufixo = NovoSufixo();

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
            ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
            : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout p5inc {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor p5inc {sufixo}", $"P5I-{sufixo}", CategoriaProvedor.Nacional);
        if (provedorPublicado)
            provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao p5inc {sufixo}");
        if (versaoPublicada)
            versao.Publicar();
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        var erro = Erro.Criar($"P5I-{sufixo}", OrigemErro.Prefeitura, $"Erro p5inc {sufixo}");
        erro.Publicar();
        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        contexto.ErrosVersoes.Add(ErroVersao.Criar(erro.Id, versao.Id));
        await contexto.SaveChangesAsync();

        return new Cenario(provedor.Slug, erro.Codigo);
    }

    private async Task<int> ContarSobOFiltroAsync(string provedorSlug)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/erros?provedor={provedorSlug}&tamanhoPagina=100");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement
            .GetProperty("items").GetArrayLength();
    }

    [Fact]
    public async Task ErroLigadoAVersaoPublicadaDeProvedorPublicado_ApareceSobOFiltro()
    {
        var cenario = await MontarAsync(provedorPublicado: true, versaoPublicada: true);

        (await ContarSobOFiltroAsync(cenario.ProvedorSlug)).Should().Be(1,
            because: "com a publicação em ordem o vínculo é público e o recorte por provedor tem de devolver o erro");
    }

    /// <summary>
    /// Provedor em rascunho com versão publicada: a versão sozinha não é pública (cascata M19), e
    /// por isso o vínculo também não é. Segundo ancestral do mesmo predicado, medido em separado.
    /// </summary>
    [Fact]
    public async Task ErroLigadoAVersaoPublicadaDeProvedorEmRascunho_NaoApareceSobOFiltro()
    {
        var cenario = await MontarAsync(provedorPublicado: false, versaoPublicada: true);

        (await ContarSobOFiltroAsync(cenario.ProvedorSlug)).Should().Be(0,
            because: "M19: versão de provedor não publicado não é pública, então o vínculo não recorta o erro sob esse provedor");
    }

    /// <summary>
    /// Contraprova de que o erro dos dois casos negativos EXISTE e é publicado — separa "erro
    /// invisível" de "filtro correto", que é a confusão que faria o teste acima passar por acidente.
    /// </summary>
    [Fact]
    public async Task ErroDosCasosNegativos_ContinuaEncontravelPeloCodigo()
    {
        var cenario = await MontarAsync(provedorPublicado: false, versaoPublicada: true);

        using var resposta = await _cliente.GetAsync($"/api/v1/erros?codigo={cenario.ErroCodigo}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement
            .GetProperty("items").GetArrayLength().Should().Be(1);
    }
}
