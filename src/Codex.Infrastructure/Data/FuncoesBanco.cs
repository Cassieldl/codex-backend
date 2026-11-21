namespace Codex.Infrastructure.Data;

/// <summary>Funções SQL do banco chamáveis dentro de consulta LINQ do EF.</summary>
public static class FuncoesBanco
{
    /// <summary>Traduz para <c>codex.f_unaccent</c>, que tira o acento do texto.</summary>
    public static string SemAcento(string texto) =>
        throw new NotSupportedException("SemAcento só pode ser usada dentro de consulta do EF.");
}
