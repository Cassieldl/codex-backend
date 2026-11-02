using Codex.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Codex.API.Common;

/// <summary>
/// Mapeamento local (só nos controllers públicos de leitura) de exceção de domínio para
/// status/código HTTP, replicando a tabela de <c>analise-backend.md</c> §4.4/§8 para os casos
/// que P3/P4/P5/P6 realmente lançam. <c>TratamentoErrosMiddleware</c> (BACK-4, fora do escopo
/// desta unidade) hoje é só um baseline que converte QUALQUER exceção em 500 — mapear a tabela
/// completa é trabalho da unidade de auth/escrita interna. Este mapeador existe para que os
/// endpoints públicos já respondam com o status correto (404/410/422) independentemente do
/// cronograma de BACK-4: o controller intercepta a exceção antes dela alcançar o middleware
/// global, então não há conflito — quando BACK-4 completar o middleware, ele nunca chega a ver
/// essas exceções nas rotas públicas (AP-SECURITY-003: provar, não só declarar).
/// </summary>
public static class MapeadorErroPublico
{
    public static IActionResult Mapear(ControllerBase controller, Exception excecao) => excecao switch
    {
        EntidadeNaoEncontradaException e => controller.NotFound(Erro(e.Codigo, e.Message)),
        ConteudoDespublicadoException e => controller.StatusCode(
            StatusCodes.Status410Gone, Erro(CodigoDespublicado(e.TipoItem), e.Message)),
        LimiteExcedidoException e => controller.StatusCode(
            StatusCodes.Status422UnprocessableEntity, Erro("LIMITE_EXCEDIDO", e.Message)),
        _ => throw excecao,
    };

    private static string CodigoDespublicado(string tipoItem) => tipoItem switch
    {
        "provedor" => "PROVEDOR_DESPUBLICADO",
        "versaoprovedor" => "VERSAO_DESPUBLICADA",
        "erro" => "ERRO_DESPUBLICADO",
        _ => "CONTEUDO_DESPUBLICADO",
    };

    private static object Erro(string codigo, string mensagem) => new { error = new { code = codigo, message = mensagem } };
}
