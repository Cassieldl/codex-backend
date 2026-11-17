using System.Text.Json;
using Codex.Application.DTOs.Interno;
using Codex.Application.DTOs.Publico;
using Codex.Domain.ValueObjects;

namespace Codex.Application.Common;

/// <summary>Mudanças da versão do layout entre o domínio e os DTOs, na ordem em que vieram.</summary>
public static class MapeamentoMudancaLayout
{
    public static IReadOnlyList<MudancaLayout> ParaDominio(IEnumerable<MudancaLayoutInternoDto> mudancas) =>
        mudancas.Select(mudanca => new MudancaLayout(mudanca.Tipo, mudanca.Antes, mudanca.Depois, mudanca.Descricao)).ToArray();

    /// <summary>Saída das áreas interna e pública, com o tipo já em texto camelCase.</summary>
    public static IReadOnlyList<MudancaLayoutDto> ParaResposta(IEnumerable<MudancaLayout> mudancas) =>
        mudancas.Select(mudanca => new MudancaLayoutDto
        {
            Tipo = JsonNamingPolicy.CamelCase.ConvertName(mudanca.Tipo.ToString()),
            Antes = mudanca.Antes,
            Depois = mudanca.Depois,
            Descricao = mudanca.Descricao,
        }).ToArray();
}
