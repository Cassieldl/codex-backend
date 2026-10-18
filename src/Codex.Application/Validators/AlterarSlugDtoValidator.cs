using Codex.Application.DTOs.Interno;
using Codex.Domain.Constantes;
using FluentValidation;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Application.Validators;

/// <summary>
/// Entrada dos 3 endpoints de troca de slug (I37/I43/I44). O texto chega livre do formulário e
/// quem normaliza é o service (<c>Slug.DeTexto</c>) — este validator existe para os dois casos em
/// que a normalização não sobrevive até o banco:
///
/// <list type="number">
/// <item>slug só de pontuação ("...", "///") passa em <c>NotEmpty</c>, normaliza para vazio e faz
/// <c>Slug.DeTexto</c> lançar <c>ArgumentException</c> → 500 onde o caso é 422;</item>
/// <item>slug longo demais estouraria o <c>varchar</c> da coluna no <c>UPDATE</c> (22001) → 500.</item>
/// </list>
/// </summary>
public sealed class AlterarSlugDtoValidator : AbstractValidator<AlterarSlugDto>
{
    /// <summary>
    /// O DTO serve provedor, erro e artigo, cujas colunas de slug divergem: vale a mais estreita,
    /// senão o slug que cabe numa entidade e não na outra viraria 500.
    /// </summary>
    private static readonly int TamanhoMaximo = Math.Min(LimitesProvedor.Slug, Math.Min(LimitesErro.Slug, LimitesArtigo.Slug));

    public AlterarSlugDtoValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().WithMessage("O slug é obrigatório.")
            .MaximumLength(TamanhoMaximo).WithMessage($"O slug deve ter no máximo {TamanhoMaximo} caracteres.")
            .Must(SlugVo.PodeDerivarDe).WithMessage("O slug precisa conter ao menos uma letra ou número.");
    }
}
