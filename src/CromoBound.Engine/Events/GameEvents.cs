using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Events;

/// <summary>Something that happened. <see cref="VisibleTo"/> null means public; otherwise only that player may see it.</summary>
public abstract record GameEvent
{
    public int Sequence { get; internal set; }
    public PlayerId? VisibleTo { get; init; }
}

public sealed record TurnStarted(PlayerId Player, int Number) : GameEvent;

public sealed record PhaseStarted(Phase Phase, TurnStep Step) : GameEvent;

/// <summary>A card changed place. In the anonymous public copy of a hidden move, the card and ids are null.</summary>
public sealed record CardMoved(string? CardId, ObjectId? From, ObjectId? To, Place FromPlace, Place ToPlace) : GameEvent;

public enum StatusKind { Exhausted, Stunned, Buffed, Empowered }

public sealed record StatusChanged(ObjectId Object, StatusKind Status, bool Value) : GameEvent;

public sealed record ResourcesAdded(PlayerId Player, int Energy, Domain? Power) : GameEvent;

public sealed record CostAdjusted(PlayerId Player, TotalCost Cost) : GameEvent;

public sealed record DamageDealt(ObjectId Unit, int Amount) : GameEvent;

public sealed record UnitsHealed : GameEvent;

/// <summary>Recorded before the unit leaves, so its death triggers can be added by hand.</summary>
public sealed record UnitDied(ObjectId Unit, string CardId, PlayerId Controller) : GameEvent;

public sealed record PointsChanged(PlayerId Player, int Points) : GameEvent;

public enum ScoreKind { Conquer, Hold }

public sealed record BattlefieldScored(PlayerId Player, int Battlefield, ScoreKind Kind, bool GainedPoint) : GameEvent;

public sealed record ControlChanged(int Battlefield, PlayerId? Controller) : GameEvent;

public sealed record ShowdownStarted(int Battlefield, PlayerId Focus) : GameEvent;

public sealed record ShowdownEnded(int Battlefield) : GameEvent;

public sealed record CombatStarted(int Battlefield, PlayerId Attacker, PlayerId Defender) : GameEvent;

public enum CombatResult { AttackerWon, DefenderWon, NoResult }

public sealed record CombatEnded(int Battlefield, CombatResult Result) : GameEvent;

public sealed record ChainItemAdded(int ItemId, PlayerId Controller) : GameEvent;

public sealed record ChainItemResolved(int ItemId) : GameEvent;

public sealed record PlayCancelled(int ItemId) : GameEvent;

public sealed record BurnedOut(PlayerId Player, PlayerId PointTo) : GameEvent;

public sealed record GameEnded(PlayerId? Winner, GameEndReason Reason) : GameEvent;
