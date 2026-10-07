using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;
using Codex.Application.Services;
using Codex.Domain.Entities;
using Codex.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Codex.Tests.Unit.Publico;

/// <summary>
/// Regras de P11 que não dependem de banco: o cap M22 (reusado de P4, sem nome próprio) e os dois
/// desfechos de resolução por slug. O predicado A1 e a fórmula M12 são provados ponta a ponta em
/// <c>LayoutPublicoHttpTests</c>, porque metade deles vive no SQL.
/// </summary>
public sealed class LayoutServicoPublicoTests
{
    private const int CapCamposPorVersao = 2000;

    private readonly Mock<ILayoutRepositorioPublico> _repositorio = new(MockBehavior.Strict);
    private readonly LayoutServicoPublico _servico;

    public LayoutServicoPublicoTests()
    {
        _servico = new LayoutServicoPublico(_repositorio.Object);
    }

    [Fact]
    public async Task ObterVersao_ComUmCampoAcimaDoCap_Lanca422()
    {
        var versao = VersaoPublicada();
        PrepararRepositorio(versao, CapCamposPorVersao + 1);

        var acao = async () => await _servico.ObterVersaoAsync(versao.Slug, CancellationToken.None);

        (await acao.Should().ThrowAsync<LimiteExcedidoException>())
            .Which.Message.Should().Contain(CapCamposPorVersao.ToString());
    }

    [Fact]
    public async Task ObterVersao_ExatamenteNoCap_Responde()
    {
        var versao = VersaoPublicada();
        PrepararRepositorio(versao, CapCamposPorVersao);

        var resultado = await _servico.ObterVersaoAsync(versao.Slug, CancellationToken.None);

        resultado.TotalCampos.Should().Be(CapCamposPorVersao);
    }

    [Fact]
    public async Task ObterVersao_SlugInexistente_LancaNaoEncontrada()
    {
        _repositorio.Setup(r => r.ObterVersaoPorSlugAsync("nao-existe", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VersaoLayout?)null);

        var acao = async () => await _servico.ObterVersaoAsync("nao-existe", CancellationToken.None);

        await acao.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterVersao_ExistenteMasNaoPublicada_LancaDespublicadoComTipoDeLayout()
    {
        var rascunho = VersaoLayout.Criar("Layout Rascunho", ordem: 7);
        _repositorio.Setup(r => r.ObterVersaoPorSlugAsync(rascunho.Slug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rascunho);

        var acao = async () => await _servico.ObterVersaoAsync(rascunho.Slug, CancellationToken.None);

        // O TipoItem é o que o middleware usa para escolher VERSAO_LAYOUT_DESPUBLICADA em vez do
        // genérico CONTEUDO_DESPUBLICADO.
        (await acao.Should().ThrowAsync<ConteudoDespublicadoException>())
            .Which.TipoItem.Should().Be("versaolayout");
    }

    /// <summary>
    /// "Novo nesta versão" compara com a versão publicada anterior. Na primeira versão do acervo não
    /// há com o que comparar: marcar todos os campos como novos — 545 selos no Layout InvoiCy 8 — não
    /// informa nada e tira a força do selo quando ele importar. Testado aqui, e não por HTTP, porque a
    /// ordem das versões é global no banco compartilhado da suíte: lá a "primeira" deixa de ser.
    /// </summary>
    [Fact]
    public async Task ObterVersao_PrimeiraVersaoPublicada_NaoMarcaNenhumCampoComoNovo()
    {
        var versao = VersaoPublicada();
        PrepararRepositorio(versao, quantidadeDeCampos: 3);

        var resultado = await _servico.ObterVersaoAsync(versao.Slug, CancellationToken.None);

        resultado.Anterior.Should().BeNull();
        resultado.CamposPorGrupo.SelectMany(grupo => grupo.Campos)
            .Should().HaveCount(3)
            .And.OnlyContain(campo => !campo.NovoNestaVersao);
    }

    [Fact]
    public async Task ObterVersao_ComVersaoPublicadaAnterior_MarcaComoNovoOCampoNascidoNela()
    {
        var versao = VersaoPublicada();
        var anterior = VersaoLayout.Criar("Layout Anterior", ordem: 2);
        anterior.Publicar();
        PrepararRepositorio(versao, quantidadeDeCampos: 1);
        _repositorio.Setup(r => r.ListarVersoesPublicadasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([anterior, versao]);

        var resultado = await _servico.ObterVersaoAsync(versao.Slug, CancellationToken.None);

        resultado.Anterior.Should().NotBeNull();
        resultado.CamposPorGrupo.SelectMany(grupo => grupo.Campos).Single().NovoNestaVersao.Should().BeTrue();
    }

    private static VersaoLayout VersaoPublicada()
    {
        var versao = VersaoLayout.Criar("Layout Cheio", ordem: 3);
        versao.Publicar();

        return versao;
    }

    private void PrepararRepositorio(VersaoLayout versao, int quantidadeDeCampos)
    {
        var campos = Enumerable.Range(1, quantidadeDeCampos)
            .Select(indice => new CampoLayoutVisivelBrutoDto
            {
                Slug = $"campo-{indice}",
                Nome = $"Campo {indice}",
                CaminhoXml = $"//raiz/Campo{indice}",
                Significado = "Significado",
                Grupo = null,
                Ordem = indice,
                VersaoLayoutOrigemId = versao.Id,
                OrigemOrdem = versao.Ordem,
                VersaoLayoutDescontinuadoId = null,
                DescontinuadoOrdem = null,
            })
            .ToArray();

        _repositorio.Setup(r => r.ObterVersaoPorSlugAsync(versao.Slug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versao);
        _repositorio.Setup(r => r.ListarVersoesPublicadasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([versao]);
        _repositorio.Setup(r => r.ListarCamposVisiveisAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(campos);
    }
}
