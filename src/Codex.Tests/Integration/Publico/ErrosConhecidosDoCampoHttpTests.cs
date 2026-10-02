using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// O elo campo → erro conhecido, na tabela da versão (escopo funcional v3.1 §6.1).
///
/// <para>
/// O sentido erro → campo já existia na página do erro. Faltava o inverso, que é o que responde
/// a pergunta que traz o leitor ao portal: "o que eu preenchi errado?". Sem ele, a pessoa lê a
/// linha do campo de que suspeita sem saber que existe rejeição registrada exatamente sobre
/// ele — com o erro listado mais abaixo, na mesma página, sem vínculo visível.
/// </para>
///
/// <para>
/// Medido pelo HTTP porque é contrato de fronteira: a associação <c>ErrosCampos</c> já existia no
/// banco e era consumida na outra direção. O que faltava era a projeção desta rota, e conferir a
/// entidade não teria mostrado nada.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class ErrosConhecidosDoCampoHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;

    public ErrosConhecidosDoCampoHttpTests(PostgresContainerFixture fixture)
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
    public async Task CampoComRejeicaoRegistrada_TrazOErroNaPropriaLinha()
    {
        var cenario = await SemearAsync();

        var campo = await LerCampoAsync(cenario.Rota, cenario.SlugCampoComErro);
        var erros = campo.GetProperty("errosConhecidos").EnumerateArray().ToArray();

        erros.Should().HaveCount(1);
        erros[0].GetProperty("codigo").GetString().Should().Be(cenario.CodigoDoErro);
        erros[0].GetProperty("slug").GetString().Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Coleção sempre presente, <c>[]</c> quando o campo não tem rejeição. A tela mapeia esta
    /// lista, e ausência derruba a linha inteira em vez de só omitir o indicador — é a classe de
    /// defeito que já apareceu nesta base com <c>versoes</c>, <c>avisos</c> e
    /// <c>campoLayoutNome</c>, e nas três a manifestação silenciosa foi a que demorou a aparecer.
    /// </summary>
    [Fact]
    public async Task CampoSemRejeicao_TrazListaVaziaNuncaAusente()
    {
        var cenario = await SemearAsync();

        var campo = await LerCampoAsync(cenario.Rota, cenario.SlugCampoSemErro);

        campo.TryGetProperty("errosConhecidos", out var erros).Should().BeTrue();
        erros.EnumerateArray().Should().BeEmpty();
    }

    /// <summary>
    /// O critério é o MESMO do bloco "Erros conhecidos" da página: erro aplicável a ESTA versão.
    ///
    /// <para>
    /// Sem este caso, uma consulta que ignorasse o vínculo de versão passaria nos dois testes
    /// acima e marcaria o campo em toda versão de todo provedor — o indicador apontaria para um
    /// erro que a página não lista, que é pior que não ter indicador nenhum.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ErroDeOutraVersao_NaoMarcaOCampoDestaVersao()
    {
        var cenario = await SemearAsync();

        var campo = await LerCampoAsync(cenario.RotaDaOutraVersao, cenario.SlugCampoComErro);

        campo.GetProperty("errosConhecidos").EnumerateArray().Should().BeEmpty();
    }

    /// <summary>
    /// Rascunho não vaza para o portal público — mesma regra do resto da página.
    /// </summary>
    [Fact]
    public async Task ErroDespublicado_NaoMarcaOCampo()
    {
        var cenario = await SemearAsync(publicarErro: false);

        var campo = await LerCampoAsync(cenario.Rota, cenario.SlugCampoComErro);

        campo.GetProperty("errosConhecidos").EnumerateArray().Should().BeEmpty();
    }

    private async Task<JsonElement> LerCampoAsync(string rota, string slugDoCampo)
    {
        var resposta = await _cliente.GetAsync(rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;

        return corpo.GetProperty("data").GetProperty("camposPorGrupo").EnumerateArray()
            .SelectMany(grupo => grupo.GetProperty("campos").EnumerateArray())
            .Single(campo => campo.GetProperty("campoLayoutSlug").GetString() == slugDoCampo);
    }

    private sealed record Cenario(
        string Rota,
        string RotaDaOutraVersao,
        string SlugCampoComErro,
        string SlugCampoSemErro,
        string CodigoDoErro);

    /// <summary>
    /// Duas versões do mesmo provedor, dois campos na versão sob teste e um erro ligado só ao
    /// primeiro campo e só à primeira versão. É o arranjo mínimo que distingue "ligado ao campo"
    /// de "ligado a qualquer campo" e "aplicável a esta versão" de "aplicável a qualquer versão".
    /// </summary>
    private async Task<Cenario> SemearAsync(bool publicarErro = true)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..10];

        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = contexto.VersoesLayout.Any() ? contexto.VersoesLayout.Max(v => v.Ordem) : 0;
        var versaoLayout = VersaoLayout.Criar($"Layout elo {sufixo}", maiorOrdem + 1);
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var campoComErro = CampoLayout.Criar(
            versaoLayout.Id, $"Campo com erro {sufixo}", $"//Elo/ComErro{sufixo}", "significado", 1, "Prestador");
        var campoSemErro = CampoLayout.Criar(
            versaoLayout.Id, $"Campo sem erro {sufixo}", $"//Elo/SemErro{sufixo}", "significado", 2, "Prestador");
        contexto.CamposLayout.AddRange(campoComErro, campoSemErro);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor elo {sufixo}", $"ELO-{sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.Add(provedor);
        await contexto.SaveChangesAsync();

        var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versão elo A {sufixo}");
        var outraVersao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, $"Versão elo B {sufixo}");
        versao.Publicar();
        outraVersao.Publicar();
        contexto.VersoesProvedor.AddRange(versao, outraVersao);
        await contexto.SaveChangesAsync();

        foreach (var versaoAlvo in new[] { versao, outraVersao })
        {
            contexto.CamposVersao.AddRange(
                CampoVersao.Criar(versaoAlvo.Id, campoComErro.Id, "Texto", EstadoPreenchimento.Obrigatorio, 1),
                CampoVersao.Criar(versaoAlvo.Id, campoSemErro.Id, "Texto", EstadoPreenchimento.Opcional, 2));
        }

        await contexto.SaveChangesAsync();

        var codigo = $"E-{sufixo[..4]}";
        var erro = Erro.Criar(codigo, OrigemErro.Prefeitura, $"Erro elo {sufixo}");

        if (publicarErro)
            erro.Publicar();

        contexto.Erros.Add(erro);
        await contexto.SaveChangesAsync();

        contexto.ErrosVersoes.Add(ErroVersao.Criar(erro.Id, versao.Id));
        contexto.ErrosCampos.Add(ErroCampo.Criar(erro.Id, campoComErro.Id));
        await contexto.SaveChangesAsync();

        return new Cenario(
            $"/api/v1/provedores/{provedor.Slug}/versoes/{versao.Slug}",
            $"/api/v1/provedores/{provedor.Slug}/versoes/{outraVersao.Slug}",
            campoComErro.Slug,
            campoSemErro.Slug,
            codigo);
    }
}
