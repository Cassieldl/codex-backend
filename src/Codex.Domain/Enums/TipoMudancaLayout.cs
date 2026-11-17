namespace Codex.Domain.Enums;

/// <summary>O que aconteceu com um caminho do layout de uma versão para a seguinte.</summary>
public enum TipoMudancaLayout
{
    /// <summary>Grupo que não existia; só tem <c>depois</c>.</summary>
    GrupoNovo,

    /// <summary>Campo que não existia; só tem <c>depois</c>.</summary>
    CampoNovo,

    /// <summary>Mesmo conteúdo em outra posição da estrutura.</summary>
    MudouDeLugar,

    /// <summary>Mesma posição com outro nome.</summary>
    Renomeado,

    /// <summary>Caminho que deixou de existir; só tem <c>antes</c>.</summary>
    Saiu,

    /// <summary>Mesmo caminho com tipo, tamanho ou regra diferente.</summary>
    Alterado,
}
