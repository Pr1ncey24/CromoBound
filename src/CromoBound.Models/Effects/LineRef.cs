using System.Text.Json;
using System.Text.Json.Serialization;

namespace CromoBound.Models.Effects;

/// <summary>1-based card-text line(s) an ability implements. JSON: 2 or [1, 2].</summary>
[JsonConverter(typeof(LineRefConverter))]
public sealed record LineRef(IReadOnlyList<int> Lines)
{
    public static implicit operator LineRef(int line) => new([line]);
}

public sealed class LineRefConverter : JsonConverter<LineRef>
{
    public override LineRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return new LineRef([ReadLine(ref reader)]);
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("'line' must be a number or an array of numbers.");
        var lines = new List<int>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.Number) throw new JsonException("'line' array must contain only numbers.");
            lines.Add(ReadLine(ref reader));
        }
        if (lines.Count == 0) throw new JsonException("'line' array cannot be empty.");
        return new LineRef(lines);
    }

    public override void Write(Utf8JsonWriter writer, LineRef value, JsonSerializerOptions options)
    {
        if (value.Lines.Count == 1)
        {
            writer.WriteNumberValue(value.Lines[0]);
            return;
        }
        writer.WriteStartArray();
        foreach (var line in value.Lines) writer.WriteNumberValue(line);
        writer.WriteEndArray();
    }

    private static int ReadLine(ref Utf8JsonReader reader) =>
        reader.TryGetInt32(out var line) && line >= 1 ? line : throw new JsonException("Line numbers are integers starting at 1.");
}
