namespace Codex.Domain.Enums;

/// <summary>
/// Espelha ck_codex_itensbusca_tipoitem — valores ASCII minúsculos, exatamente como persistidos
/// (convenção divergente do restante dos enums de domínio, que usam PascalCase; aqui o DBA
/// optou por minúsculo na coluna de <c>codex.itensbusca</c>/<c>codex.redirecionamentos</c>, e o
/// nome do membro segue a mesma regra de identidade caractere a caractere com o CHECK).
/// </summary>
public enum TipoItemIndexado
{
    erro,
    provedor,
    versaoprovedor,
    campolayout,
    aviso,
    artigo,
}
