namespace CromoBound.Models.Effects;

public sealed record AddStep : Step
{
    public Value? Energy { get; init; }
    public IReadOnlyList<PowerSymbol> Power { get; init; } = [];
}

public sealed record ChannelStep : Step
{
    public Value Count { get; init; } = 1;
    public bool? Exhausted { get; init; }
}

public sealed record ScoreStep : Step
{
    public Value Amount { get; init; } = 1;
}

public sealed record GainXpStep : Step
{
    public required Value Amount { get; init; }
}

public sealed record SpendXpStep : Step
{
    public required Value Amount { get; init; }
}

public sealed record PayStep : Step
{
    public required Cost Cost { get; init; }
}

public sealed record ExtraTurnStep : Step;
