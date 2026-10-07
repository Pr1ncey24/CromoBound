using System.Text.Json;
using System.Text.Json.Serialization;

namespace CromoBound.Models.Effects;

/// <summary>A single-key object, e.g. { "legion": true } or { "all": [ ... ] }.</summary>
public sealed record Condition : IJsonOnDeserialized
{
    public IReadOnlyList<Condition>? All { get; init; }
    public IReadOnlyList<Condition>? Any { get; init; }
    public Condition? Not { get; init; }
    public ObjectRef? Exists { get; init; }
    public Comparison? Compare { get; init; }
    public string? Paid { get; init; }
    public string? Did { get; init; }
    public bool? Legion { get; init; }
    public int? Level { get; init; }
    public bool? Empowered { get; init; }
    public Phase? Phase { get; init; }
    public PlayerRef? TurnOf { get; init; }
    public ObjectRef? PlayedThisTurn { get; init; }

    void IJsonOnDeserialized.OnDeserialized()
    {
        object?[] keys = [All, Any, Not, Exists, Compare, Paid, Did, Legion, Level, Empowered, Phase, TurnOf, PlayedThisTurn];
        if (keys.Count(k => k is not null) != 1)
            throw new JsonException("A condition needs exactly one key, e.g. { \"legion\": true }.");
    }
}
