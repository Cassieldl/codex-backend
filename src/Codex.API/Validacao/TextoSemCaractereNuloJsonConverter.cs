using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codex.API.Validacao;

/// <summary>
/// Recusa texto com o byte NUL no corpo JSON. A <see cref="JsonException"/> vira falha de model
/// binding, que a API já responde como 400 <c>MALFORMED_REQUEST</c>, em vez de chegar ao Postgres,
/// que não aceita o caractere e responderia 500.
/// </summary>
public sealed class TextoSemCaractereNuloJsonConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var texto = reader.GetString();

        if (texto is not null && texto.Contains('\0', StringComparison.Ordinal))
            throw new JsonException("Texto com caractere nulo.");

        return texto;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
