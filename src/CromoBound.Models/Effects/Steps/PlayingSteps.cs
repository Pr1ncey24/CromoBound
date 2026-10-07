namespace CromoBound.Models.Effects;

public sealed record PlayStep : Step
{
    public required ObjectRef Card { get; init; }
    public ZoneRef? From { get; init; }
    public PlayCostMode? Cost { get; init; }
    /// <summary>"Play it for [cost]": replaces the base cost.</summary>
    public Cost? For { get; init; }
    public ObjectRef? Location { get; init; }
    public bool? Exhausted { get; init; }
}

public sealed record PlayTokenStep : Step
{
    /// <summary>Token card id, e.g. "token-recruit".</summary>
    public required string Token { get; init; }
    public Value Count { get; init; } = 1;
    public ObjectRef? Location { get; init; }
    public bool? Exhausted { get; init; }
}
