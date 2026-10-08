# Engine Plan C: Match, Manual Actions, Undo, Views

> Steps use checkbox (`- [ ]`) syntax for tracking. Third of three plans for Phase 2a (A: decks and state; B: rules; C: this plan).

**Goal:** Wrap the Plan B `Game` in a `Match`. This plan adds:
- Bo1 and Bo3 matches with the pre-game steps: battlefields, d20 roll-off, play order, sideboarding, setup and mulligan;
- manual actions for hand-resolved effects;
- undo by agreement;
- saving and loading with version checks;
- per-player views that never leak hidden cards.

**Architecture:**
- `Match` (public) is a thin wrapper around an internal `MatchCore`.
  - The core runs the pre-game steps as its own decisions, then delegates to `Game`.
  - It logs every accepted action and keeps the match-wide event list.
- **Undo and loading** rebuild a fresh core by replaying the log from the setup. The match's single seeded generator makes the replay identical.
- **Views** are built from the core on demand, filtering cards, decisions and events by who may see them.

**Tech Stack:** .NET 10, xUnit 2.9.3. No new dependencies.

**Design document:** `docs/engine-architecture.md`. This plan implements §4, §6.5–6.7, §8, §9.3, §10 and §11, plus the remaining parts of §6.2–6.3.

## Global Constraints

- Plans A and B's Global Constraints still apply.
- Saved matches use `CromoJson`. Every action and decision type must round-trip through it; any list that may be empty needs a default or `[KeepEmpty]`.
- Only `Match`, `Game`, `PlayerView` and the action, decision, event and result records are public API. `MatchCore` is internal.
- No view or event visible to a player may contain the `CardId`, `PrintingId` or `ObjectId` of a card in a secret zone (Main Deck, Rune Deck), or in the other player's private zones (hand, facedown).

## Deliberate deviations from the spec (reviewers: these are intended)

1. **No draws in 2a.** A game can't end in a draw (no time limits; ties at 8+ points continue), so the "after a draw" branches of §9.3 aren't implemented: keeping battlefields, keeping play order, no sideboarding.
2. **Undo requests aren't logged.** `RequestUndo` and `AnswerUndo` are never written to the log. An accepted undo removes entries instead, so a replayed log never contains undo traffic.
3. **`Match.Events` exposes the full, unfiltered event list.** It's for the server only. Players see events through `PlayerView.Log`.

## Review Focus

1. **A move into a hidden zone reveals the new object id** (e.g. Burn Out shuffling the trash into the deck). The public event must omit ids of cards in secret or opponent-private zones. Pinned in Task 6 (`Opponent_view_and_log_never_contain_hidden_cards`).
2. **A saved match from another engine build or other card data.** Loading must refuse with `MatchVersionMismatchException`, not replay into a different game. Pinned in Task 5 (`Loading_a_record_from_another_version_is_refused`).
3. **Undo while a payment or showdown is half-way.** After undo, the match must equal one where the undone action never happened. Pinned in Task 5 (`Accepted_undo_equals_never_taking_the_action`).
4. **A manual action while a decision is pending** (e.g. damage during a payment, during a turn-point pause, or while resolving by hand). The pending decision must be rebuilt, not lost, and cleanup must still apply. Pinned in Task 4 (`Manual_damage_during_a_turn_point_is_cleaned_up_right_away`, `Manual_actions_are_allowed_while_resolving_by_hand`).
5. **An illegal sideboard swap** (removing a card that isn't there, or ending with an illegal deck). It must be rejected with the reasons, and the player can resubmit. Pinned in Task 3 (`Illegal_sideboard_is_rejected_and_can_be_resubmitted`).

---

## File Structure

```
src/CromoBound.Engine/
  Actions/MatchActions.cs        PickBattlefield, ChoosePlayOrder, SideboardSwap, SubmitSideboard, Mulligan, RequestUndo, AnswerUndo, Concede
  Actions/ManualActions.cs       ManualAction and the 15 manual actions (spec §8)
  Actions/PlayerAction.cs        (modify) register the new action types
  Decisions/Decisions.cs         (modify) TotalCost.Power keeps empty lists
  Decisions/MatchDecisions.cs    BattlefieldChoice, PickBattlefieldDecision, ChoosePlayOrderDecision, SideboardDecision, MulliganDecision, ConfirmUndoDecision
  Events/MatchEvents.cs          match and manual-action events
  Matches/MatchTypes.cs            MatchFormat, MatchStage, MatchSetup, MatchCreateResult, MatchResult, LoggedAction, MatchRecord, MatchVersionMismatchException
  Matches/Match.cs                 public facade: Create, Submit, undo, ToRecord, Load, ViewFor
  Matches/MatchCore.cs             state, Submit routing, logging, events
  Matches/MatchCore.PreGame.cs     battlefields, roll-off, play order, sideboarding, setup, mulligan
  Matches/MatchCore.Results.cs     concede, game results, next game, match end
  Matches/Sideboarding.cs          applying swaps to a deck
  Rules/Game.cs                  (modify) TakeEvents
  Rules/Game.Mutations.cs        (modify) MoveCard visibility (leak fix)
  Rules/Game.Manual.cs           SubmitManual and the manual actions, AbilityTask, counter
  Views/PlayerView.cs            view records
  Views/ViewBuilder.cs           builds a PlayerView from a MatchCore
tests/CromoBound.Engine.Tests/
  EngineTestDb.cs                (modify) filler cards, battlefields, TestDecks
  MatchJsonTests.cs, MatchBo1Tests.cs, MatchBo3Tests.cs, ManualTests.cs, UndoAndSaveTests.cs, ViewTests.cs
  Bot.cs                         (modify) match decisions
```

---

### Task 1: Match, manual and pre-game types

**Files:**
- Create: `src/CromoBound.Engine/Actions/MatchActions.cs`, `Actions/ManualActions.cs`, `Decisions/MatchDecisions.cs`, `Events/MatchEvents.cs`, `Matches/MatchTypes.cs`
- Modify: `src/CromoBound.Engine/Actions/PlayerAction.cs`, `src/CromoBound.Engine/Decisions/Decisions.cs`
- Test: `tests/CromoBound.Engine.Tests/MatchJsonTests.cs`

**Interfaces:**
- Consumes: Plan B types (`PlayerAction`, `PendingDecision`, `GameEvent`, `TotalCost`, `StatusKind`, `AbilityKind`, `GameEndReason`); `Deck` (Plan A).
- Produces: every type in the code below. Later tasks use these exact names.

- [x] **Step 1: Write the failing test**

Create `tests/CromoBound.Engine.Tests/MatchJsonTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Engine.Tests;

public class MatchJsonTests
{
    [Fact]
    public void A_match_record_round_trips_with_every_kind_of_action()
    {
        var deck = new Deck { Name = "d", Legend = "l", Champion = "c", Main = [new DeckEntry { Printing = "p", Count = 3 }] };
        var p1 = new PlayerId(0);
        PlayerAction[] actions =
        [
            new PickBattlefield("bf"),
            new ChoosePlayOrder(true),
            new SubmitSideboard { Swaps = [new SideboardSwap("a", "b")], Champion = "c2" },
            new Mulligan { SetAside = [new ObjectId(3)] },
            new Concede(),
            new ManualMoveCard(new ObjectId(5), Place.Trash(p1)),
            new ManualSetStatus(new ObjectId(5), StatusKind.Stunned, true),
            new ManualModifyMight(new ObjectId(5), 2, Duration.ThisTurn),
            new ManualAdjustPool(p1, 1, Domain.Fury, 1),
            new ManualLookAtTop(p1, PlaceKind.MainDeck, 3),
            new AddAbilityToChain(new ObjectId(5), 1, AbilityKind.Triggered) { Cost = new TotalCost(1, []) },
        ];
        var record = new MatchRecord("1.0.0+abc", "fp", new MatchSetup(MatchFormat.Bo3, deck, deck, 42),
            [.. actions.Select(a => new LoggedAction(p1, a))]);

        var json = CromoJson.Serialize(record);
        var back = CromoJson.Deserialize<MatchRecord>(json);

        Assert.Equal(json, CromoJson.Serialize(back));
        var ability = Assert.IsType<AddAbilityToChain>(back.Log[^1].Action);
        Assert.NotNull(ability.Cost!.Power);
        Assert.Equal(42UL, back.Setup.Seed);
    }
}
```

- [x] **Step 2: Run the test to verify it fails**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~MatchJsonTests"`
Expected: FAIL (compilation errors: `CromoBound.Engine.Matches`, `PickBattlefield` … not found).

- [x] **Step 3: Keep empty power lists in TotalCost**

In `src/CromoBound.Engine/Decisions/Decisions.cs`, add `using CromoBound.Models.Json;` and replace the `TotalCost` record with:
```csharp
/// <summary>Energy plus one power symbol per entry. Power is written even when empty, so it never reads back as null.</summary>
public sealed record TotalCost(int Energy, [property: KeepEmpty] IReadOnlyList<PowerSymbol> Power);
```

- [x] **Step 4: Create the action types**

Create `src/CromoBound.Engine/Actions/MatchActions.cs`:
```csharp
using CromoBound.Engine.State;

namespace CromoBound.Engine.Actions;

/// <summary>Bo3: pick one of your unused battlefields (by printing id).</summary>
public sealed record PickBattlefield(string Printing) : PlayerAction;

public sealed record ChoosePlayOrder(bool First) : PlayerAction;

/// <summary>One copy of <see cref="Out"/> goes from the main deck to the sideboard; one copy of <see cref="In"/> comes back.</summary>
public sealed record SideboardSwap(string Out, string In);

/// <summary>Sideboard swaps (empty = no changes), and optionally a new Chosen Champion taken from the main deck or sideboard.</summary>
public sealed record SubmitSideboard : PlayerAction
{
    public IReadOnlyList<SideboardSwap> Swaps { get; init; } = [];
    public string? Champion { get; init; }
}

/// <summary>Set aside up to 2 cards: draw that many, then recycle them to the bottom in random order (CR 117).</summary>
public sealed record Mulligan : PlayerAction
{
    public IReadOnlyList<ObjectId> SetAside { get; init; } = [];
}

/// <summary>Ask to roll back to just before your last action; the opponent must agree.</summary>
public sealed record RequestUndo : PlayerAction;

public sealed record AnswerUndo(bool Accept) : PlayerAction;

/// <summary>Concede the current game; the opponent wins it.</summary>
public sealed record Concede : PlayerAction;
```

Create `src/CromoBound.Engine/Actions/ManualActions.cs`:
```csharp
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Actions;

/// <summary>Escape hatches for effects resolved by hand (spec §8). Either player, any time during play; logged and highlighted.</summary>
public abstract record ManualAction : PlayerAction;

/// <summary>Draw, discard, kill, banish, return to hand, recall, recycle… Not for cards on the chain (use ManualCounter).</summary>
public sealed record ManualMoveCard(ObjectId Card, Place Destination, DeckPosition Position = DeckPosition.Top) : ManualAction;

public sealed record ManualDamage(ObjectId Unit, int Amount) : ManualAction;

public sealed record ManualHeal(ObjectId Unit, int Amount) : ManualAction;

public sealed record ManualSetStatus(ObjectId Card, StatusKind Status, bool Value) : ManualAction;

public sealed record ManualModifyMight(ObjectId Unit, int Amount, Duration Duration) : ManualAction;

public sealed record ManualAdjustPoints(PlayerId Player, int Amount) : ManualAction;

public sealed record ManualAdjustXp(PlayerId Player, int Amount) : ManualAction;

/// <summary>Adds (or, with negative amounts, removes) energy, power of one domain, and universal power. Never below 0.</summary>
public sealed record ManualAdjustPool(PlayerId Player, int Energy, Domain? Domain = null, int Power = 0, int UniversalPower = 0) : ManualAction;

public sealed record ManualCreateToken(string TokenId, Place Location, PlayerId Controller) : ManualAction;

public sealed record ManualGainControl(ObjectId Card, PlayerId Player) : ManualAction;

public sealed record ManualShuffle(PlayerId Owner, PlaceKind Deck) : ManualAction;

/// <summary>The submitting player looks at the top cards of a deck; only they see which cards.</summary>
public sealed record ManualLookAtTop(PlayerId Owner, PlaceKind Deck, int Count) : ManualAction;

/// <summary>Shows a card from a hidden zone to both players.</summary>
public sealed record ManualReveal(ObjectId Card) : ManualAction;

public sealed record ManualCounter(int ChainItem) : ManualAction;

/// <summary>Puts a triggered or activated ability (a line of the source's text, 1-based) on the chain, with an optional cost.</summary>
public sealed record AddAbilityToChain(ObjectId Source, int Line, AbilityKind Kind) : ManualAction
{
    public TotalCost? Cost { get; init; }
}
```

In `src/CromoBound.Engine/Actions/PlayerAction.cs`, add these attribute lines directly above `public abstract record PlayerAction;`:
```csharp
[JsonDerivedType(typeof(PickBattlefield), "PickBattlefield")]
[JsonDerivedType(typeof(ChoosePlayOrder), "ChoosePlayOrder")]
[JsonDerivedType(typeof(SubmitSideboard), "SubmitSideboard")]
[JsonDerivedType(typeof(Mulligan), "Mulligan")]
[JsonDerivedType(typeof(RequestUndo), "RequestUndo")]
[JsonDerivedType(typeof(AnswerUndo), "AnswerUndo")]
[JsonDerivedType(typeof(Concede), "Concede")]
[JsonDerivedType(typeof(ManualMoveCard), "ManualMoveCard")]
[JsonDerivedType(typeof(ManualDamage), "ManualDamage")]
[JsonDerivedType(typeof(ManualHeal), "ManualHeal")]
[JsonDerivedType(typeof(ManualSetStatus), "ManualSetStatus")]
[JsonDerivedType(typeof(ManualModifyMight), "ManualModifyMight")]
[JsonDerivedType(typeof(ManualAdjustPoints), "ManualAdjustPoints")]
[JsonDerivedType(typeof(ManualAdjustXp), "ManualAdjustXp")]
[JsonDerivedType(typeof(ManualAdjustPool), "ManualAdjustPool")]
[JsonDerivedType(typeof(ManualCreateToken), "ManualCreateToken")]
[JsonDerivedType(typeof(ManualGainControl), "ManualGainControl")]
[JsonDerivedType(typeof(ManualShuffle), "ManualShuffle")]
[JsonDerivedType(typeof(ManualLookAtTop), "ManualLookAtTop")]
[JsonDerivedType(typeof(ManualReveal), "ManualReveal")]
[JsonDerivedType(typeof(ManualCounter), "ManualCounter")]
[JsonDerivedType(typeof(AddAbilityToChain), "AddAbilityToChain")]
```

- [x] **Step 5: Create the decision, event and match types**

Create `src/CromoBound.Engine/Decisions/MatchDecisions.cs`:
```csharp
using CromoBound.Engine.State;

namespace CromoBound.Engine.Decisions;

public sealed record BattlefieldChoice(PlayerId Player, IReadOnlyList<string> Printings);

/// <summary>Bo3: every listed player picks one of their unused battlefields, in any order; picks stay hidden until all are in.</summary>
public sealed record PickBattlefieldDecision(IReadOnlyList<PlayerId> Players, IReadOnlyList<BattlefieldChoice> Choices) : PendingDecision(Players);

public sealed record ChoosePlayOrderDecision(PlayerId Player) : PendingDecision([Player]);

/// <summary>Every listed player submits their sideboard swaps (or none), in any order.</summary>
public sealed record SideboardDecision(IReadOnlyList<PlayerId> Players) : PendingDecision(Players);

public sealed record MulliganDecision(PlayerId Player, IReadOnlyList<ObjectId> Hand) : PendingDecision([Player]);

public sealed record ConfirmUndoDecision(PlayerId Player, PlayerId RequestedBy) : PendingDecision([Player]);
```

Create `src/CromoBound.Engine/Events/MatchEvents.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Events;

public sealed record GameStarted(int GameNumber) : GameEvent;

/// <summary>The battlefields for this game, by player index, revealed together.</summary>
public sealed record BattlefieldsChosen(IReadOnlyList<string> Printings) : GameEvent;

public sealed record D20Rolled(PlayerId Player, int Value) : GameEvent;

public sealed record PlayOrderChosen(PlayerId Chooser, PlayerId First) : GameEvent;

public sealed record SideboardChanged(PlayerId Player, int Swaps, bool ChampionChanged) : GameEvent;

public sealed record MulliganTaken(PlayerId Player, int Count) : GameEvent;

public sealed record GameRecorded(int GameNumber, PlayerId? Winner, GameEndReason Reason) : GameEvent;

public sealed record MatchEnded(PlayerId Winner) : GameEvent;

public sealed record UndoRequested(PlayerId Player) : GameEvent;

/// <summary>Highlights a manual action in the log.</summary>
public sealed record ManualActionTaken(PlayerId Player, PlayerAction Action) : GameEvent;

public sealed record UnitHealed(ObjectId Unit, int Amount) : GameEvent;

public sealed record MightModified(ObjectId Unit, int Amount, Duration Duration) : GameEvent;

public sealed record XpChanged(PlayerId Player, int Xp) : GameEvent;

public sealed record PoolAdjusted(PlayerId Player) : GameEvent;

public sealed record TokenCreated(ObjectId Token, string CardId, Place Location) : GameEvent;

public sealed record ControlGained(ObjectId Card, PlayerId Player) : GameEvent;

public sealed record DeckShuffled(PlayerId Owner, PlaceKind Deck) : GameEvent;

/// <summary>The private copy lists the cards; the public copy only the count.</summary>
public sealed record CardsLookedAt(PlayerId Looker, PlayerId Owner, PlaceKind Deck, int Count, IReadOnlyList<string>? CardIds) : GameEvent;

public sealed record CardRevealed(ObjectId Card, string CardId) : GameEvent;

public sealed record ChainItemCountered(int ItemId) : GameEvent;
```

Create `src/CromoBound.Engine/Matches/MatchTypes.cs`:
```csharp
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
```

`MatchCreateResult` refers to `Match`, which Task 2 creates. Until then, add this temporary placeholder in `src/CromoBound.Engine/Matches/Match.cs` so the project compiles (Task 2 replaces the whole file):
```csharp
namespace CromoBound.Engine.Matches;

public sealed partial class Match;
```

- [x] **Step 6: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests).

