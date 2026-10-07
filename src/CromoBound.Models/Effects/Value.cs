using System.Text.Json;
using System.Text.Json.Serialization;

namespace CromoBound.Models.Effects;

/// <summary>An integer literal or a computed number: count, prop+of, var, sum, mul, min, max.</summary>
[JsonConverter(typeof(ValueConverter))]
public sealed record Value
{
    public int? Literal { get; init; }
    public ObjectRef? Count { get; init; }
    public ValueProperty? Prop { get; init; }
    public ObjectRef? Of { get; init; }
    public string? Var { get; init; }
    public IReadOnlyList<Value>? Sum { get; init; }
    public IReadOnlyList<Value>? Mul { get; init; }
    public IReadOnlyList<Value>? Min { get; init; }
    public IReadOnlyList<Value>? Max { get; init; }

    public static implicit operator Value(int literal) => new() { Literal = literal };
}

public sealed class ValueConverter : JsonConverter<Value>
{
    public override Value Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.TryGetInt32(out var literal)
                ? new Value { Literal = literal }
                : throw new JsonException("A numeric value must be an integer.");
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A value must be an integer or an object.");

        var form = JsonSerializer.Deserialize<ValueForm>(ref reader, options)
            ?? throw new JsonException("A value object cannot be null.");
        var keys = new object?[] { form.Count, form.Prop, form.Var, form.Sum, form.Mul, form.Min, form.Max }.Count(k => k is not null);
        if (keys != 1)
            throw new JsonException("A value object needs exactly one of: count, prop, var, sum, mul, min, max.");
        if ((form.Prop is null) != (form.Of is null))
            throw new JsonException("'prop' and 'of' must be used together.");
        return new Value
        {
            Count = form.Count, Prop = form.Prop, Of = form.Of, Var = form.Var,
            Sum = form.Sum, Mul = form.Mul, Min = form.Min, Max = form.Max,
        };
    }

    public override void Write(Utf8JsonWriter writer, Value value, JsonSerializerOptions options)
    {
        if (value.Literal is { } literal)
        {
            writer.WriteNumberValue(literal);
            return;
        }
        JsonSerializer.Serialize(writer, new ValueForm
        {
            Count = value.Count, Prop = value.Prop, Of = value.Of, Var = value.Var,
            Sum = value.Sum, Mul = value.Mul, Min = value.Min, Max = value.Max,
        }, options);
    }

    private sealed record ValueForm
    {
        public ObjectRef? Count { get; init; }
        public ValueProperty? Prop { get; init; }
        public ObjectRef? Of { get; init; }
        public string? Var { get; init; }
        public IReadOnlyList<Value>? Sum { get; init; }
        public IReadOnlyList<Value>? Mul { get; init; }
        public IReadOnlyList<Value>? Min { get; init; }
        public IReadOnlyList<Value>? Max { get; init; }
    }
}
