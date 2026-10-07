using System.Text.Json.Serialization;

namespace CromoBound.Models.Effects;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ModifyMightModifier), "ModifyMight")]
[JsonDerivedType(typeof(GrantKeywordModifier), "GrantKeyword")]
[JsonDerivedType(typeof(CostReductionModifier), "CostReduction")]
[JsonDerivedType(typeof(CostIncreaseModifier), "CostIncrease")]
[JsonDerivedType(typeof(KeywordCostReductionModifier), "KeywordCostReduction")]
[JsonDerivedType(typeof(PermissionModifier), "Permission")]
[JsonDerivedType(typeof(EnterReadyModifier), "EnterReady")]
[JsonDerivedType(typeof(UntargetableModifier), "Untargetable")]
[JsonDerivedType(typeof(IgnoreCostModifier), "IgnoreCost")]
public abstract record Modifier
{
    public ObjectRef? AppliesTo { get; init; }
}

public sealed record ModifyMightModifier : Modifier
{
    public required Value Amount { get; init; }
}

public sealed record GrantKeywordModifier : Modifier
{
    public required KeywordEntry Keyword { get; init; }
}

public abstract record CostChangeModifier : Modifier
{
    public Value? Energy { get; init; }
    public IReadOnlyList<PowerSymbol> Power { get; init; } = [];
    public int? Minimum { get; init; }
    public Zone? FromZone { get; init; }
}

public sealed record CostReductionModifier : CostChangeModifier;

public sealed record CostIncreaseModifier : CostChangeModifier;

public sealed record KeywordCostReductionModifier : Modifier
{
    public required MechanicalKeyword Keyword { get; init; }
    public Value? Energy { get; init; }
}

public sealed record PermissionModifier : Modifier
{
    public required Permission Permission { get; init; }
}

public sealed record EnterReadyModifier : Modifier;

public sealed record UntargetableModifier : Modifier
{
    public UntargetableBy By { get; init; } = UntargetableBy.EnemySpellsAndAbilities;
}

public sealed record IgnoreCostModifier : Modifier;
