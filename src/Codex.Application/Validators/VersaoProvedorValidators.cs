using Codex.Application.DTOs.Interno;
using FluentValidation;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Application.Validators;

public sealed class CriarVersaoDtoValidator : AbstractValidator<CriarVersaoDto>
{
    public CriarVersaoDtoValidator()
    {
        RuleFor(x => x.VersaoLayoutId).GreaterThan(0).WithMessage("A versão do layout de origem é obrigatória.");

        // versoesprovedor.rotulo é varchar(80)
        // (VersaoProvedorConfiguration.cs) — 160 deixava passar 81–160 e morrer em 22001 (500).
        RuleFor(x => x.Rotulo).NotEmpty().WithMessage("O rótulo da versão é obrigatório.")
            .MaximumLength(80).WithMessage("O rótulo da versão deve ter no máximo 80 caracteres.")
            // O slug nasce do rótulo (VersaoProvedor.Criar) — rótulo só de pontuação viraria 500.
            .Must(SlugVo.PodeDerivarDe).WithMessage("O rótulo da versão precisa conter ao menos uma letra ou número.");
    }
}

public sealed class AtualizarVersaoDtoValidator : AbstractValidator<AtualizarVersaoDto>
{
    public AtualizarVersaoDtoValidator()
    {
        // Mesmo teto de 80 do validator de criação —
        // vale a coluna, não um número arbitrário duplicado.
        RuleFor(x => x.Rotulo).NotEmpty().WithMessage("O rótulo da versão é obrigatório.")
            .MaximumLength(80).WithMessage("O rótulo da versão deve ter no máximo 80 caracteres.");

        // null = mantém o layout atual (default do frontend hoje). Informado, precisa ser um id válido.
        RuleFor(x => x.VersaoLayoutId).GreaterThan(0)
            .When(x => x.VersaoLayoutId.HasValue)
            .WithMessage("A versão do layout de origem, quando informada, deve ser um id válido.");
    }
}

public sealed class AtualizarVerificacaoDtoValidator : AbstractValidator<AtualizarVerificacaoDto>
{
    public AtualizarVerificacaoDtoValidator()
    {
        RuleFor(x => x.VerificadoEm).NotEmpty().WithMessage("A data de verificação é obrigatória.");
    }
}
