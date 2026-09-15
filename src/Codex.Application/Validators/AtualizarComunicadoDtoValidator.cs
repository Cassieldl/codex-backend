using Codex.Application.DTOs.Interno;
using FluentValidation;

namespace Codex.Application.Validators;

/// <summary>
/// Atualização de comunicado (I33) — mesmas regras da criação, inclusive <c>expiraEm</c> no
/// futuro. Estender a expiração de um comunicado já encerrado é o gesto legítimo de reabertura que
/// decisoes.md B-4 indica como próximo passo do <c>422 COMUNICADO_EXPIRADO</c>; encurtar para o
/// passado, não — quem quer tirar do ar despublica (I35) ou remove (I34).
///
/// <para>
/// O limite de <c>corpo</c> (2000, decisoes.md B-6) e as duas regras de consistência de escopo são
/// reusados de <see cref="CriarComunicadoDtoValidator"/> — uma fonte só, para os dois caminhos de
/// escrita não divergirem.
/// </para>
/// </summary>
public sealed class AtualizarComunicadoDtoValidator : AbstractValidator<AtualizarComunicadoDto>
{
    public AtualizarComunicadoDtoValidator(TimeProvider relogio)
    {
        RuleFor(x => x.Titulo)
            .NotEmpty().MaximumLength(200)
            .WithMessage("O título do comunicado é obrigatório e tem no máximo 200 caracteres.");

        RuleFor(x => x.Corpo)
            .NotEmpty().MaximumLength(CriarComunicadoDtoValidator.LimiteCorpo)
            .WithMessage($"O corpo do comunicado é obrigatório e tem no máximo {CriarComunicadoDtoValidator.LimiteCorpo} caracteres.");

        RuleFor(x => x.Severidade).IsInEnum().WithMessage("Severidade inválida.");
        RuleFor(x => x.Abrangencia).IsInEnum().WithMessage("Abrangência inválida.");

        RuleFor(x => x.ExpiraEm)
            .Must(expiraEm => expiraEm > relogio.GetUtcNow())
            .WithMessage("A expiração do comunicado precisa estar no futuro.");

        RuleFor(x => x.ProvedorId)
            .Must((dto, provedorId) => CriarComunicadoDtoValidator.ProvedorIdConsistente(dto.Abrangencia, provedorId))
            .WithMessage("O provedor é obrigatório para a abrangência informada e precisa ficar vazio na abrangência global.");

        RuleFor(x => x.VersaoProvedorId)
            .Must((dto, versaoProvedorId) => CriarComunicadoDtoValidator.VersaoProvedorIdConsistente(dto.Abrangencia, versaoProvedorId))
            .WithMessage("A versão do provedor só é preenchida na abrangência 'ProvedorVersao', onde é obrigatória.");
    }
}
