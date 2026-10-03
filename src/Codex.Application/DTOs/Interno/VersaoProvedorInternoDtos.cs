namespace Codex.Application.DTOs.Interno;

/// <summary>Item de listagem interna de versões de um provedor (I6).</summary>
public sealed record VersaoAdminListaItemDto
{
    public required int Id { get; init; }

    public required int ProvedorId { get; init; }

    public required string Slug { get; init; }

    public required string Rotulo { get; init; }

    public required int VersaoLayoutId { get; init; }

    public required bool Publicado { get; init; }

    public DateOnly? VerificadoEm { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Detalhe interno de versão de provedor (I8a).</summary>
public sealed record VersaoAdminDetalheDto
{
    public required int Id { get; init; }

    public required int ProvedorId { get; init; }

    public required string Slug { get; init; }

    public required string Rotulo { get; init; }

    public string? Introducao { get; init; }

    public required int VersaoLayoutId { get; init; }

    public required bool Publicado { get; init; }

    public DateOnly? VerificadoEm { get; init; }

    /// <summary>
    /// Avisos DESTA versão, como <c>ProvedorAdminDetalheDto.Avisos</c> faz para o provedor. A tela
    /// de versão edita os avisos no lugar e invalida o detalhe depois de cada mutação, então eles
    /// são parte do agregado — o cliente já os lia, e sem eles a tela inteira deixava de abrir.
    /// </summary>
    public required IReadOnlyCollection<AvisoAdminDto> Avisos { get; init; }

    public required uint VersaoConcorrencia { get; init; }
}

/// <summary>Criação (I7) — sem <c>publicado</c> (M19).</summary>
public sealed record CriarVersaoDto
{
    public required int VersaoLayoutId { get; init; }

    public required string Rotulo { get; init; }

    public string? Introducao { get; init; }
}

/// <summary>
/// Atualização (I8b). <see cref="VersaoLayoutId"/> é opcional — <c>null</c> mantém o layout de
/// origem atual, e é o que o frontend hoje envia (campo não existe no formulário). Trocar o
/// layout exige a versão de provedor sem nenhum <c>CampoVersao</c>: campo da versão só existe
/// porque o campo canônico tem origem no layout ATUAL — ver <c>VersaoProvedorServicoInterno.AtualizarAsync</c>.
/// </summary>
public sealed record AtualizarVersaoDto
{
    public required string Rotulo { get; init; }

    public string? Introducao { get; init; }

    public int? VersaoLayoutId { get; init; }
}

/// <summary>Registro de verificação (I10) — sem <c>ambiente</c> (U3).</summary>
public sealed record AtualizarVerificacaoDto
{
    public required DateOnly VerificadoEm { get; init; }
}
