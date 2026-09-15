using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Extensoes;

/// <summary>
/// Envelope único de erro para os status que o FRAMEWORK produz sem passar por controller nem por
/// exceção (achado <c>BAIXO-B09</c>): <c>415</c> de negociação de media type, <c>405</c>,
/// <c>404</c> de rota não casada e o <c>401</c>/<c>403</c> do pipeline de autorização. Sem isto o
/// <c>415</c> sai no <c>ProblemDetails</c> do framework (<c>type</c>/<c>title</c>/<c>status</c>/
/// <c>traceId</c>) e os demais saem sem corpo — três formatos diferentes de erro na mesma API,
/// contra o envelope <c>{ error: { code, message } }</c> de <c>rules/api.md</c>.
///
/// <para>
/// São dois passos que só funcionam juntos: <see cref="AddEnvelopeErroDeStatus"/> desliga o
/// mapeamento automático de client error do MVC (que é o que injeta o <c>ProblemDetails</c>) e
/// <see cref="UseEnvelopeErroDeStatus"/> escreve o envelope nas respostas de erro que ficaram sem
/// corpo. Respostas que já têm corpo — todo erro de negócio dos controllers e o
/// <c>TratamentoErrosMiddleware</c> — não são tocadas.
/// </para>
///
/// <para>
/// <b>Não é superfície pré-autenticação:</b> o auditor verificou que o <c>415</c> só ocorre
/// DEPOIS da autorização (sem token dá <c>401</c>, com role errada dá <c>403</c>). A correção
/// aqui é de consistência de contrato, não de brecha — e por isso a mensagem continua genérica,
/// sem ecoar o media type recebido.
/// </para>
/// </summary>
public static class EnvelopeErroStatusExtensoes
{
    public static IServiceCollection AddEnvelopeErroDeStatus(this IServiceCollection services)
    {
        // SuppressMapClientErrors: sem isto o MVC converte 415/405/404 em ProblemDetails ANTES de
        // o pipeline de status code pages ver a resposta, e o corpo deixa de estar vazio.
        services.Configure<ApiBehaviorOptions>(opcoes => opcoes.SuppressMapClientErrors = true);

        return services;
    }

    public static IApplicationBuilder UseEnvelopeErroDeStatus(this IApplicationBuilder app) =>
        app.UseStatusCodePages(async contexto =>
        {
            var resposta = contexto.HttpContext.Response;
            var (codigo, mensagem) = MapearStatus(resposta.StatusCode);

            resposta.ContentType = "application/json";

            await resposta.WriteAsJsonAsync(new { error = new { code = codigo, message = mensagem } });
        });

    /// <summary>Status HTTP → (código do envelope, mensagem pública). Nunca detalha internals.</summary>
    private static (string Codigo, string Mensagem) MapearStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest =>
            ("REQUISICAO_INVALIDA", "Requisição inválida."),

        StatusCodes.Status401Unauthorized =>
            ("NAO_AUTENTICADO", "Autenticação necessária."),

        StatusCodes.Status403Forbidden =>
            ("SEM_PERMISSAO", "Você não tem permissão para esta operação."),

        StatusCodes.Status404NotFound =>
            ("NAO_ENCONTRADO", "Recurso não encontrado."),

        StatusCodes.Status405MethodNotAllowed =>
            ("METODO_NAO_PERMITIDO", "Método HTTP não permitido para este recurso."),

        StatusCodes.Status406NotAcceptable =>
            ("FORMATO_NAO_ACEITO", "Nenhum formato de resposta aceitável foi negociado."),

        StatusCodes.Status415UnsupportedMediaType =>
            ("MEDIA_TYPE_NAO_SUPORTADO", "Content-Type não suportado. Use application/json."),

        StatusCodes.Status429TooManyRequests =>
            ("LIMITE_REQUISICOES", "Muitas requisições. Tente novamente em instantes."),

        >= 500 =>
            ("ERRO_INTERNO", "Ocorreu um erro inesperado."),

        _ =>
            ("ERRO_REQUISICAO", "Não foi possível processar a requisição."),
    };
}