- [x] **Step 7: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests/MatchJsonTests.cs
git commit -m "feat(engine): add match, manual action and pre-game types"
```

---
### Task 2: Match core and the Bo1 pre-game

**Files:**
- Modify: `src/CromoBound.Engine/Rules/Game.cs` (`TakeEvents`)
- Create: `src/CromoBound.Engine/Matches/MatchCore.cs`, `MatchCore.PreGame.cs`, `MatchCore.Results.cs`, `Sideboarding.cs`
- Replace: `src/CromoBound.Engine/Matches/Match.cs`
- Modify: `tests/CromoBound.Engine.Tests/EngineTestDb.cs`
- Create: `tests/CromoBound.Engine.Tests/MatchTestExtensions.cs`
- Test: `tests/CromoBound.Engine.Tests/MatchBo1Tests.cs`

**Interfaces:**
- Consumes: Task 1 types; `Game` (Plan B), including internal `Draw`, `MoveCard`; `DeckValidator` (Plan A).
- Produces:
  - `Match.Create(MatchSetup, CardDatabase) : MatchCreateResult`
  - Properties `Match.Pending`, `Result`, `Stage`, `GameNumber`, `Game`, `Events`, `CurrentDecks`; method `Match.Submit(PlayerId, PlayerAction) : SubmitResult`
  - `Game.TakeEvents()` (internal)
  - Internal `MatchCore` members used by Tasks 3–6:
    - collections: `Players` (static), `Decks`, `Available`, `Wins`, `Picks`, `Log`, `Events`;
    - state: `Stage`, `GameNumber`, `First`, `LastLoser`, `Winner`, `UndoRequestedBy`, `PlayStartIndex`;
    - methods: `Submit`, `Emit`, `RecordGame`, `BeginGame`.
  - `Sideboarding.Apply(Deck, SubmitSideboard, out string? error) : Deck?`
  - Test helpers:
    - `TestDecks.Jinx(params string[] battlefields)`, `TestDecks.Setup(MatchFormat, ulong seed = 7)`;
    - extensions `match.Accept(...)`, `match.Decision<T>()`, `match.ToMulligan()`, `match.ToPlay()`.

- [x] **Step 1: Add the deck test data**

In `tests/CromoBound.Engine.Tests/EngineTestDb.cs`, add these entries at the end of the `Cards` list (before the closing `];`):
```csharp
        .. Enumerable.Range(1, 14).Select(i => Unit($"filler-{i}", i % 2 == 0 ? Domain.Fury : Domain.Chaos, energy: 1, might: 1)),
        Simple("bf-c", CardType.Battlefield, []),
        Simple("bf-d", CardType.Battlefield, []),
        Simple("bf-e", CardType.Battlefield, []),
        Simple("bf-f", CardType.Battlefield, []),
```

At the end of the same file, add:
```csharp
/// <summary>Legal decks built from <see cref="EngineTestDb"/>.</summary>
internal static class TestDecks
{
    /// <summary>Jinx legend and champion, filler-1..13 × 3, 6 + 6 runes, the given battlefields, filler-14 × 3 in the sideboard.</summary>
    public static Deck Jinx(params string[] battlefields) => new()
    {
        Name = "Jinx",
        Legend = "p-jinx-legend",
        Champion = "p-jinx-champ",
        Main = [.. Enumerable.Range(1, 13).Select(i => new DeckEntry { Printing = $"p-filler-{i}", Count = 3 })],
        Runes = [new DeckEntry { Printing = "p-fury-rune", Count = 6 }, new DeckEntry { Printing = "p-chaos-rune", Count = 6 }],
        Battlefields = [.. battlefields.Select(b => $"p-{b}")],
        Sideboard = [new DeckEntry { Printing = "p-filler-14", Count = 3 }],
    };

    public static MatchSetup Setup(MatchFormat format, ulong seed = 7) =>
        new(format, Jinx("bf-a", "bf-b", "bf-c"), Jinx("bf-d", "bf-e", "bf-f"), seed);
}
```
Add `using CromoBound.Engine.Matches;` to the top of the file.

Create `tests/CromoBound.Engine.Tests/MatchTestExtensions.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Tests;

