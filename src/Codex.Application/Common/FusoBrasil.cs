namespace Codex.Application.Common;

/// <summary>
/// "Hoje" no calendário do Brasil. A data de verificação é <c>DateOnly</c> digitado por quem opera o
/// Codex, então a conta de idade tem de usar o dia que essa pessoa está vendo — e não o dia em UTC,
/// que já virou às 21h de Brasília.
/// </summary>
/// <remarks>
/// Deslocamento fixo de -03:00: o Brasil não tem horário de verão desde 2019, e a imagem de runtime
/// não garante o banco de fusos (<c>TimeZoneInfo.FindSystemTimeZoneById</c> lançaria sem tzdata).
/// </remarks>
public static class FusoBrasil
{
    public static readonly TimeSpan Deslocamento = TimeSpan.FromHours(-3);

    public static DateOnly Hoje(TimeProvider relogio) =>
        DateOnly.FromDateTime(relogio.GetUtcNow().ToOffset(Deslocamento).DateTime);
}
