using System.Text.Json;
using System.Text.Json.Serialization;

namespace CromoBound.Models.Effects;

/// <summary>"You" | "Opponent" | "EachPlayer" | "EachOpponent" | { "var": name } | { "controllerOf": ObjectRef }.</summary>
[JsonConverter(typeof(PlayerRefConverter))]
public sealed record PlayerRef
{
    public PlayerKind? Kind { get; init; }
    public string? Var { get; init; }
    public ObjectRef? ControllerOf { get; init; }

    public static PlayerRef You { get; } = new() { Kind = PlayerKind.You };
}

public sealed class PlayerRefConverter : JsonConverter<PlayerRef>
{
    public override PlayerRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString()!;
            if (!Enum.GetNames<PlayerKind>().Contains(text, StringComparer.Ordinal))
                throw new JsonException($"Unknown player reference '{text}'.");
            return new PlayerRef { Kind = Enum.Parse<PlayerKind>(text) };
        }
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A player reference must be a string or an object.");
        var form = JsonSerializer.Deserialize<PlayerRefForm>(ref reader, options)
            ?? throw new JsonException("A player reference cannot be null.");
        if ((form.Var is null) == (form.ControllerOf is null))
            throw new JsonException("A player reference object needs exactly one of 'var' or 'controllerOf'.");
        return new PlayerRef { Var = form.Var, ControllerOf = form.ControllerOf };
    }

    public override void Write(Utf8JsonWriter writer, PlayerRef value, JsonSerializerOptions options)
    {
        if (value.Kind is { } kind)
        {
            writer.WriteStringValue(kind.ToString());
            return;
        }
        JsonSerializer.Serialize(writer, new PlayerRefForm { Var = value.Var, ControllerOf = value.ControllerOf }, options);
    }

    private sealed record PlayerRefForm
    {
        public string? Var { get; init; }
        public ObjectRef? ControllerOf { get; init; }
    }
}