internal static class MatchTestExtensions
{
    public static SubmitResult Accept(this Match match, PlayerId player, PlayerAction action)
    {
        var result = match.Submit(player, action);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    public static T Decision<T>(this Match match) where T : PendingDecision => Assert.IsType<T>(match.Pending);

    /// <summary>The roll-off winner plays first, then (if asked) both players keep their decks: up to the first mulligan.</summary>
    public static Match ToMulligan(this Match match)
    {
        if (match.Pending is ChoosePlayOrderDecision order) match.Accept(order.Player, new ChoosePlayOrder(true));
        while (match.Pending is SideboardDecision sideboard) match.Accept(sideboard.Players[0], new SubmitSideboard());
        return match;
    }

    /// <summary>Up to the first decision of the first turn: nobody mulligans.</summary>
    public static Match ToPlay(this Match match)
    {
        match.ToMulligan();
        while (match.Pending is MulliganDecision mulligan) match.Accept(mulligan.Player, new Mulligan());
        return match;
    }
}
```

- [x] **Step 2: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/MatchBo1Tests.cs`:
```csharp
using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class MatchBo1Tests
{
    private static Match NewMatch(ulong seed = 7) => Match.Create(TestDecks.Setup(MatchFormat.Bo1, seed), EngineTestDb.Create()).Match!;

    [Fact]
    public void An_illegal_deck_creates_no_match_and_reports_why()
    {
        var setup = TestDecks.Setup(MatchFormat.Bo1) with { Player2Deck = TestDecks.Jinx("bf-d", "bf-e") };

        var result = Match.Create(setup, EngineTestDb.Create());

        Assert.Null(result.Match);
        Assert.True(result.Reports[0].IsLegal);
        Assert.Contains(result.Reports[1].Issues, i => i.Code == DeckIssueCode.BattlefieldCount);
    }

    [Fact]
    public void Battlefields_are_random_and_the_roll_off_winner_chooses_play_order()
    {
        var match = NewMatch();

        var chosen = Assert.Single(match.Events.OfType<BattlefieldsChosen>()).Printings;
        Assert.Contains(chosen[0], new[] { "p-bf-a", "p-bf-b", "p-bf-c" });
        Assert.Contains(chosen[1], new[] { "p-bf-d", "p-bf-e", "p-bf-f" });
        var rolls = match.Events.OfType<D20Rolled>().ToList();
        Assert.All(rolls.SkipLast(2).Chunk(2), tie => Assert.Equal(tie[0].Value, tie[1].Value));
        var final = rolls.TakeLast(2).ToList();
        Assert.NotEqual(final[0].Value, final[1].Value);
        Assert.Equal(final.MaxBy(r => r.Value)!.Player, match.Decision<ChoosePlayOrderDecision>().Player);
    }

    [Fact]
    public void Bo1_goes_through_sideboarding_and_mulligan_to_the_first_turn()
    {
        var match = NewMatch();
        var first = match.Decision<ChoosePlayOrderDecision>().Player;
        match.Accept(first, new ChoosePlayOrder(true));

        Assert.Equal(new[] { P1, P2 }, match.Decision<SideboardDecision>().Players);
        match.Accept(P2, new SubmitSideboard());
        match.Accept(P1, new SubmitSideboard { Swaps = [new SideboardSwap("p-filler-1", "p-filler-14")] });
        Assert.Equal(2, match.CurrentDecks[0].Main.Single(e => e.Printing == "p-filler-1").Count);
        Assert.Equal(1, match.CurrentDecks[0].Main.Single(e => e.Printing == "p-filler-14").Count);

        var mulligan = match.Decision<MulliganDecision>();
        Assert.Equal(first, mulligan.Player);
        Assert.Equal(4, mulligan.Hand.Count);
        var game = match.Game!;
        var asideCards = mulligan.Hand.Take(2).Select(id => game.State[id].CardId).Order().ToList();
        match.Accept(first, new Mulligan { SetAside = [mulligan.Hand[0], mulligan.Hand[1]] });
        var deck = game.State.At(Place.MainDeck(first));
        Assert.Equal(asideCards, deck.TakeLast(2).Select(id => game.State[id].CardId).Order().ToList());
        match.Accept(match.Decision<MulliganDecision>().Player, new Mulligan());

        Assert.Equal(MatchStage.Playing, match.Stage);
        Assert.Equal(first, game.State.Turn.TurnPlayer);
        Assert.Equal(5, game.State.At(Place.Hand(first)).Count);
        Assert.IsType<PriorityDecision>(match.Pending);
    }

    [Fact]
    public void A_mulligan_of_more_than_two_cards_is_rejected()
    {
        var match = NewMatch().ToMulligan();
        var mulligan = match.Decision<MulliganDecision>();

        var result = match.Submit(mulligan.Player, new Mulligan { SetAside = [.. mulligan.Hand.Take(3)] });

        Assert.Equal(RejectionCode.UnexpectedAction, result.Rejection!.Code);
        Assert.IsType<MulliganDecision>(match.Pending);
    }

    [Fact]
    public void Game_actions_go_to_the_game()
    {
        var match = NewMatch().ToPlay();
        var player = match.Decision<PriorityDecision>().Player;

        match.Accept(player, new EndTurn());

        Assert.Equal(2, match.Game!.State.Turn.Number);
        Assert.Equal(1, match.Events.OfType<TurnStarted>().Count(t => t.Number == 2));
    }

    [Fact]
    public void The_same_seed_gives_the_same_match_events()
    {
        static string Describe(Match m) => string.Join("|", m.ToPlay().Events.Select(e => $"{e.Sequence}:{e.GetType().Name}"));

        Assert.Equal(Describe(NewMatch(seed: 9)), Describe(NewMatch(seed: 9)));
    }
}
```

- [x] **Step 3: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~MatchBo1Tests"`
Expected: FAIL (compilation errors: `Match.Create`, `TestDecks` … not found).

- [x] **Step 4: Let the match collect game events**

In `src/CromoBound.Engine/Rules/Game.cs`, replace the body of `Continue()` and add `TakeEvents()` after it:
```csharp
    public IReadOnlyList<GameEvent> Continue()
    {
        RunLoop();
        return TakeEvents();
    }

    /// <summary>Events produced since the last call (e.g. by setup draws before <see cref="Start"/>).</summary>
    internal IReadOnlyList<GameEvent> TakeEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }
```

- [x] **Step 5: Implement the core**

Create `src/CromoBound.Engine/Matches/MatchCore.cs`:
```csharp
using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Random;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Matches;

/// <summary>A match's state and the rules around its games. Undo replaces it with a fresh replay.</summary>
internal sealed partial class MatchCore
{
    public static readonly IReadOnlyList<PlayerId> Players = [new(0), new(1)];

    private readonly List<GameEvent> _newEvents = [];
    private PendingDecision? _pending;
    private Func<PlayerId, PlayerAction, Rejection?>? _handler;
    private int _nextSequence = 1;

    public MatchCore(MatchSetup setup, CardDatabase db)
    {
        Setup = setup;
        Db = db;
        Rng = new SeededRandom(setup.Seed);
        Decks = [setup.Player1Deck, setup.Player2Deck];
        Available = [[.. setup.Player1Deck.Battlefields], [.. setup.Player2Deck.Battlefields]];
        BeginGame();
        Flush();
    }

    public MatchSetup Setup { get; }
    public CardDatabase Db { get; }

    /// <summary>One generator for the whole match, shared with every game, so a replay is identical.</summary>
    public SeededRandom Rng { get; }

    /// <summary>Current decks (after sideboarding), by player index. The registered decks stay in <see cref="Setup"/>.</summary>
    public Deck[] Decks { get; }

    /// <summary>Battlefield printings each player may still use this match.</summary>
    public List<string>[] Available { get; }

    public int[] Wins { get; } = new int[2];
    public string?[] Picks { get; } = new string?[2];
    public int GameNumber { get; private set; }
    public MatchStage Stage { get; private set; }
    public Game? Game { get; private set; }
    public PlayerId First { get; private set; }
    public PlayerId? LastLoser { get; private set; }
    public PlayerId? Winner { get; private set; }
    public PlayerId? UndoRequestedBy { get; set; }

    /// <summary>Log index of the current game's first action after the mulligans; -1 before play starts.</summary>
    public int PlayStartIndex { get; private set; } = -1;

    public List<LoggedAction> Log { get; } = [];

    /// <summary>Every event of the match, numbered match-wide.</summary>
    public List<GameEvent> Events { get; } = [];

    public MatchResult Result => new([.. Wins], Winner);

    public PendingDecision? Pending =>
        UndoRequestedBy is { } requester ? new ConfirmUndoDecision(Opponent(requester), requester)
        : _pending ?? (Stage == MatchStage.Playing ? Game!.Pending : null);

    public static PlayerId Opponent(PlayerId player) => new(1 - player.Index);

    public SubmitResult Submit(PlayerId player, PlayerAction action)
    {
        if (Winner is not null) return SubmitResult.Reject(RejectionCode.MatchOver, "The match is over.");
        if (UndoRequestedBy is not null && action is not Concede)
            return SubmitResult.Reject(RejectionCode.UnexpectedAction, "Answer the undo request first.");

        var rejection = action switch
        {
            _ when _pending is not null => AnswerMatchDecision(player, action),
            _ when Stage == MatchStage.Playing => SubmitToGame(player, action),
            _ => Reject(RejectionCode.UnexpectedAction, "Nothing is waiting for that action."),
        };
        if (rejection is not null)
        {
            _newEvents.Clear();
            return new SubmitResult(false, rejection, []);
        }
        Log.Add(new LoggedAction(player, action));
        if (Stage == MatchStage.Playing && PlayStartIndex < 0) PlayStartIndex = Log.Count;
        return new SubmitResult(true, null, Flush());
    }

    internal static Rejection Reject(RejectionCode code, string message) => new(code, message);

    /// <summary>Adds a match event after any game events produced before it, keeping the order.</summary>
    internal void Emit(GameEvent gameEvent)
    {
        PullGameEvents();
        _newEvents.Add(gameEvent);
    }

    private void PullGameEvents()
    {
        if (Game is not null) _newEvents.AddRange(Game.TakeEvents());
    }

    /// <summary>Numbers the new events match-wide, keeps them, and returns them.</summary>
    private IReadOnlyList<GameEvent> Flush()
    {
        PullGameEvents();
        foreach (var gameEvent in _newEvents) gameEvent.Sequence = _nextSequence++;
        Events.AddRange(_newEvents);
        var result = _newEvents.ToList();
        _newEvents.Clear();
        return result;
    }

    private void Ask(PendingDecision decision, Func<PlayerId, PlayerAction, Rejection?> handler)
    {
        _pending = decision;
        _handler = handler;
    }

    private Rejection? AnswerMatchDecision(PlayerId player, PlayerAction action)
    {
        if (!_pending!.Players.Contains(player)) return Reject(RejectionCode.NotYourDecision, $"{player} is not deciding now.");
        var (decision, handler) = (_pending, _handler!);
        _pending = null;
        _handler = null;
        var rejection = handler(player, action);
        if (rejection is not null && _pending is null) (_pending, _handler) = (decision, handler);
        return rejection;
    }

    private Rejection? SubmitToGame(PlayerId player, PlayerAction action)
    {
        var result = Game!.Submit(player, action);
        if (!result.Accepted) return result.Rejection;
        _newEvents.AddRange(result.Events);
        AfterGameChange();
        return null;
    }
}
```

Create `src/CromoBound.Engine/Matches/MatchCore.PreGame.cs`:
```csharp
using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Matches;

/// <summary>The pre-game steps of spec §9.3: battlefields, play order, sideboarding, setup, mulligan.</summary>
internal sealed partial class MatchCore
{
    internal void BeginGame()
    {
        GameNumber++;
        Game = null;
        PlayStartIndex = -1;
        Array.Clear(Picks);
        Stage = MatchStage.PickBattlefields;
        Emit(new GameStarted(GameNumber));
        if (Setup.Format == MatchFormat.Bo1)
        {
            foreach (var player in Players) Picks[player.Index] = Available[player.Index][Rng.NextInt(Available[player.Index].Count)];
            BattlefieldsPicked();
            return;
        }
        AskPicks();
    }

    /// <summary>Bo3: each player picks an unused battlefield; a single remaining option is picked automatically.</summary>
    private void AskPicks()
    {
        foreach (var player in Players)
            if (Picks[player.Index] is null && Available[player.Index].Count == 1) Picks[player.Index] = Available[player.Index][0];
        var waiting = Players.Where(p => Picks[p.Index] is null).ToList();
        if (waiting.Count == 0)
        {
            BattlefieldsPicked();
            return;
        }
        Ask(new PickBattlefieldDecision(waiting, [.. waiting.Select(p => new BattlefieldChoice(p, [.. Available[p.Index]]))]), (player, action) =>
        {
            if (action is not PickBattlefield pick || !Available[player.Index].Contains(pick.Printing))
                return Reject(RejectionCode.UnexpectedAction, "Pick one of your unused battlefields.");
            Picks[player.Index] = pick.Printing;
            AskPicks();
            return null;
        });
    }

    private void BattlefieldsPicked()
    {
        Emit(new BattlefieldsChosen([.. Picks.Select(p => p!)]));
        Stage = MatchStage.PlayOrder;
        var chooser = GameNumber == 1 ? RollOff() : LastLoser!.Value;
        Ask(new ChoosePlayOrderDecision(chooser), (_, action) =>
        {
            if (action is not ChoosePlayOrder order) return Reject(RejectionCode.UnexpectedAction, "Choose to play first or last.");
            First = order.First ? chooser : Opponent(chooser);
            Emit(new PlayOrderChosen(chooser, First));
            AfterPlayOrder();
            return null;
        });
    }

    /// <summary>Each player rolls a d20 until the results differ; the higher roll chooses play order.</summary>
    private PlayerId RollOff()
    {
        while (true)
        {
            var rolls = Players.Select(p => (Player: p, Value: Rng.RollD20())).ToList();
            foreach (var (player, value) in rolls) Emit(new D20Rolled(player, value));
            if (rolls[0].Value != rolls[1].Value) return rolls.MaxBy(r => r.Value).Player;
        }
    }

    /// <summary>Bo1 sideboards before its only game; Bo3 from game 2 on.</summary>
    private void AfterPlayOrder()
    {
        if (Setup.Format == MatchFormat.Bo3 && GameNumber == 1)
        {
            SetUpGame();
            return;
        }
        Stage = MatchStage.Sideboarding;
        AskSideboards([.. Players]);
    }

    private void AskSideboards(List<PlayerId> waiting)
    {
        if (waiting.Count == 0)
        {
            SetUpGame();
            return;
        }
        Ask(new SideboardDecision(waiting), (player, action) =>
        {
            if (action is not SubmitSideboard submit) return Reject(RejectionCode.UnexpectedAction, "Submit your sideboard swaps, or none.");
            var current = Decks[player.Index];
            var deck = Sideboarding.Apply(current, submit, out var error);
            if (deck is null) return Reject(RejectionCode.InvalidSideboard, error!);
            var report = DeckValidator.Validate(deck, Db);
            if (!report.IsLegal) return Reject(RejectionCode.InvalidSideboard, string.Join(" ", report.Issues.Select(i => i.Message)));
            Decks[player.Index] = deck;
            Emit(new SideboardChanged(player, submit.Swaps.Count, deck.Champion != current.Champion));
            AskSideboards([.. waiting.Where(p => p != player)]);
            return null;
        });
    }

    /// <summary>Creates the game's cards from the current decks and picked battlefields, shuffles, and deals 4 each (CR 110–116).</summary>
    private void SetUpGame()
    {
        var state = new GameState(Players.Count, Rng);
        Game = new Game(state, Db);
        foreach (var player in Players)
        {
            var deck = Decks[player.Index];
            Create(state, deck.Legend, player, Place.LegendZone(player));
            Create(state, deck.Champion, player, Place.ChampionZone(player));
            foreach (var entry in deck.Main)
                for (var i = 0; i < entry.Count; i++) Create(state, entry.Printing, player, Place.MainDeck(player));
            foreach (var entry in deck.Runes)
                for (var i = 0; i < entry.Count; i++) Create(state, entry.Printing, player, Place.RuneDeck(player));
            var battlefield = Create(state, Picks[player.Index]!, player, Place.BattlefieldCard(player.Index));
            state.Battlefields.Add(new BattlefieldState(player.Index, battlefield));
            state.Shuffle(Place.MainDeck(player));
            state.Shuffle(Place.RuneDeck(player));
        }
        Stage = MatchStage.Mulligan;
        Game.Draw(First, 4);
        Game.Draw(Opponent(First), 4);
        AskMulligan(First);
    }

    private ObjectId Create(GameState state, string printing, PlayerId owner, Place place) =>
        state.Create(Db.Printings[printing].CardId, printing, owner, place);

    /// <summary>CR 117, in turn order: set aside up to 2, draw that many, then recycle the set-aside cards to the bottom in random order.</summary>
    private void AskMulligan(PlayerId player)
    {
        var state = Game!.State;
        Ask(new MulliganDecision(player, [.. state.At(Place.Hand(player))]), (_, action) =>
        {
            if (action is not Mulligan mulligan) return Reject(RejectionCode.UnexpectedAction, "Choose up to 2 cards to set aside, or none.");
            var hand = state.At(Place.Hand(player));
            if (mulligan.SetAside.Count > 2 || mulligan.SetAside.Distinct().Count() != mulligan.SetAside.Count
                || mulligan.SetAside.Any(id => !hand.Contains(id)))
                return Reject(RejectionCode.UnexpectedAction, "Set aside at most 2 different cards from your hand.");
            Game.Draw(player, mulligan.SetAside.Count);
            var aside = mulligan.SetAside.ToList();
            Rng.Shuffle(aside);
            foreach (var id in aside) Game.MoveCard(id, Place.MainDeck(player), DeckPosition.Bottom);
            Emit(new MulliganTaken(player, aside.Count));
            if (player == First) AskMulligan(Opponent(First));
            else StartPlay();
            return null;
        });
    }

    private void StartPlay()
    {
        Stage = MatchStage.Playing;
        PullGameEvents();
        _newEvents.AddRange(Game!.Start(First));
        AfterGameChange();
    }
}
```

Create `src/CromoBound.Engine/Matches/MatchCore.Results.cs`:
```csharp
using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Matches;

internal sealed partial class MatchCore
{
    private void AfterGameChange()
    {
        if (Game?.Outcome is { } outcome) RecordGame(outcome);
    }

    /// <summary>Records the game. Bo3 removes the battlefields used in it. The match ends at 1 win (Bo1) or 2 (Bo3); otherwise the next game begins.</summary>
    internal void RecordGame(GameOutcome outcome)
    {
        Emit(new GameRecorded(GameNumber, outcome.Winner, outcome.Reason));
        if (outcome.Winner is { } winner)
        {
            Wins[winner.Index]++;
            LastLoser = Opponent(winner);
        }
        if (Setup.Format == MatchFormat.Bo3)
            foreach (var player in Players) Available[player.Index].Remove(Picks[player.Index]!);

        var needed = Setup.Format == MatchFormat.Bo1 ? 1 : 2;
        foreach (var player in Players)
        {
            if (Wins[player.Index] < needed) continue;
            Winner = player;
            Stage = MatchStage.Over;
            Emit(new MatchEnded(player));
            return;
        }
        BeginGame();
    }
}
```

Create `src/CromoBound.Engine/Matches/Sideboarding.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Matches;

/// <summary>Applies sideboard swaps to a deck (TR 403). Only the main deck, champion and sideboard change.</summary>
internal static class Sideboarding
{
    /// <summary>The new deck, or null with the reason when a swap names a card that isn't there.</summary>
    public static Deck? Apply(Deck deck, SubmitSideboard submit, out string? error)
    {
        var main = Count(deck.Main);
        var side = Count(deck.Sideboard);
        foreach (var swap in submit.Swaps)
        {
            if (main.GetValueOrDefault(swap.Out) < 1) return Fail($"'{swap.Out}' is not in your main deck.", out error);
            if (side.GetValueOrDefault(swap.In) < 1) return Fail($"'{swap.In}' is not in your sideboard.", out error);
            main[swap.Out]--;
            side[swap.Out] = side.GetValueOrDefault(swap.Out) + 1;
            side[swap.In]--;
            main[swap.In] = main.GetValueOrDefault(swap.In) + 1;
        }

        var champion = deck.Champion;
        if (submit.Champion is { } chosen && chosen != deck.Champion)
        {
            var source = main.GetValueOrDefault(chosen) > 0 ? main : side.GetValueOrDefault(chosen) > 0 ? side : null;
            if (source is null) return Fail($"'{chosen}' is not in your main deck or sideboard.", out error);
            source[chosen]--;
            source[deck.Champion] = source.GetValueOrDefault(deck.Champion) + 1;
            champion = chosen;
        }

        error = null;
        return deck with { Champion = champion, Main = Entries(main), Sideboard = Entries(side) };
    }

    private static Dictionary<string, int> Count(IEnumerable<DeckEntry> entries)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in entries) counts[entry.Printing] = counts.GetValueOrDefault(entry.Printing) + entry.Count;
        return counts;
    }

    private static IReadOnlyList<DeckEntry> Entries(Dictionary<string, int> counts) =>
        [.. counts.Where(c => c.Value > 0).OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => new DeckEntry { Printing = c.Key, Count = c.Value })];

    private static Deck? Fail(string message, out string? error)
    {
        error = message;
        return null;
    }
}
```

Replace all of `src/CromoBound.Engine/Matches/Match.cs` with:
```csharp
using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Matches;

/// <summary>A Bo1 or Bo3 match between two players (spec §4). The server's entry point to the engine.</summary>
public sealed partial class Match
{
    private MatchCore _core;

    private Match(MatchCore core) => _core = core;

    /// <summary>Validates both decks; returns no match when either is illegal.</summary>
    public static MatchCreateResult Create(MatchSetup setup, CardDatabase db)
    {
        DeckReport[] reports = [DeckValidator.Validate(setup.Player1Deck, db), DeckValidator.Validate(setup.Player2Deck, db)];
        return reports.All(r => r.IsLegal) ? new(new Match(new MatchCore(setup, db)), reports) : new(null, reports);
    }

    public PendingDecision? Pending => _core.Pending;
    public MatchResult Result => _core.Result;
    public MatchStage Stage => _core.Stage;
    public int GameNumber => _core.GameNumber;

    /// <summary>The current game, for the server. Players see <c>ViewFor</c>.</summary>
    public Game? Game => _core.Game;

    /// <summary>Every event of the match, unfiltered. Server-side only; players see their view's log.</summary>
    public IReadOnlyList<GameEvent> Events => _core.Events;

    /// <summary>Each player's deck after sideboarding.</summary>
    public IReadOnlyList<Deck> CurrentDecks => _core.Decks;

    public SubmitResult Submit(PlayerId player, PlayerAction action) => _core.Submit(player, action);
}
```

- [x] **Step 6: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests).

