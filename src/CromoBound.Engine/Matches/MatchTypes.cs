using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Matches;

public enum MatchFormat { Bo1, Bo3 }

public enum MatchStage { PickBattlefields, PlayOrder, Sideboarding, Mulligan, Playing, Over }

/// <summary>Everything needed to start (and replay) a match. Decks are copies; Player1Deck belongs to player index 0.</summary>
public sealed record MatchSetup(MatchFormat Format, Deck Player1Deck, Deck Player2Deck, ulong Seed);

/// <summary>The match, or null when a deck is illegal; the reports list every problem of both decks.</summary>
public sealed record MatchCreateResult(Match? Match, IReadOnlyList<DeckReport> Reports);

public sealed record MatchResult(IReadOnlyList<int> GameWins, PlayerId? Winner);

public sealed record LoggedAction(PlayerId Player, PlayerAction Action);

/// <summary>A saved match: the versions it was played with, its setup and its action log (spec §6.5, §6.7).</summary>
public sealed record MatchRecord(string EngineVersion, string DataFingerprint, MatchSetup Setup, IReadOnlyList<LoggedAction> Log);

/// <summary>A saved match from another engine build or other card data can't be replayed faithfully.</summary>
public sealed class MatchVersionMismatchException(string recordedEngine, string currentEngine, string recordedData, string currentData)
    : Exception($"This match was saved with engine {recordedEngine} and card data {recordedData}; now running engine {currentEngine} and card data {currentData}.");
