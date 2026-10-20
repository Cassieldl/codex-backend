using Codex.API.Common;
using FluentAssertions;

namespace Codex.Tests.Unit.Api;

/// <summary>
/// A ETag de leitura pública precisa mudar quando muda o que o cliente recebe — o dado de qualquer
/// tabela que a página reúna, ou o formato da resposta depois de um deploy — e só então.
/// </summary>
public sealed class ETagDeConteudoTests
{
    private sealed record Pagina(string Titulo, IReadOnlyCollection<string> Erros);

    [Fact]
    public void MesmoCorpo_EmBuildsDiferentes_GeraETagsDiferentes()
    {
        var corpo = new Pagina("0.20", ["E0595"]);

        ETagDeConteudo.MontarDoCorpo("aaaaaaaa", corpo).Should().NotBe(ETagDeConteudo.MontarDoCorpo("bbbbbbbb", corpo),
            because: "o formato da resposta pode ter mudado no deploy");
    }

    [Fact]
    public void CorpoComUmErroAMais_NaMesmaBuild_GeraETagDiferente()
    {
        // O caso que motivou o hash: a linha da versão é a mesma, e o que mudou foi um filho.
        var antes = ETagDeConteudo.MontarDoCorpo("aaaaaaaa", new Pagina("0.20", []));
        var depois = ETagDeConteudo.MontarDoCorpo("aaaaaaaa", new Pagina("0.20", ["E0595"]));

        antes.Should().NotBe(depois);
    }

    [Fact]
    public void CorposIguais_EmInstanciasDiferentes_GeramAMesmaETag()
    {
        // O controle positivo: sem ele, uma ETag aleatória passaria nos casos acima e destruiria o
        // cache por completo.
        ETagDeConteudo.MontarDoCorpo("aaaaaaaa", new Pagina("0.20", ["E0595"]))
            .Should().Be(ETagDeConteudo.MontarDoCorpo("aaaaaaaa", new Pagina("0.20", ["E0595"])));
    }

    [Fact]
    public void ETag_EhFraca_NoFormatoQueOClienteDevolveEmIfNoneMatch()
    {
        ETagDeConteudo.MontarDoCorpo("aaaaaaaa", new Pagina("0.20", []))
            .Should().MatchRegex("^W/\"aaaaaaaa-[0-9a-f]{16}\"$");
    }

    [Fact]
    public void VersaoDoBinario_EhAIdentidadeDestaBuild()
    {
        var mvidDoAssembly = typeof(ETagDeConteudo).Assembly.ManifestModule.ModuleVersionId.ToString("N")[..8];

        ETagDeConteudo.VersaoDoBinario.Should().Be(mvidDoAssembly,
            because: "uma constante faria o deploy que muda o DTO devolver 304 com o corpo velho");
    }

    [Fact]
    public void SobrecargaUsadaPelosControllers_CompoeAVersaoDoBinario()
    {
        var corpo = new Pagina("0.20", ["E0595"]);

        var etag = ETagDeConteudo.MontarDoCorpo(corpo);

        etag.Should().Contain(ETagDeConteudo.VersaoDoBinario);
        etag.Should().Be(ETagDeConteudo.MontarDoCorpo(ETagDeConteudo.VersaoDoBinario, corpo));
    }
}