- [x] **Step 7: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): add match core with the Bo1 pre-game"
```

---

### Task 3: Bo3, conceding and the end of the match

**Files:**
- Modify: `src/CromoBound.Engine/Matches/MatchCore.cs` (route `Concede`), `src/CromoBound.Engine/Matches/MatchCore.Results.cs` (`ConcedeGame`)
- Modify: `tests/CromoBound.Engine.Tests/EngineTestDb.cs` (a second Jinx champion), `tests/CromoBound.Engine.Tests/MatchTestExtensions.cs`
- Test: `tests/CromoBound.Engine.Tests/MatchBo3Tests.cs`

**Interfaces:**
- Consumes: Task 2 (`MatchCore`, `RecordGame`, `BeginGame`); `Game.End` (Plan B, internal).
- Produces: `MatchCore.ConcedeGame(PlayerId) : Rejection?`; test helper `match.PickFirstBattlefields()`.

- [x] **Step 1: Add test helpers and data**

In `tests/CromoBound.Engine.Tests/EngineTestDb.cs`, add to the end of the `Cards` list:
```csharp
        Unit("jinx-alt", Domain.Chaos, energy: 2, might: 2) with { Supertype = Supertype.Champion, Tags = ["Jinx"] },
```

In `tests/CromoBound.Engine.Tests/MatchTestExtensions.cs`, add to `MatchTestExtensions`:
```csharp
    /// <summary>Bo3: every waiting player picks their first offered battlefield.</summary>
    public static Match PickFirstBattlefields(this Match match)
    {
        while (match.Pending is PickBattlefieldDecision pick)
        {
            var player = pick.Players[0];
            match.Accept(player, new PickBattlefield(pick.Choices.Single(c => c.Player == player).Printings[0]));
        }
        return match;
    }
```

- [x] **Step 2: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/MatchBo3Tests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class MatchBo3Tests
{
    private static Match NewBo3(MatchSetup? setup = null) =>
        Match.Create(setup ?? TestDecks.Setup(MatchFormat.Bo3), EngineTestDb.Create()).Match!;

    private static PlayerId Other(PlayerId player) => player == P1 ? P2 : P1;

    /// <summary>Plays the current game's pre-game, then the given player concedes it.</summary>
    private static void LoseGame(Match match, PlayerId loser)
    {
        match.PickFirstBattlefields().ToPlay();
        match.Accept(loser, new Concede());
    }

    [Fact]
    public void Both_players_pick_an_unused_battlefield_in_any_order()
    {
        var match = NewBo3();
        var pick = match.Decision<PickBattlefieldDecision>();
        Assert.Equal(new[] { P1, P2 }, pick.Players);
        Assert.Equal(new[] { "p-bf-a", "p-bf-b", "p-bf-c" }, pick.Choices.Single(c => c.Player == P1).Printings);

        Assert.Equal(RejectionCode.UnexpectedAction, match.Submit(P1, new PickBattlefield("p-bf-d")).Rejection!.Code);
        match.Accept(P2, new PickBattlefield("p-bf-e"));
        Assert.Equal(new[] { P1 }, match.Decision<PickBattlefieldDecision>().Players);
        match.Accept(P1, new PickBattlefield("p-bf-a"));

        Assert.Equal(new[] { "p-bf-a", "p-bf-e" }, Assert.Single(match.Events.OfType<BattlefieldsChosen>()).Printings);
        Assert.IsType<ChoosePlayOrderDecision>(match.Pending);
    }

    [Fact]
    public void Game_one_of_a_bo3_has_no_sideboarding()
    {
        var match = NewBo3().PickFirstBattlefields();
        var order = match.Decision<ChoosePlayOrderDecision>();

        match.Accept(order.Player, new ChoosePlayOrder(true));

        Assert.IsType<MulliganDecision>(match.Pending);
    }

    [Fact]
    public void Conceding_records_the_game_removes_its_battlefields_and_the_loser_chooses_next()
    {
        var match = NewBo3();
        match.PickFirstBattlefields().ToPlay();
        var loser = match.Game!.State.Turn.TurnPlayer;

        match.Accept(loser, new Concede());

        var recorded = Assert.Single(match.Events.OfType<GameRecorded>());
        Assert.Equal((Other(loser), GameEndReason.Concede), (recorded.Winner!.Value, recorded.Reason));
        Assert.Equal(2, match.GameNumber);
        Assert.Equal(new[] { "p-bf-b", "p-bf-c" }, match.Decision<PickBattlefieldDecision>().Choices.Single(c => c.Player == P1).Printings);
        match.PickFirstBattlefields();
        Assert.Equal(loser, match.Decision<ChoosePlayOrderDecision>().Player);
        match.Accept(loser, new ChoosePlayOrder(false));
        Assert.IsType<SideboardDecision>(match.Pending);
    }

    [Fact]
    public void Illegal_sideboard_is_rejected_and_can_be_resubmitted()
    {
        var match = NewBo3();
        LoseGame(match, P2);
        match.PickFirstBattlefields();
        match.Accept(P2, new ChoosePlayOrder(true));

        var missing = match.Submit(P1, new SubmitSideboard { Swaps = [new SideboardSwap("p-filler-14", "p-filler-1")] });
        var illegal = match.Submit(P1, new SubmitSideboard { Champion = "p-filler-2" });

        Assert.Equal(RejectionCode.InvalidSideboard, missing.Rejection!.Code);
        Assert.Equal(RejectionCode.InvalidSideboard, illegal.Rejection!.Code);
        Assert.Contains("champion", illegal.Rejection.Message, StringComparison.OrdinalIgnoreCase);
        match.Accept(P1, new SubmitSideboard { Swaps = [new SideboardSwap("p-filler-1", "p-filler-14")] });
        Assert.Equal(new[] { P2 }, match.Decision<SideboardDecision>().Players);
    }

    [Fact]
    public void The_champion_can_be_switched_while_sideboarding()
    {
        var p1Deck = TestDecks.Jinx("bf-a", "bf-b", "bf-c") with
        {
            Sideboard = [new DeckEntry { Printing = "p-filler-14", Count = 3 }, new DeckEntry { Printing = "p-jinx-alt", Count = 1 }],
        };
        var match = NewBo3(TestDecks.Setup(MatchFormat.Bo3) with { Player1Deck = p1Deck });
        LoseGame(match, P1);
        match.PickFirstBattlefields();
        match.Accept(P1, new ChoosePlayOrder(true));

        match.Accept(P1, new SubmitSideboard { Champion = "p-jinx-alt" });

        Assert.Equal("p-jinx-alt", match.CurrentDecks[0].Champion);
        Assert.Contains(match.CurrentDecks[0].Sideboard, e => e.Printing == "p-jinx-champ");
        Assert.Equal("p-jinx-champ", p1Deck.Champion);
    }

    [Fact]
    public void Two_wins_end_the_match_and_a_single_battlefield_left_is_picked_automatically()
    {
        var match = NewBo3();
        LoseGame(match, P2);
        LoseGame(match, P1);

        Assert.Equal(3, match.GameNumber);
        Assert.IsNotType<PickBattlefieldDecision>(match.Pending);
        Assert.Equal(new[] { "p-bf-c", "p-bf-f" }, match.Events.OfType<BattlefieldsChosen>().Last().Printings);

        match.ToPlay();
        match.Accept(P2, new Concede());

        Assert.Equal(MatchStage.Over, match.Stage);
        Assert.Equal(P1, match.Result.Winner);
        Assert.Equal(new[] { 2, 1 }, match.Result.GameWins);
        Assert.Single(match.Events.OfType<MatchEnded>());
        Assert.Equal(RejectionCode.MatchOver, match.Submit(P1, new Concede()).Rejection!.Code);
    }
}
```

- [x] **Step 3: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~MatchBo3Tests"`
Expected: FAIL. The two tests that never concede pass; the rest fail because `Concede` is rejected.

- [x] **Step 4: Implement conceding**

In `src/CromoBound.Engine/Matches/MatchCore.cs`, add this arm first in the `switch` inside `Submit`:
```csharp
            Concede => ConcedeGame(player),
```

In `src/CromoBound.Engine/Matches/MatchCore.Results.cs`, add to the class:
```csharp
    /// <summary>The player concedes the current game at any moment, even before it starts: the opponent wins it.</summary>
    private Rejection? ConcedeGame(PlayerId player)
    {
        UndoRequestedBy = null;
        _pending = null;
        _handler = null;
        var winner = Opponent(player);
        if (Game is { Outcome: null } game) game.End(winner, GameEndReason.Concede);
        RecordGame(new GameOutcome(winner, GameEndReason.Concede));
        return null;
    }
```

