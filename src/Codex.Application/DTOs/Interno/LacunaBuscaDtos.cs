using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Interno;

/// <summary>
/// Registro de uma busca sem resultado. Quem registra e quando NÃO são campos: vêm do token e do
/// relógio do servidor — um valor enviado aqui sobre isso não existe para o desserializador.
/// </summary>
public sealed record RegistrarLacunaBuscaDto
{
    /// <summary>Termo digitado na busca interna (2 a 300 caracteres, como a própria busca).</summary>
    public required string Termo { get; init; }

    /// <summary>Parceiro que originou o atendimento (até 160 caracteres).</summary>
    public string? Parceiro { get; init; }

    /// <summary>Número do chamado (até 60 caracteres).</summary>
    public string? NumeroChamado { get; init; }
}

/// <summary>
/// Mudança de estado da lacuna: resolver, descartar ou reabrir. <see cref="IncluirMesmoTermo"/>
/// estende a mudança às demais ocorrências ABERTAS do mesmo termo normalizado — é o gesto de
/// "documentei isto" sobre uma recorrência de dez linhas.
/// </summary>
public sealed record AlterarEstadoLacunaBuscaDto
{
    public required EstadoLacunaBusca Estado { get; init; }

    /// <summary>O que foi feito, ou por que foi descartada (até 500 caracteres). Ignorada ao reabrir.</summary>
    public string? Nota { get; init; }

    /// <summary>Só vale ao resolver ou descartar; combinado com <c>Aberta</c> é <c>422</c>.</summary>
    public bool IncluirMesmoTermo { get; init; }
}

/// <summary>Lacuna da fila editorial — o mesmo formato na listagem, no detalhe e nas respostas de escrita.</summary>
public sealed record LacunaBuscaDto
{
    public required int Id { get; init; }

    public required string Termo { get; init; }

    public string? Parceiro { get; init; }

    public string? NumeroChamado { get; init; }

    /// <summary>Enum como nome (<c>Aberta</c>, <c>Resolvida</c>, <c>Descartada</c>), nunca ordinal.</summary>
    public required string Estado { get; init; }

    public string? Nota { get; init; }

    /// <summary>Nome do token de quem registrou; <c>null</c> quando o token não trazia nome.</summary>
    public string? RegistradoPorNome { get; init; }

    public required DateTimeOffset RegistradoEm { get; init; }

    public DateTimeOffset? ResolvidaEm { get; init; }

    public string? ResolvidoPorNome { get; init; }

    /// <summary>
    /// Quantas ocorrências ABERTAS existem hoje com o mesmo termo normalizado (sem acento, sem
    /// diferença de caixa, espaços colapsados), contando esta quando ela própria está aberta. É a
    /// medida de prioridade da fila.
    /// </summary>
    public required int OcorrenciasAbertas { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Resposta de <c>PUT /{id}/estado</c>.</summary>
public sealed record AlteracaoEstadoLacunaBuscaDto
{
    public required LacunaBuscaDto Lacuna { get; init; }

    /// <summary>Outras ocorrências do mesmo termo alteradas junto (<c>0</c> sem <c>incluirMesmoTermo</c>).</summary>
    public required int MesmoTermoAlteradas { get; init; }
}

/// <summary>Filtro da listagem. <see cref="SortBy"/> aceita <c>criadoEm</c> (padrão), <c>ocorrencias</c> e <c>termo</c>.</summary>
public sealed record FiltroLacunaBusca(
    EstadoLacunaBusca? Estado,
    string? SortBy,
    string? SortDirection,
    int Pagina,
    int TamanhoPagina);

/// <summary>
/// Termo da fila editorial: todas as ocorrências do mesmo termo normalizado, no estado filtrado,
/// numa linha só. É a unidade de priorização — "o que documentar a seguir".
/// </summary>
public sealed record LacunaBuscaTermoDto
{
    /// <summary>Chave do grupo: minúsculas, sem acento, espaços colapsados — a mesma que <c>incluirMesmoTermo</c> usa.</summary>
    public required string TermoNormalizado { get; init; }

    /// <summary>Forma exibível: como a ocorrência MAIS RECENTE do grupo foi digitada.</summary>
    public required string Termo { get; init; }

    /// <summary>Ocorrências do termo no estado filtrado.</summary>
    public required int Ocorrencias { get; init; }

    public required DateTimeOffset PrimeiraEm { get; init; }

    public required DateTimeOffset UltimaEm { get; init; }

    /// <summary>Parceiros distintos (sem diferença de caixa), do mais recente ao mais antigo, até o teto.</summary>
    public required IReadOnlyList<string> Parceiros { get; init; }

    /// <summary>Total de parceiros distintos, inclusive os que o teto deixou fora de <see cref="Parceiros"/>.</summary>
    public required int TotalParceiros { get; init; }

    /// <summary>Números de chamado distintos (sem diferença de caixa), do mais recente ao mais antigo, até o teto.</summary>
    public required IReadOnlyList<string> Chamados { get; init; }

    /// <summary>Total de chamados distintos, inclusive os que o teto deixou fora de <see cref="Chamados"/>.</summary>
    public required int TotalChamados { get; init; }

    /// <summary>Nome de quem registrou a ocorrência mais recente; <c>null</c> quando o token não trazia nome.</summary>
    public string? UltimoRegistradoPorNome { get; init; }

    /// <summary>
    /// Ocorrência de referência do grupo (a mais recente no estado filtrado). É ela que a tela usa em
    /// <c>PUT /{id}/estado</c> com <c>incluirMesmoTermo</c> para tratar o grupo inteiro.
    /// </summary>
    public required int LacunaReferenciaId { get; init; }

    /// <summary>Token de concorrência da <see cref="LacunaReferenciaId"/>, para o <c>If-Match</c>.</summary>
    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>
/// Filtro da fila agrupada. <see cref="Estado"/> é sempre um estado (a fila é "o que está em
/// aberto" por padrão). <see cref="SortBy"/> aceita <c>ocorrencias</c> (padrão), <c>recente</c> e <c>termo</c>.
/// </summary>
public sealed record FiltroLacunaBuscaTermo(
    EstadoLacunaBusca Estado,
    string? SortBy,
    string? SortDirection,
    int Pagina,
    int TamanhoPagina);
