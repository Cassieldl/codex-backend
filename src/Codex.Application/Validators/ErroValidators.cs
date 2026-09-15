using Codex.Application.DTOs.Interno;
using Codex.Domain.Enums;
using FluentValidation;

namespace Codex.Application.Validators;

/// <summary>
/// U9 (§5.1d, §9 — teste obrigatório): <c>Variantes</c> nunca pode ser vazio, para NENHUMA
/// origem. <c>VersoesProvedorIds</c> só é obrigatório para <see cref="OrigemErro.Prefeitura"/> —
/// <see cref="OrigemErro.InvoiCy"/>/<see cref="OrigemErro.Schema"/> aceitam lista vazia livremente.
/// </summary>
public sealed class CriarErroDtoValidator : AbstractValidator<CriarErroDto>
{
    public CriarErroDtoValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().WithMessage("O código do erro é obrigatório.");
        RuleFor(x => x.Titulo).NotEmpty().WithMessage("O título do erro é obrigatório.");
        RuleFor(x => x.Origem).IsInEnum().WithMessage("Origem de erro inválida.");

        RuleFor(x => x.Variantes).NotEmpty()
            .WithMessage("Informe ao menos uma variante de mensagem observada.");

        RuleFor(x => x.VersoesProvedorIds).NotEmpty()
            .When(x => x.Origem == OrigemErro.Prefeitura)
            .WithMessage("Erro de origem Prefeitura precisa de ao menos uma versão de provedor aplicável.");
    }
}

public sealed class AtualizarErroDtoValidator : AbstractValidator<AtualizarErroDto>
{
    public AtualizarErroDtoValidator()
    {
        RuleFor(x => x.Titulo).NotEmpty().WithMessage("O título do erro é obrigatório.");
        RuleFor(x => x.Origem).IsInEnum().WithMessage("Origem de erro inválida.");

        RuleFor(x => x.Variantes).NotEmpty()
            .WithMessage("Informe ao menos uma variante de mensagem observada.");

        RuleFor(x => x.VersoesProvedorIds).NotEmpty()
            .When(x => x.Origem == OrigemErro.Prefeitura)
            .WithMessage("Erro de origem Prefeitura precisa de ao menos uma versão de provedor aplicável.");
    }
}