- [x] **Step 5: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests).

- [x] **Step 6: Commit**

```bash
git add src/CromoBound.Engine/Matches tests/CromoBound.Engine.Tests
git commit -m "feat(engine): play Bo3 matches with conceding and sideboarding"
```

---

### Task 4: Manual actions

**Files:**
- Modify: `src/CromoBound.Engine/Rules/GameTask.cs`, `Rules/Game.cs` (`RunLoop`), `Rules/Game.TurnPoints.cs` (tasks that wait for the chain)
- Create: `src/CromoBound.Engine/Rules/Game.Manual.cs`
- Modify: `src/CromoBound.Engine/Matches/MatchCore.cs` (route manual actions)
- Test: `tests/CromoBound.Engine.Tests/ManualTests.cs`

**Interfaces:**
- Consumes: Plan B mutations, `AskPay`, `AfterResolution`, `ChainEmptied`; Task 1 manual action and event types.
- Produces:
  - `Game.SubmitManual(PlayerId, ManualAction) : SubmitResult` (public)
  - `GameTask.WaitsForEmptyChain`, `AbilityTask`
  - `MatchCore.ApplyManual`

**Behavior (spec §8):**
- Either player, at any time during play, on any object. Manual actions are rejected before play starts.
- **After a manual action:**
  - The pending decision is rebuilt: the task or loop that raised it asks again with fresh options. A `ResolveManually` decision stays as it is.
  - Cleanup runs.
- **Turn-point pauses:** a `TurnPointTask` *waits while the chain is open*. An ability added during a turn-point pause (e.g. the trigger of the card that caused it) is handled first: priority, passes, resolution by hand. Then the pause asks again.

- [x] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/ManualTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ManualTests
{
    private static SubmitResult Manual(Game game, PlayerId player, ManualAction action)
    {
        var result = game.SubmitManual(player, action);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    [Fact]
    public void Manual_damage_kills_through_cleanup_and_keeps_the_decision()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        var result = Manual(engine, P2, new ManualDamage(unit, 2));

        Assert.False(game.State.Exists(unit));
        Assert.Contains(result.Events, e => e is ManualActionTaken { Action: ManualDamage });
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Manual_damage_during_a_turn_point_is_cleaned_up_right_away()
    {
        var game = new TestGame();
        game.Put("dawn-relic", Place.Base(P1));
        var enemy = game.Put("unit-2", Place.Base(P2));
        var engine = game.Start();
        Assert.IsType<TurnPointDecision>(engine.Pending);

        Manual(engine, P1, new ManualDamage(enemy, 2));

        Assert.False(game.State.Exists(enemy));
        Assert.Equal(P1, engine.Decision<TurnPointDecision>().Player);
    }

    [Fact]
    public void Manual_actions_are_allowed_while_resolving_by_hand()
    {
        var game = new TestGame();
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var discard = game.Put("unit-3", Place.Hand(P2));
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Manual(engine, P2, new ManualMoveCard(discard, Place.Trash(P2)));

        Assert.IsType<ResolveManuallyDecision>(engine.Pending);
        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "unit-3");
        engine.Accept(P1, new ResolveDone());
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void Moving_a_unit_to_a_battlefield_contests_it()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Manual(engine, P1, new ManualMoveCard(unit, Place.Battlefield(1)));

        Assert.Equal(1, game.State.Showdown!.Battlefield);
        Assert.True(engine.Decision<PriorityDecision>().CanPass);
    }

    [Fact]
    public void Tokens_points_xp_pool_and_statuses_can_be_set_by_hand()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Manual(engine, P1, new ManualCreateToken("token-recruit", Place.Base(P1), P1));
        Manual(engine, P1, new ManualAdjustPoints(P1, 3));
        Manual(engine, P1, new ManualAdjustXp(P1, 2));
        Manual(engine, P1, new ManualAdjustPool(P1, 2, Models.Cards.Domain.Fury, 1));
        Manual(engine, P1, new ManualSetStatus(unit, StatusKind.Stunned, true));
        Manual(engine, P1, new ManualModifyMight(unit, 2, Duration.ThisTurn));

        Assert.Contains(game.State.At(Place.Base(P1)), id => game.State[id].IsToken);
        Assert.Equal(3, game.State.Player(P1).Points);
        Assert.Equal(2, game.State.Player(P1).Xp);
        Assert.Equal(2, game.State.Player(P1).Pool.Energy);
        Assert.Equal(1, game.State.Player(P1).Pool.Power[Models.Cards.Domain.Fury]);
        Assert.True(game.State[unit].Stunned);
        Assert.Equal(4, engine.MightOf(unit));
    }

    [Fact]
    public void Looking_at_the_top_is_private_and_revealing_is_public()
    {
        var game = new TestGame();
        var engine = game.Start();

        var look = Manual(engine, P1, new ManualLookAtTop(P1, PlaceKind.MainDeck, 2));
        var reveal = Manual(engine, P1, new ManualReveal(game.State.At(Place.Hand(P1))[0]));

        Assert.Contains(look.Events, e => e is CardsLookedAt { VisibleTo: { Index: 0 }, CardIds.Count: 2 });
        Assert.Contains(look.Events, e => e is CardsLookedAt { VisibleTo: null, CardIds: null, Count: 2 });
        Assert.Contains(reveal.Events, e => e is CardRevealed { CardId: "unit-2", VisibleTo: null });
    }

    [Fact]
    public void Countering_removes_a_finalized_item_and_trashes_its_card()
    {
        var game = new TestGame();
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);

        Manual(engine, P2, new ManualCounter(game.State.Chain[0].Id));

        Assert.Empty(game.State.Chain);
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.False(engine.Decision<PriorityDecision>().CanPass);
    }

    [Fact]
    public void An_ability_added_by_hand_goes_on_the_chain_and_is_resolved_by_hand()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Manual(engine, P2, new AddAbilityToChain(unit, 1, AbilityKind.Triggered));

        var item = Assert.Single(game.State.Chain);
        Assert.Equal((P1, ChainItemStatus.Finalized), (item.Controller, item.Status));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        var resolve = engine.Decision<ResolveManuallyDecision>();
        Assert.Equal(("unit-2", "unit-2"), (resolve.CardId, resolve.Text));
        engine.Accept(P1, new ResolveDone());
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void An_ability_with_a_cost_asks_its_controller_to_pay()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();

        Manual(engine, P1, new AddAbilityToChain(unit, 1, AbilityKind.Activated) { Cost = new TotalCost(1, []) });

        Assert.Equal(P1, engine.Decision<PayCostDecision>().Player);
        engine.PayWithSuggestion(P1);
        Assert.Equal(ChainItemStatus.Finalized, Assert.Single(game.State.Chain).Status);
    }

    [Fact]
    public void A_trigger_added_during_a_turn_point_resolves_before_the_turn_goes_on()
    {
        var game = new TestGame();
        var relic = game.Put("dawn-relic", Place.Base(P1));
        var engine = game.Start();
        Assert.IsType<TurnPointDecision>(engine.Pending);

        Manual(engine, P1, new AddAbilityToChain(relic, 1, AbilityKind.Triggered));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        engine.Accept(P1, new ResolveDone());

        Assert.Equal(TurnPoint.StartOfBeginning, engine.Decision<TurnPointDecision>().Point);
        engine.Accept(P1, new ContinueTurn());
        Assert.Equal(Phase.Main, game.State.Turn.Phase);
    }

    [Fact]
    public void Invalid_manual_actions_are_rejected()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Assert.Equal(RejectionCode.IllegalLocation, engine.SubmitManual(P1, new ManualMoveCard(unit, Place.Chain)).Rejection!.Code);
        Assert.Equal(RejectionCode.UnknownObject, engine.SubmitManual(P1, new ManualDamage(new ObjectId(999), 1)).Rejection!.Code);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.SubmitManual(P1, new AddAbilityToChain(unit, 5, AbilityKind.Triggered)).Rejection!.Code);
    }
}
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~ManualTests"`
Expected: FAIL (compilation error: `Game` has no `SubmitManual`).

- [x] **Step 3: Let tasks wait for the chain**

In `src/CromoBound.Engine/Rules/GameTask.cs`, add to `GameTask`:
```csharp
    /// <summary>True for tasks that pause while a chain is open (turn points): the chain is handled first, then the task runs again.</summary>
    public virtual bool WaitsForEmptyChain => false;
```

In `src/CromoBound.Engine/Rules/Game.TurnPoints.cs`, add to `TurnPointTask`:
```csharp
    public override bool WaitsForEmptyChain => true;
```

In `src/CromoBound.Engine/Rules/Game.cs`, replace `RunLoop` with:
```csharp
    /// <summary>Handle outstanding tasks, then the chain, the showdown, the Main phase (CR 334–336).
    /// A task that waits for an empty chain is skipped while one is open.</summary>
    private void RunLoop()
    {
        while (Outcome is null && Pending is null)
        {
            var head = _tasks.Count > 0 ? _tasks[0] : null;
            var paused = head is { WaitsForEmptyChain: true } && IsClosed;
            if (_cleanupNeeded && !ResolvingManually && (head is null || !head.Started || head.WaitsForEmptyChain))
            {
                _cleanupNeeded = false;
                Push(new CleanupTask(CleanupMode.Normal));
                continue;
            }
            if (head is not null && !paused)
            {
                head.Started = true;
                if (head.Run(this)) _tasks.Remove(head);
                continue;
            }
            AskPriority();
        }
    }
```

- [x] **Step 4: Implement the manual actions**

