using Codex.Application.Common;
using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Exceptions;

namespace Codex.Application.Services.Interno;

/// <summary>
/// Versões cuja verificação venceu (escopo funcional v3.1 §9.1). Uma versão está vencida quando
/// nunca foi verificada ou quando já se passaram <b>N dias ou mais</b> desde a verificação: com
/// N = 90, a verificação de 90 dias atrás já está vencida, a de 89 não. "Hoje" vem do
/// <see cref="TimeProvider"/> injetado, no calendário do Brasil (<see cref="FusoBrasil"/>).
/// </summary>
public sealed class VerificacaoVencidaServicoInterno : IVerificacaoVencidaServicoInterno
{
    public const int DiasMinimo = 1;
    public const int DiasMaximo = 3650;
    public const int DiasPadrao = 90;

    private const int TamanhoPaginaPadrao = 25;
    private const int TamanhoPaginaMaximo = 200;

    private readonly IVerificacaoVencidaRepositorioInterno _repositorio;
    private readonly TimeProvider _relogio;

    public VerificacaoVencidaServicoInterno(IVerificacaoVencidaRepositorioInterno repositorio, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _relogio = relogio;
    }

    public async Task<PaginaResultado<VersaoVerificacaoVencidaDto>> ListarAsync(FiltroVerificacaoVencida filtro, CancellationToken ct)
    {
        if (filtro.Dias is < DiasMinimo or > DiasMaximo)
            throw new RegraDeNegocioException($"O parâmetro 'dias' deve estar entre {DiasMinimo} e {DiasMaximo}.");

        var (pagina, tamanhoPagina) = Paginacao.Normalizar(
            filtro.Pagina, filtro.TamanhoPagina, TamanhoPaginaPadrao, TamanhoPaginaMaximo);

        var hoje = FusoBrasil.Hoje(_relogio);
        var resultado = await _repositorio.ListarAsync(hoje.AddDays(-filtro.Dias), pagina, tamanhoPagina, ct);

        var itens = resultado.Itens.Select(linha => Mapear(linha, hoje)).ToArray();

        return new PaginaResultado<VersaoVerificacaoVencidaDto>(itens, resultado.TotalCount, resultado.Pagina, resultado.TamanhoPagina);
    }

    private static VersaoVerificacaoVencidaDto Mapear(VersaoVerificacaoVencidaBrutaDto linha, DateOnly hoje) => new()
    {
        VersaoId = linha.VersaoId,
        Rotulo = linha.Rotulo,
        Slug = linha.Slug,
        Publicado = linha.Publicado,
        VerificadoEm = linha.VerificadoEm,
        DiasDesdeVerificacao = linha.VerificadoEm is { } verificadoEm ? hoje.DayNumber - verificadoEm.DayNumber : null,
        UrlInterna = $"/interno/provedores/{linha.ProvedorId}/versoes/{linha.VersaoId}",
        Provedor = new ProvedorDaVerificacaoVencidaDto
        {
            Id = linha.ProvedorId,
            Nome = linha.ProvedorNome,
            Slug = linha.ProvedorSlug,
        },
    };
}
