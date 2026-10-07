namespace Codex.Application.DTOs.Interno;

/// <summary>Provedor da versão listada na fila de verificação.</summary>
public sealed record ProvedorDaVerificacaoVencidaDto
{
    public required int Id { get; init; }

    public required string Nome { get; init; }

    public required string Slug { get; init; }
}

/// <summary>
/// Versão de provedor cuja verificação venceu (escopo funcional v3.1 §9.1). Rascunho entra — fica
/// sinalizado por <see cref="Publicado"/>.
/// </summary>
public sealed record VersaoVerificacaoVencidaDto
{
    public required int VersaoId { get; init; }

    public required string Rotulo { get; init; }

    public required string Slug { get; init; }

    public required ProvedorDaVerificacaoVencidaDto Provedor { get; init; }

    /// <summary>Data da última verificação; <c>null</c> quando a versão nunca foi verificada.</summary>
    public DateOnly? VerificadoEm { get; init; }

    /// <summary>Dias corridos entre <see cref="VerificadoEm"/> e hoje (calendário do Brasil); <c>null</c> quando nunca verificada.</summary>
    public int? DiasDesdeVerificacao { get; init; }

    public required bool Publicado { get; init; }

    /// <summary>Rota da tela interna da versão (<c>/interno/provedores/{provedorId}/versoes/{versaoId}</c>).</summary>
    public required string UrlInterna { get; init; }
}

/// <summary>Filtro da fila: <see cref="Dias"/> é a idade mínima da verificação, em dias.</summary>
public sealed record FiltroVerificacaoVencida(int Dias, int Pagina, int TamanhoPagina);

/// <summary>Linha crua do repositório — o serviço acrescenta a idade, que depende de "hoje".</summary>
public sealed record VersaoVerificacaoVencidaBrutaDto(
    int VersaoId,
    string Rotulo,
    string Slug,
    bool Publicado,
    DateOnly? VerificadoEm,
    int ProvedorId,
    string ProvedorNome,
    string ProvedorSlug);
