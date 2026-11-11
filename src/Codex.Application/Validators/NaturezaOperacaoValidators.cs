using Codex.Application.DTOs.Interno;
using Codex.Domain.Constantes;
using FluentValidation;

namespace Codex.Application.Validators;

public sealed class AtualizarNaturezasOperacaoDtoValidator : AbstractValidator<AtualizarNaturezasOperacaoDto>
{
    public AtualizarNaturezasOperacaoDtoValidator()
    {
        RuleFor(x => x.Itens)
            .NotNull().WithMessage("A lista de naturezas da operação é obrigatória.")
            .Must(itens => itens is null || itens.Count <= LimitesNaturezaOperacao.ItensPorVersao)
            .WithMessage($"A versão aceita no máximo {LimitesNaturezaOperacao.ItensPorVersao} naturezas da operação.")
            .Must(itens => itens is null || itens.Where(i => i is not null).Select(i => i.CodigoInvoicy).Distinct().Count() == itens.Count(i => i is not null))
            .WithMessage("Cada código do InvoiCy aparece uma vez só na versão.");

        RuleForEach(x => x.Itens).NotNull().SetValidator(new ItemNaturezaOperacaoDtoValidator());
    }
}

public sealed class ItemNaturezaOperacaoDtoValidator : AbstractValidator<ItemNaturezaOperacaoDto>
{
    public ItemNaturezaOperacaoDtoValidator()
    {
        RuleFor(x => x.CodigoInvoicy)
            .InclusiveBetween(0, LimitesNaturezaOperacao.CodigoInvoicyMaximo)
            .WithMessage($"O código do InvoiCy vai de 0 a {LimitesNaturezaOperacao.CodigoInvoicyMaximo}.");

        RuleFor(x => x.Descricao)
            .NotEmpty().WithMessage("A descrição é obrigatória.")
            .MaximumLength(LimitesNaturezaOperacao.Descricao);

        RuleFor(x => x.ValorProvedor)
            .NotEmpty().WithMessage("O valor no layout do provedor é obrigatório.")
            .MaximumLength(LimitesNaturezaOperacao.ValorProvedor);

        RuleFor(x => x.Observacao).MaximumLength(LimitesNaturezaOperacao.Observacao);
    }
}
