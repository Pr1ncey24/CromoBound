using System.Text.Json;
using System.Text.Json.Serialization;

namespace CromoBound.Models.Effects;

/// <summary>JSON form: [left, "op", right] with op one of eq, lte, gte, lt, gt.</summary>
[JsonConverter(typeof(ComparisonConverter))]
public sealed record Comparison(Value Left, CompareOp Op, Value Right);

public sealed class ComparisonConverter : JsonConverter<Comparison>
{
    private static readonly Dictionary<string, CompareOp> Ops = new(StringComparer.Ordinal)
    {
        ["eq"] = CompareOp.Eq, ["lte"] = CompareOp.Lte, ["gte"] = CompareOp.Gte, ["lt"] = CompareOp.Lt, ["gt"] = CompareOp.Gt,
    };

    public override Comparison Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("A comparison must be an array: [value, op, value].");
        reader.Read();
        var left = JsonSerializer.Deserialize<Value>(ref reader, options) ?? throw new JsonException("Comparison left side is null.");
        reader.Read();
        if (reader.TokenType != JsonTokenType.String || !Ops.TryGetValue(reader.GetString()!, out var op))
            throw new JsonException("Comparison operator must be one of: eq, lte, gte, lt, gt.");
        reader.Read();
        var right = JsonSerializer.Deserialize<Value>(ref reader, options) ?? throw new JsonException("Comparison right side is null.");
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("A comparison has exactly three elements.");
        return new Comparison(left, op, right);
    }

    public override void Write(Utf8JsonWriter writer, Comparison value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        JsonSerializer.Serialize(writer, value.Left, options);
        writer.WriteStringValue(Ops.First(pair => pair.Value == value.Op).Key);
        JsonSerializer.Serialize(writer, value.Right, options);
        writer.WriteEndArray();
    }
}
