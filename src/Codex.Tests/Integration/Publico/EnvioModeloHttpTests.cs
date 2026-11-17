using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// Envio modelo da versão de provedor contra um Postgres real, lido pelo JSON literal da resposta:
/// o contrato HTTP só se prova pelo texto, nunca pelo DTO desserializado pelo próprio .NET.
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class EnvioModeloHttpTests : IAsyncLifetime
{
    private const string RoleAtualizarLayout = "versoeslayout-atualizar";

    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private Cenario _cenario = null!;

    public EnvioModeloHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient();
        _cenario = await SemearAsync();
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Maximo_Responde200_ComTodoCampoDaVersao_ESemPosicaoListado()
    {
        using var resposta = await _cliente.GetAsync($"{_cenario.RotaPublicada}?alcance=maximo");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: texto);
        resposta.Headers.ETag.Should().NotBeNull();

        var dados = JsonDocument.Parse(texto).RootElement.GetProperty("data");
        dados.EnumerateObject().Select(p => p.Name).Should().Equal(
            "alcance", "cenario", "cenariosDisponiveis", "versaoLayoutRotulo", "xml", "json", "camposSemPosicao");
        dados.GetProperty("alcance").GetString().Should().Be("maximo");
        dados.GetProperty("cenario").GetString().Should().Be("nacional", because: "sem o parâmetro, o cenário é o nacional");
        dados.GetProperty("cenariosDisponiveis").EnumerateArray().Select(e => e.GetString()).Should().Equal("nacional");
        dados.GetProperty("versaoLayoutRotulo").GetString().Should().Be(_cenario.RotuloLayout);
        dados.GetProperty("camposSemPosicao").EnumerateArray().Select(e => e.GetString()).Should().Equal(_cenario.SlugFantasma);

        var xml = dados.GetProperty("xml");
        xml.GetProperty("nome").GetString().Should().Be("Envio");
        Nomes(xml).Should().Equal("Envio", "Numero", "Tomador", "TomaCNPJ", "TomaCPF", "TomaNome");
        var cnpj = xml.GetProperty("filhos")[1].GetProperty("filhos")[0];
        cnpj.EnumerateObject().Select(p => p.Name).Should().Equal(
            "nome", "caminho", "ehGrupo", "repete", "escolha", "valorModelo", "campoSlug", "formato", "filhos");
        cnpj.GetProperty("escolha").GetBoolean().Should().BeTrue();
        cnpj.GetProperty("campoSlug").GetString().Should().Be(_cenario.SlugCnpj);

        var json = dados.GetProperty("json");
        json.GetProperty("nome").GetString().Should().Be("Documento");
        Nomes(json).Should().Equal("Documento", "Numero", "Tomador", "TomaCNPJ", "TomaCPF", "TomaNome");
    }

    [Fact]
    public async Task Minimo_Responde200_SoComObrigatorios_EUmaAlternativaDaEscolha()
    {
        using var resposta = await _cliente.GetAsync($"{_cenario.RotaPublicada}?alcance=minimo");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: texto);
        var dados = JsonDocument.Parse(texto).RootElement.GetProperty("data");
        dados.GetProperty("alcance").GetString().Should().Be("minimo");
        Nomes(dados.GetProperty("xml")).Should().Equal("Envio", "Numero", "Tomador", "TomaCNPJ");
        Nomes(dados.GetProperty("json")).Should().Equal("Documento", "Numero", "Tomador", "TomaCNPJ");
        texto.Should().NotContain(_cenario.SlugNome, because: "campo condicional nunca entra no mínimo");
        dados.GetProperty("camposSemPosicao").EnumerateArray().Select(e => e.GetString()).Should().Equal(_cenario.SlugFantasma);
    }

    [Fact]
    public async Task MesmoEnvio_ComIfNoneMatchDaRespostaAnterior_Responde304()
    {
        var rota = $"{_cenario.RotaPublicada}?alcance=maximo";
        using var primeira = await _cliente.GetAsync(rota);

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.TryAddWithoutValidation("If-None-Match", primeira.Headers.ETag!.ToString());
        using var segunda = await _cliente.SendAsync(requisicao);

        segunda.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?alcance=")]
    [InlineData("?alcance=total")]
    [InlineData("?alcance=MAXIMO")]
    [InlineData("?alcance=0")]
    public async Task AlcanceAusenteOuInvalido_Responde400(string query)
    {
        using var resposta = await _cliente.GetAsync($"{_cenario.RotaPublicada}{query}");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: texto);
        var erro = JsonDocument.Parse(texto).RootElement.GetProperty("error");
        erro.GetProperty("code").GetString().Should().Be("MALFORMED_REQUEST");
        erro.GetProperty("details").EnumerateArray().Select(d => d.GetProperty("field").GetString()).Should().Equal("alcance");
    }

    [Theory]
    [InlineData("?alcance=minimo&cenario=exterior")]
    [InlineData("?alcance=maximo&cenario=NACIONAL")]
    [InlineData("?alcance=maximo&cenario=exportacao")]
    [InlineData("?alcance=minimo&cenario=Retencoes-Federais")]
    public async Task CenarioDesconhecido_Responde400NoMesmoFormatoDoAlcance(string query)
    {
        using var resposta = await _cliente.GetAsync($"{_cenario.RotaPublicada}{query}");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: texto);
        var erro = JsonDocument.Parse(texto).RootElement.GetProperty("error");
        erro.GetProperty("code").GetString().Should().Be("MALFORMED_REQUEST");
        erro.GetProperty("details").EnumerateArray().Select(d => d.GetProperty("field").GetString()).Should().Equal("cenario");
    }

    [Theory]
    [InlineData("tomador-estrangeiro")]
    [InlineData("exportacao-servico")]
    [InlineData("construcao-civil")]
    [InlineData("retencoes-federais")]
    public async Task CenarioQueAVersaoNaoAceita_Responde422CenarioIndisponivel(string cenario)
    {
        using var resposta = await _cliente.GetAsync($"{_cenario.RotaPublicada}?alcance=minimo&cenario={cenario}");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, because: texto);
        JsonDocument.Parse(texto).RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("CENARIO_INDISPONIVEL");
    }

    [Theory]
    [InlineData("oculta")]
    [InlineData("inexistente")]
    [InlineData("provedor-oculto")]
    public async Task VersaoOcultaOuInexistente_Responde404NaoEncontrado(string caso)
    {
        var rota = caso switch
        {
            "oculta" => _cenario.RotaOculta,
            "inexistente" => $"/api/v1/provedores/{_cenario.ProvedorSlug}/versoes/nunca-existiu-{_sufixo}/envio-modelo",
            _ => _cenario.RotaProvedorOculto,
        };

        using var resposta = await _cliente.GetAsync($"{rota}?alcance=maximo");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound, because: texto);
        JsonDocument.Parse(texto).RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("NAO_ENCONTRADO");
    }

    [Fact]
    public async Task LayoutSemEstrutura_Responde404EstruturaIndisponivel()
    {
        using var resposta = await _cliente.GetAsync($"{_cenario.RotaSemEstrutura}?alcance=minimo");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound, because: texto);
        JsonDocument.Parse(texto).RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("ESTRUTURA_INDISPONIVEL");
    }

    // ---------------------------------------------------------------------------------- apoio

    private static IEnumerable<string> Nomes(JsonElement no) =>
        new[] { no.GetProperty("nome").GetString()! }
            .Concat(no.GetProperty("filhos").EnumerateArray().SelectMany(Nomes));

    private sealed record Cenario
    {
        public required string ProvedorSlug { get; init; }
        public required string RotuloLayout { get; init; }
        public required string RotaPublicada { get; init; }
        public required string RotaOculta { get; init; }
        public required string RotaSemEstrutura { get; init; }
        public required string RotaProvedorOculto { get; init; }
        public required string SlugCnpj { get; init; }
        public required string SlugNome { get; init; }
        public required string SlugFantasma { get; init; }
    }

    private async Task<Cenario> SemearAsync()
    {
        await using var contexto = _fixture.CriarContexto();
        var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => (int?)v.Ordem) ?? 0;

        var comEstrutura = VersaoLayout.Criar($"Envio modelo {_sufixo}", maiorOrdem + 1);
        var semEstrutura = VersaoLayout.Criar($"Envio modelo vazio {_sufixo}", maiorOrdem + 2);
        comEstrutura.Publicar();
        semEstrutura.Publicar();
        contexto.VersoesLayout.AddRange(comEstrutura, semEstrutura);
        await contexto.SaveChangesAsync();

        var prefixo = $"//Envio{_sufixo}";
        CampoLayout Campo(string nome, int ordem) => CampoLayout.Criar(comEstrutura.Id, nome, $"{prefixo}/{nome}", $"Significado de {nome}.", ordem);
        var numero = Campo("Numero", 1);
        var cnpj = Campo("TomaCNPJ", 2);
        var cpf = Campo("TomaCPF", 3);
        var nome = Campo("TomaNome", 4);
        var fantasma = Campo("Fantasma", 5);
        var foraDaVersao = Campo("Observacao", 6);
        contexto.CamposLayout.AddRange(numero, cnpj, cpf, nome, fantasma, foraDaVersao);
        await contexto.SaveChangesAsync();

        var provedor = Provedor.Criar($"Provedor envio {_sufixo}", $"ENVIO-{_sufixo}", CategoriaProvedor.Nacional);
        var provedorOculto = Provedor.Criar($"Provedor envio oculto {_sufixo}", $"ENVIO-OC-{_sufixo}", CategoriaProvedor.Nacional);
        provedor.Publicar();
        contexto.Provedores.AddRange(provedor, provedorOculto);
        await contexto.SaveChangesAsync();

        var publicada = VersaoProvedor.Criar(provedor.Id, comEstrutura.Id, $"Publicada {_sufixo}");
        var oculta = VersaoProvedor.Criar(provedor.Id, comEstrutura.Id, $"Oculta {_sufixo}");
        var daSemEstrutura = VersaoProvedor.Criar(provedor.Id, semEstrutura.Id, $"Sem estrutura {_sufixo}");
        var doProvedorOculto = VersaoProvedor.Criar(provedorOculto.Id, comEstrutura.Id, $"Do oculto {_sufixo}");
        publicada.Publicar();
        daSemEstrutura.Publicar();
        doProvedorOculto.Publicar();
        contexto.VersoesProvedor.AddRange(publicada, oculta, daSemEstrutura, doProvedorOculto);
        await contexto.SaveChangesAsync();

        contexto.CamposVersao.AddRange(
            CampoVersao.Criar(publicada.Id, numero.Id, "texto", EstadoPreenchimento.Obrigatorio),
            CampoVersao.Criar(publicada.Id, cnpj.Id, "texto", EstadoPreenchimento.Obrigatorio),
            CampoVersao.Criar(publicada.Id, cpf.Id, "texto", EstadoPreenchimento.Obrigatorio),
            CampoVersao.Criar(publicada.Id, nome.Id, "texto", EstadoPreenchimento.Condicional),
            CampoVersao.Criar(publicada.Id, fantasma.Id, "texto", EstadoPreenchimento.Obrigatorio),
            CampoVersao.Criar(daSemEstrutura.Id, numero.Id, "texto", EstadoPreenchimento.Obrigatorio));
        await contexto.SaveChangesAsync();

        await GravarEstruturaAsync(comEstrutura.Id, comEstrutura.Versao, Arvores(prefixo));

        string Rota(Provedor dono, VersaoProvedor versao) => $"/api/v1/provedores/{dono.Slug}/versoes/{versao.Slug}/envio-modelo";
        return new Cenario
        {
            ProvedorSlug = provedor.Slug,
            RotuloLayout = comEstrutura.Rotulo,
            RotaPublicada = Rota(provedor, publicada),
            RotaOculta = Rota(provedor, oculta),
            RotaSemEstrutura = Rota(provedor, daSemEstrutura),
            RotaProvedorOculto = Rota(provedorOculto, doProvedorOculto),
            SlugCnpj = cnpj.Slug,
            SlugNome = nome.Slug,
            SlugFantasma = fantasma.Slug,
        };
    }

    /// <summary>XML e JSON com a mesma forma: Tomador traz CNPJ e CPF em escolha; Observacao não é da versão.</summary>
    private static object Arvores(string prefixo)
    {
        object Arvore(string raiz)
        {
            object Folha(string caminhoPai, string nome, bool escolha = false) => new
            {
                nome,
                caminho = $"{caminhoPai}/{nome}",
                ehGrupo = false,
                repete = false,
                escolha,
                valorModelo = "0",
                formato = "ambos",
                caminhoCampo = $"{prefixo}/{nome}",
                filhos = Array.Empty<object>(),
            };

            var tomador = $"{raiz}/Tomador";
            return new
            {
                nome = raiz,
                caminho = raiz,
                ehGrupo = true,
                formato = "ambos",
                filhos = new object[]
                {
                    Folha(raiz, "Numero"),
                    new
                    {
                        nome = "Tomador",
                        caminho = tomador,
                        ehGrupo = true,
                        formato = "ambos",
                        filhos = new[] { Folha(tomador, "TomaCNPJ", escolha: true), Folha(tomador, "TomaCPF", escolha: true), Folha(tomador, "TomaNome") },
                    },
                    Folha(raiz, "Observacao"),
                },
            };
        }

        return new { xml = Arvore("Envio"), json = Arvore("Documento") };
    }

    private async Task GravarEstruturaAsync(int versaoLayoutId, uint versao, object corpo)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/interno/layout/versoes/{versaoLayoutId}/estrutura")
        {
            Content = new StringContent(JsonSerializer.Serialize(corpo, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
        };
        requisicao.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        requisicao.Headers.Add(TestAuthHandler.HeaderRoles, RoleAtualizarLayout);
        requisicao.Headers.TryAddWithoutValidation("If-Match", versao.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var resposta = await _cliente.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: await resposta.Content.ReadAsStringAsync());
    }
}