Create `src/CromoBound.Engine/Rules/Game.Manual.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Rules;

/// <summary>An ability added by hand: pay its cost (if any), then it's finalized and its controller gets priority.</summary>
internal sealed class AbilityTask(ChainItem item, TotalCost? cost) : GameTask
{
    public ChainItem Item { get; } = item;
    public TotalCost? Cost { get; set; } = cost;
    public bool Paid { get; set; } = cost is null;
    public bool Cancelled { get; set; }

    public override bool Run(Game game) => game.RunAbility(this);
}

public sealed partial class Game
{
    /// <summary>Applies a manual action (spec §8), then lets the rules catch up: the pending decision is asked again with fresh
    /// options (a hand resolution stays as it is) and cleanup runs.</summary>
    public SubmitResult SubmitManual(PlayerId player, ManualAction action)
    {
        if (Outcome is not null) return SubmitResult.Reject(RejectionCode.MatchOver, "The game is over.");
        if (ApplyManual(player, action) is { } rejection) return new SubmitResult(false, rejection, []);
        Emit(new ManualActionTaken(player, action));
        if (Pending is not ResolveManuallyDecision)
        {
            Pending = null;
            _handler = null;
        }
        return new SubmitResult(true, null, Continue());
    }

    private bool IsPlayer(PlayerId player) => player.Index >= 0 && player.Index < State.Players.Count;

    private CardInstance? OnBoard(ObjectId id) => State.Exists(id) && State[id].Place.IsLocation ? State[id] : null;

    private CardInstance? BoardUnit(ObjectId id) => OnBoard(id) is { } instance && IsUnit(instance) ? instance : null;

    private static bool IsDeck(PlaceKind kind) => kind is PlaceKind.MainDeck or PlaceKind.RuneDeck;

    private Rejection? ApplyManual(PlayerId player, ManualAction action)
    {
        switch (action)
        {
            case ManualMoveCard move:
                return ManualMove(move);
            case ManualDamage damage:
                if (BoardUnit(damage.Unit) is null || damage.Amount <= 0) return Reject(RejectionCode.UnknownObject, "Choose a unit in play and a positive amount.");
                DealDamage(damage.Unit, damage.Amount);
                return null;
            case ManualHeal heal:
                if (BoardUnit(heal.Unit) is not { } wounded || heal.Amount <= 0) return Reject(RejectionCode.UnknownObject, "Choose a unit in play and a positive amount.");
                var healed = Math.Min(heal.Amount, wounded.Damage);
                wounded.Damage -= healed;
                Emit(new UnitHealed(heal.Unit, healed));
                MarkDirty();
                return null;
            case ManualSetStatus status:
                if (OnBoard(status.Card) is null) return Reject(RejectionCode.UnknownObject, "Choose a card in play.");
                SetStatus(status.Card, status.Status, status.Value);
                return null;
            case ManualModifyMight might:
                if (BoardUnit(might.Unit) is not { } target) return Reject(RejectionCode.UnknownObject, "Choose a unit in play.");
                target.Modifiers.Add(new MightModifier(might.Amount, might.Duration));
                Emit(new MightModified(might.Unit, might.Amount, might.Duration));
                MarkDirty();
                return null;
            case ManualAdjustPoints points:
                if (!IsPlayer(points.Player)) return Reject(RejectionCode.UnknownObject, "No such player.");
                GainPoints(points.Player, points.Amount);
                return null;
            case ManualAdjustXp xp:
                if (!IsPlayer(xp.Player)) return Reject(RejectionCode.UnknownObject, "No such player.");
                var holder = State.Player(xp.Player);
                holder.Xp = Math.Max(0, holder.Xp + xp.Amount);
                Emit(new XpChanged(xp.Player, holder.Xp));
                return null;
            case ManualAdjustPool pool:
                return AdjustPool(pool);
            case ManualCreateToken token:
                return CreateToken(token);
            case ManualGainControl control:
                if (OnBoard(control.Card) is null || !IsPlayer(control.Player)) return Reject(RejectionCode.UnknownObject, "Choose a card in play and a player.");
                State[control.Card].Controller = control.Player;
                Emit(new ControlGained(control.Card, control.Player));
                MarkDirty();
                return null;
            case ManualShuffle shuffle:
                if (!IsPlayer(shuffle.Owner) || !IsDeck(shuffle.Deck)) return Reject(RejectionCode.UnknownObject, "Choose a player's Main Deck or Rune Deck.");
                State.Shuffle(new Place(shuffle.Deck, shuffle.Owner, null));
                Emit(new DeckShuffled(shuffle.Owner, shuffle.Deck));
                return null;
            case ManualLookAtTop look:
                return LookAtTop(player, look);
            case ManualReveal reveal:
                if (!State.Exists(reveal.Card)) return Reject(RejectionCode.UnknownObject, $"{reveal.Card} doesn't exist.");
                Emit(new CardRevealed(reveal.Card, State[reveal.Card].CardId));
                return null;
            case ManualCounter counter:
                return Counter(counter.ChainItem);
            case AddAbilityToChain ability:
                return AddAbility(ability);
            default:
                return Reject(RejectionCode.UnexpectedAction, $"{action.GetType().Name} is not a manual action.");
        }
    }

    /// <summary>Moves a card anywhere except the chain, battlefield cards and the Legend Zone. A unit arriving at a battlefield
    /// applies Contested; a card put face down can be played from the next turn.</summary>
    private Rejection? ManualMove(ManualMoveCard move)
    {
        if (!State.Exists(move.Card)) return Reject(RejectionCode.UnknownObject, $"{move.Card} doesn't exist.");
        var to = move.Destination;
        if (State[move.Card].Place.Kind is PlaceKind.Chain or PlaceKind.BattlefieldCard or PlaceKind.LegendZone
            || to.Kind is PlaceKind.Chain or PlaceKind.BattlefieldCard or PlaceKind.LegendZone)
            return Reject(RejectionCode.IllegalLocation, "Cards on the chain, battlefields and legends can't be moved by hand (use ManualCounter for the chain).");
        if ((to.Kind is PlaceKind.Battlefield or PlaceKind.Facedown) && (to.Index is not { } index || index < 0 || index >= State.Battlefields.Count))
            return Reject(RejectionCode.IllegalLocation, "No such battlefield.");
        if ((to.IsPlayerPile || to.Kind == PlaceKind.Base) && (to.Player is not { } owner || !IsPlayer(owner)))
            return Reject(RejectionCode.IllegalLocation, "Name the player whose zone it is.");

        if (MoveCard(move.Card, to, move.Position) is not { } moved) return null;
        var card = State[moved];
        if (to.Kind == PlaceKind.Facedown)
        {
            card.Facedown = true;
            card.HiddenOnTurn = State.Turn.Number;
        }
        if (to.Kind == PlaceKind.Battlefield && IsUnit(card)) ApplyContested(card.Controller, to.Index!.Value);
        return null;
    }

    private Rejection? AdjustPool(ManualAdjustPool adjust)
    {
        if (!IsPlayer(adjust.Player)) return Reject(RejectionCode.UnknownObject, "No such player.");
        if (adjust.Power != 0 && adjust.Domain is null) return Reject(RejectionCode.UnexpectedAction, "Name the domain of the power.");
        var pool = State.Player(adjust.Player).Pool;
        pool.Energy = Math.Max(0, pool.Energy + adjust.Energy);
        pool.UniversalPower = Math.Max(0, pool.UniversalPower + adjust.UniversalPower);
        if (adjust.Domain is { } domain) pool.Power[domain] = Math.Max(0, pool.Power.GetValueOrDefault(domain) + adjust.Power);
        Emit(new PoolAdjusted(adjust.Player));
        return null;
    }

    /// <summary>The creator owns and controls the token (CR 183); it enters at a Base or a battlefield.</summary>
    private Rejection? CreateToken(ManualCreateToken token)
    {
        if (!Db.Cards.TryGetValue(token.TokenId, out var card) || card.Supertype != Supertype.Token)
            return Reject(RejectionCode.UnknownObject, $"'{token.TokenId}' is not a token.");
        var location = token.Location;
        var valid = IsPlayer(token.Controller) && location.Kind switch
        {
            PlaceKind.Base => location.Player is { } owner && IsPlayer(owner),
            PlaceKind.Battlefield => location.Index is { } index && index >= 0 && index < State.Battlefields.Count,
            _ => false,
        };
        if (!valid) return Reject(RejectionCode.IllegalLocation, "Tokens enter at a Base or a battlefield.");
        var id = State.Create(token.TokenId, null, token.Controller, location, isToken: true);
        Emit(new TokenCreated(id, token.TokenId, location));
        MarkDirty();
        if (location.Kind == PlaceKind.Battlefield && card.Type == CardType.Unit) ApplyContested(token.Controller, location.Index!.Value);
        return null;
    }

    /// <summary>Only the looker sees which cards; everyone else sees how many.</summary>
    private Rejection? LookAtTop(PlayerId looker, ManualLookAtTop look)
    {
        if (!IsPlayer(look.Owner) || !IsDeck(look.Deck) || look.Count < 1)
            return Reject(RejectionCode.UnknownObject, "Choose a player's Main Deck or Rune Deck and how many cards.");
        var top = State.At(new Place(look.Deck, look.Owner, null)).Take(look.Count).Select(id => State[id].CardId).ToList();
        Emit(new CardsLookedAt(looker, look.Owner, look.Deck, top.Count, top) { VisibleTo = looker });
        Emit(new CardsLookedAt(looker, look.Owner, look.Deck, top.Count, null));
        return null;
    }

    /// <summary>Removes a finalized chain item; its card goes to its owner's trash (CR 425). Countering the item being
    /// resolved by hand ends that resolution.</summary>
    private Rejection? Counter(int itemId)
    {
        var item = State.Chain.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return Reject(RejectionCode.UnknownObject, $"There is no chain item {itemId}.");
        if (item.Status != ChainItemStatus.Finalized) return Reject(RejectionCode.WrongTiming, "Only finalized chain items can be countered.");
        State.Chain.Remove(item);
        if (item.Card is { } card && State.Exists(card)) MoveCard(card, Place.Trash(State[card].Owner));
        Emit(new ChainItemCountered(itemId));
        MarkDirty();
        if (Pending is ResolveManuallyDecision resolving && resolving.ChainItem == itemId)
        {
            ResolvingManually = false;
            Pending = null;
            _handler = null;
        }
        AfterResolution();
        return null;
    }

    /// <summary>Puts line <c>Line</c> of the source's text on the chain as a pending ability controlled by the source's controller.</summary>
    private Rejection? AddAbility(AddAbilityToChain add)
    {
        if (!State.Exists(add.Source)) return Reject(RejectionCode.UnknownObject, $"{add.Source} doesn't exist.");
        var source = State[add.Source];
        var lines = RichText.Lines(CardOf(source).Text.Rich);
        if (add.Line < 1 || add.Line > lines.Count) return Reject(RejectionCode.UnexpectedAction, $"Line {add.Line} doesn't exist on that card.");
        if (State.Chain.Count == 0) ChainStartedByTrigger = add.Kind == AbilityKind.Triggered;
        var item = new ChainItem
        {
            Id = State.NextChainItemId(),
            Kind = ChainItemKind.Ability,
            Controller = source.Controller,
            Source = add.Source,
            TextLine = add.Line,
            AbilityKind = add.Kind,
            SourceCardId = source.CardId,
            Text = lines[add.Line - 1],
        };
        State.Chain.Add(item);
        Emit(new ChainItemAdded(item.Id, item.Controller));
        MarkDirty();
        Push(new AbilityTask(item, add.Cost));
        return null;
    }

    internal bool RunAbility(AbilityTask task)
    {
        if (task.Cancelled) return true;
        if (!task.Paid)
        {
            AskPay(task.Item.Controller, task.Cost!, Db.Cards[task.Item.SourceCardId!].Domains,
                onPaid: () => task.Paid = true,
                onCancel: () =>
                {
                    State.Chain.Remove(task.Item);
                    Emit(new PlayCancelled(task.Item.Id));
                    task.Cancelled = true;
                },
                onAdjust: adjusted => task.Cost = adjusted);
            return false;
        }
        task.Item.Status = ChainItemStatus.Finalized;
        ChainPasses = 0;
        State.Turn.Priority = task.Item.Controller;
        return true;
    }
}
```

- [x] **Step 5: Route manual actions in the match**

In `src/CromoBound.Engine/Matches/MatchCore.cs`, add this arm right after the `Concede` arm in `Submit`'s `switch`:
```csharp
            ManualAction manual => ApplyManual(player, manual),
```

Add this method to `MatchCore` (in `MatchCore.cs`):
```csharp
    /// <summary>Manual actions are possible only while a game is being played (not during pre-game steps).</summary>
    private Rejection? ApplyManual(PlayerId player, ManualAction action)
    {
        if (Stage != MatchStage.Playing) return Reject(RejectionCode.WrongTiming, "Manual actions are only possible during play.");
        var result = Game!.SubmitManual(player, action);
        if (!result.Accepted) return result.Rejection;
        _newEvents.AddRange(result.Events);
        AfterGameChange();
        return null;
    }
```

- [x] **Step 6: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests, including Plan B's turn-point and battlefield tests).

- [x] **Step 7: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests/ManualTests.cs
git commit -m "feat(engine): add manual actions and hand-added abilities"
```

---

### Task 5: Undo, saving and loading

**Files:**
- Create: `src/CromoBound.Engine/Matches/MatchCore.Undo.cs`
- Modify: `src/CromoBound.Engine/Matches/Match.cs` (undo routing, `ToRecord`, `Load`)
- Modify: `tests/CromoBound.Engine.Tests/MatchTestExtensions.cs` (`Snapshot`)
- Test: `tests/CromoBound.Engine.Tests/UndoAndSaveTests.cs`

**Interfaces:**
- Consumes: Task 2 (`MatchCore`, `Log`, `PlayStartIndex`, `UndoRequestedBy`), `EngineInfo.Version` (Plan A), `CardDatabase.Fingerprint` (Plan A).
- Produces:
  - `Match.ToRecord() : MatchRecord`, `Match.Load(MatchRecord, CardDatabase) : Match`
  - Undo through `Submit(RequestUndo)` / `Submit(AnswerUndo)`
  - `MatchCore.Replay(setup, db, log)`, `MatchCore.UndoIndex(player)`
  - Test helper `match.Snapshot() : string`

**Rules (spec §6.6, §6.7):**
- **Undo:**
  - Only during play, by a player with an action logged since play began in the current game.
  - It rolls back to just before that player's most recent action.
  - The opponent answers. While the request is open, only `AnswerUndo` (from the opponent) and `Concede` are accepted.
  - Accepting rebuilds the match by replaying the log without that action and everything after it.
- **Loading:** first compare the record's engine version and data fingerprint with the current ones; on any difference, throw `MatchVersionMismatchException` without replaying. Then replay. A rejected log entry fails loading, naming the entry.

- [x] **Step 1: Add the snapshot helper**

In `tests/CromoBound.Engine.Tests/MatchTestExtensions.cs`, add to `MatchTestExtensions`:
```csharp
    /// <summary>A text fingerprint of a match: stage, score, every event, every object and the turn state. Equal snapshots = same match.</summary>
    public static string Snapshot(this Match match)
    {
        var parts = new List<string> { $"{match.Stage} game {match.GameNumber} wins {string.Join(",", match.Result.GameWins)}" };
        parts.AddRange(match.Events.Select(e => $"{e.Sequence}: {e}"));
        if (match.Game is { } game)
        {
            parts.AddRange(game.State.Objects.Select(o => $"{o.Id} {o.CardId} @{o.Place} ctrl {o.Controller} ex {o.Exhausted} dmg {o.Damage}"));
            parts.AddRange(game.State.Players.Select(p => $"{p.Id} pts {p.Points} energy {p.Pool.Energy}"));
            var turn = game.State.Turn;
            parts.Add($"turn {turn.Number} {turn.Phase} priority {turn.Priority} chain {game.State.Chain.Count} pending {game.Pending?.GetType().Name}");
        }
        return string.Join("\n", parts);
    }
