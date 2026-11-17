using Codex.Application.DTOs.Interno;
using Codex.Domain.Constantes;
using Codex.Domain.ValueObjects;
using FluentValidation;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Application.Validators;

public sealed class CriarVersaoLayoutDtoValidator : AbstractValidator<CriarVersaoLayoutDto>
{
    public CriarVersaoLayoutDtoValidator()
    {
        RuleFor(x => x.Rotulo).NotEmpty().WithMessage("O rótulo da versão de layout é obrigatório.")
            .MaximumLength(LimitesVersaoLayout.Rotulo)
            .WithMessage($"O rótulo da versão de layout tem no máximo {LimitesVersaoLayout.Rotulo} caracteres.")
            // O slug nasce do rótulo (VersaoLayout.Criar) e a coluna dele é mais larga que a do rótulo.
            .Must(SlugVo.PodeDerivarDe).WithMessage("O rótulo da versão de layout precisa conter ao menos uma letra ou número.");

        RuleFor(x => x.NormaOrigem).MaximumLength(LimitesVersaoLayout.NormaOrigem)
            .WithMessage(MensagensVersaoLayout.NormaOrigemLonga);

        RuleFor(x => x.Mudancas!.Count).LessThanOrEqualTo(LimitesVersaoLayout.MudancasPorVersao)
            .When(x => x.Mudancas is not null)
            .OverridePropertyName("mudancas")
            .WithMessage(MensagensVersaoLayout.MudancasDemais);

        RuleForEach(x => x.Mudancas).NotNull().WithMessage("Mudança vazia na lista.")
            .SetValidator(new MudancaLayoutInternoDtoValidator());
    }
}

public sealed class AtualizarVersaoLayoutDtoValidator : AbstractValidator<AtualizarVersaoLayoutDto>
{
    public AtualizarVersaoLayoutDtoValidator()
    {
        RuleFor(x => x.Rotulo).NotEmpty()
            .When(x => x.Rotulo is not null)
            .WithMessage("O rótulo da versão de layout não pode ser vazio.");

        RuleFor(x => x.Rotulo).MaximumLength(LimitesVersaoLayout.Rotulo)
            .WithMessage($"O rótulo da versão de layout tem no máximo {LimitesVersaoLayout.Rotulo} caracteres.");

        RuleFor(x => x.Estado).IsInEnum()
            .When(x => x.Estado.HasValue)
            .WithMessage("Estado de versão de layout inválido.");

        RuleFor(x => x.Ordem).GreaterThan(0)
            .When(x => x.Ordem.HasValue)
            .WithMessage("Ordem precisa ser positiva.");

        RuleFor(x => x.NormaOrigem).MaximumLength(LimitesVersaoLayout.NormaOrigem)
            .WithMessage(MensagensVersaoLayout.NormaOrigemLonga);

        RuleFor(x => x.Mudancas!.Count).LessThanOrEqualTo(LimitesVersaoLayout.MudancasPorVersao)
            .When(x => x.Mudancas is not null)
            .OverridePropertyName("mudancas")
            .WithMessage(MensagensVersaoLayout.MudancasDemais);

        RuleForEach(x => x.Mudancas).NotNull().WithMessage("Mudança vazia na lista.")
            .SetValidator(new MudancaLayoutInternoDtoValidator());
    }
}

/// <summary>Um item de <c>mudancas</c>: tipo conhecido, descrição e os caminhos que o tipo pede.</summary>
public sealed class MudancaLayoutInternoDtoValidator : AbstractValidator<MudancaLayoutInternoDto>
{
    public MudancaLayoutInternoDtoValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum().WithMessage("Tipo de mudança inválido.");

        RuleFor(x => x.Descricao).NotEmpty().WithMessage("A descrição da mudança é obrigatória.")
            .MaximumLength(LimitesVersaoLayout.DescricaoMudanca)
            .WithMessage($"A descrição da mudança tem no máximo {LimitesVersaoLayout.DescricaoMudanca} caracteres.");

        RuleFor(x => x.Antes).NotEmpty()
            .When(x => MudancaLayout.ExigeAntes(x.Tipo))
            .WithMessage("Este tipo de mudança pede o caminho de antes.");

        RuleFor(x => x.Depois).NotEmpty()
            .When(x => MudancaLayout.ExigeDepois(x.Tipo))
            .WithMessage("Este tipo de mudança pede o caminho de depois.");

        RuleFor(x => x.Antes).Empty()
            .When(x => !MudancaLayout.ExigeAntes(x.Tipo))
            .WithMessage("Caminho novo não tem caminho de antes.");

        RuleFor(x => x.Depois).Empty()
            .When(x => !MudancaLayout.ExigeDepois(x.Tipo))
            .WithMessage("Caminho que saiu não tem caminho de depois.");

        RuleFor(x => x.Antes).MaximumLength(LimitesVersaoLayout.CaminhoMudanca)
            .WithMessage(MensagensVersaoLayout.CaminhoMudancaLongo);

        RuleFor(x => x.Depois).MaximumLength(LimitesVersaoLayout.CaminhoMudanca)
            .WithMessage(MensagensVersaoLayout.CaminhoMudancaLongo);
    }
}

internal static class MensagensVersaoLayout
{
    public static readonly string NormaOrigemLonga =
        $"A norma de origem da versão de layout tem no máximo {LimitesVersaoLayout.NormaOrigem} caracteres.";

    public static readonly string MudancasDemais =
        $"A versão de layout tem no máximo {LimitesVersaoLayout.MudancasPorVersao} mudanças.";

    public static readonly string CaminhoMudancaLongo =
        $"O caminho da mudança tem no máximo {LimitesVersaoLayout.CaminhoMudanca} caracteres.";
}
