using Codex.Application.DTOs.Interno;
using FluentValidation;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Application.Validators;

public sealed class CriarVersaoDtoValidator : AbstractValidator<CriarVersaoDto>
{
    public CriarVersaoDtoValidator()
    {
        RuleFor(x => x.VersaoLayoutId).GreaterThan(0).WithMessage("A versão do layout de origem é obrigatória.");

        RuleFor(x => x.Rotulo).NotEmpty().WithMessage("O rótulo da versão é obrigatório.")
            .MaximumLength(160).WithMessage("O rótulo da versão deve ter no máximo 160 caracteres.")
            // O slug nasce do rótulo (VersaoProvedor.Criar) — rótulo só de pontuação viraria 500.
            .Must(SlugVo.PodeDerivarDe).WithMessage("O rótulo da versão precisa conter ao menos uma letra ou número.");
    }
}

public sealed class AtualizarVersaoDtoValidator : AbstractValidator<AtualizarVersaoDto>
{
    public AtualizarVersaoDtoValidator()
    {
        RuleFor(x => x.Rotulo).NotEmpty().WithMessage("O rótulo da versão é obrigatório.")
            .MaximumLength(160).WithMessage("O rótulo da versão deve ter no máximo 160 caracteres.");
    }
}

public sealed class AtualizarVerificacaoDtoValidator : AbstractValidator<AtualizarVerificacaoDto>
{
    public AtualizarVerificacaoDtoValidator()
    {
        RuleFor(x => x.VerificadoEm).NotEmpty().WithMessage("A data de verificação é obrigatória.");
    }
}