```

- [x] **Step 2: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/UndoAndSaveTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Json;

namespace CromoBound.Engine.Tests;

public class UndoAndSaveTests
{
    private static Match NewMatch() => Match.Create(TestDecks.Setup(MatchFormat.Bo1), EngineTestDb.Create()).Match!;

    private static PlayerId Other(PlayerId player) => new(1 - player.Index);

    /// <summary>To play, then the first player starts playing their first playable card (stopping at the payment).</summary>
    private static (Match Match, PlayerId Player) StartPlaying()
    {
        var match = NewMatch().ToPlay();
        var priority = match.Decision<PriorityDecision>();
        match.Accept(priority.Player, new PlayCard(priority.Playable[0]));
        return (match, priority.Player);
    }

    [Fact]
    public void Accepted_undo_equals_never_taking_the_action()
    {
        var (undone, player) = StartPlaying();
        var (reference, _) = StartPlaying();
        var pay = undone.Decision<PayCostDecision>().Suggested!;
        undone.Accept(player, new PayCost { Exhaust = pay.Exhaust, Recycle = pay.Recycle });

        undone.Accept(player, new RequestUndo());
        var confirm = undone.Decision<ConfirmUndoDecision>();
        Assert.Equal((Other(player), player), (confirm.Player, confirm.RequestedBy));
        undone.Accept(Other(player), new AnswerUndo(true));

        Assert.IsType<PayCostDecision>(undone.Pending);
        Assert.Equal(reference.Snapshot(), undone.Snapshot());
    }

    [Fact]
    public void A_declined_undo_changes_nothing()
    {
        var (match, player) = StartPlaying();
        var logBefore = match.ToRecord().Log.Count;

        match.Accept(player, new RequestUndo());
        match.Accept(Other(player), new AnswerUndo(false));

        Assert.IsType<PayCostDecision>(match.Pending);
        Assert.Equal(logBefore, match.ToRecord().Log.Count);
    }

    [Fact]
    public void Undo_needs_an_action_of_yours_since_play_began()
    {
        var match = NewMatch().ToMulligan();
        Assert.Equal(RejectionCode.UndoNotAllowed, match.Submit(new PlayerId(0), new RequestUndo()).Rejection!.Code);

        match.ToPlay();
        var opponent = Other(match.Decision<PriorityDecision>().Player);
        Assert.Equal(RejectionCode.UndoNotAllowed, match.Submit(opponent, new RequestUndo()).Rejection!.Code);
    }

    [Fact]
    public void While_an_undo_is_waiting_only_the_answer_or_a_concession_is_accepted()
    {
        var (match, player) = StartPlaying();
        match.Accept(player, new RequestUndo());

        Assert.Equal(RejectionCode.UnexpectedAction, match.Submit(player, new CancelPlay()).Rejection!.Code);
        Assert.Equal(RejectionCode.UnexpectedAction, match.Submit(player, new AnswerUndo(true)).Rejection!.Code);
        match.Accept(player, new Concede());
        Assert.Equal(MatchStage.Over, match.Stage);
    }

    [Fact]
    public void A_saved_match_loads_into_the_same_state()
    {
        var (match, player) = StartPlaying();
        var pay = match.Decision<PayCostDecision>().Suggested!;
        match.Accept(player, new PayCost { Exhaust = pay.Exhaust, Recycle = pay.Recycle });
        match.Accept(player, new EndTurn());

        var json = CromoJson.Serialize(match.ToRecord());
        var loaded = Match.Load(CromoJson.Deserialize<MatchRecord>(json), EngineTestDb.Create());

        Assert.Equal(match.Snapshot(), loaded.Snapshot());
    }

    [Fact]
    public void Loading_a_record_from_another_version_is_refused()
    {
        var record = NewMatch().ToPlay().ToRecord();

        Assert.Throws<MatchVersionMismatchException>(() => Match.Load(record with { EngineVersion = "0.0.0+other" }, EngineTestDb.Create()));
        Assert.Throws<MatchVersionMismatchException>(() => Match.Load(record with { DataFingerprint = "other" }, EngineTestDb.Create()));
    }

    [Fact]
    public void A_log_entry_rejected_on_replay_fails_loading_and_names_it()
    {
        var record = NewMatch().ToPlay().ToRecord();
        var bad = record with { Log = [.. record.Log, new LoggedAction(new PlayerId(0), new ResolveDone())] };

        var error = Assert.Throws<InvalidDataException>(() => Match.Load(bad, EngineTestDb.Create()));

        Assert.Contains($"Log entry {record.Log.Count}", error.Message);
    }
}
```

- [x] **Step 3: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~UndoAndSaveTests"`
Expected: FAIL (compilation errors: `Match.ToRecord`, `Match.Load` not found).

- [x] **Step 4: Implement undo and replay in the core**

Create `src/CromoBound.Engine/Matches/MatchCore.Undo.cs`:
```csharp
using CromoBound.Data;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Matches;

internal sealed partial class MatchCore
{
    /// <summary>Log index of the player's most recent action since play began in this game; -1 when there is none.</summary>
    public int UndoIndex(PlayerId player)
    {
        if (Stage != MatchStage.Playing || PlayStartIndex < 0) return -1;
        for (var i = Log.Count - 1; i >= PlayStartIndex; i--)
            if (Log[i].Player == player) return i;
        return -1;
    }

    public SubmitResult RequestUndo(PlayerId player)
    {
        if (Winner is not null) return SubmitResult.Reject(RejectionCode.MatchOver, "The match is over.");
        if (UndoRequestedBy is not null) return SubmitResult.Reject(RejectionCode.UndoNotAllowed, "An undo request is already waiting.");
        if (UndoIndex(player) < 0) return SubmitResult.Reject(RejectionCode.UndoNotAllowed, "You have no action to undo in this game.");
        UndoRequestedBy = player;
        Emit(new UndoRequested(player));
        return new SubmitResult(true, null, Flush());
    }

    /// <summary>A fresh core with <paramref name="log"/> replayed. Same setup + same log = same match (spec §6.5).</summary>
    public static MatchCore Replay(MatchSetup setup, CardDatabase db, IEnumerable<LoggedAction> log)
    {
        var core = new MatchCore(setup, db);
        var index = 0;
        foreach (var entry in log)
        {
            var result = core.Submit(entry.Player, entry.Action);
            if (!result.Accepted)
                throw new InvalidDataException(
                    $"Log entry {index} ({entry.Action.GetType().Name} by {entry.Player}) was rejected on replay: {result.Rejection!.Message}");
            index++;
        }
        return core;
    }
}
```

- [x] **Step 5: Route undo, save and load in the facade**

In `src/CromoBound.Engine/Matches/Match.cs`, replace the `Submit` method with:
```csharp
    /// <summary>Submits an action. Undo requests and answers are handled here, because an accepted undo replaces the whole core.</summary>
    public SubmitResult Submit(PlayerId player, PlayerAction action)
    {
        switch (action)
        {
            case RequestUndo:
                return _core.RequestUndo(player);
            case AnswerUndo answer:
                if (_core.UndoRequestedBy is not { } requester || MatchCore.Opponent(requester) != player)
                    return SubmitResult.Reject(RejectionCode.UnexpectedAction, "There is no undo request for you to answer.");
                if (!answer.Accept)
                {
                    _core.UndoRequestedBy = null;
                    return new SubmitResult(true, null, []);
                }
                _core = MatchCore.Replay(_core.Setup, _core.Db, _core.Log.Take(_core.UndoIndex(requester)));
                return new SubmitResult(true, null, []);
            default:
                return _core.Submit(player, action);
        }
    }

    /// <summary>What to save: the versions, the setup and the log (spec §6.5).</summary>
    public MatchRecord ToRecord() => new(EngineInfo.Version, _core.Db.Fingerprint, _core.Setup, [.. _core.Log]);

    /// <summary>Rebuilds a saved match by replaying it. Refuses records from another engine build or other card data (spec §6.7).</summary>
    public static Match Load(MatchRecord record, CardDatabase db)
    {
        if (record.EngineVersion != EngineInfo.Version || record.DataFingerprint != db.Fingerprint)
            throw new MatchVersionMismatchException(record.EngineVersion, EngineInfo.Version, record.DataFingerprint, db.Fingerprint);
        return new Match(MatchCore.Replay(record.Setup, db, record.Log));
    }
```

- [x] **Step 6: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests).

- [x] **Step 7: Commit**

```bash
git add src/CromoBound.Engine/Matches tests/CromoBound.Engine.Tests
git commit -m "feat(engine): add undo by agreement and saved matches"
```

---

### Task 6: Views without leaks, and a full scripted Bo3

**Files:**
- Modify: `src/CromoBound.Engine/Rules/Game.Mutations.cs` (`MoveCard` visibility)
- Modify: `src/CromoBound.Engine/Events/GameEvents.cs`, `src/CromoBound.Engine/Decisions/Decisions.cs` (polymorphic JSON)
- Create: `src/CromoBound.Engine/Views/PlayerView.cs`, `src/CromoBound.Engine/Views/ViewBuilder.cs`
- Modify: `src/CromoBound.Engine/Matches/Match.cs` (`ViewFor`)
- Modify: `tests/CromoBound.Engine.Tests/Bot.cs` (match decisions)
- Test: `tests/CromoBound.Engine.Tests/ViewTests.cs`

**Interfaces:**
- Consumes: everything before.
- Produces:
  - `Match.ViewFor(PlayerId) : PlayerView`
  - The view records `PlayerView`, `PlayerSideView`, `CardView`, `PoolView`, `BattlefieldView`, `ChainItemView`, `TurnView`
  - Events and decisions serialize with a `type` discriminator
  - `Bot.Choose(Match)`

**What each player may see (spec §10):**
- Public: everything on the board and in the trash, banishment, legend and champion zones; points, XP, pools; the turn, priority, focus, showdown and scores; the chain.
- Own hand, own facedown cards, own sideboard: full. Opponent's: counts only (a facedown slot shows only that it's occupied).
- Decks: counts only, for both.
- The pending decision: everyone sees its kind and who decides. Only deciders see its options (a battlefield pick shows each player only their own choices).
- Events: public ones, plus those marked for the viewer.

**The leak fix:** a public `CardMoved` hides the object id of any side that is a deck, a hand or a facedown slot, and hides the card's identity when both sides are hidden. When a side is a hand or a facedown slot, the owner (or the facedown card's controller) also gets a private copy. That copy shows hand and facedown ids, but never deck ids, since deck order is secret even from the owner.

- [x] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/ViewTests.cs`:
```csharp
using System.Text.RegularExpressions;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ViewTests
{
    private static Match NewMatch(MatchFormat format = MatchFormat.Bo1, ulong seed = 7) =>
        Match.Create(TestDecks.Setup(format, seed), EngineTestDb.Create()).Match!;

    /// <summary>Object ids appear in JSON as { "value": n }.</summary>
    private static HashSet<int> ObjectIdsIn(string json) =>
        [.. Regex.Matches(json, @"\{\s*""value"":\s*(\d+)\s*\}").Select(m => int.Parse(m.Groups[1].Value))];

    [Fact]
    public void Opponent_view_and_log_never_contain_hidden_cards()
    {
        var match = NewMatch().ToPlay();
        var state = match.Game!.State;
        var p2Hand = state.At(Place.Hand(P2)).ToList();
        match.Accept(P2, new ManualMoveCard(p2Hand[0], Place.MainDeck(P2)));
        match.Accept(P2, new ManualMoveCard(p2Hand[1], Place.Trash(P2)));
        match.Accept(P2, new ManualMoveCard(state.At(Place.Trash(P2))[0], Place.MainDeck(P2), DeckPosition.Bottom));
        match.Accept(state.Turn.TurnPlayer, new EndTurn());

        var json = CromoJson.Serialize(match.ViewFor(P1));

        var hidden = state.At(Place.Hand(P2))
            .Concat(new[] { P1, P2 }.SelectMany(p => state.At(Place.MainDeck(p)).Concat(state.At(Place.RuneDeck(p)))))
            .Select(id => id.Value)
            .ToHashSet();
        Assert.Empty(ObjectIdsIn(json).Intersect(hidden));
    }

    [Fact]
    public void A_player_sees_their_own_hand_and_only_counts_of_the_opponents()
    {
        var match = NewMatch().ToPlay();
        var state = match.Game!.State;

        var view = match.ViewFor(P1);

        Assert.Equal(state.At(Place.Hand(P1)).Count, view.Players[0].Hand!.Count);
        Assert.Null(view.Players[1].Hand);
        Assert.Equal(state.At(Place.Hand(P2)).Count, view.Players[1].HandCount);
        Assert.Equal(state.At(Place.MainDeck(P2)).Count, view.Players[1].MainDeckCount);
        Assert.NotNull(view.Players[0].Sideboard);
        Assert.Null(view.Players[1].Sideboard);
        Assert.Equal(3, view.Players[1].SideboardCount);
    }

    [Fact]
    public void A_facedown_card_shows_only_to_its_controller()
    {
        var match = NewMatch().ToPlay();
        var state = match.Game!.State;
        var player = state.Turn.TurnPlayer;
        var other = player == P1 ? P2 : P1;
        state.Battlefields[0].Controller = player;
        match.Accept(player, new ManualCreateToken("token-recruit", Place.Battlefield(0), player));
        match.Accept(player, new ManualMoveCard(state.At(Place.Hand(player))[0], Place.Facedown(0)));

        var mine = match.ViewFor(player).Battlefields[0];
        var theirs = match.ViewFor(other).Battlefields[0];

        Assert.NotNull(mine.Facedown);
        Assert.True(theirs.HasFacedown);
        Assert.Null(theirs.Facedown);
    }

    [Fact]
    public void Only_deciders_see_a_decisions_options()
    {
        var bo3 = NewMatch(MatchFormat.Bo3);

        var p1View = bo3.ViewFor(P1);
        var pick = Assert.IsType<PickBattlefieldDecision>(p1View.Decision);
        Assert.Equal(P1, Assert.Single(pick.Choices).Player);
        Assert.Equal("PickBattlefield", p1View.DecisionKind);

        var bo1 = NewMatch().ToPlay();
        var decider = bo1.Pending!.Players[0];
        var watcher = decider == P1 ? P2 : P1;
        Assert.IsType<PriorityDecision>(bo1.ViewFor(decider).Decision);
        Assert.Null(bo1.ViewFor(watcher).Decision);
        Assert.Equal(new[] { decider }, bo1.ViewFor(watcher).Deciding);
    }

    [Fact]
    public void Scripted_players_finish_a_bo3_and_the_saved_match_replays_identically()
    {
        var match = NewMatch(MatchFormat.Bo3, seed: 11);
        var bots = new[] { new Bot(), new Bot() };
        for (var i = 0; i < 20000 && match.Stage != MatchStage.Over; i++)
        {
            var player = match.Pending!.Players[0];
            match.Accept(player, bots[player.Index].Choose(match));
        }

        Assert.Equal(MatchStage.Over, match.Stage);
        Assert.Contains(2, match.Result.GameWins);
        var loaded = Match.Load(match.ToRecord(), EngineTestDb.Create());
        Assert.Equal(CromoJson.Serialize(match.ViewFor(P1)), CromoJson.Serialize(loaded.ViewFor(P1)));
    }
}
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~ViewTests"`
Expected: FAIL (compilation errors: `Match.ViewFor`, `Bot.Choose(Match)` not found).

