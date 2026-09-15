using Codex.API.Validacao;
using Codex.Application.Interfaces;
using Codex.Application.Services;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

namespace Codex.API.Extensoes;

/// <summary>
/// Wiring de validação automática (FluentValidation + AutoValidation, analise-backend.md §5.3,
/// completo — BACK-4): registra todos os validators de <c>Codex.Application.Validators</c> via
/// scan de assembly e troca o result factory padrão por <see cref="EnvelopeErroResultFactory"/>
/// (422 no envelope único de erro, ver rules/api.md). Sem os dois passos — registrar E trocar o
/// factory — o <c>422</c> nunca dispara no formato certo (AP-SECURITY-003: registrado ≠ provado;
/// prova é <c>Codex.Tests/Integration/Interno/FluentValidationWiringTests.cs</c>).
/// </summary>
public static class ValidacaoExtensoes
{
    public static IServiceCollection AddValidacao(this IServiceCollection services)
    {
        // Âncora de assembly: qualquer tipo já existente em Codex.Application basta — os
        // validators concretos (Codex.Application.Validators.*) são descobertos pelo scan,
        // não precisam ser referenciados nominalmente aqui.
        services.AddValidatorsFromAssemblyContaining<ProvedorServicoPublico>();

        services.AddFluentValidationAutoValidation(configuracao =>
        {
            configuracao.OverrideDefaultResultFactoryWith<EnvelopeErroResultFactory>();
        });

        // IContextoRequisicaoHttp — usado por validators que precisam checar uma invariante
        // contra a entidade já existente identificada pelo {id} da rota (ex.: escopo do aviso em
        // AtualizarAvisoDtoValidator, ordem de descontinuação em AtualizarCampoLayoutDtoValidator).
        services.AddHttpContextAccessor();
        services.AddScoped<IContextoRequisicaoHttp, ContextoRequisicaoHttp>();

        return services;
    }
}
