namespace Codex.API.Common;

/// <summary>
/// Corpo das sondas de <see cref="Codex.API.Controllers.Publico.SaudeController"/>. Um campo só,
/// e de propósito: quem lê é o orquestrador, que decide pelo <b>status HTTP</b> — o corpo existe
/// para diagnóstico humano rápido, não como contrato de dados.
///
/// <para>
/// Sai <b>cru</b>, sem <see cref="ApiResponse{T}"/>: o envelope é a convenção dos recursos que o
/// frontend consome, e nenhuma tela lê isto. Embrulhar produziria
/// <c>{"data":{"status":"saudavel"},"message":null}</c> num payload que o probe lê a cada poucos
/// segundos, sem ganho nenhum. A exceção está documentada aqui para não virar uma terceira
/// convenção por acidente.
/// </para>
/// </summary>
public sealed class SaudeDto
{
    public const string Saudavel = "saudavel";

    public const string Indisponivel = "indisponivel";

    public required string Status { get; init; }
}
