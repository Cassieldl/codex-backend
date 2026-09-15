using Codex.Application.DTOs.Interno;
using FluentValidation;

namespace Codex.Application.Validators;

public sealed class CriarVersaoLayoutDtoValidator : AbstractValidator<CriarVersaoLayoutDto>
{
    public CriarVersaoLayoutDtoValidator()
    {
        RuleFor(x => x.Rotulo).NotEmpty().WithMessage("O rótulo da versão de layout é obrigatório.")
            .MaximumLength(160);
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
