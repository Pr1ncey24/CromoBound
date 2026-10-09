using System.Text.Json.Serialization;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Events;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TurnStarted), "TurnStarted")]
[JsonDerivedType(typeof(PhaseStarted), "PhaseStarted")]
[JsonDerivedType(typeof(CardMoved), "CardMoved")]
[JsonDerivedType(typeof(StatusChanged), "StatusChanged")]
[JsonDerivedType(typeof(ResourcesAdded), "ResourcesAdded")]
[JsonDerivedType(typeof(CostAdjusted), "CostAdjusted")]
[JsonDerivedType(typeof(DamageDealt), "DamageDealt")]
[JsonDerivedType(typeof(UnitsHealed), "UnitsHealed")]
[JsonDerivedType(typeof(UnitDied), "UnitDied")]
[JsonDerivedType(typeof(PointsChanged), "PointsChanged")]
[JsonDerivedType(typeof(BattlefieldScored), "BattlefieldScored")]
[JsonDerivedType(typeof(ControlChanged), "ControlChanged")]
[JsonDerivedType(typeof(ShowdownStarted), "ShowdownStarted")]
[JsonDerivedType(typeof(ShowdownEnded), "ShowdownEnded")]
[JsonDerivedType(typeof(CombatStarted), "CombatStarted")]
[JsonDerivedType(typeof(CombatEnded), "CombatEnded")]
[JsonDerivedType(typeof(ChainItemAdded), "ChainItemAdded")]
[JsonDerivedType(typeof(ChainItemResolved), "ChainItemResolved")]
[JsonDerivedType(typeof(PlayCancelled), "PlayCancelled")]
[JsonDerivedType(typeof(BurnedOut), "BurnedOut")]
[JsonDerivedType(typeof(GameEnded), "GameEnded")]
[JsonDerivedType(typeof(GameStarted), "GameStarted")]
[JsonDerivedType(typeof(LegendsRevealed), "LegendsRevealed")]
[JsonDerivedType(typeof(BattlefieldsChosen), "BattlefieldsChosen")]
[JsonDerivedType(typeof(D20Rolled), "D20Rolled")]
[JsonDerivedType(typeof(PlayOrderChosen), "PlayOrderChosen")]
[JsonDerivedType(typeof(SideboardChanged), "SideboardChanged")]
[JsonDerivedType(typeof(MulliganTaken), "MulliganTaken")]
[JsonDerivedType(typeof(GameRecorded), "GameRecorded")]
[JsonDerivedType(typeof(MatchEnded), "MatchEnded")]
[JsonDerivedType(typeof(UndoRequested), "UndoRequested")]
[JsonDerivedType(typeof(ManualActionTaken), "ManualActionTaken")]
[JsonDerivedType(typeof(UnitHealed), "UnitHealed")]
[JsonDerivedType(typeof(MightModified), "MightModified")]
[JsonDerivedType(typeof(XpChanged), "XpChanged")]
[JsonDerivedType(typeof(PoolAdjusted), "PoolAdjusted")]
[JsonDerivedType(typeof(TokenCreated), "TokenCreated")]
[JsonDerivedType(typeof(ControlGained), "ControlGained")]
[JsonDerivedType(typeof(DeckShuffled), "DeckShuffled")]
[JsonDerivedType(typeof(CardsLookedAt), "CardsLookedAt")]
[JsonDerivedType(typeof(CardRevealed), "CardRevealed")]
[JsonDerivedType(typeof(ChainItemCountered), "ChainItemCountered")]
[JsonDerivedType(typeof(ChoiceMade), "ChoiceMade")]
[JsonDerivedType(typeof(TargetsChosen), "TargetsChosen")]
[JsonDerivedType(typeof(CardPlayed), "CardPlayed")]
[JsonDerivedType(typeof(AbilityActivated), "AbilityActivated")]
[JsonDerivedType(typeof(TriggerAdded), "TriggerAdded")]
[JsonDerivedType(typeof(PlayerChosen), "PlayerChosen")]
[JsonDerivedType(typeof(Predicted), "Predicted")]
[JsonDerivedType(typeof(Attached), "Attached")]
[JsonDerivedType(typeof(Detached), "Detached")]
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

/// <summary>The engine made a forced choice for the player (exactly one legal answer, nothing to decline; spec §6.2).</summary>
public sealed record ChoiceMade(PlayerId Player, string Kind, IReadOnlyList<ObjectId> Chosen) : GameEvent;

/// <summary>Public: the targets of one target slot of a chain item, whether chosen or forced (spec §6.4).</summary>
public sealed record TargetsChosen(int ItemId, int Slot, IReadOnlyList<ObjectId> Targets) : GameEvent;

/// <summary>A play finalized. <see cref="Card"/> is the card where it ended up: the board, or the chain for a spell.</summary>
public sealed record CardPlayed(ObjectId Card, string CardId, PlayerId Controller) : GameEvent;

/// <summary>An activated ability's costs were paid; <see cref="Ability"/> is its index in the card's abilities.</summary>
public sealed record AbilityActivated(ObjectId Source, int Ability, PlayerId Controller) : GameEvent;

/// <summary>A triggered ability went on the chain as item <see cref="ItemId"/>.</summary>
public sealed record TriggerAdded(int ItemId, ObjectId Source, PlayerId Controller) : GameEvent;

/// <summary>Public: <see cref="Player"/> chose (or was forced to choose) <see cref="Chosen"/> for an effect.</summary>
public sealed record PlayerChosen(PlayerId Player, PlayerId Chosen) : GameEvent;

/// <summary>A player looked at the top <see cref="Count"/> cards of their Main Deck. Only their own copy has the card ids.</summary>
public sealed record Predicted(PlayerId Player, int Count, IReadOnlyList<string>? CardIds) : GameEvent;

/// <summary>Gear was attached to a unit (spec §8.2).</summary>
public sealed record Attached(ObjectId Gear, ObjectId Unit) : GameEvent;

/// <summary>Gear was detached from its unit.</summary>
public sealed record Detached(ObjectId Gear) : GameEvent;
