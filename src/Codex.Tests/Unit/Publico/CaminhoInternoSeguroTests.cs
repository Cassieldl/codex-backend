using Codex.Domain.ValueObjects;
using FluentAssertions;

namespace Codex.Tests.Unit.Publico;

/// <summary>
/// Restrição de segurança (achado BAIXO-02 da auditoria de frontend, comunicada pelo Manager):
/// todo <c>url</c>/redirect emitido por esta unidade precisa ser um path interno relativo — o
/// frontend injeta o valor direto em <c>&lt;Link to&gt;</c>/<c>&lt;a href&gt;</c> sem validação
/// própria; URL absoluta atravessa verbatim. Este teste prova (não só declara) que o guard usado
/// antes de qualquer <c>RedirectPermanentPreserveMethod</c> rejeita host/esquema absoluto.
///
/// <para>
/// Estendido (achado da auditoria, 2026-09-02): bypass via caractere de controle C0 (TAB/LF/CR)
/// que o parser WHATWG do navegador remove de qualquer posição ANTES de resolver a URL — uma
/// string como <c>"/\t/evil.com"</c> passava pelas checagens de prefixo aqui (o TAB ainda está no
/// meio) e só colapsava para <c>"//evil.com"</c> depois, no navegador. Casos novos provam que a
/// checagem de controle roda antes de qualquer <c>StartsWith</c>/<c>Contains</c>.
/// </para>
/// </summary>
public sealed class CaminhoInternoSeguroTests
{
    [Theory]
    [InlineData("/provedores/acme")]
    [InlineData("/provedores/acme/v1")]
    [InlineData("/erros/nfse-123")]
    public void EhValido_ComPathInternoRelativo_RetornaTrue(string caminho)
    {
        CaminhoInternoSeguro.EhValido(caminho).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.com/x")]
    [InlineData("http://evil.com")]
    [InlineData("//evil.com/x")]
    [InlineData("/\\evil.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("provedores/acme")]
    [InlineData("/\t/evil.com")]
    [InlineData("/\n/evil.com")]
    [InlineData("/\r/evil.com")]
    [InlineData("/\t\\evil.com")]
    [InlineData("/\t\t/evil.com")]
    [InlineData("//evil.com")]
    public void EhValido_ComUrlAbsolutaOuEsquemaPerigoso_RetornaFalse(string? caminho)
    {
        CaminhoInternoSeguro.EhValido(caminho).Should().BeFalse();
    }
}
