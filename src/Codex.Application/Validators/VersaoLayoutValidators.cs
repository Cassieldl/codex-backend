using Codex.Application.DTOs.Interno;
using FluentValidation;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Application.Validators;

public sealed class CriarVersaoLayoutDtoValidator : AbstractValidator<CriarVersaoLayoutDto>
{
    public CriarVersaoLayoutDtoValidator()
    {
        RuleFor(x => x.Rotulo).NotEmpty().WithMessage("O rótulo da versão de layout é obrigatório.")
            .MaximumLength(160)
            // O slug nasce do rótulo (VersaoLayout.Criar) — rótulo só de pontuação viraria 500.
            .Must(SlugVo.PodeDerivarDe).WithMessage("O rótulo da versão de layout precisa conter ao menos uma letra ou número.");
    }
}

public sealed class AtualizarVersaoLayoutDtoValidator : AbstractValidator<AtualizarVersaoLayoutDto>
{
    public AtualizarVersaoLayoutDtoValidator()
    {
        RuleFor(x => x.Rotulo).NotEmpty().MaximumLength(160)
            .When(x => x.Rotulo is not null)
            .WithMessage("O rótulo da versão de layout não pode ser vazio.");

        RuleFor(x => x.Estado).IsInEnum()
            .When(x => x.Estado.HasValue)
            .WithMessage("Estado de versão de layout inválido.");

        RuleFor(x => x.Ordem).GreaterThan(0)
            .When(x => x.Ordem.HasValue)
            .WithMessage("Ordem precisa ser positiva.");
    }
}
