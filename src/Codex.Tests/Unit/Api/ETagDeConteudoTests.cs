using Codex.API.Common;
using FluentAssertions;

namespace Codex.Tests.Unit.Api;

/// <summary>
/// O ETag de leitura pública precisa mudar quando muda o FORMATO da resposta, não só o dado.
///
/// <para>
/// O defeito que motivou isto não se reproduz numa execução só: ele vive entre duas builds. O
/// servidor ganhou um campo no DTO, nenhuma linha do banco mudou, o <c>xmin</c> continuou igual —
/// e todo cliente com a resposta anterior em cache revalidou, levou <c>304</c> e seguiu servindo o
/// corpo velho. A página de versão quebrou inteira lendo o campo que o corpo em cache não tinha.
/// Por isso o caso compara duas versões de binário explicitamente, em vez de conferir o valor de
/// uma só.
/// </para>
/// </summary>
public sealed class ETagDeConteudoTests
{
    [Fact]
    public void MesmoDado_EmBuildsDiferentes_GeraETagsDiferentes()
    {
        const uint mesmoDado = 1066;

        var antes = ETagDeConteudo.Montar("aaaaaaaa", mesmoDado);
        var depois = ETagDeConteudo.Montar("bbbbbbbb", mesmoDado);

        antes.Should().NotBe(depois,
            because: "o formato da resposta pode ter mudado no deploy, e o dado não denuncia isso");
    }

    [Fact]
    public void DadosDiferentes_NaMesmaBuild_GeramETagsDiferentes()
    {
        var antes = ETagDeConteudo.Montar("aaaaaaaa", 1066);
        var depois = ETagDeConteudo.Montar("aaaaaaaa", 1067);

        antes.Should().NotBe(depois, because: "é o que o ETag já fazia, e não pode ter se perdido");
    }

    [Fact]
    public void MesmoDado_NaMesmaBuild_GeraOMesmoETag()
    {
        // O controle positivo: sem ele, um ETag aleatório passaria nos dois casos acima e
        // destruiria o cache por completo.
        ETagDeConteudo.Montar("aaaaaaaa", 1066).Should().Be(ETagDeConteudo.Montar("aaaaaaaa", 1066));
    }

    [Fact]
    public void ETag_EhFraca_NoFormatoQueOClienteDevolveEmIfNoneMatch()
    {
        ETagDeConteudo.Montar("aaaaaaaa", 1066).Should().Be("W/\"aaaaaaaa-1066\"");
    }

    [Fact]
    public void VersaoDoBinario_NaoEhVazia()
    {
        // Vazia, a combinação degeneraria para o comportamento antigo sem nada acusar.
        ETagDeConteudo.VersaoDoBinario.Should().NotBeNullOrWhiteSpace();
    }
}
