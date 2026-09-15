namespace Codex.Domain.Enums;

/// <summary>
/// Identifica qual das seis entidades com <c>Publicado</c> próprio (modelo-dados.md §5.6.1,
/// analise-backend.md §6.6) mudou de estado de publicação — parâmetro de
/// <see cref="Codex.Application.Interfaces.IIndiceBuscaWriter.RecalcularVisibilidadeAsync"/>.
/// Não confundir com <see cref="TipoItemIndexado"/>, que é o valor persistido na coluna
/// <c>codex.itensbusca.tipoitem</c> (5 valores, sem <see cref="VersaoLayout"/>, porque uma
/// versão de layout nunca tem linha própria em <c>itensbusca</c> — só os camposlayout
/// descendentes têm).
/// </summary>
public enum TipoItemBusca
{
    Provedor,
    VersaoProvedor,
    VersaoLayout,
    Aviso,
    Erro,
    CampoLayout,
    Artigo,
}
