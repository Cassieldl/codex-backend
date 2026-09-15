using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Interno;

/// <summary>
/// Filtro de listagem interna de comunicados (I30). <see cref="IncluirEncerrados"/> é o que
/// separa "o que está no ar" (predicado de vigência, modelo-dados.md §7.1) do acervo completo que
/// a tela 12 precisa mostrar — default <c>false</c>, como manda arquitetura.md §4.3.
/// </summary>
public sealed record FiltroComunicadoInterno(
    Severidade? Severidade,
    AbrangenciaComunicado? Abrangencia,
    int? ProvedorId,
    bool IncluirEncerrados,
    int Pagina,
    int TamanhoPagina);

/// <summary>
/// Item de listagem interna de comunicado (I30) — compacto, SEM <c>corpo</c> nem os ids brutos de
/// provedor/versão (§0.2/§0.3: listagem nunca carrega texto livre; o modal de edição usa I31).
/// </summary>
public sealed record ComunicadoAdminListaItemDto
{
    public required int Id { get; init; }

    public required string Titulo { get; init; }

    /// <summary>Enum mapeado para <c>string</c> explicitamente — nunca enum cru na resposta.</summary>
    public required string Severidade { get; init; }

    public required string Abrangencia { get; init; }

    public string? ProvedorNome { get; init; }

    public string? VersaoRotulo { get; init; }

    public required DateTimeOffset ExpiraEm { get; init; }

    public required bool Publicado { get; init; }

    /// <summary>Derivado na leitura (<c>ExpiraEm &lt;= agora</c>) — nunca persistido.</summary>
    public required bool Encerrado { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>
/// Detalhe interno de comunicado (I31 e resposta de I32/I33/I35) — COM <c>corpo</c> e os ids
/// brutos de provedor/versão (§0.3): sem eles o modal de edição abre com o campo de corpo vazio e
/// os selects sem opção pré-selecionada, bug de UX que nenhum teste de listagem pega.
/// </summary>
public sealed record ComunicadoAdminDto
{
    public required int Id { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public required string Severidade { get; init; }

    public required string Abrangencia { get; init; }

    public int? ProvedorId { get; init; }

    public string? ProvedorNome { get; init; }

    public int? VersaoProvedorId { get; init; }

    public string? VersaoRotulo { get; init; }

    public required DateTimeOffset ExpiraEm { get; init; }

    public required bool Publicado { get; init; }

    public required bool Encerrado { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>
/// Criação (I32) — <c>publicado</c> nunca é campo de request (publicação só muda por I35, M19).
/// <see cref="ExpiraEm"/> é obrigatório (decisoes.md M-3) e tem de estar no futuro.
/// </summary>
public sealed record CriarComunicadoDto
{
    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public required Severidade Severidade { get; init; }

    public required AbrangenciaComunicado Abrangencia { get; init; }

    public int? ProvedorId { get; init; }

    public int? VersaoProvedorId { get; init; }

    public required DateTimeOffset ExpiraEm { get; init; }
}

/// <summary>
/// Atualização (I33). <see cref="ExpiraEm"/> continua tendo de estar no futuro, inclusive quando o
/// comunicado já está encerrado — é exatamente o gesto de "estender a expiração para republicar"
/// que decisoes.md B-4 aponta como próximo passo do 422 de I35.
/// </summary>
public sealed record AtualizarComunicadoDto
{
    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public required Severidade Severidade { get; init; }

    public required AbrangenciaComunicado Abrangencia { get; init; }

    public int? ProvedorId { get; init; }

    public int? VersaoProvedorId { get; init; }

    public required DateTimeOffset ExpiraEm { get; init; }
}
