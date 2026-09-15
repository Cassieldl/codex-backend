using System.Text.Json;
using Codex.API.Common;
using Codex.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Codex.API.Middleware;

/// <summary>
/// Middleware global de erros (analise-backend.md §8, completo — BACK-4): converte cada exceção
/// de domínio (e <see cref="DbUpdateConcurrencyException"/> do EF) no status/código HTTP da
/// tabela §8, nunca vaza stack trace em produção, loga <c>Warning</c> para 4xx e <c>Error</c>
/// (com <c>traceId</c>) para 5xx — captura tudo abaixo dele.
///
/// <para>
/// Só duas coisas ficam ACIMA dele em <c>Program.cs</c>, e por motivo: <c>UseForwardedHeaders</c>
/// (precisa traduzir <c>X-Forwarded-*</c> antes de qualquer leitura de esquema ou IP) e
/// <see cref="RegistroAcessoMiddleware"/> (precisa empurrar o <c>TraceId</c> ao contexto de log
/// antes, senão o <c>traceId</c> devolvido no corpo do 5xx não aparece no evento registrado).
/// </para>
///
/// <para>
/// Nota de coexistência com <see cref="MapeadorErroPublico"/> (BACK-3): os controllers públicos
/// (P3–P6) interceptam <see cref="EntidadeNaoEncontradaException"/>/<see cref="ConteudoDespublicadoException"/>/
/// <see cref="LimiteExcedidoException"/> localmente, ANTES de qualquer exceção alcançar este
/// middleware — então este middleware nunca chega a vê-las nessas rotas, e o comportamento de
/// P3–P6 não muda com esta implementação. A tabela de mapeamento abaixo é intencionalmente um
/// SUPERCONJUNTO da de <see cref="MapeadorErroPublico"/> (mesmos códigos/status para os tipos em
/// comum) — se algum dia o mapeador local for removido, o comportamento público continua idêntico
/// porque as duas tabelas concordam.
/// </para>
/// </summary>
public sealed class TratamentoErrosMiddleware
{
    private readonly RequestDelegate _proximo;
    private readonly ILogger<TratamentoErrosMiddleware> _logger;

    public TratamentoErrosMiddleware(RequestDelegate proximo, ILogger<TratamentoErrosMiddleware> logger)
    {
        _proximo = proximo;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _proximo(contexto);
        }
        catch (Exception excecao)
        {
            // Resposta já começou a ser escrita (ex.: streaming) — não dá para reescrever status
            // nem corpo; propagar é mais seguro do que produzir um corpo HTTP inconsistente.
            if (contexto.Response.HasStarted)
            {
                _logger.LogError(excecao, "Erro não tratado após a resposta já ter iniciado em {Path}", contexto.Request.Path);
                throw;
            }

            var (status, codigo, mensagemPublica) = Mapear(excecao);

            if (status >= 500)
                _logger.LogError(excecao, "Erro não tratado processando {Path}", contexto.Request.Path);
            else
                _logger.LogWarning(excecao, "Erro de negócio ({Codigo}) processando {Path}", codigo, contexto.Request.Path);

            contexto.Response.ContentType = "application/json";
            contexto.Response.StatusCode = status;

            var corpo = status >= 500
                ? new { error = new { code = codigo, message = mensagemPublica, traceId = contexto.TraceIdentifier } }
                : (object)new { error = new { code = codigo, message = mensagemPublica } };

            await contexto.Response.WriteAsync(JsonSerializer.Serialize(corpo));
        }
    }

    /// <summary>Tabela de mapeamento exceção → (status, código, mensagem pública) de §8.</summary>
    private static (int Status, string Codigo, string Mensagem) Mapear(Exception excecao) => excecao switch
    {
        EntidadeNaoEncontradaException e =>
            (StatusCodes.Status404NotFound, "NAO_ENCONTRADO", e.Message),

        ConteudoDespublicadoException e =>
            (StatusCodes.Status410Gone, CodigoDespublicado(e.TipoItem), e.Message),

        RegistroDuplicadoException e =>
            (StatusCodes.Status409Conflict, "REGISTRO_DUPLICADO", e.Message),

        ConflitoConcorrenciaException e =>
            (StatusCodes.Status409Conflict, "CONFLITO_CONCORRENCIA", e.Message),

        // EF só lança isto se a checagem prévia via If-Match (ConflitoConcorrenciaException)
        // não pegou — janela estreita entre leitura e escrita. Nunca vaza qual coluna divergiu.
        DbUpdateConcurrencyException =>
            (StatusCodes.Status409Conflict, "CONFLITO_CONCORRENCIA",
                "O recurso foi alterado por outra requisição enquanto você editava. Recarregue e tente novamente."),

        LimiteExcedidoException e =>
            (StatusCodes.Status422UnprocessableEntity, "LIMITE_EXCEDIDO", e.Message),

        TransicaoDeEstadoInvalidaException e =>
            (StatusCodes.Status422UnprocessableEntity, "VALIDACAO_NEGOCIO", e.Message),

        RegraDeNegocioException e =>
            (StatusCodes.Status422UnprocessableEntity, e.Codigo, e.Message),

        IfMatchObrigatorioException e =>
            (StatusCodes.Status400BadRequest, "IF_MATCH_OBRIGATORIO", e.Message),

        _ => (StatusCodes.Status500InternalServerError, "ERRO_INTERNO", "Ocorreu um erro inesperado."),
    };

    private static string CodigoDespublicado(string tipoItem) => tipoItem switch
    {
        "provedor" => "PROVEDOR_DESPUBLICADO",
        "versaoprovedor" => "VERSAO_DESPUBLICADA",
        "erro" => "ERRO_DESPUBLICADO",
        _ => "CONTEUDO_DESPUBLICADO",
    };
}
