using System.Net;
using System.Text.Json;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Tests.Integration.Api;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Codex.Tests.Integration.Publico;

/// <summary>
/// "Onde é usado" do verbete P12 medido pelo JSON literal da resposta HTTP, contra um Postgres
/// real: forma do item (6 chaves, <c>destinoXml</c> sempre presente), valores crus, ordem total
/// (nome do provedor, id da versão), estabilidade sob UPDATE e as cinco metades do predicado de
/// visibilidade.
///
/// <para>
/// Provedores semeados em ordem Maria, Zeta, Alfa — nem alfabética nem inversa. As duas versões
/// de Zeta têm rótulo em ordem CONTRÁRIA ao id ("Versao Z" antes de "Versao A"), então ordenar
/// por rótulo, ou omitir o desempate, produz outra sequência.
/// </para>
///
/// <para>
/// A versão de layout NÃO é marcada como Vigente: o slot é global e pertence a outra classe.
/// </para>
/// </summary>
[Collection(PostgresCollection.Nome)]
public sealed class LayoutUsosDoCampoHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    private CodexApiFactoryComBanco _factory = null!;
    private HttpClient _cliente = null!;
    private Cenario _cenario = null!;

    public LayoutUsosDoCampoHttpTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new CodexApiFactoryComBanco(_fixture.ConnectionString);
        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _cenario = await SemearAsync();
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        await LimparAsync();
    }

    [Fact]
    public async Task OndeEUsado_CadaItemTemExatamenteAsSeisChaves_ComDestinoXmlSempreSerializado()
    {
        var usos = await ObterUsosAsync(_cenario.CampoAlvoSlug);

        usos.Should().HaveCount(4);
        foreach (var uso in usos)
        {
            uso.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
                ["provedorSlug", "provedorNome", "versaoSlug", "versaoRotulo", "destinoXml", "estadoPreenchimento"]);
        }

        var semDestino = usos.Single(u => u.GetProperty("provedorSlug").GetString() == _cenario.AlfaSlug);
        semDestino.TryGetProperty("destinoXml", out var destino).Should().BeTrue(because: "nulo omitido derruba a validação do cliente");
        destino.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task OndeEUsado_TrazDestinoEEstadoCrus_EEstadoSempreTexto()
    {
        var usos = await ObterUsosAsync(_cenario.CampoAlvoSlug);

        usos.Should().OnlyContain(u => u.GetProperty("estadoPreenchimento").ValueKind == JsonValueKind.String);

        var alfa = Uso(usos, _cenario.AlfaSlug, "versao-a1");
        alfa.GetProperty("destinoXml").ValueKind.Should().Be(JsonValueKind.Null);
        alfa.GetProperty("estadoPreenchimento").GetString().Should().Be("Opcional");

        var maria = Uso(usos, _cenario.MariaSlug, "versao-m");
        maria.GetProperty("destinoXml").GetString().Should().Be("//m/x");
        maria.GetProperty("estadoPreenchimento").GetString().Should().Be("Condicional");

        var zetaZ = Uso(usos, _cenario.ZetaSlug, "versao-z");
        zetaZ.GetProperty("destinoXml").GetString().Should().Be("//z/a | //z/b");
        zetaZ.GetProperty("estadoPreenchimento").GetString().Should().Be("Obrigatorio");

        var zetaA = Uso(usos, _cenario.ZetaSlug, "versao-a");
        zetaA.GetProperty("destinoXml").GetString().Should().Be("…");
        zetaA.GetProperty("estadoPreenchimento").GetString().Should().Be("Obrigatorio");
    }

    [Fact]
    public async Task OndeEUsado_OrdenaPorNomeDoProvedor_EDepoisPorIdDaVersao()
    {
        var usos = await ObterUsosAsync(_cenario.CampoAlvoSlug);

        Sequencia(usos).Should().Equal(SequenciaEsperada());
    }

    [Fact]
    public async Task OndeEUsado_MantemAOrdem_AposUpdateDasLinhasDeUmaVersaoEmpatada()
    {
        // Cada UPDATE reescreve as linhas da versão atualizada. As duas versões de Zeta (empatadas
        // no nome do provedor) são atualizadas uma de cada vez, e a sequência tem de continuar a
        // mesma depois de cada uma: sem desempate por chave única, a ordem fica a cargo do plano.
        await AtualizarLinhasDaVersaoAsync(_cenario.UsoZetaAId, _cenario.VersaoZetaAId, "…");
        var depoisDeAtualizarVersaoA = Sequencia(await ObterUsosAsync(_cenario.CampoAlvoSlug));

        await AtualizarLinhasDaVersaoAsync(_cenario.UsoZetaZId, _cenario.VersaoZetaZId, "//z/a | //z/b");
        var depoisDeAtualizarVersaoZ = Sequencia(await ObterUsosAsync(_cenario.CampoAlvoSlug));

        depoisDeAtualizarVersaoA.Should().Equal(SequenciaEsperada());
        depoisDeAtualizarVersaoZ.Should().Equal(SequenciaEsperada());
    }

    [Fact]
    public async Task OndeEUsado_NaoTrazProvedorOuVersaoForaDoPredicado_NemUsoDeOutroCampo()
    {
        var usos = await ObterUsosAsync(_cenario.CampoAlvoSlug);
        var slugsDeProvedor = usos.Select(u => u.GetProperty("provedorSlug").GetString()!).ToList();

        slugsDeProvedor.Should().NotContain(_cenario.ProvedoresExcluidosSlugs);
        slugsDeProvedor.Should().NotContain(_cenario.OutroProvedorSlug);
        usos.Should().HaveCount(4);
    }

    [Fact]
    public async Task OndeEUsado_CampoSemUso_RetornaArrayVazio()
    {
        var usos = await ObterUsosAsync(_cenario.CampoVazioSlug);

        usos.Should().BeEmpty();
    }

    [Fact]
    public async Task OndeEUsado_FiltraPorCampo_ECampoOutroTrazSoOProprioUso()
    {
        var usos = await ObterUsosAsync(_cenario.CampoOutroSlug);

        usos.Should().ContainSingle();
        usos[0].GetProperty("provedorSlug").GetString().Should().Be(_cenario.OutroProvedorSlug);
    }

    // ---------------------------------------------------------------------------------- apoio

    private List<string> SequenciaEsperada() =>
    [
        $"{_cenario.AlfaSlug}/versao-a1",
        $"{_cenario.MariaSlug}/versao-m",
        $"{_cenario.ZetaSlug}/versao-z",
        $"{_cenario.ZetaSlug}/versao-a",
    ];

    private static List<string> Sequencia(List<JsonElement> usos) =>
        usos.Select(u => $"{u.GetProperty("provedorSlug").GetString()}/{u.GetProperty("versaoSlug").GetString()}").ToList();

    private static JsonElement Uso(List<JsonElement> usos, string provedorSlug, string versaoSlug) =>
        usos.Single(u => u.GetProperty("provedorSlug").GetString() == provedorSlug
            && u.GetProperty("versaoSlug").GetString() == versaoSlug);

    private async Task<List<JsonElement>> ObterUsosAsync(string campoSlug)
    {
        using var resposta = await _cliente.GetAsync($"/api/v1/layout/campos/{campoSlug}");
        var texto = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, because: texto);

        var corpo = JsonDocument.Parse(texto).RootElement;
        var ondeEUsado = corpo.GetProperty("data").GetProperty("ondeEUsado");

        ondeEUsado.ValueKind.Should().Be(JsonValueKind.Array);
        return ondeEUsado.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private async Task AtualizarLinhasDaVersaoAsync(int usoId, int versaoId, string destinoXml)
    {
        await using var contexto = _fixture.CriarContexto();

        var uso = await contexto.CamposVersao.FirstAsync(cv => cv.Id == usoId);
        uso.AtualizarComportamento(
            "C", EstadoPreenchimento.Obrigatorio, ordem: 1,
            destinoXml: destinoXml, notaPreenchimento: "atualizada");

        var versao = await contexto.VersoesProvedor.FirstAsync(v => v.Id == versaoId);
        versao.AtualizarIntroducao("atualizada");

        await contexto.SaveChangesAsync();
    }

    private sealed record Cenario
    {
        public required string CampoAlvoSlug { get; init; }
        public required string CampoOutroSlug { get; init; }
        public required string CampoVazioSlug { get; init; }
        public required string AlfaSlug { get; init; }
        public required string MariaSlug { get; init; }
        public required string ZetaSlug { get; init; }
        public required string OutroProvedorSlug { get; init; }
        public required string[] ProvedoresExcluidosSlugs { get; init; }
        public required int UsoZetaZId { get; init; }
        public required int VersaoZetaZId { get; init; }
        public required int UsoZetaAId { get; init; }
        public required int VersaoZetaAId { get; init; }
    }

    private async Task<Cenario> SemearAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var maiorOrdem = await contexto.VersoesLayout.MaxAsync(v => (int?)v.Ordem) ?? 0;
        var versaoLayout = VersaoLayout.Criar($"Layout Usos {_sufixo}", maiorOrdem + 1);
        versaoLayout.Publicar();
        contexto.VersoesLayout.Add(versaoLayout);
        await contexto.SaveChangesAsync();

        var campoAlvo = CampoLayout.Criar(versaoLayout.Id, $"campo alvo {_sufixo}", $"//raiz/Alvo{_sufixo}", "Campo alvo", ordem: 1);
        var campoOutro = CampoLayout.Criar(versaoLayout.Id, $"campo outro {_sufixo}", $"//raiz/Outro{_sufixo}", "Campo outro", ordem: 2);
        var campoVazio = CampoLayout.Criar(versaoLayout.Id, $"campo vazio {_sufixo}", $"//raiz/Vazio{_sufixo}", "Campo sem uso", ordem: 3);
        foreach (var campo in new[] { campoAlvo, campoOutro, campoVazio })
            campo.Publicar();
        contexto.CamposLayout.AddRange(campoAlvo, campoOutro, campoVazio);
        await contexto.SaveChangesAsync();

        async Task<(Provedor Provedor, List<VersaoProvedor> Versoes)> SemearProvedorAsync(
            string nome, string identificador, params string[] rotulos)
        {
            var provedor = Provedor.Criar($"{nome} {_sufixo}", $"USO-{identificador}-{_sufixo}", CategoriaProvedor.Nacional);
            provedor.Publicar();
            contexto.Provedores.Add(provedor);
            await contexto.SaveChangesAsync();

            var versoes = new List<VersaoProvedor>();
            foreach (var rotulo in rotulos)
            {
                var versao = VersaoProvedor.Criar(provedor.Id, versaoLayout.Id, rotulo);
                versao.Publicar();
                contexto.VersoesProvedor.Add(versao);
                await contexto.SaveChangesAsync();
                versoes.Add(versao);
            }

            return (provedor, versoes);
        }

        async Task<CampoVersao> UsarAsync(
            VersaoProvedor versao, CampoLayout campo, EstadoPreenchimento estado, string? destino)
        {
            var uso = CampoVersao.Criar(versao.Id, campo.Id, "C", estado, ordem: 1, destinoXml: destino);
            contexto.CamposVersao.Add(uso);
            await contexto.SaveChangesAsync();
            return uso;
        }

        // Inserção: Maria, Zeta, Alfa — diferente da ordem alfabética e da inversa.
        var maria = await SemearProvedorAsync("Maria", "M", "Versao M");
        await UsarAsync(maria.Versoes[0], campoAlvo, EstadoPreenchimento.Condicional, "//m/x");

        var zeta = await SemearProvedorAsync("Zeta", "Z", "Versao Z", "Versao A");
        var usoZetaZ = await UsarAsync(zeta.Versoes[0], campoAlvo, EstadoPreenchimento.Obrigatorio, "//z/a | //z/b");
        var usoZetaA = await UsarAsync(zeta.Versoes[1], campoAlvo, EstadoPreenchimento.Obrigatorio, "…");

        var alfa = await SemearProvedorAsync("Alfa", "A", "Versao A1");
        await UsarAsync(alfa.Versoes[0], campoAlvo, EstadoPreenchimento.Opcional, null);

        // Fora do predicado, cada um isolando uma metade.
        var provedorDespublicado = await SemearProvedorAsync("Excl Provedor Despublicado", "XPD", "Versao XPD");
        await UsarAsync(provedorDespublicado.Versoes[0], campoAlvo, EstadoPreenchimento.Opcional, "//xpd");
        provedorDespublicado.Provedor.Despublicar();

        var versaoDespublicada = await SemearProvedorAsync("Excl Versao Despublicada", "XVD", "Versao XVD");
        await UsarAsync(versaoDespublicada.Versoes[0], campoAlvo, EstadoPreenchimento.Opcional, "//xvd");
        versaoDespublicada.Versoes[0].Despublicar();

        var versaoDesativada = await SemearProvedorAsync("Excl Versao Desativada", "XVA", "Versao XVA");
        await UsarAsync(versaoDesativada.Versoes[0], campoAlvo, EstadoPreenchimento.Opcional, "//xva");
        versaoDesativada.Versoes[0].Desativar();

        var provedorDesativado = await SemearProvedorAsync("Excl Provedor Desativado", "XPA", "Versao XPA");
        await UsarAsync(provedorDesativado.Versoes[0], campoAlvo, EstadoPreenchimento.Opcional, "//xpa");
        provedorDesativado.Provedor.Desativar();

        var campoVersaoDesativado = await SemearProvedorAsync("Excl Campo Desativado", "XCV", "Versao XCV");
        var usoDesativado = await UsarAsync(campoVersaoDesativado.Versoes[0], campoAlvo, EstadoPreenchimento.Opcional, "//xcv");
        usoDesativado.Desativar();

        var outro = await SemearProvedorAsync("Outro Campo", "OUT", "Versao OUT");
        await UsarAsync(outro.Versoes[0], campoOutro, EstadoPreenchimento.Opcional, "//outro");

        await contexto.SaveChangesAsync();

        return new Cenario
        {
            CampoAlvoSlug = campoAlvo.Slug,
            CampoOutroSlug = campoOutro.Slug,
            CampoVazioSlug = campoVazio.Slug,
            AlfaSlug = alfa.Provedor.Slug,
            MariaSlug = maria.Provedor.Slug,
            ZetaSlug = zeta.Provedor.Slug,
            OutroProvedorSlug = outro.Provedor.Slug,
            ProvedoresExcluidosSlugs =
            [
                provedorDespublicado.Provedor.Slug,
                versaoDespublicada.Provedor.Slug,
                versaoDesativada.Provedor.Slug,
                provedorDesativado.Provedor.Slug,
                campoVersaoDesativado.Provedor.Slug,
            ],
            UsoZetaZId = usoZetaZ.Id,
            VersaoZetaZId = zeta.Versoes[0].Id,
            UsoZetaAId = usoZetaA.Id,
            VersaoZetaAId = zeta.Versoes[1].Id,
        };
    }

    /// <summary>Hard delete, filhos antes dos pais, selecionando só o que este cenário semeou.</summary>
    private async Task LimparAsync()
    {
        await using var contexto = _fixture.CriarContexto();

        var versoesLayout = await contexto.VersoesLayout.Where(v => v.Rotulo.Contains(_sufixo)).ToListAsync();
        var idsLayout = versoesLayout.Select(v => v.Id).ToList();

        var campos = await contexto.CamposLayout.Where(c => idsLayout.Contains(c.VersaoLayoutId)).ToListAsync();
        var idsCampo = campos.Select(c => c.Id).ToList();

        var camposVersao = await contexto.CamposVersao.Where(cv => idsCampo.Contains(cv.CampoLayoutId)).ToListAsync();
        var versoesProvedor = await contexto.VersoesProvedor.Where(v => idsLayout.Contains(v.VersaoLayoutId)).ToListAsync();
        var idsProvedor = versoesProvedor.Select(v => v.ProvedorId).Distinct().ToList();
        var provedores = await contexto.Provedores.Where(p => idsProvedor.Contains(p.Id)).ToListAsync();

        contexto.CamposVersao.RemoveRange(camposVersao);
        contexto.VersoesProvedor.RemoveRange(versoesProvedor);
        contexto.Provedores.RemoveRange(provedores);
        contexto.CamposLayout.RemoveRange(campos);
        contexto.VersoesLayout.RemoveRange(versoesLayout);

        await contexto.SaveChangesAsync();
    }
}
