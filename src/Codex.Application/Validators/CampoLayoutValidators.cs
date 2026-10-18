using Codex.Application.DTOs.Interno;
using Codex.Application.Interfaces;
using Codex.Domain.Constantes;
using Codex.Domain.Enums;
using FluentValidation;
using SlugVo = Codex.Domain.ValueObjects.Slug;

namespace Codex.Application.Validators;

public sealed class CriarCampoLayoutDtoValidator : AbstractValidator<CriarCampoLayoutDto>
{
    public CriarCampoLayoutDtoValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("O nome do campo é obrigatório.")
            .MaximumLength(LimitesCampoLayout.Nome)
            .WithMessage($"O nome do campo tem no máximo {LimitesCampoLayout.Nome} caracteres.");
        RuleFor(x => x.CaminhoXml).NotEmpty().WithMessage("O caminho XML do campo é obrigatório.")
            // O slug nasce do caminho XML (CampoLayout.Criar). Caminho só de separadores ("//",
            // "..") normaliza para vazio — Slug.DeTexto lançaria, e o 500 esconderia o 422 real.
            .Must(SlugVo.PodeDerivarDe).WithMessage("O caminho XML do campo precisa conter ao menos uma letra ou número.")
            .MaximumLength(LimitesCampoLayout.CaminhoXml)
            .WithMessage($"O caminho XML do campo tem no máximo {LimitesCampoLayout.CaminhoXml} caracteres.");
        RuleFor(x => x.Significado).NotEmpty().WithMessage("O significado do campo é obrigatório.");
        RuleFor(x => x.Grupo).MaximumLength(LimitesCampoLayout.Grupo)
            .WithMessage($"O grupo do campo tem no máximo {LimitesCampoLayout.Grupo} caracteres.");
        RuleFor(x => x.Ordem).GreaterThanOrEqualTo(0).WithMessage("A ordem não pode ser negativa.");
    }
}

/// <summary>
/// §5.1b/§9 (risco #2 do DBA): <c>versaoLayoutDescontinuadoId</c> obrigatório quando
/// <c>estado="Descontinuado"</c>, proibido caso contrário — replica em 422 o
/// <c>CHECK ck_codex_camposlayout_descontinuado_consistente</c> do banco. A comparação
/// "descontinuado.ordem &gt; origem.ordem" também é checada aqui (carregando o campo pelo
/// <c>{id}</c> da rota via <see cref="IContextoRequisicaoHttp"/>, mesma técnica de
/// <see cref="CriarCampoVersaoDtoValidator"/>) — o domínio
/// (<see cref="Codex.Domain.Entities.CampoLayout.Descontinuar"/>, já coberto por
/// <c>Dominio/CampoLayoutTests.cs</c>) replica a mesma guarda como defesa final.
/// </summary>
public sealed class AtualizarCampoLayoutDtoValidator : AbstractValidator<AtualizarCampoLayoutDto>
{
    public AtualizarCampoLayoutDtoValidator(
        ICampoLayoutRepositorioInterno campoLayoutRepositorio,
        IVersaoLayoutRepositorioInterno versaoLayoutRepositorio,
        IContextoRequisicaoHttp contextoRequisicao)
    {
        RuleFor(x => x.Nome).NotEmpty().When(x => x.Nome is not null)
            .WithMessage("O nome do campo não pode ser vazio.");

        RuleFor(x => x.Nome).MaximumLength(LimitesCampoLayout.Nome)
            .WithMessage($"O nome do campo tem no máximo {LimitesCampoLayout.Nome} caracteres.");

        RuleFor(x => x.Grupo).MaximumLength(LimitesCampoLayout.Grupo)
            .WithMessage($"O grupo do campo tem no máximo {LimitesCampoLayout.Grupo} caracteres.");

        RuleFor(x => x.Significado).NotEmpty().When(x => x.Significado is not null)
            .WithMessage("O significado do campo não pode ser vazio.");

        RuleFor(x => x.Ordem).GreaterThanOrEqualTo(0).When(x => x.Ordem.HasValue)
            .WithMessage("A ordem não pode ser negativa.");

        RuleFor(x => x.Estado).IsInEnum().When(x => x.Estado.HasValue)
            .WithMessage("Estado de campo canônico inválido.");

        RuleFor(x => x.VersaoLayoutDescontinuadoId)
            .NotNull()
            .When(x => x.Estado == EstadoCampoLayout.Descontinuado)
            .WithMessage("Informe a versão do layout em que o campo foi descontinuado.");

        RuleFor(x => x.VersaoLayoutDescontinuadoId)
            .Null()
            .When(x => x.Estado is null or EstadoCampoLayout.Ativo)
            .WithMessage("A versão de descontinuação só se aplica quando o estado é 'Descontinuado'.");

        RuleFor(x => x.VersaoLayoutDescontinuadoId)
            .MustAsync(async (dto, versaoLayoutDescontinuadoId, ct) =>
            {
                if (dto.Estado != EstadoCampoLayout.Descontinuado || versaoLayoutDescontinuadoId is null)
                    return true; // regras "obrigatório/proibido se" acima já cobrem o inverso

                var campoLayoutId = contextoRequisicao.ObterIdDaRota();
                if (campoLayoutId is null)
                    return true;

                var campo = await campoLayoutRepositorio.ObterPorIdAsync(campoLayoutId.Value, rastreada: false, comOrigemCarregada: true, ct);
                if (campo is null)
                    return true; // 404 é responsabilidade do service

                var versaoDescontinuada = await versaoLayoutRepositorio.ObterPorIdAsync(versaoLayoutDescontinuadoId.Value, rastreada: false, ct);
                return versaoDescontinuada is not null && versaoDescontinuada.Ordem > campo.VersaoLayoutOrigem.Ordem;
            })
            .WithMessage("A versão de descontinuação precisa ser posterior à versão de origem do campo.");
    }
}
