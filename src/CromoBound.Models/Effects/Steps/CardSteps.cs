namespace CromoBound.Models.Effects;

public sealed record DrawStep : Step
{
    public Value Amount { get; init; } = 1;
}

public sealed record DiscardStep : Step
{
    public Value Count { get; init; } = 1;
}

public sealed record BurnStep : Step
{
    public Value Amount { get; init; } = 1;
}

public sealed record RecycleStep : Step
{
    public ZoneRef? From { get; init; }
    public ObjectRef? Target { get; init; }
    public Value? Count { get; init; }
}

public sealed record BanishStep : TargetStep
{
    public ZoneRef? From { get; init; }
}

public sealed record ReturnToHandStep : TargetStep;

public sealed record RevealStep : Step
{
    public ZoneRef? From { get; init; }
    public ObjectRef? Target { get; init; }
    public Value? Count { get; init; }
}

public sealed record LookAtStep : Step
{
    public ZoneRef? From { get; init; }
    public ObjectRef? Target { get; init; }
    public Value? Count { get; init; }
}

public sealed record PredictStep : Step
{
    public Value Amount { get; init; } = 1;
}

public sealed record CounterStep : TargetStep;
