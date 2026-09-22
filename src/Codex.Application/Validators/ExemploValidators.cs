using System.Text.RegularExpressions;
using Codex.Application.DTOs.Interno;
using FluentValidation;

namespace Codex.Application.Validators;

/// <summary>
/// Régua de negócio do XML do exemplo (C3): <b>64 KiB medidos em CARACTERES</b> de uma
/// <see cref="string"/> .NET (UTF-16) — deliberadamente diferente do
/// <c>ck_codex_exemplos_xml_tamanho</c>, que é 256 KiB medidos em BYTES UTF-8
/// (<c>octet_length</c>) e existe como guarda de catástrofe.
/// <b>Não igualar os dois números</b>: as unidades são diferentes, e um XML acentuado de 64 KiB em
/// caracteres passa de 64 KiB em bytes — igualar faria a escrita já validada morrer com 23514/500
/// em vez de 422 (modelo-dados.md §1.5).
/// </summary>
internal static class TetosExemplo
{
    internal const int XmlCaracteres = 64 * 1024;

    /// <summary>O slug é derivado do nome — nome sem letra nem dígito normalizaria para vazio.</summary>
    internal static bool NomeGeraSlug(string? nome) =>
        !string.IsNullOrWhiteSpace(nome) && Regex.IsMatch(nome, "[a-zA-Z0-9]");
}

public sealed class CriarExemploDtoValidator : AbstractValidator<CriarExemploDto>
{
    public CriarExemploDtoValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("O nome do exemplo é obrigatório.")
            .MaximumLength(160).WithMessage("O nome do exemplo deve ter no máximo 160 caracteres.")
            .Must(TetosExemplo.NomeGeraSlug).WithMessage("O nome do exemplo precisa conter ao menos uma letra ou número.");

        RuleFor(x => x.Xml).NotEmpty().WithMessage("O XML do exemplo é obrigatório.")
            .Must(xml => xml is null || xml.Length <= TetosExemplo.XmlCaracteres)
            .WithMessage($"O XML do exemplo deve ter no máximo {TetosExemplo.XmlCaracteres} caracteres.");
    }
}

public sealed class AtualizarExemploDtoValidator : AbstractValidator<AtualizarExemploDto>
{
    public AtualizarExemploDtoValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("O nome do exemplo é obrigatório.")
            .MaximumLength(160).WithMessage("O nome do exemplo deve ter no máximo 160 caracteres.")
            .Must(TetosExemplo.NomeGeraSlug).WithMessage("O nome do exemplo precisa conter ao menos uma letra ou número.");

        RuleFor(x => x.Xml).NotEmpty().WithMessage("O XML do exemplo é obrigatório.")
            .Must(xml => xml is null || xml.Length <= TetosExemplo.XmlCaracteres)
            .WithMessage($"O XML do exemplo deve ter no máximo {TetosExemplo.XmlCaracteres} caracteres.");
    }
}
