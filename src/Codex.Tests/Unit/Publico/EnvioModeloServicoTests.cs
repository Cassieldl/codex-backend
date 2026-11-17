using System.Text.Json;
using Codex.Application.DTOs.Interno;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Application.Services;
using Codex.Domain.Constantes;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Codex.Tests.Unit.Publico;

/// <summary>
/// Regra do recorte do envio modelo: máximo leva todo campo da versão com posição, mínimo só os
/// obrigatórios e uma alternativa por escolha, e o que não tem nó vai para <c>camposSemPosicao</c>.
/// </summary>
public sealed class EnvioModeloServicoTests
{
    private const int VersaoLayoutId = 8;

    private readonly Mock<IVersaoProvedorRepositorioPublico> _versaoRepositorio = new();
    private readonly ProvedorServicoPublico _sut;

    public EnvioModeloServicoTests()
    {
        _sut = new ProvedorServicoPublico(Mock.Of<IProvedorRepositorioPublico>(), _versaoRepositorio.Object);

        var provedor = Provedor.Criar("ACME", "INV-ACME", CategoriaProvedor.Nacional);
        provedor.Publicar();
        var versao = VersaoProvedor.Criar(provedor.Id, VersaoLayoutId, "v1");
        versao.Publicar();

        _versaoRepositorio.Setup(r => r.ObterPorSlugsAsync("acme", "v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvedorVersaoBrutaDto(provedor, versao, "8"));
        _versaoRepositorio.Setup(r => r.ObterEstruturaDoLayoutPublicadoAsync(VersaoLayoutId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(Estrutura(), JsonSerializerOptions.Web));
        _versaoRepositorio.Setup(r => r.ListarCamposDoEnvioModeloAsync(versao.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new("numero", "//Numero", EstadoPreenchimento.Obrigatorio),
                new("toma-cnpj", "//Tomador/TomaCNPJ", EstadoPreenchimento.Obrigatorio),
                new("toma-cpf", "//Tomador/TomaCPF", EstadoPreenchimento.Obrigatorio),
                new("toma-nome", "//Tomador/TomaNome", EstadoPreenchimento.Condicional),
                new("descricao", "//Servico/Descricao", EstadoPreenchimento.Opcional),
                new("fantasma", "//Fantasma", EstadoPreenchimento.Obrigatorio),
            ]);
    }

    [Fact]
    public async Task Maximo_TrazTodoCampoDaVersaoComPosicao_ComAncestrais_ESemCampoDeFora()
    {
        var envio = await _sut.ObterEnvioModeloAsync("acme", "v1", AlcanceEnvioModelo.Maximo, CenarioEnvioModelo.Nacional, CancellationToken.None);

        envio.Alcance.Should().Be("maximo");
        envio.VersaoLayoutRotulo.Should().Be("8");
        foreach (var raiz in new[] { envio.Xml, envio.Json })
        {
            Caminhos(raiz).Should().Equal(
                "Envio", "Envio/Numero", "Envio/Tomador", "Envio/Tomador/TomaCNPJ", "Envio/Tomador/TomaCPF",
                "Envio/Tomador/TomaNome", "Envio/Servico", "Envio/Servico/Descricao");
            Nos(raiz).Where(no => no.CampoSlug is not null).Select(no => no.CampoSlug)
                .Should().Equal("numero", "toma-cnpj", "toma-cpf", "toma-nome", "descricao");
        }

        var cnpj = Nos(envio.Xml).Single(no => no.Nome == "TomaCNPJ");
        (cnpj.Escolha, cnpj.ValorModelo, cnpj.Formato).Should().Be((true, "texto(14)", "ambos"));
        Nos(envio.Xml).Single(no => no.Nome == "Servico").Repete.Should().BeTrue();
        envio.CamposSemPosicao.Should().Equal("fantasma");
    }

    [Fact]
    public async Task Minimo_SoObrigatorios_UmaAlternativaPorEscolha_EGrupoVazioSai()
    {
        var envio = await _sut.ObterEnvioModeloAsync("acme", "v1", AlcanceEnvioModelo.Minimo, CenarioEnvioModelo.Nacional, CancellationToken.None);

        envio.Alcance.Should().Be("minimo");
        foreach (var raiz in new[] { envio.Xml, envio.Json })
            Caminhos(raiz).Should().Equal("Envio", "Envio/Numero", "Envio/Tomador", "Envio/Tomador/TomaCNPJ");

        Nos(envio.Xml).Should().NotContain(no => no.CampoSlug == "toma-nome", because: "condicional nunca entra no mínimo");
        envio.CamposSemPosicao.Should().Equal("fantasma");
        (envio.Cenario, envio.CenariosDisponiveis).Should().BeEquivalentTo(("nacional", new[] { "nacional" }));
    }

    [Fact]
    public async Task TomadorEstrangeiro_VersaoSemIdentificacaoEstrangeira_RecusaComCenarioIndisponivel()
    {
        var pedir = () => _sut.ObterEnvioModeloAsync(
            "acme", "v1", AlcanceEnvioModelo.Maximo, CenarioEnvioModelo.TomadorEstrangeiro, CancellationToken.None);

        (await pedir.Should().ThrowAsync<RegraDeNegocioException>())
            .Which.Codigo.Should().Be(CodigosErro.CenarioIndisponivel);
    }

    [Fact]
    public void Minimo_DuasEscolhasSeparadasPorIrmaoQueSaiu_SeguemSendoDuas()
    {
        var raiz = Grupo("Envio", Folha("A", escolha: true), Folha("B", escolha: true), Folha("C"), Folha("D", escolha: true), Folha("E", escolha: true));
        var slugs = new[] { "A", "B", "D", "E" }.ToDictionary(nome => $"//{nome}", nome => nome);

        var minimo = RecorteEnvioModelo.Recortar(raiz, slugs, AlcanceEnvioModelo.Minimo);
        var maximo = RecorteEnvioModelo.Recortar(raiz, slugs, AlcanceEnvioModelo.Maximo);

        minimo.Raiz.Filhos.Select(no => no.Nome).Should().Equal("A", "D");
        maximo.Raiz.Filhos.Select(no => no.Nome).Should().Equal("A", "B", "D", "E");
        minimo.CaminhosComNo.Should().BeEquivalentTo(["//A", "//B", "//D", "//E"],
            because: "alternativa cortada pela escolha tem posição na estrutura");
    }

    private static AtualizarEstruturaVersaoLayoutDto Estrutura()
    {
        var raiz = Grupo("Envio",
            Folha("Numero"),
            Grupo("Tomador",
                Folha("TomaCNPJ", escolha: true, caminhoCampo: "//Tomador/TomaCNPJ", valorModelo: "texto(14)"),
                Folha("TomaCPF", escolha: true, caminhoCampo: "//Tomador/TomaCPF"),
                Folha("TomaNome", caminhoCampo: "//Tomador/TomaNome")),
            Grupo("Servico", Folha("Descricao", caminhoCampo: "//Servico/Descricao")) with { Repete = true },
            Grupo("Extra", Folha("Coisa", caminhoCampo: "//Extra/Coisa")));

        return new AtualizarEstruturaVersaoLayoutDto { Xml = ComCaminhos(raiz, null), Json = ComCaminhos(raiz, null) };
    }

    /// <summary>Caminho de cada nó a partir do pai, para a árvore de teste se escrever só com nomes.</summary>
    private static NoEstruturaLayoutDto ComCaminhos(NoEstruturaLayoutDto no, string? caminhoDoPai)
    {
        var caminho = caminhoDoPai is null ? no.Nome : $"{caminhoDoPai}/{no.Nome}";
        return no with { Caminho = caminho, Filhos = no.Filhos.Select(filho => ComCaminhos(filho, caminho)).ToArray() };
    }

    private static NoEstruturaLayoutDto Grupo(string nome, params NoEstruturaLayoutDto[] filhos) => new()
    {
        Nome = nome,
        Caminho = nome,
        EhGrupo = true,
        Formato = "ambos",
        Filhos = filhos,
    };

    private static NoEstruturaLayoutDto Folha(string nome, bool escolha = false, string? caminhoCampo = null, string valorModelo = "0") => new()
    {
        Nome = nome,
        Caminho = nome,
        Escolha = escolha,
        ValorModelo = valorModelo,
        Formato = "ambos",
        CaminhoCampo = caminhoCampo ?? $"//{nome}",
    };

    private static IEnumerable<NoEstruturaDto> Nos(NoEstruturaDto raiz) => new[] { raiz }.Concat(raiz.Filhos.SelectMany(Nos));

    private static IEnumerable<string> Caminhos(NoEstruturaDto raiz) => Nos(raiz).Select(no => no.Caminho);
}
