using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using FluentValidation;

namespace Codex.Application.Validators;

public static class LimitesAviso
{
    /// <summary>Mesmo teto de campos por versão que a página pública aceita exibir.</summary>
    public const int MaximoCamposAfetados = 2000;
}

public sealed class CriarAvisoDeVersaoDtoValidator : AbstractValidator<CriarAvisoDeVersaoDto>
{
    public CriarAvisoDeVersaoDtoValidator()
    {
        RuleFor(x => x.Severidade).IsInEnum().WithMessage("Severidade inválida.");
        RuleFor(x => x.Titulo).NotEmpty().WithMessage("O título do aviso é obrigatório.").MaximumLength(200);
        RuleFor(x => x.Corpo).NotEmpty().WithMessage("O corpo do aviso é obrigatório.");

        RuleFor(x => x.CampoVersaoIds)
            .Must(campoVersaoIds => campoVersaoIds is null || campoVersaoIds.Count <= LimitesAviso.MaximoCamposAfetados)
            .WithMessage($"Informe no máximo {LimitesAviso.MaximoCamposAfetados} campos afetados.");
    }
}

public sealed class CriarAvisoDeProvedorDtoValidator : AbstractValidator<CriarAvisoDeProvedorDto>
{
    public CriarAvisoDeProvedorDtoValidator()
    {
        RuleFor(x => x.Severidade).IsInEnum().WithMessage("Severidade inválida.");
        RuleFor(x => x.Titulo).NotEmpty().WithMessage("O título do aviso é obrigatório.").MaximumLength(200);
        RuleFor(x => x.Corpo).NotEmpty().WithMessage("O corpo do aviso é obrigatório.");
    }
}

/// <summary>
/// Única rota de atualização para os dois escopos de aviso (I14b) — <see cref="AtualizarAvisoDto.CampoVersaoIds"/>
/// só é aplicável quando o aviso carregado (via <c>{id}</c> da rota) é de escopo VERSÃO (risco #3
/// do DBA, U8). Carrega o aviso pelo id da rota via <see cref="IContextoRequisicaoHttp"/> +
/// repositório — mesma técnica de <see cref="CriarCampoVersaoDtoValidator"/>. O domínio
/// (<see cref="Codex.Domain.Entities.Aviso.SubstituirCamposAfetados"/>) replica a mesma guarda
/// como defesa final, independente deste validator.
/// </summary>
public sealed class AtualizarAvisoDtoValidator : AbstractValidator<AtualizarAvisoDto>
{
    public AtualizarAvisoDtoValidator(IAvisoRepositorioInterno repositorio, IContextoRequisicaoHttp contextoRequisicao)
    {
        RuleFor(x => x.Severidade).IsInEnum().WithMessage("Severidade inválida.");
        RuleFor(x => x.Titulo).NotEmpty().WithMessage("O título do aviso é obrigatório.").MaximumLength(200);
        RuleFor(x => x.Corpo).NotEmpty().WithMessage("O corpo do aviso é obrigatório.");

        RuleFor(x => x.CampoVersaoIds)
            .Must(campoVersaoIds => campoVersaoIds is null || campoVersaoIds.Count <= LimitesAviso.MaximoCamposAfetados)
            .WithMessage($"Informe no máximo {LimitesAviso.MaximoCamposAfetados} campos afetados.");

        RuleFor(x => x.CampoVersaoIds)
            .MustAsync(async (campoVersaoIds, ct) =>
            {
                if (campoVersaoIds is not { Count: > 0 })
                    return true;

                var avisoId = contextoRequisicao.ObterIdDaRota();
                if (avisoId is null)
                    return true; // sem rota — deixa o service/404 tratar

                var aviso = await repositorio.ObterPorIdAsync(avisoId.Value, rastreada: false, ct);
                return aviso is null || aviso.ProvedorId is null; // só pode ter campos se for aviso de VERSÃO
            })
            .WithMessage("Aviso de escopo provedor não pode ter campos afetados.");
    }
}
