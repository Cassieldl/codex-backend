using Codex.Domain.Enums;

namespace Codex.Application.DTOs.Interno;

/// <summary>
/// Aviso na visão interna (U8) — <see cref="Escopo"/> é <c>"Provedor"</c> ou <c>"Versao"</c>,
/// nunca os dois nem nenhum (espelha o XOR do domínio). Enxerga rascunho e publicado.
/// </summary>
public sealed record AvisoAdminDto
{
    public required int Id { get; init; }

    public required string Escopo { get; init; }

    public int? ProvedorId { get; init; }

    public int? VersaoProvedorId { get; init; }

    public required string Severidade { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public required DateOnly VigenciaInicio { get; init; }

    public required bool Publicado { get; init; }

    public required IReadOnlyCollection<int> CamposAfetadosIds { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Criação de aviso de VERSÃO (I14a) — único ponto de entrada que aceita <see cref="CampoVersaoIds"/>.</summary>
public sealed record CriarAvisoDeVersaoDto
{
    public required Severidade Severidade { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public DateOnly? VigenciaInicio { get; init; }

    public IReadOnlyCollection<int> CampoVersaoIds { get; init; } = [];
}

/// <summary>
/// Criação de aviso de PROVEDOR (I14a2, U8) — a propriedade <c>CampoVersaoIds</c> NÃO EXISTE
/// neste tipo (impede a violação do risco #3 do DBA por construção do DTO, não por validação).
/// </summary>
public sealed record CriarAvisoDeProvedorDto
{
    public required Severidade Severidade { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public DateOnly? VigenciaInicio { get; init; }
}

/// <summary>
/// Atualização (I14b) — única rota para os dois escopos, porque o escopo nunca muda depois de
/// criado (o <c>id</c> da rota já identifica um aviso com escopo fixo). <see cref="CampoVersaoIds"/>
/// só é aplicável quando o aviso carregado é de escopo VERSÃO — violação vira
/// <c>RegraDeNegocioException</c> (422) no domínio (<see cref="Codex.Domain.Entities.Aviso.SubstituirCamposAfetados"/>),
/// linha de defesa que este DTO por si só não pode garantir (ver nota de arquitetura no handoff:
/// a checagem de escopo precisaria do <c>id</c> da rota, que não é campo deste corpo — carregá-la
/// aqui exigiria injetar acesso a rota HTTP na camada de Application).
/// </summary>
public sealed record AtualizarAvisoDto
{
    public required Severidade Severidade { get; init; }

    public required string Titulo { get; init; }

    public required string Corpo { get; init; }

    public DateOnly? VigenciaInicio { get; init; }

    public IReadOnlyCollection<int>? CampoVersaoIds { get; init; }
}
