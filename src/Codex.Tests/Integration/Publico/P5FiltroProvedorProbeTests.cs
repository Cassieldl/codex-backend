using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// PROBE DE QA (2ª rodada) — o gêmeo do P-08 em <c>GET /erros?provedor=</c> (P5).
///
/// <para>P-08 foi corrigido em <c>ConsultaBuscaPostgres.CarregarErroIdsDoProvedorAsync</c>, que
/// passou a resolver a pertinência erro↔provedor de forma publicação-aware. O MESMO join existe
/// em <c>ErroRepositorioPublico.ListarAsync</c> (linhas 37–44) e lá continua sem nenhum filtro de
/// <c>Publicado</c>/<c>DesativadoEm</c> sobre a versão e o provedor — a correção não passou por
/// este arquivo.</para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class P5FiltroProvedorProbeTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public P5FiltroProvedorProbeTests(PostgresContainerFixture fixture)
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

    /// <summary>
    /// Provedor publicado com UMA versão em rascunho, e um erro publicado vinculado só a ela.
    /// Nenhum dado em rascunho é exibido — o que vaza é a PERTINÊNCIA: o erro aparece recortado
    /// sob o provedor por causa de um vínculo que o público não pode ver.
    /// </summary>
    [Fact]
    public async Task P5_FiltroPorProvedor_NaoDevolveErroCujoUnicoVinculoEVersaoEmRascunho()
    {
        var sufixo = NovoSufixo();
        string provedorSlug;
        string erroCodigo;

        await using (var contexto = _fixture.CriarContexto())
        {
            var maiorOrdem = await contexto.VersoesLayout.AnyAsync()
                ? await contexto.VersoesLayout.MaxAsync(v => v.Ordem)
                : 0;
            var versaoLayout = VersaoLayout.Criar($"Layout p5 {sufixo}", maiorOrdem + 1);
            contexto.VersoesLayout.Add(versaoLayout);
            await contexto.SaveChangesAsync();

            var provedor = Provedor.Criar($"Provedor p5 {sufixo}", $"P5F-{sufixo}", CategoriaProvedor.Nacional);
            provedor.Publicar();
            contexto.Provedores.Add(provedor);
            await contexto.SaveChangesAsync();
            provedorSlug = provedor.Slug;

            // Versão deliberadamente NÃO publicada.
            var versaoRascunho = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versao p5 {sufixo}");
            contexto.VersoesProvedor.Add(versaoRascunho);
            await contexto.SaveChangesAsync();

            var erro = Erro.Criar($"P5F-{sufixo}", OrigemErro.Prefeitura, $"Erro p5 {sufixo}");
            erro.Publicar();
            contexto.Erros.Add(erro);
            await contexto.SaveChangesAsync();
            erroCodigo = erro.Codigo;

            contexto.ErrosVersoes.Add(ErroVersao.Criar(erro.Id, versaoRascunho.Id));
            await contexto.SaveChangesAsync();
        }

        // Sanidade: o erro existe e é publicado, então P5 sem filtro o encontra pelo código.
        using var semFiltro = await _cliente.GetAsync($"/api/v1/erros?codigo={erroCodigo}");
        semFiltro.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonDocument.Parse(await semFiltro.Content.ReadAsStringAsync()).RootElement
            .GetProperty("items").GetArrayLength().Should().Be(1, because: "sanidade — o erro está publicado");

        using var comFiltro = await _cliente.GetAsync($"/api/v1/erros?provedor={provedorSlug}");
        comFiltro.StatusCode.Should().Be(HttpStatusCode.OK);
        var itens = JsonDocument.Parse(await comFiltro.Content.ReadAsStringAsync()).RootElement
            .GetProperty("items");

        itens.GetArrayLength().Should().Be(0,
            because: "o único vínculo entre este erro e este provedor é uma versão em RASCUNHO. "
                     + "Recortar o erro sob o provedor por causa dela repete, em P5, exatamente a incoerência "
                     + "filtro↔exibição que o P-08 corrigiu na busca");
    }
}
