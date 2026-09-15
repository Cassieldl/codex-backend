using Codex.Domain.Entities;

namespace Codex.Application.Common;

/// <summary>
/// Monta os 4 pesos (A/B/C/D) da projeção de busca a partir da entidade de origem
/// (arquitetura.md §6.3), num único lugar reusado por criação/edição de conteúdo E pela
/// reconstrução total (I22) — nunca duas fórmulas divergentes para o mesmo tipo.
/// </summary>
public static class PesosBusca
{
    public static string? Concatenar(params string?[] partes)
    {
        var texto = string.Join(' ', partes.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    public static (string? A, string? B, string? C, string? D) DoProvedor(Provedor provedor) =>
        (null, Concatenar(provedor.Nome, string.Join(' ', provedor.Aliases.Select(a => a.Alias))), null, null);

    public static (string? A, string? B, string? C, string? D) DoVersaoProvedor(VersaoProvedor versao) =>
        (null, Concatenar(versao.Rotulo), null, Concatenar(versao.Introducao));

    public static (string? A, string? B, string? C, string? D) DoAviso(Aviso aviso) =>
        (null, Concatenar(aviso.Titulo), Concatenar(aviso.Corpo), null);

    public static (string? A, string? B, string? C, string? D) DoErro(Erro erro) => (
        Concatenar(erro.Codigo, string.Join(' ', erro.Variantes.Select(v => v.Texto))),
        Concatenar(erro.Titulo),
        Concatenar(erro.CausaProvavel, erro.SolucaoSugerida),
        null);

    public static (string? A, string? B, string? C, string? D) DoCampoLayout(CampoLayout campoLayout) =>
        (null, Concatenar(campoLayout.Nome, campoLayout.CaminhoXml), Concatenar(campoLayout.Significado), null);

    /// <summary>
    /// arquitetura.md §5.A: A = título, C = corpo, D = rótulo do tipo. B fica vazio — artigo não
    /// tem análogo de "nome próprio/alias" (provedor) ou "caminho XML" (campo), e inflar B com o
    /// título duplicaria peso e faria artigo vencer erro em consulta por código.
    /// </summary>
    public static (string? A, string? B, string? C, string? D) DoArtigo(Artigo artigo) =>
        (Concatenar(artigo.Titulo), null, Concatenar(artigo.Corpo), Concatenar(artigo.Tipo.ToString()));
}
