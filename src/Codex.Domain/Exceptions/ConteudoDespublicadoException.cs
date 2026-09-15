namespace Codex.Domain.Exceptions;

/// <summary>
/// Mapeada para 410 pelo middleware de erro (analise-backend.md §4.4/§8). Carrega o tipo do
/// item e o slug para o middleware distinguir o código de erro
/// (VERSAO_DESPUBLICADA | PROVEDOR_DESPUBLICADO | ERRO_DESPUBLICADO).
/// </summary>
public sealed class ConteudoDespublicadoException : Exception
{
    public string TipoItem { get; }
    public string Slug { get; }

    public ConteudoDespublicadoException(string tipoItem, string slug)
        : base($"Conteúdo '{tipoItem}' ({slug}) não está publicado.")
    {
        TipoItem = tipoItem;
        Slug = slug;
    }
}
