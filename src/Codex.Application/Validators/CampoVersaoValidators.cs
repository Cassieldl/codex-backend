using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using FluentValidation;

namespace Codex.Application.Validators;

/// <summary>
/// §9: <c>EstadoPreenchimento</c> fora do enum → erro (teste obrigatório). §5.2: <c>CampoLayoutId</c>
/// precisa existir e estar <c>Ativo</c> na versão de layout usada pela <c>VersaoProvedor</c> alvo
/// (identificada pelo <c>{id}</c> da rota <c>POST /interno/versoes/{id}/campos</c>) — regra
/// cross-entity via <see cref="IContextoRequisicaoHttp"/> + repositório (nunca acesso direto a
/// HTTP, ver <see cref="IContextoRequisicaoHttp"/>).
/// </summary>
public sealed class CriarCampoVersaoDtoValidator : AbstractValidator<CriarCampoVersaoDto>
{
    public CriarCampoVersaoDtoValidator(
        ICampoVersaoRepositorioInterno repositorio, IContextoRequisicaoHttp contextoRequisicao)
    {
        RuleFor(x => x.CampoLayoutId).GreaterThan(0).WithMessage("O campo canônico (campoLayoutId) é obrigatório — U4.");

        RuleFor(x => x.Tipo).NotEmpty().WithMessage("O tipo do campo é obrigatório.");

        RuleFor(x => x.EstadoPreenchimento).IsInEnum().WithMessage("Estado de preenchimento inválido.");

        RuleFor(x => x.Ordem).GreaterThanOrEqualTo(0).WithMessage("A ordem não pode ser negativa.");

        RuleFor(x => x.CampoLayoutId)
            .MustAsync(async (campoLayoutId, ct) =>
            {
                var versaoProvedorId = contextoRequisicao.ObterIdDaRota();
                if (versaoProvedorId is null)
                    return true; // sem rota (ex.: uso fora de HTTP) — deixa o service/404 tratar

                return await repositorio.CampoLayoutEstaAtivoNaVersaoDoProvedorAsync(campoLayoutId, versaoProvedorId.Value, ct);
            })
            .When(x => x.CampoLayoutId > 0)
            .WithMessage("O campo canônico informado não existe ou não está ativo na versão do layout desta versão de provedor.");
    }
}

public sealed class AtualizarCampoVersaoDtoValidator : AbstractValidator<AtualizarCampoVersaoDto>
{
    public AtualizarCampoVersaoDtoValidator()
    {
        RuleFor(x => x.Tipo).NotEmpty().WithMessage("O tipo do campo é obrigatório.");

        RuleFor(x => x.EstadoPreenchimento).IsInEnum().WithMessage("Estado de preenchimento inválido.");

        RuleFor(x => x.Ordem).GreaterThanOrEqualTo(0).WithMessage("A ordem não pode ser negativa.");
    }
}
