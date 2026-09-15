using Codex.Application.DTOs.Interno;
using FluentAssertions;

namespace Codex.Tests.Unit.Dtos;

/// <summary>
/// Risco #3 do DBA (U8, §4.2 I14a2, §9 <c>AvisoEscopoEndpointTests</c>): impossível submeter
/// <c>campoVersaoIds</c> num aviso de PROVEDOR porque a propriedade não existe neste tipo — não é
/// erro de validação em runtime, é ausência estrutural provada por reflexão (mesma técnica de
/// <c>CampoLayoutDtoNaoTemComportamentoTests</c>).
/// </summary>
public sealed class CriarAvisoDeProvedorDtoEstruturaTests
{
    [Fact]
    public void CriarAvisoDeProvedorDto_NuncaTemPropriedadeCampoVersaoIds()
    {
        var propriedades = typeof(CriarAvisoDeProvedorDto).GetProperties().Select(p => p.Name);

        propriedades.Should().NotContain("CampoVersaoIds",
            because: "aviso de provedor nunca pode ter campos afetados (U8/risco #3) — a garantia é o tipo não declarar a propriedade");
    }

    [Fact]
    public void CriarAvisoDeVersaoDto_TemPropriedadeCampoVersaoIds()
    {
        var propriedades = typeof(CriarAvisoDeVersaoDto).GetProperties().Select(p => p.Name);

        propriedades.Should().Contain("CampoVersaoIds",
            because: "só aviso de VERSÃO pode apontar campos afetados");
    }
}
