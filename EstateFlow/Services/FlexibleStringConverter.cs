
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EstateFlow.Services;

/// <summary>
/// Converts JSON string, number and boolean tokens
/// to C# strings during deserialization.
/// Used for structured AI responses.
/// </summary>
public sealed class FlexibleStringConverter : JsonConverter<string>
{
    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),

            JsonTokenType.Number =>
                JsonDocument.ParseValue(ref reader)
                    .RootElement.GetRawText(),

            JsonTokenType.True => "true",

            JsonTokenType.False => "false",

            JsonTokenType.Null => null,

            _ => throw new JsonException(
                $"Unsupported JSON token: {reader.TokenType}")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        string value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
