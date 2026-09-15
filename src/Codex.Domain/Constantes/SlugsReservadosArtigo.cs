using System.Collections.Frozen;

namespace Codex.Domain.Constantes;

/// <summary>
/// A noção de "artigo reservado" (posição fixa no menu, D2/§5.C de arquitetura.md) mora em
/// código, no domínio — nunca em coluna de banco nem em <c>appsettings</c>: um contrato de URL
/// pública não pode ser desmontado por uma operação de conteúdo comum (renomear/despublicar), e
/// a rota <c>/como-funciona</c> é compilada no frontend, então rota e reserva precisam mudar no
/// mesmo commit.
/// </summary>
public static class SlugsReservadosArtigo
{
    public const string ComoFunciona = "como-funciona";

    private static readonly FrozenSet<string> Reservados = FrozenSet.ToFrozenSet([ComoFunciona]);

    public static bool EhReservado(string slug) => Reservados.Contains(slug);

    /// <summary>
    /// "/como-funciona" para reservado, "/artigos/{slug}" caso contrário — fonte ÚNICA da URL
    /// canônica, consumida pela indexação de busca (<c>IndiceBuscaWriter.CarregarMetadadosAsync</c>)
    /// e pelo DTO público (<c>ArtigoDetalheDto.UrlCanonica</c>), para nunca haver duas fórmulas de
    /// URL divergindo entre si.
    /// </summary>
    public static string RotaCanonica(string slug) =>
        EhReservado(slug) ? $"/{slug}" : $"/artigos/{slug}";
}
