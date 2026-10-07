using System.Text.Json.Nodes;

namespace CromoBound.Models.Effects;

/// <summary>"You may [pay cost to] do X". Reflexive marks "do this:" blocks that become a new chain item.</summary>
public sealed record OptionalStep : Step
{
    public Cost? Cost { get; init; }
    public bool? Reflexive { get; init; }
    public IReadOnlyList<Step> Steps { get; init; } = [];
}

public sealed record IfStep : Step
{
    public required Condition Condition { get; init; }
    public IReadOnlyList<Step> Then { get; init; } = [];
    public IReadOnlyList<Step> Else { get; init; } = [];
}

public sealed record ForEachStep : Step
{
    public required ObjectRef Selector { get; init; }
    /// <summary>Variable name bound to the current item inside Steps.</summary>
    public string? As { get; init; }
    public IReadOnlyList<Step> Steps { get; init; } = [];
}

public sealed record RepeatStep : Step
{
    public required Value Times { get; init; }
    public IReadOnlyList<Step> Steps { get; init; } = [];
}

public sealed record Mode
{
    public string? Label { get; init; }
    public IReadOnlyList<Step> Steps { get; init; } = [];
}

public sealed record ChooseOneStep : Step
{
    public IReadOnlyList<Mode> Modes { get; init; } = [];
}

public sealed record ChooseNStep : Step
{
    public required Value Count { get; init; }
    public IReadOnlyList<Mode> Modes { get; init; } = [];
}

public sealed record CreateDelayedStep : Step
{
    public required Ability Ability { get; init; }
    public Duration? Duration { get; init; }
}

/// <summary>C# fallback step. The handler name goes in the base <see cref="Step.Script"/> property.</summary>
public sealed record ScriptStep : Step
{
    public JsonObject? Args { get; init; }
}
