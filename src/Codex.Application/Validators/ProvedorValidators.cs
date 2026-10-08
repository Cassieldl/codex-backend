using System.Linq.Expressions;
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

        RegrasDeAliasProvedor.Aplicar(this, x => x.Aliases);
    }
}

public sealed class AtualizarProvedorDtoValidator : AbstractValidator<AtualizarProvedorDto>
{
    public AtualizarProvedorDtoValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("O nome do provedor é obrigatório.")
            .MaximumLength(160).WithMessage("O nome do provedor deve ter no máximo 160 caracteres.");

        RuleFor(x => x.Categoria).IsInEnum().WithMessage("Categoria de provedor inválida.");

        RegrasDeAliasProvedor.Aplicar(this, x => x.Aliases);
    }
}

/// <summary>
/// Regras de alias compartilhadas por criar e atualizar provedor — cada uma recusa com 422 o que o
/// banco recusaria com 500.
/// </summary>
internal static class RegrasDeAliasProvedor
{
    /// <summary><c>aliasesprovedor.alias</c> é <c>varchar(160)</c> (<c>AliasProvedorConfiguration</c>).</summary>
    private const int TamanhoMaximo = 160;

    public static void Aplicar<T>(AbstractValidator<T> validador, Expression<Func<T, IEnumerable<string>>> aliases)
    {
        validador.RuleForEach(aliases)
            .NotEmpty().WithMessage("Alias não pode ser vazio.")
            .MaximumLength(TamanhoMaximo).WithMessage($"Cada alias deve ter no máximo {TamanhoMaximo} caracteres.");

        validador.RuleFor(aliases).Custom((lista, contexto) =>
        {
            var repetido = PrimeiroRepetido(lista);
            if (repetido is not null)
                contexto.AddFailure($"O alias '{repetido}' aparece mais de uma vez na lista.");
        });
    }

    /// <summary>
    /// Compara EXATAMENTE como <c>uq_codex_aliasesprovedor_provedorid_alias</c>: índice único comum
    /// sobre a coluna <c>varchar</c>, sem <c>lower()</c>, sem <c>citext</c> e sem Trim antes de
    /// gravar — então "ADN" e "adn" são distintos, e " ADN" difere de "ADN". Recusar mais que isso
    /// barraria o que o banco aceita; recusar menos devolveria 500.
    /// </summary>
    private static string? PrimeiroRepetido(IEnumerable<string>? aliases)
    {
        if (aliases is null)
            return null;

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var alias in aliases)
        {
            if (alias is not null && !vistos.Add(alias))
                return alias;
        }

        return null;
    }
}
