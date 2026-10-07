namespace CromoBound.Models.Effects;

/// <summary>A cost. Non-standard costs (recycle, discard, kill…) are steps in <see cref="Actions"/>, run in cost mode.</summary>
public sealed record Cost
{
    public int? Energy { get; init; }
    public IReadOnlyList<PowerSymbol> Power { get; init; } = [];
    public bool? ExhaustSelf { get; init; }
    public IReadOnlyList<Step> Actions { get; init; } = [];
}
