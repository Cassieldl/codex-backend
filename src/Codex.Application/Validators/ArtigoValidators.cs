using Codex.Application.DTOs.Interno;
using FluentValidation;

namespace Codex.Application.Validators;

/// <summary>Criação de artigo (I25) — mesmo padrão de <c>CriarErroDtoValidator</c>.</summary>
public sealed class CriarArtigoDtoValidator : AbstractValidator<CriarArtigoDto>
{
    public CriarArtigoDtoValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum().WithMessage("Tipo de artigo inválido.");
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(200).WithMessage("O título do artigo é obrigatório e tem no máximo 200 caracteres.");
        RuleFor(x => x.Corpo).NotEmpty().WithMessage("O corpo do artigo é obrigatório.");

        RuleFor(x => x.CampoLayoutIds).Must(SemDuplicataENemIdInvalido)
            .WithMessage("A lista de campos do layout não pode ter ids duplicados ou inválidos.");
        RuleFor(x => x.ErroIds).Must(SemDuplicataENemIdInvalido)
            .WithMessage("A lista de erros relacionados não pode ter ids duplicados ou inválidos.");
    }

    private static bool SemDuplicataENemIdInvalido(IReadOnlyCollection<int> ids) =>
        ids.All(id => id >= 1) && ids.Distinct().Count() == ids.Count;
}

/// <summary>Atualização de artigo (I26) — inclui <c>Tipo</c> (decisoes.md B-2, editável).</summary>
public sealed class AtualizarArtigoDtoValidator : AbstractValidator<AtualizarArtigoDto>
{
    public AtualizarArtigoDtoValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum().WithMessage("Tipo de artigo inválido.");
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(200).WithMessage("O título do artigo é obrigatório e tem no máximo 200 caracteres.");
        RuleFor(x => x.Corpo).NotEmpty().WithMessage("O corpo do artigo é obrigatório.");

        RuleFor(x => x.CampoLayoutIds).Must(SemDuplicataENemIdInvalido)
            .WithMessage("A lista de campos do layout não pode ter ids duplicados ou inválidos.");
        RuleFor(x => x.ErroIds).Must(SemDuplicataENemIdInvalido)
            .WithMessage("A lista de erros relacionados não pode ter ids duplicados ou inválidos.");
    }

    private static bool SemDuplicataENemIdInvalido(IReadOnlyCollection<int> ids) =>
        ids.All(id => id >= 1) && ids.Distinct().Count() == ids.Count;
}

/// <summary>Registro de verificação (I29) — não faz sentido verificar no futuro.</summary>
public sealed class AtualizarVerificacaoArtigoDtoValidator : AbstractValidator<AtualizarVerificacaoArtigoDto>
{
    public AtualizarVerificacaoArtigoDtoValidator()
    {
        RuleFor(x => x.VerificadoEm)
            .NotEmpty()
            .Must(data => data <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("A data de verificação não pode estar no futuro.");
    }
}
