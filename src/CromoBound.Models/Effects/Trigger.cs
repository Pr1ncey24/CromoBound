namespace CromoBound.Models.Effects;

public sealed record Trigger
{
    public required TriggerEvent Event { get; init; }
    public ObjectRef? Subject { get; init; }
    public PlayerRef? By { get; init; }
    public ObjectRef? Where { get; init; }
    public Filter? Filter { get; init; }
    public Phase? Phase { get; init; }
}

public sealed record Limit
{
    public LimitPeriod Per { get; init; } = LimitPeriod.Turn;
    public int Times { get; init; } = 1;
}
