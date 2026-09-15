using Codex.Application.Common;
using Codex.Application.DTOs.Publico;
using Codex.Application.Interfaces;

namespace Codex.Application.UseCases.Busca;

/// <summary>
/// Camada fina de validação/proteção de custo antes de delegar para <see cref="IConsultaBusca"/>
/// (analise-backend.md §6.1–§6.5): cap de tamanho do termo (2–300), cap de página (1–50) e
/// normalização de página/tamanho ausentes para os defaults do contrato (P1: pagina=1,
/// tamanhoPagina=20). Nenhuma lógica de SQL/cascata mora aqui — isso é
/// <c>Codex.Infrastructure/Busca/ConsultaBuscaPostgres</c>, testado só com Postgres real (§6.7).
/// </summary>
public sealed class BuscaService : IBuscaService
{
    private const int TamanhoTermoMinimo = 2;
    private const int TamanhoTermoMaximo = 300;
    private const int TamanhoPaginaPadrao = 20;
    private const int TamanhoPaginaMaximo = 50;

    private readonly IConsultaBusca _consultaBusca;

    public BuscaService(IConsultaBusca consultaBusca)
    {
        _consultaBusca = consultaBusca;
    }

    public Task<ResultadoBuscaDto> BuscarAsync(BuscaConsultaEntrada entrada, bool incluirRascunho, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var termo = (entrada.Termo ?? string.Empty).Trim();
        if (termo.Length < TamanhoTermoMinimo || termo.Length > TamanhoTermoMaximo)
        {
            throw new TermoBuscaInvalidoException(
                $"O termo de busca precisa ter entre {TamanhoTermoMinimo} e {TamanhoTermoMaximo} caracteres.");
        }

        var pagina = entrada.Pagina > 0 ? entrada.Pagina : 1;
        var tamanhoPagina = entrada.TamanhoPagina switch
        {
            null or <= 0 => TamanhoPaginaPadrao,
            > TamanhoPaginaMaximo => TamanhoPaginaMaximo,
            _ => entrada.TamanhoPagina.Value,
        };

        var tipos = entrada.Tipos is { Count: > 0 } ? entrada.Tipos : null;

        return _consultaBusca.BuscarAsync(termo, entrada.ProvedorSlug, tipos, pagina, tamanhoPagina, incluirRascunho, ct);
    }
}
