using System.Text.Json;
using System.Text.Json.Serialization;
using Codex.Domain.Enums;

namespace Codex.Application.Common;

/// <summary>
/// <see cref="TipoMudancaLayout"/> no corpo JSON como texto camelCase (<c>"grupoNovo"</c>), na
/// entrada e na saída. Vence o conversor global da API, que manteria PascalCase e aceitaria número.
/// </summary>
public sealed class TipoMudancaLayoutJsonConverter()
    : JsonStringEnumConverter<TipoMudancaLayout>(JsonNamingPolicy.CamelCase, allowIntegerValues: false);
