using System.Collections;
using System.Reflection;
using Codex.API.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Codex.Tests.Unit.Dtos;

/// <summary>
/// Invariante de contrato: NENHUM tipo devolvido por action de controller expõe propriedade de
/// enum crua — todo enum sai da API como <c>string</c> mapeada explicitamente no service/
/// repositório (<c>enum.ToString()</c>).
///
/// <para>
/// Por que isto é um teste e não um comentário: registrar <c>JsonStringEnumConverter</c> ativa a
/// serialização de enum como nome nos DOIS sentidos. Hoje isso não muda resposta nenhuma
/// justamente porque nenhuma resposta carrega enum — a grafia que o frontend consome continua
/// vindo do <c>ToString()</c>. No dia em que alguém tipar um campo de resposta como enum, a grafia
/// passaria a depender da configuração do converter (uma naming policy nova mudaria o wire em
/// silêncio, e o frontend tipa esses valores como união de literais). Este teste transforma "hoje
/// não muda nada" em garantia estrutural em vez de observação de um momento.
/// </para>
/// </summary>
public sealed class RespostaNuncaExpoeEnumCruTests
{
    [Fact]
    public void NenhumTipoDeRespostaDeControllerExpoeEnumCru()
    {
        var tiposDeResposta = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(m => DesembrulharRetorno(m.ReturnType))
            .Where(t => t is not null)
            .Select(t => t!)
            .Distinct()
            .ToList();

        tiposDeResposta.Should().NotBeEmpty(
            because: "se a varredura não achar nenhum tipo de resposta, o teste seria vacuamente verde");

        var infracoes = new List<string>();

        foreach (var tipo in tiposDeResposta)
            ColetarEnumsExpostos(tipo, caminho: tipo.Name, visitados: [], infracoes);

        infracoes.Should().BeEmpty(
            because: "enum em resposta precisa ser mapeado para string explicitamente (enum.ToString()) " +
                     "no service/repositório — a grafia do wire não pode depender da configuração do " +
                     "JsonStringEnumConverter, que o frontend não enxerga");
    }

    /// <summary>
    /// Descasca os invólucros de retorno de action (<c>Task&lt;&gt;</c>,
    /// <c>ActionResult&lt;&gt;</c>, <c>ApiResponse&lt;&gt;</c>, <c>PaginatedResponse&lt;&gt;</c>,
    /// coleções) até chegar ao DTO que de fato vai para o corpo da resposta.
    /// </summary>
    private static Type? DesembrulharRetorno(Type tipo)
    {
        while (true)
        {
            if (tipo == typeof(Task) || tipo == typeof(void) || tipo == typeof(IActionResult))
                return null;

            if (!tipo.IsGenericType)
                return EhDeInteresse(tipo) ? tipo : null;

            var definicao = tipo.GetGenericTypeDefinition();

            if (definicao == typeof(Task<>)
                || definicao == typeof(ActionResult<>)
                || definicao == typeof(ApiResponse<>)
                || definicao == typeof(PaginatedResponse<>))
            {
                tipo = tipo.GetGenericArguments()[0];
                continue;
            }

            if (typeof(IEnumerable).IsAssignableFrom(tipo))
            {
                tipo = tipo.GetGenericArguments()[0];
                continue;
            }

            return EhDeInteresse(tipo) ? tipo : null;
        }
    }

    /// <summary>Só DTOs nossos interessam — tipos do framework e primitivos não são contrato do projeto.</summary>
    private static bool EhDeInteresse(Type tipo) =>
        tipo.Namespace?.StartsWith("Codex.", StringComparison.Ordinal) == true;

    private static void ColetarEnumsExpostos(Type tipo, string caminho, HashSet<Type> visitados, List<string> infracoes)
    {
        if (!visitados.Add(tipo))
            return;

        foreach (var propriedade in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var tipoDaPropriedade = Nullable.GetUnderlyingType(propriedade.PropertyType) ?? propriedade.PropertyType;
            var caminhoDaPropriedade = $"{caminho}.{propriedade.Name}";

            if (tipoDaPropriedade.IsEnum)
            {
                infracoes.Add($"{caminhoDaPropriedade} ({tipoDaPropriedade.Name})");
                continue;
            }

            // Coleção: o que importa é o tipo do item.
            if (tipoDaPropriedade != typeof(string) && typeof(IEnumerable).IsAssignableFrom(tipoDaPropriedade))
            {
                var tipoDoItem = tipoDaPropriedade.IsGenericType
                    ? tipoDaPropriedade.GetGenericArguments().FirstOrDefault()
                    : tipoDaPropriedade.GetElementType();

                if (tipoDoItem is null)
                    continue;

                var itemDesembrulhado = Nullable.GetUnderlyingType(tipoDoItem) ?? tipoDoItem;

                if (itemDesembrulhado.IsEnum)
                    infracoes.Add($"{caminhoDaPropriedade}[] ({itemDesembrulhado.Name})");
                else if (EhDeInteresse(itemDesembrulhado))
                    ColetarEnumsExpostos(itemDesembrulhado, $"{caminhoDaPropriedade}[]", visitados, infracoes);

                continue;
            }

            if (EhDeInteresse(tipoDaPropriedade))
                ColetarEnumsExpostos(tipoDaPropriedade, caminhoDaPropriedade, visitados, infracoes);
        }
    }
}
