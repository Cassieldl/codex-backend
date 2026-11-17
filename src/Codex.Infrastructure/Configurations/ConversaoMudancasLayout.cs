using System.Text.Json;
using System.Text.Json.Serialization;
using Codex.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Codex.Infrastructure.Configurations;

/// <summary>
/// Lista de <see cref="MudancaLayout"/> gravada como array <c>jsonb</c>, com chaves e tipo em
/// camelCase: <c>[{"tipo":"renomeado","antes":"...","depois":"...","descricao":"..."}]</c>.
/// </summary>
internal static class ConversaoMudancasLayout
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static readonly ValueConverter<IReadOnlyList<MudancaLayout>, string> Conversor = new(
        mudancas => Serializar(mudancas),
        json => Desserializar(json));

    /// <summary>Compara pelo conteúdo: trocar a lista por outra igual não marca a entidade como alterada.</summary>
    public static readonly ValueComparer<IReadOnlyList<MudancaLayout>> Comparador = new(
        (primeira, segunda) => primeira!.SequenceEqual(segunda!),
        mudancas => mudancas.Aggregate(0, (hash, mudanca) => HashCode.Combine(hash, mudanca.GetHashCode())),
        mudancas => mudancas.ToArray());

    private static string Serializar(IReadOnlyList<MudancaLayout> mudancas) => JsonSerializer.Serialize(mudancas, Opcoes);

    private static IReadOnlyList<MudancaLayout> Desserializar(string json) =>
        JsonSerializer.Deserialize<MudancaLayout[]>(json, Opcoes) ?? [];
}
