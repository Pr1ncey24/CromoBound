namespace CromoBound.Models.Effects;

/// <summary>A mechanical keyword with its parameter: value (Shield 2), cost (Empower, Equip, Repeat, Flow) or steps (Deathknell).</summary>
public sealed record KeywordEntry
{
    public required MechanicalKeyword Keyword { get; init; }
    public int? Value { get; init; }
    public Cost? Cost { get; init; }
    public IReadOnlyList<Step> Steps { get; init; } = [];
}
