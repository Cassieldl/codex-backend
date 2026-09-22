using Codex.Application.DTOs.Interno;
using Codex.Domain.Enums;
using FluentValidation;
using SlugVo = Codex.Domain.ValueObjects.Slug;

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

        // O slug do erro nasce de "{codigo}-{titulo}" (Erro.Criar), então a guarda é sobre o PAR:
        // cada campo pode ser só pontuação sem que o outro seja, e só a concatenação diz se a
        // normalização sobra vazia (Slug.DeTexto lançaria ArgumentException → 500 em vez de 422).
        RuleFor(x => x).Must(dto => SlugVo.PodeDerivarDe($"{dto.Codigo}-{dto.Titulo}"))
            .WithName(nameof(CriarErroDto.Titulo))
            .WithMessage("O código e o título do erro precisam conter ao menos uma letra ou número.");

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
