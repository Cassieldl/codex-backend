using Codex.Application.DTOs.Interno;
using Codex.Domain.Constantes;
using Codex.Domain.Enums;
using FluentValidation;

namespace Codex.Application.Validators;

/// <summary>
/// Registro de lacuna. Os tetos são os de <see cref="LimitesLacunaBusca"/>, os mesmos das colunas:
/// o que passa aqui sempre cabe no banco, e o que não cabe volta como 422 com nome de campo.
/// O comprimento é medido sobre o texto aparado, que é o que a entidade grava.
/// </summary>
public sealed class RegistrarLacunaBuscaDtoValidator : AbstractValidator<RegistrarLacunaBuscaDto>
{
    public RegistrarLacunaBuscaDtoValidator()
    {
        RuleFor(x => x.Termo)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O termo buscado é obrigatório.")
            .Must(termo => termo.Trim().Length >= LimitesLacunaBusca.TermoMinimo)
            .WithMessage($"O termo buscado precisa ter ao menos {LimitesLacunaBusca.TermoMinimo} caracteres.")
            .Must(termo => termo.Trim().Length <= LimitesLacunaBusca.Termo)
            .WithMessage($"O termo buscado tem no máximo {LimitesLacunaBusca.Termo} caracteres.");

        RuleFor(x => x.Parceiro)
            .Must(parceiro => parceiro is null || parceiro.Trim().Length <= LimitesLacunaBusca.Parceiro)
            .WithMessage($"O parceiro tem no máximo {LimitesLacunaBusca.Parceiro} caracteres.");

        RuleFor(x => x.NumeroChamado)
            .Must(chamado => chamado is null || chamado.Trim().Length <= LimitesLacunaBusca.NumeroChamado)
            .WithMessage($"O número do chamado tem no máximo {LimitesLacunaBusca.NumeroChamado} caracteres.");
    }
}

/// <summary>Mudança de estado da lacuna.</summary>
public sealed class AlterarEstadoLacunaBuscaDtoValidator : AbstractValidator<AlterarEstadoLacunaBuscaDto>
{
    public AlterarEstadoLacunaBuscaDtoValidator()
    {
        RuleFor(x => x.Estado).IsInEnum().WithMessage("Estado inválido.");

        RuleFor(x => x.Nota)
            .Must(nota => nota is null || nota.Trim().Length <= LimitesLacunaBusca.Nota)
            .WithMessage($"A nota tem no máximo {LimitesLacunaBusca.Nota} caracteres.");

        RuleFor(x => x.IncluirMesmoTermo)
            .Must(incluir => !incluir)
            .When(x => x.Estado == EstadoLacunaBusca.Aberta)
            .WithMessage("Reabrir vale só para esta ocorrência: 'incluirMesmoTermo' não se combina com o estado 'Aberta'.");
    }
}
