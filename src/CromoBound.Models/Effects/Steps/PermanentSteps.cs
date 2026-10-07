namespace CromoBound.Models.Effects;

public sealed record DealStep : TargetStep
{
    public required Value Amount { get; init; }
    public bool? Split { get; init; }
    public Value? Bonus { get; init; }
    public ObjectRef? Source { get; init; }
}

public sealed record HealStep : TargetStep;

public sealed record KillStep : TargetStep;

public sealed record StunStep : TargetStep;

public sealed record BuffStep : TargetStep;

public sealed record SpendBuffStep : TargetStep;

public sealed record ReadyStep : TargetStep;

public sealed record ExhaustStep : TargetStep;

public sealed record MoveStep : TargetStep
{
    public required ObjectRef To { get; init; }
}

public sealed record RecallStep : TargetStep;

public sealed record AttachStep : TargetStep
{
    /// <summary>The host the target gets attached to.</summary>
    public required ObjectRef To { get; init; }
}

public sealed record DetachStep : TargetStep;

public sealed record EmpowerStep : TargetStep;

public sealed record GainControlStep : TargetStep
{
    public Duration? Duration { get; init; }
}

public sealed record ModifyMightStep : TargetStep
{
    public required Value Amount { get; init; }
    public Duration? Duration { get; init; }
}

public sealed record GrantKeywordStep : TargetStep
{
    public required KeywordEntry Keyword { get; init; }
    public Duration? Duration { get; init; }
}

public sealed record GrantTagStep : TargetStep
{
    public required string Tag { get; init; }
    public Duration? Duration { get; init; }
}
