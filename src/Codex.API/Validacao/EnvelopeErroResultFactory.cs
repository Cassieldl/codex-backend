using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Results;

namespace Codex.API.Validacao;

/// <summary>
/// Resultado customizado do FluentValidation.AutoValidation (analise-backend.md §5.3): sem isto,
/// o pacote produz o <c>ValidationProblemDetails</c> padrão do ASP.NET Core (shape
/// <c>{ errors: { campo: [...] } }</c>), que quebra o envelope único de erro do resto da API
/// (rules/api.md — <c>{ error: { code, message, details[] } }</c>). Registrado via
/// <c>OverrideDefaultResultFactoryWith&lt;EnvelopeErroResultFactory&gt;()</c> em
/// <see cref="Codex.API.Extensoes.ValidacaoExtensoes"/>.
/// </summary>
public sealed class EnvelopeErroResultFactory : IFluentValidationAutoValidationResultFactory
{
    public Task<IActionResult?> CreateActionResult(
        ActionExecutingContext contexto,
        ValidationProblemDetails? problemaDeValidacao,
        IDictionary<IValidationContext, ValidationResult> resultadosDeValidacao)
    {
        var falhasDeNegocio = resultadosDeValidacao.Values
            .SelectMany(resultado => resultado.Errors)
            .Select(falha => new DetalheErro(ParaCamelCase(falha.PropertyName), falha.ErrorMessage))
            .ToArray();

        // Regra de negócio reprovou (FluentValidation) → 422, o caso original desta factory.
        if (falhasDeNegocio.Length > 0)
            return Resultado(StatusCodes.Status422UnprocessableEntity, "VALIDATION_ERROR",
                "Um ou mais campos são inválidos.", falhasDeNegocio);

        // Nenhum validator reprovou, mas a factory foi chamada: o que invalidou o ModelState foi o
        // próprio model binding — corpo que o System.Text.Json não conseguiu converter (ex.: enum
        // com valor inexistente), JSON sintaticamente inválido, ou parâmetro de rota/query com
        // tipo incompatível. Isso é requisição MALFORMADA, não validação de negócio: rules/api.md
        // reserva 422 para regra de negócio e 400 para request malformado.
        var falhasDeForma = ExtrairFalhasDeBinding(contexto.ModelState);

        if (falhasDeForma.Length > 0)
            return Resultado(StatusCodes.Status400BadRequest, "MALFORMED_REQUEST",
                "A requisição está malformada.", falhasDeForma);

        // Inalcançável na prática (a factory só roda com ModelState inválido, e as duas únicas
        // fontes de invalidez estão cobertas acima); mantido para não devolver corpo indefinido.
        return Resultado(StatusCodes.Status422UnprocessableEntity, "VALIDATION_ERROR",
            "Um ou mais campos são inválidos.", falhasDeNegocio);
    }

    private static Task<IActionResult?> Resultado(
        int status, string codigo, string mensagem, IReadOnlyCollection<DetalheErro> detalhes)
    {
        var corpo = new
        {
            error = new
            {
                code = codigo,
                message = mensagem,
                details = detalhes.Select(d => new { field = d.Field, message = d.Message }).ToArray(),
            },
        };

        return Task.FromResult<IActionResult?>(new ObjectResult(corpo) { StatusCode = status });
    }

    /// <summary>
    /// Traduz os erros de model binding em <c>details[]</c> por campo — sem isto o corpo do erro
    /// sai com <c>details</c> VAZIO e o formulário do frontend anuncia "campos inválidos" sem ter
    /// como destacar campo nenhum.
    ///
    /// <para>
    /// A mensagem é SEMPRE nossa, nunca a do binder: em falha de desserialização o texto original
    /// é a mensagem da <c>JsonException</c>, que carrega o nome do tipo .NET interno
    /// (<c>Codex.Domain.Enums.CategoriaProvedor</c>) e o offset de byte — detalhe interno que não
    /// pode vazar em resposta de API (rules/security.md A05).
    /// </para>
    /// </summary>
    private static DetalheErro[] ExtrairFalhasDeBinding(ModelStateDictionary modelState) => modelState
        .Where(entrada => entrada.Value?.Errors.Count > 0)
        .Select(entrada => NomeDoCampo(entrada.Key))
        .Distinct(StringComparer.Ordinal)
        .Select(campo => campo.Length == 0
            ? new DetalheErro(campo, "O corpo da requisição não é um JSON válido.")
            : new DetalheErro(campo, "Valor inválido para este campo."))
        .ToArray();

    /// <summary>
    /// A chave do <c>ModelState</c> para erro de corpo vem como caminho JSON (<c>"$.categoria"</c>,
    /// <c>"$.aliases[0]"</c>) e já em camelCase — o prefixo <c>$.</c> é ruído do serializador e
    /// sai. Erro de rota/query vem como nome do parâmetro C#, aí sim normalizado para camelCase.
    /// Chave vazia ou <c>"$"</c> significa que a falha é do corpo como um todo, não de um campo.
    /// </summary>
    private static string NomeDoCampo(string chaveModelState)
    {
        if (string.IsNullOrEmpty(chaveModelState) || chaveModelState == "$")
            return string.Empty;

        return chaveModelState.StartsWith("$.", StringComparison.Ordinal)
            ? chaveModelState[2..]
            : ParaCamelCase(chaveModelState);
    }

    private sealed record DetalheErro(string Field, string Message);

    /// <summary>
    /// <c>PropertyName</c> do FluentValidation vem em PascalCase (nome da propriedade C# do DTO);
    /// a API serializa em camelCase (padrão do System.Text.Json no ASP.NET Core) — converter aqui
    /// para o <c>field</c> do erro bater com o nome que o frontend realmente usa no formulário.
    /// </summary>
    private static string ParaCamelCase(string nomePropriedade)
    {
        if (string.IsNullOrEmpty(nomePropriedade))
            return nomePropriedade;

        // Propriedades aninhadas (ex.: "Endereco.Rua") — só o primeiro segmento vira camelCase
        // por nível, preservando a estrutura de path.
        var segmentos = nomePropriedade.Split('.');
        for (var i = 0; i < segmentos.Length; i++)
        {
            var segmento = segmentos[i];
            segmentos[i] = segmento.Length == 0
                ? segmento
                : char.ToLowerInvariant(segmento[0]) + segmento[1..];
        }

        return string.Join('.', segmentos);
    }
}
