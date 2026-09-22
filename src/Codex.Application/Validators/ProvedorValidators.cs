using Codex.Application.DTOs.Interno;
using FluentValidation;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Application.Validators;

public sealed class CriarProvedorDtoValidator : AbstractValidator<CriarProvedorDto>
{
    public CriarProvedorDtoValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("O nome do provedor é obrigatório.")
            .MaximumLength(160).WithMessage("O nome do provedor deve ter no máximo 160 caracteres.")
            // O slug nasce do nome (Provedor.Criar). Nome sem letra nem dígito normaliza para
            // vazio e Slug.DeTexto lança ArgumentException — 500 no lugar do 422 que o caso é.
            .Must(SlugVo.PodeDerivarDe).WithMessage("O nome do provedor precisa conter ao menos uma letra ou número.");

        RuleFor(x => x.IdentificadorInvoicy).NotEmpty().WithMessage("O identificador InvoiCy é obrigatório.")
            .MaximumLength(80).WithMessage("O identificador InvoiCy deve ter no máximo 80 caracteres.");

        RuleFor(x => x.Categoria).IsInEnum().WithMessage("Categoria de provedor inválida.");

        RuleForEach(x => x.Aliases).NotEmpty().WithMessage("Alias não pode ser vazio.");
    }
}

public sealed class AtualizarProvedorDtoValidator : AbstractValidator<AtualizarProvedorDto>
{
    public AtualizarProvedorDtoValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("O nome do provedor é obrigatório.")
            .MaximumLength(160).WithMessage("O nome do provedor deve ter no máximo 160 caracteres.");

        RuleFor(x => x.Categoria).IsInEnum().WithMessage("Categoria de provedor inválida.");

        RuleForEach(x => x.Aliases).NotEmpty().WithMessage("Alias não pode ser vazio.");
    }
}
