namespace Codex.Domain.Enums;

/// <summary>Espelha <c>ck_codex_lacunasbusca_estado</c>.</summary>
public enum EstadoLacunaBusca
{
    /// <summary>Busca sem resultado ainda não tratada — é o que compõe a fila editorial.</summary>
    Aberta,

    /// <summary>O conteúdo que faltava foi documentado.</summary>
    Resolvida,

    /// <summary>Não vira conteúdo (termo fora do escopo do Codex, erro de digitação, duplicado).</summary>
    Descartada,
}
