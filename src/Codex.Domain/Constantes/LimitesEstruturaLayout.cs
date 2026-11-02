namespace Codex.Domain.Constantes;

/// <summary>
/// Tetos da estrutura de uma versão de layout (<c>versoeslayout.estrutura</c>). O XSD 9 do InvoiCy
/// tem 627 elementos e profundidade 9; os tetos deixam folga sem aceitar árvore arbitrária.
/// </summary>
public static class LimitesEstruturaLayout
{
    public const int Nos = 5000;

    public const int Profundidade = 32;

    public const int Nome = 120;

    public const int Caminho = 600;

    public const int ValorModelo = 200;
}
