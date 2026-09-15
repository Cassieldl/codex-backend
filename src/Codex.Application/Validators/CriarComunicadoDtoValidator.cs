using Codex.Application.DTOs.Interno;
using Codex.Domain.Enums;
using FluentValidation;

namespace Codex.Application.Validators;

/// <summary>
/// Criação de comunicado (I32). Replica no backend as regras que o banco também impõe — tamanho e
/// consistência de abrangência × ids — para que a violação chegue ao editor como <c>422</c> com
/// nome de campo, e não como <c>500</c> de <c>CHECK</c> estourado.
///
/// <para>
/// O limite de <c>corpo</c> é <b>2000</b> caracteres (decisoes.md B-6), o mesmo número do schema
/// Zod do formulário: o que importa não é o número, é os dois lados concordarem — limite que só
/// existe de um lado vira truncamento silencioso ou 500 do outro. Ao mudar aqui, mudar também em
/// <see cref="AtualizarComunicadoDtoValidator"/> e no frontend.
/// </para>
///
/// <para>
/// "Agora" vem do <see cref="TimeProvider"/> injetado, nunca de <c>DateTimeOffset.UtcNow</c>: é o
/// que permite ao teste fixar o instante em vez de depender do relógio da máquina.
/// </para>
/// </summary>
public sealed class CriarComunicadoDtoValidator : AbstractValidator<CriarComunicadoDto>
{
    /// <summary>Limite de <c>corpo</c> acordado entre backend e frontend (decisoes.md B-6).</summary>
    public const int LimiteCorpo = 2000;

    public CriarComunicadoDtoValidator(TimeProvider relogio)
    {
        RuleFor(x => x.Titulo)
            .NotEmpty().MaximumLength(200)
            .WithMessage("O título do comunicado é obrigatório e tem no máximo 200 caracteres.");

        RuleFor(x => x.Corpo)
            .NotEmpty().MaximumLength(LimiteCorpo)
            .WithMessage($"O corpo do comunicado é obrigatório e tem no máximo {LimiteCorpo} caracteres.");

        RuleFor(x => x.Severidade).IsInEnum().WithMessage("Severidade inválida.");
        RuleFor(x => x.Abrangencia).IsInEnum().WithMessage("Abrangência inválida.");

        RuleFor(x => x.ExpiraEm)
            .Must(expiraEm => expiraEm > relogio.GetUtcNow())
            .WithMessage("A expiração do comunicado precisa estar no futuro.");

        RuleFor(x => x.ProvedorId)
            .Must((dto, provedorId) => ProvedorIdConsistente(dto.Abrangencia, provedorId))
            .WithMessage("O provedor é obrigatório para a abrangência informada e precisa ficar vazio na abrangência global.");

        RuleFor(x => x.VersaoProvedorId)
            .Must((dto, versaoProvedorId) => VersaoProvedorIdConsistente(dto.Abrangencia, versaoProvedorId))
            .WithMessage("A versão do provedor só é preenchida na abrangência 'ProvedorVersao', onde é obrigatória.");
    }

    internal static bool ProvedorIdConsistente(AbrangenciaComunicado abrangencia, int? provedorId) => abrangencia switch
    {
        AbrangenciaComunicado.Global => provedorId is null,
        AbrangenciaComunicado.Provedor or AbrangenciaComunicado.ProvedorVersao => provedorId >= 1,
        _ => false,
    };

    internal static bool VersaoProvedorIdConsistente(AbrangenciaComunicado abrangencia, int? versaoProvedorId) => abrangencia switch
    {
        AbrangenciaComunicado.Global or AbrangenciaComunicado.Provedor => versaoProvedorId is null,
        AbrangenciaComunicado.ProvedorVersao => versaoProvedorId >= 1,
        _ => false,
    };
}
