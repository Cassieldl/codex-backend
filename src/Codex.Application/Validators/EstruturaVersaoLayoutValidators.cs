using Codex.Application.DTOs.Interno;
using Codex.Domain.Constantes;
using FluentValidation;

namespace Codex.Application.Validators;

/// <summary>
/// Tetos da árvore gravada: a estrutura vai inteira para uma coluna e para toda leitura pública,
/// então tamanho e profundidade precisam de limite antes de chegar ao banco.
/// </summary>
public sealed class AtualizarEstruturaVersaoLayoutDtoValidator : AbstractValidator<AtualizarEstruturaVersaoLayoutDto>
{
    public AtualizarEstruturaVersaoLayoutDtoValidator()
    {
        RuleFor(x => x.Xml).NotNull().WithMessage("A árvore XML é obrigatória.")
            .Custom((raiz, contexto) => ValidarArvore(raiz, "xml", contexto));
        RuleFor(x => x.Json).NotNull().WithMessage("A árvore JSON é obrigatória.")
            .Custom((raiz, contexto) => ValidarArvore(raiz, "json", contexto));
    }

    /// <summary>Percurso iterativo: uma árvore funda não pode estourar a pilha do próprio validador.</summary>
    private static void ValidarArvore(NoEstruturaLayoutDto? raiz, string arvore, ValidationContext<AtualizarEstruturaVersaoLayoutDto> contexto)
    {
        if (raiz is null)
            return;

        var pendentes = new Stack<(NoEstruturaLayoutDto No, int Profundidade)>();
        pendentes.Push((raiz, 1));
        var total = 0;

        while (pendentes.Count > 0)
        {
            var (no, profundidade) = pendentes.Pop();
            total++;

            if (total > LimitesEstruturaLayout.Nos)
            {
                contexto.AddFailure(arvore, $"A árvore {arvore} tem mais de {LimitesEstruturaLayout.Nos} nós.");
                return;
            }

            if (profundidade > LimitesEstruturaLayout.Profundidade)
            {
                contexto.AddFailure(arvore, $"A árvore {arvore} passa da profundidade {LimitesEstruturaLayout.Profundidade}.");
                return;
            }

            var erro = ErroDoNo(no);
            if (erro is not null)
            {
                contexto.AddFailure(arvore, $"Nó '{Recortar(no?.Caminho)}' da árvore {arvore}: {erro}");
                return;
            }

            foreach (var filho in no!.Filhos)
                pendentes.Push((filho, profundidade + 1));
        }
    }

    private static string? ErroDoNo(NoEstruturaLayoutDto? no)
    {
        if (no is null)
            return "nó vazio.";
        if (string.IsNullOrWhiteSpace(no.Nome) || no.Nome.Length > LimitesEstruturaLayout.Nome)
            return $"o nome é obrigatório e tem no máximo {LimitesEstruturaLayout.Nome} caracteres.";
        if (string.IsNullOrWhiteSpace(no.Caminho) || no.Caminho.Length > LimitesEstruturaLayout.Caminho)
            return $"o caminho é obrigatório e tem no máximo {LimitesEstruturaLayout.Caminho} caracteres.";
        if (no.ValorModelo is { Length: > LimitesEstruturaLayout.ValorModelo })
            return $"o valor-modelo tem no máximo {LimitesEstruturaLayout.ValorModelo} caracteres.";
        if (no.CaminhoCampo is { Length: > LimitesCampoLayout.ColunaCaminhoXml })
            return $"o caminho do campo tem no máximo {LimitesCampoLayout.ColunaCaminhoXml} caracteres.";
        if (!FormatosNoEstrutura.Todos.Contains(no.Formato ?? string.Empty))
            return "o formato precisa ser ambos, soXml ou soJson.";
        if (no.Filhos is null)
            return "a lista de filhos é obrigatória.";

        return null;
    }

    private static string Recortar(string? caminho) =>
        caminho is null ? "?" : caminho.Length <= 80 ? caminho : caminho[..80];
}
