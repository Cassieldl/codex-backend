using Codex.Application.DTOs.Interno;
using Codex.Domain.Constantes;
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
    }
}
