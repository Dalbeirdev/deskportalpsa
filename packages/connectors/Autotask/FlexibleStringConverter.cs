using System.Text.Json;
using System.Text.Json.Serialization;

namespace Desk.Connectors.Autotask;

/// <summary>
/// Reads a JSON value that may be a string OR a number into a string. Autotask returns picklist and
/// reference fields (status, priority, queueID, ticketCategory, assignedResourceID) as NUMBERS,
/// while some entities/tests emit them as strings. Accepting both keeps deserialization working
/// against the live API without forcing every caller to care about the wire type.
/// </summary>
internal sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => null,
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue(); else writer.WriteStringValue(value);
    }
}
