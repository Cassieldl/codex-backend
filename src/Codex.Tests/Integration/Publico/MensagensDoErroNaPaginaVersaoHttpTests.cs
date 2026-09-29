using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// O bloco "Erros conhecidos" da página de versão leva as mensagens que o provedor devolve.
///
/// <para>
/// É por elas que o integrador reconhece o próprio caso: quem chega nesta página tem em mãos o
/// texto que a prefeitura retornou, não o código interno (<c>E-1023</c>) nem o título que damos
/// a ele. Enquanto o resumo trazia só código e título, a lista respondia apenas a quem já sabia
/// o código — e o texto ficava a um clique de distância, na página do erro.
/// </para>
///
/// <para>
/// Medido pelo HTTP porque é contrato de fronteira: as variantes já existiam no domínio e no
/// endpoint de detalhe, e o que faltava era exatamente a projeção desta rota. Conferir a entidade
/// não teria mostrado nada.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class MensagensDoErroNaPaginaVersaoHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public MensagensDoErroNaPaginaVersaoHttpTests(PostgresContainerFixture fixture)
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
    public async Task ErroConhecido_TrazAsMensagensObservadasDoProvedor()
    {
        var rota = await SemearVersaoComErroAsync(
            ["Inscrição municipal inválida ou não cadastrada", "Prestador não localizado no cadastro mobiliário"]);

        var erro = await LerUnicoErroAsync(rota);

        erro.GetProperty("variantes").EnumerateArray().Select(v => v.GetString())
            .Should().ContainInOrder(
                "Inscrição municipal inválida ou não cadastrada",
                "Prestador não localizado no cadastro mobiliário");
    }

    /// <summary>
    /// Coleção sempre presente, `[]` quando o erro não tem variante (U9) — a tela mapeia esta
    /// lista, e ausência derruba o bloco inteiro em vez de só omitir as mensagens. É a classe de
    /// defeito que já apareceu aqui com `versoes` e `avisos`.
    /// </summary>
    [Fact]
    public async Task ErroSemVariante_TrazListaVaziaNuncaAusente()
    {
        var rota = await SemearVersaoComErroAsync([]);

        var erro = await LerUnicoErroAsync(rota);

        var variantes = erro.GetProperty("variantes");
        variantes.ValueKind.Should().Be(JsonValueKind.Array);
        variantes.EnumerateArray().Should().BeEmpty();
    }

    private async Task<JsonElement> LerUnicoErroAsync(string rota)
    {
        var resposta = await _cliente.GetAsync(rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        return corpo.GetProperty("data").GetProperty("errosResumo")
            .GetProperty("itens").EnumerateArray().Single();
    }

    private async Task<string> SemearVersaoComErroAsync(string[] variantes)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout msgerro {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor msgerro {sufixo}", $"MSG-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versão msgerro {sufixo}");
        versao.Publicar();
        contexto.VersoesProvedor.Add(versao);
        await contexto.SaveChangesAsync();

        var erro = Erro.Criar($"E-{sufixo[..4]}", OrigemErro.Prefeitura, $"Erro msgerro {sufixo}");
        erro.Publicar();
        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        if (variantes.Length > 0)
        {
            erro.SubstituirVariantes(variantes);
            await contexto.SaveChangesAsync();
        }

        contexto.ErrosVersoes.Add(ErroVersao.Criar(erro.Id, versao.Id));
        await contexto.SaveChangesAsync();

        return $"/api/v1/provedores/{provedor.Slug}/versoes/{versao.Slug}";
    }
}
