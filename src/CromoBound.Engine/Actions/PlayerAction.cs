using System.Text.Json.Serialization;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Actions;

/// <summary>Something a player submits. Serialized with a "type" discriminator for the action log.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PlayCard), "PlayCard")]
[JsonDerivedType(typeof(UseRune), "UseRune")]
[JsonDerivedType(typeof(StandardMove), "StandardMove")]
[JsonDerivedType(typeof(Hide), "Hide")]
[JsonDerivedType(typeof(Pass), "Pass")]
[JsonDerivedType(typeof(EndTurn), "EndTurn")]
[JsonDerivedType(typeof(ChoosePlayOptions), "ChoosePlayOptions")]
[JsonDerivedType(typeof(AdjustCost), "AdjustCost")]
[JsonDerivedType(typeof(PayCost), "PayCost")]
[JsonDerivedType(typeof(CancelPlay), "CancelPlay")]
[JsonDerivedType(typeof(ChooseShowdown), "ChooseShowdown")]
[JsonDerivedType(typeof(AssignDamage), "AssignDamage")]
[JsonDerivedType(typeof(ResolveDone), "ResolveDone")]
[JsonDerivedType(typeof(ContinueTurn), "ContinueTurn")]
public abstract record PlayerAction;

/// <summary>Start playing a card from hand, the Champion Zone, or face down.</summary>
public sealed record PlayCard(ObjectId Card) : PlayerAction;

public enum RuneUse { Exhaust, Recycle }

/// <summary>A rune's Reaction Add: exhaust for 1 energy, or recycle for 1 power of its domain.</summary>
public sealed record UseRune(ObjectId Rune, RuneUse Use) : PlayerAction;

public sealed record StandardMove : PlayerAction
{
    public IReadOnlyList<ObjectId> Units { get; init; } = [];
    public required Place Destination { get; init; }
}

public sealed record Hide(ObjectId Card, int Battlefield) : PlayerAction;

public sealed record Pass : PlayerAction;

public sealed record EndTurn : PlayerAction;

/// <summary>Where the permanent enters (null for spells) and whether to pay Accelerate.</summary>
public sealed record ChoosePlayOptions(Place? Location, bool Accelerate) : PlayerAction;

/// <summary>Changes the cost being paid, for text-based cost changes the engine doesn't know in 2a.</summary>
public sealed record AdjustCost : PlayerAction
{
    public int Energy { get; init; }
    public IReadOnlyList<PowerSymbol> AddPower { get; init; } = [];
    public IReadOnlyList<PowerSymbol> RemovePower { get; init; } = [];
}

/// <summary>Runes to exhaust (1 energy each) and to recycle (1 power each); the rest comes from the rune pool.</summary>
public sealed record PayCost : PlayerAction
{
    public IReadOnlyList<ObjectId> Exhaust { get; init; } = [];
    public IReadOnlyList<ObjectId> Recycle { get; init; } = [];
}

public sealed record CancelPlay : PlayerAction;

public sealed record ChooseShowdown(int Battlefield) : PlayerAction;

public sealed record DamageAssignment(ObjectId Unit, int Amount);

public sealed record AssignDamage : PlayerAction
{
    public IReadOnlyList<DamageAssignment> Assignments { get; init; } = [];
}

/// <summary>The controller has carried out a chain item's effect by hand.</summary>
public sealed record ResolveDone : PlayerAction;

/// <summary>Start/end-of-turn effects have been applied by hand; the turn goes on.</summary>
public sealed record ContinueTurn : PlayerAction;
