using System.Text.Json.Serialization;

namespace CromoBound.Models.Effects;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SpellAbility), "Spell")]
[JsonDerivedType(typeof(TriggeredAbility), "Triggered")]
[JsonDerivedType(typeof(ActivatedAbility), "Activated")]
[JsonDerivedType(typeof(PassiveAbility), "Passive")]
[JsonDerivedType(typeof(ReplacementAbility), "Replacement")]
public abstract record Ability
{
    public LineRef? Line { get; init; }
    public Condition? Condition { get; init; }
    public Zone? ActiveIn { get; init; }
    public string? Script { get; init; }
}

public sealed record SpellAbility : Ability
{
    public IReadOnlyList<Step> Steps { get; init; } = [];
}

public sealed record TriggeredAbility : Ability
{
    public required Trigger Trigger { get; init; }
    public Condition? If { get; init; }
    public bool? Optional { get; init; }
    public Cost? Cost { get; init; }
    public Limit? Limit { get; init; }
    public IReadOnlyList<Step> Steps { get; init; } = [];
}

public sealed record ActivatedAbility : Ability
{
    public Cost? Cost { get; init; }
    public Timing? Timing { get; init; }
    public Condition? UseOnlyIf { get; init; }
    public Limit? Limit { get; init; }
    public IReadOnlyList<Step> Steps { get; init; } = [];
}

public sealed record PassiveAbility : Ability
{
    public Condition? While { get; init; }
    public IReadOnlyList<Modifier> Modifiers { get; init; } = [];
}

public sealed record ReplacementAbility : Ability
{
    public required Trigger Replaces { get; init; }
    public IReadOnlyList<Step> With { get; init; } = [];
    public bool? Optional { get; init; }
    public Limit? Limit { get; init; }
}
