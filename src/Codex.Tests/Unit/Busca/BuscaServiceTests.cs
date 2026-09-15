using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Application.UseCases.Busca;
using FluentAssertions;
using Moq;

namespace Codex.Tests.Unit.Busca;

/// <summary>
/// <see cref="BuscaService"/> é a camada fina de validação/proteção de custo — testada com
/// <see cref="IConsultaBusca"/> mockado (§6.7 da análise: mock prova que o C# chamou o método
/// certo com os parâmetros certos, nunca que o SQL funciona — isso é papel dos testes de
/// integração de <c>ConsultaBuscaPostgres</c>).
/// </summary>
public sealed class BuscaServiceTests
{
    private readonly Mock<IConsultaBusca> _consultaBusca = new();
    private readonly BuscaService _sut;

    public BuscaServiceTests()
    {
        _sut = new BuscaService(_consultaBusca.Object);
    }

    private static ResultadoBuscaDto ResultadoVazio() => new()
    {
        Termo = "x",
        TermoUsado = "x",
        Qualidade = "vazia",
        TotalGeral = 0,
        Grupos = [],
    };

    [Fact]
    public async Task BuscarAsync_TermoValido_DelegaParaConsultaBuscaComParametrosCorretos()
    {
        _consultaBusca
            .Setup(c => c.BuscarAsync("aliquota", "prefeitura-sp", null, 1, 20, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoVazio());

        var entrada = new BuscaConsultaEntrada { Termo = "aliquota", ProvedorSlug = "prefeitura-sp" };

        var resultado = await _sut.BuscarAsync(entrada, incluirRascunho: false, CancellationToken.None);

        resultado.Should().NotBeNull();
        _consultaBusca.Verify(
            c => c.BuscarAsync("aliquota", "prefeitura-sp", null, 1, 20, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuscarAsync_IncluirRascunhoTrue_RepassaParaConsultaBusca()
    {
        _consultaBusca
            .Setup(c => c.BuscarAsync(It.IsAny<string>(), null, null, 1, 20, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoVazio());

        var entrada = new BuscaConsultaEntrada { Termo = "erro123" };

        await _sut.BuscarAsync(entrada, incluirRascunho: true, CancellationToken.None);

        _consultaBusca.Verify(
            c => c.BuscarAsync(It.IsAny<string>(), null, null, 1, 20, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public async Task BuscarAsync_TermoMenorQueMinimo_LancaTermoBuscaInvalido_NuncaChamaConsulta(string termo)
    {
        var entrada = new BuscaConsultaEntrada { Termo = termo };

        var acao = async () => await _sut.BuscarAsync(entrada, incluirRascunho: false, CancellationToken.None);

        await acao.Should().ThrowAsync<TermoBuscaInvalidoException>();
        _consultaBusca.Verify(c => c.BuscarAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BuscarAsync_TermoMaiorQue300Caracteres_LancaTermoBuscaInvalido()
    {
        var entrada = new BuscaConsultaEntrada { Termo = new string('a', 301) };

        var acao = async () => await _sut.BuscarAsync(entrada, incluirRascunho: false, CancellationToken.None);

        var excecao = await acao.Should().ThrowAsync<TermoBuscaInvalidoException>();

        // BAIXO-B08 na origem: sem paramName, a Message já nasce limpa — o controller não precisa
        // reescrever o texto do cap, que continua vivendo num lugar só.
        excecao.Which.Message.Should().Be("O termo de busca precisa ter entre 2 e 300 caracteres.");
    }

    [Fact]
    public async Task BuscarAsync_TamanhoPaginaAcimaDoCap_ClampaEm50()
    {
        _consultaBusca
            .Setup(c => c.BuscarAsync(It.IsAny<string>(), null, null, 1, 50, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoVazio());

        var entrada = new BuscaConsultaEntrada { Termo = "termo", TamanhoPagina = 999 };

        await _sut.BuscarAsync(entrada, incluirRascunho: false, CancellationToken.None);

        _consultaBusca.Verify(
            c => c.BuscarAsync(It.IsAny<string>(), null, null, 1, 50, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuscarAsync_PaginaZeroOuNegativa_UsaPaginaUm()
    {
        _consultaBusca
            .Setup(c => c.BuscarAsync(It.IsAny<string>(), null, null, 1, 20, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoVazio());

        var entrada = new BuscaConsultaEntrada { Termo = "termo", Pagina = -5 };

        await _sut.BuscarAsync(entrada, incluirRascunho: false, CancellationToken.None);

        _consultaBusca.Verify(
            c => c.BuscarAsync(It.IsAny<string>(), null, null, 1, 20, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuscarAsync_TamanhoPaginaAusente_UsaPadraoVinte()
    {
        _consultaBusca
            .Setup(c => c.BuscarAsync(It.IsAny<string>(), null, null, 1, 20, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoVazio());

        var entrada = new BuscaConsultaEntrada { Termo = "termo", TamanhoPagina = null };

        await _sut.BuscarAsync(entrada, incluirRascunho: false, CancellationToken.None);

        _consultaBusca.Verify(
            c => c.BuscarAsync(It.IsAny<string>(), null, null, 1, 20, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuscarAsync_TermoComEspacosNasPontas_AparaAntesDeDelegar()
    {
        _consultaBusca
            .Setup(c => c.BuscarAsync("aliquota", null, null, 1, 20, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoVazio());

        var entrada = new BuscaConsultaEntrada { Termo = "  aliquota  " };

        await _sut.BuscarAsync(entrada, incluirRascunho: false, CancellationToken.None);

        _consultaBusca.Verify(
            c => c.BuscarAsync("aliquota", null, null, 1, 20, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuscarAsync_TiposVazio_RepassaNuloEmVezDeColecaoVazia()
    {
        _consultaBusca
            .Setup(c => c.BuscarAsync(It.IsAny<string>(), null, null, 1, 20, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoVazio());

        var entrada = new BuscaConsultaEntrada { Termo = "termo", Tipos = [] };

        await _sut.BuscarAsync(entrada, incluirRascunho: false, CancellationToken.None);

        _consultaBusca.Verify(
            c => c.BuscarAsync(It.IsAny<string>(), null, null, 1, 20, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