- [x] **Step 3: Fix the move events**

In `src/CromoBound.Engine/Rules/Game.Mutations.cs`, replace `MoveCard` and `IsHidden` with:
```csharp
    /// <summary>Moves a card. The public event hides the object id of any side that is a deck, a hand or a facedown slot, and
    /// the card's identity when both sides are hidden. If a side is a hand or a facedown slot, the owner (or the facedown card's
    /// controller) also gets a private copy showing hand and facedown ids, never deck ids (deck order is secret to everyone).</summary>
    internal ObjectId? MoveCard(ObjectId id, Place to, DeckPosition position = DeckPosition.Top)
    {
        var instance = State[id];
        var from = instance.Place;
        var cardId = instance.CardId;
        var viewer = from.Kind == PlaceKind.Facedown ? instance.Controller : instance.Owner;
        var newId = State.Move(id, to, position);
        var landed = newId is { } moved ? State[moved].Place : to;
        var bothHidden = IsHidden(from) && IsHidden(landed);
        Emit(new CardMoved(bothHidden ? null : cardId, IsHidden(from) ? null : id, IsHidden(landed) ? null : newId, from, landed));
        if (IsPrivate(from) || IsPrivate(landed))
            Emit(new CardMoved(cardId, IsSecret(from) ? null : id, IsSecret(landed) ? null : newId, from, landed) { VisibleTo = viewer });
        MarkDirty();
        return newId;
    }

    private static bool IsSecret(Place place) => place.Kind is PlaceKind.MainDeck or PlaceKind.RuneDeck;

    private static bool IsPrivate(Place place) => place.Kind is PlaceKind.Hand or PlaceKind.Facedown;

    private static bool IsHidden(Place place) => IsSecret(place) || IsPrivate(place);
```

- [x] **Step 4: Make events and decisions serializable**

In `src/CromoBound.Engine/Events/GameEvents.cs`, add `using System.Text.Json.Serialization;` and put these attributes on `GameEvent`:
```csharp
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
```

In `src/CromoBound.Engine/Decisions/Decisions.cs`, add `using System.Text.Json.Serialization;` and put these attributes on `PendingDecision`:
```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PriorityDecision), "Priority")]
[JsonDerivedType(typeof(PlayChoicesDecision), "PlayChoices")]
[JsonDerivedType(typeof(PayCostDecision), "PayCost")]
[JsonDerivedType(typeof(ChooseShowdownDecision), "ChooseShowdown")]
[JsonDerivedType(typeof(AssignDamageDecision), "AssignDamage")]
[JsonDerivedType(typeof(ResolveManuallyDecision), "ResolveManually")]
[JsonDerivedType(typeof(TurnPointDecision), "TurnPoint")]
[JsonDerivedType(typeof(PickBattlefieldDecision), "PickBattlefield")]
[JsonDerivedType(typeof(ChoosePlayOrderDecision), "ChoosePlayOrder")]
[JsonDerivedType(typeof(SideboardDecision), "Sideboard")]
[JsonDerivedType(typeof(MulliganDecision), "Mulligan")]
[JsonDerivedType(typeof(ConfirmUndoDecision), "ConfirmUndo")]
```

- [x] **Step 5: Implement the views**

Create `src/CromoBound.Engine/Views/PlayerView.cs`:
```csharp
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Views;

/// <summary>A card the viewer is allowed to see. Might is set for units only.</summary>
public sealed record CardView(
    ObjectId Id, string CardId, string? PrintingId, PlayerId Owner, PlayerId Controller,
    bool Exhausted, bool Stunned, bool Buffed, bool Empowered, int Damage, int? Might, CombatRole? Role);

public sealed record PoolView(int Energy, IReadOnlyDictionary<Domain, int> Power, int UniversalPower);

/// <summary>One player's side. <see cref="Hand"/> and <see cref="Sideboard"/> are null in the opponent's view.</summary>
public sealed record PlayerSideView(
    PlayerId Player, int Points, int Xp, int GameWins, PoolView Pool,
    IReadOnlyList<CardView> Legend, IReadOnlyList<CardView> ChampionZone, IReadOnlyList<CardView> Base,
    IReadOnlyList<CardView>? Hand, int HandCount, int MainDeckCount, int RuneDeckCount,
    IReadOnlyList<CardView> Trash, IReadOnlyList<CardView> Banishment,
    IReadOnlyList<DeckEntry>? Sideboard, int SideboardCount);

/// <summary><see cref="Facedown"/> is set only for the facedown card's controller; others see <see cref="HasFacedown"/>.</summary>
public sealed record BattlefieldView(
    int Index, CardView Card, PlayerId? Controller, PlayerId? ContestedBy, IReadOnlyList<CardView> Units, bool HasFacedown, CardView? Facedown);

public sealed record ChainItemView(
    int Id, ChainItemKind Kind, PlayerId Controller, ChainItemStatus Status, CardView? Card, string? SourceCardId, string? Text,
    Place? Location, bool Accelerate);

/// <summary>Turn state; <see cref="Scored"/> lists, per player index, the battlefields scored this turn.</summary>
public sealed record TurnView(
    int Number, PlayerId TurnPlayer, Phase Phase, TurnStep Step, PlayerId? Priority, PlayerId? Focus, bool Closed,
    int? ShowdownAt, bool Combat, PlayerId? Attacker, PlayerId? Defender, IReadOnlyList<IReadOnlyList<int>> Scored);

/// <summary>Everything one player may see (spec §10). <see cref="Decision"/> is set only when the viewer is deciding.</summary>
public sealed record PlayerView(
    PlayerId Viewer, MatchFormat Format, MatchStage Stage, int GameNumber, PlayerId? MatchWinner,
    IReadOnlyList<PlayerSideView> Players, TurnView? Turn, IReadOnlyList<BattlefieldView> Battlefields, IReadOnlyList<ChainItemView> Chain,
    IReadOnlyList<PlayerId> Deciding, string? DecisionKind, PendingDecision? Decision, IReadOnlyList<GameEvent> Log);
```

Create `src/CromoBound.Engine/Views/ViewBuilder.cs`:
```csharp
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Views;

/// <summary>Builds a player's view from the match, leaving out everything they may not see.</summary>
internal static class ViewBuilder
{
    public static PlayerView Build(MatchCore core, PlayerId viewer)
    {
        var game = core.Game;
        var pending = core.Pending;
        return new PlayerView(
            viewer, core.Setup.Format, core.Stage, core.GameNumber, core.Winner,
            [.. MatchCore.Players.Select(p => Side(core, game, p, viewer))],
            game is null ? null : Turn(game),
            game is null ? [] : [.. game.State.Battlefields.Select(b => Battlefield(game, b, viewer))],
            game is null ? [] : [.. game.State.Chain.Select(i => ChainItem(game, i))],
            pending?.Players ?? Array.Empty<PlayerId>(),
            pending?.GetType().Name.Replace("Decision", ""),
            Decision(pending, viewer),
            [.. core.Events.Where(e => e.VisibleTo is null || e.VisibleTo == viewer)]);
    }

    private static PendingDecision? Decision(PendingDecision? pending, PlayerId viewer) => pending switch
    {
        PickBattlefieldDecision pick when pick.Players.Contains(viewer) => pick with { Choices = [.. pick.Choices.Where(c => c.Player == viewer)] },
        { } decision when decision.Players.Contains(viewer) => decision,
        _ => null,
    };

    private static PlayerSideView Side(MatchCore core, Game? game, PlayerId player, PlayerId viewer)
    {
        var own = player == viewer;
        var deck = core.Decks[player.Index];
        var sideboard = own ? deck.Sideboard : null;
        var sideboardCount = deck.Sideboard.Sum(e => e.Count);
        if (game is null)
            return new PlayerSideView(player, 0, 0, core.Wins[player.Index], new PoolView(0, new SortedDictionary<Domain, int>(), 0),
                [], [], [], own ? [] : null, 0, 0, 0, [], [], sideboard, sideboardCount);

        var state = game.State;
        var side = state.Player(player);
        List<CardView> At(Place place) => [.. state.At(place).Select(id => Card(game, state[id]))];
        return new PlayerSideView(
            player, side.Points, side.Xp, core.Wins[player.Index],
            new PoolView(side.Pool.Energy, new SortedDictionary<Domain, int>(side.Pool.Power), side.Pool.UniversalPower),
            At(Place.LegendZone(player)), At(Place.ChampionZone(player)), At(Place.Base(player)),
            own ? At(Place.Hand(player)) : null, state.At(Place.Hand(player)).Count,
            state.At(Place.MainDeck(player)).Count, state.At(Place.RuneDeck(player)).Count,
            At(Place.Trash(player)), At(Place.Banishment(player)),
            sideboard, sideboardCount);
    }

    private static CardView Card(Game game, CardInstance card) => new(
        card.Id, card.CardId, card.PrintingId, card.Owner, card.Controller,
        card.Exhausted, card.Stunned, card.Buffed, card.Empowered, card.Damage,
        game.Db.Cards[card.CardId].Type == CardType.Unit ? game.MightOf(card.Id) : null, card.Role);

    private static BattlefieldView Battlefield(Game game, BattlefieldState battlefield, PlayerId viewer)
    {
        var state = game.State;
        var facedown = state.At(Place.Facedown(battlefield.Index));
        var visible = facedown.Select(id => state[id]).FirstOrDefault(c => c.Controller == viewer);
        return new BattlefieldView(
            battlefield.Index, Card(game, state[battlefield.Card]), battlefield.Controller, battlefield.ContestedBy,
            [.. state.At(Place.Battlefield(battlefield.Index)).Select(id => Card(game, state[id]))],
            facedown.Count > 0, visible is null ? null : Card(game, visible));
    }

    private static ChainItemView ChainItem(Game game, ChainItem item) => new(
        item.Id, item.Kind, item.Controller, item.Status,
        item.Card is { } card && game.State.Exists(card) ? Card(game, game.State[card]) : null,
        item.SourceCardId, item.Text, item.Location, item.Accelerate);

    private static TurnView Turn(Game game)
    {
        var turn = game.State.Turn;
        var showdown = game.State.Showdown;
        IReadOnlyList<IReadOnlyList<int>> scored =
        [
            .. game.State.Players.Select(p => game.State.Battlefields.Where(b => turn.HasScored(p.Id, b.Index)).Select(b => b.Index).ToList()),
        ];
        return new TurnView(
            turn.Number, turn.TurnPlayer, turn.Phase, turn.Step, turn.Priority, turn.Focus, game.State.Chain.Count > 0,
            showdown?.Battlefield, showdown?.IsCombat ?? false, showdown?.Attacker, showdown?.Defender, scored);
    }
}
```

In `src/CromoBound.Engine/Matches/Match.cs`, add `using CromoBound.Engine.Views;` and this method:
```csharp
    /// <summary>What <paramref name="player"/> may see: the only thing the server should send to that player (spec §10).</summary>
    public PlayerView ViewFor(PlayerId player) => ViewBuilder.Build(_core, player);
```

- [x] **Step 6: Teach the scripted player the match decisions**

In `tests/CromoBound.Engine.Tests/Bot.cs`, add `using CromoBound.Engine.Matches;` and this method to `Bot`:
```csharp
    /// <summary>Pre-game: first offered battlefield, play first, no sideboard changes, no mulligan; in play, as for a game.</summary>
    public PlayerAction Choose(Match match) => match.Pending switch
    {
        PickBattlefieldDecision pick => new PickBattlefield(pick.Choices.Single(c => c.Player == pick.Players[0]).Printings[0]),
        ChoosePlayOrderDecision => new ChoosePlayOrder(true),
        SideboardDecision => new SubmitSideboard(),
        MulliganDecision => new Mulligan(),
        _ => Choose(match.Game!),
    };
```

- [x] **Step 7: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests, including Plan B's `HiddenTests`, whose event checks still hold).

If the scripted Bo3 doesn't finish within the cap, find where it stops progressing and fix the engine, as in Plan B Task 8.

- [x] **Step 8: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): add per-player views without hidden-card leaks"
```

---

## Done criteria

- [x] Match, manual and pre-game types round-trip through JSON (Task 1).
- [x] Bo1: random battlefields, d20 roll-off, play order, sideboarding, setup, mulligan, first turn (Task 2).
- [x] Bo3: battlefield picks and removal, loser chooses, sideboarding from game 2 (champion switch, illegal swaps rejected), conceding, 2 wins end the match (Task 3).
- [x] All manual actions work, at any moment of play, without losing the pending decision; abilities added by hand go on the chain (Task 4).
- [x] Undo by agreement equals never taking the action; saved matches load identically; other versions are refused (Task 5).
- [x] Views show each player only what they may see; a full scripted Bo3 finishes and replays identically (Task 6).
- [x] `dotnet test CromoBound.slnx` passes.
