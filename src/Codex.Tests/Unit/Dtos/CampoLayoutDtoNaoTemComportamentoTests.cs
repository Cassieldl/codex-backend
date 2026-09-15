using Codex.Application.DTOs.Interno;
using FluentAssertions;

namespace Codex.Tests.Unit.Dtos;

/// <summary>
/// R3 (analise-backend.md §5.1a): a família <c>CampoLayout*Dto</c> é vocabulário canônico — nunca
/// pode carregar propriedade de comportamento (isso vive em <see cref="CampoVersaoAdminDto"/>).
/// Prova por reflexão, não por revisão manual de cada DTO novo.
/// </summary>
public sealed class CampoLayoutDtoNaoTemComportamentoTests
{
    private static readonly string[] PropriedadesProibidas =
    [
        "Tipo", "Tamanho", "Mascara", "Dominio", "EstadoPreenchimento",
        "Condicao", "Exemplo", "NotaPreenchimento", "DestinoXml", "NotaTransformacao",
    ];

    [Fact]
    public void Familia_CampoLayout_NaoContemPropriedadeDeComportamento()
    {
        var tiposCampoLayout = typeof(CampoLayoutResponseDto).Assembly.GetTypes()
            .Where(t => t.Namespace is not null
                     && t.Namespace.StartsWith("Codex.Application.DTOs", StringComparison.Ordinal)
                     && t.Name.StartsWith("CampoLayout", StringComparison.Ordinal))
            .ToList();

        tiposCampoLayout.Should().NotBeEmpty();

        foreach (var tipo in tiposCampoLayout)
        {
            tipo.GetProperties().Select(p => p.Name).Should().NotContain(PropriedadesProibidas,
                because: $"{tipo.Name} é vocabulário canônico — comportamento vive em CampoVersao*");
        }
    }
}
