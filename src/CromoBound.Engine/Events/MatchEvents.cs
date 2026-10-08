using CromoBound.Engine.Actions;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Events;

public sealed record GameStarted(int GameNumber) : GameEvent;

/// <summary>Both legends, by player index, revealed at the start of every game (spec §9.3).</summary>
public sealed record LegendsRevealed(IReadOnlyList<string> Printings) : GameEvent;

/// <summary>The battlefields for this game, by player index, revealed together.</summary>
public sealed record BattlefieldsChosen(IReadOnlyList<string> Printings) : GameEvent;

public sealed record D20Rolled(PlayerId Player, int Value) : GameEvent;

public sealed record PlayOrderChosen(PlayerId Chooser, PlayerId First) : GameEvent;

public sealed record SideboardChanged(PlayerId Player, int Swaps, bool ChampionChanged) : GameEvent;

public sealed record MulliganTaken(PlayerId Player, int Count) : GameEvent;

public sealed record GameRecorded(int GameNumber, PlayerId? Winner, GameEndReason Reason) : GameEvent;

public sealed record MatchEnded(PlayerId Winner) : GameEvent;

/// <summary>Returned only to the player who asked, never stored in <c>Match.Events</c>; its Sequence is 0.</summary>
public sealed record UndoRequested(PlayerId Player) : GameEvent;

/// <summary>Highlights a manual action in the log. The public copy has no Action, because it can name cards in hidden zones; the acting player gets the full action.</summary>
public sealed record ManualActionTaken(PlayerId Player, string Kind, PlayerAction? Action) : GameEvent;

public sealed record UnitHealed(ObjectId Unit, int Amount) : GameEvent;

public sealed record MightModified(ObjectId Unit, int Amount, Duration Duration) : GameEvent;

public sealed record XpChanged(PlayerId Player, int Xp) : GameEvent;

public sealed record PoolAdjusted(PlayerId Player) : GameEvent;

public sealed record TokenCreated(ObjectId Token, string CardId, Place Location) : GameEvent;

public sealed record ControlGained(ObjectId Card, PlayerId Player) : GameEvent;

public sealed record DeckShuffled(PlayerId Owner, PlaceKind Deck) : GameEvent;

/// <summary>The private copy lists the cards; the public copy only the count.</summary>
public sealed record CardsLookedAt(PlayerId Looker, PlayerId Owner, PlaceKind Deck, int Count, IReadOnlyList<string>? CardIds) : GameEvent;

public sealed record CardRevealed(ObjectId? Card, string CardId) : GameEvent;

public sealed record ChainItemCountered(int ItemId) : GameEvent;
