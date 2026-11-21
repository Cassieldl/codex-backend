namespace Codex.Infrastructure.Data;

/// <summary>
/// Collation para ORDER BY de texto. A padrão do banco (en_US.utf8 no Alpine/musl) compara por
/// byte: acento vai para o fim e minúscula vem depois de toda maiúscula. A ICU do português ordena
/// como o usuário lê.
/// </summary>
internal static class OrdenacaoTexto
{
    public const string CollationPortugues = "pt-BR-x-icu";
}
