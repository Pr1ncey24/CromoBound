# Engine Plan B: Rules

> Steps use checkbox (`- [ ]`) syntax for tracking. Second of three plans for Phase 2a (A: decks and state, done; B: rules; C: match, manual actions, views).

**Goal:** A `Game` that runs one Riftbound game under the rules core: turn structure, playing and paying, the chain and priority, movement, showdowns, combat, scoring, Burn Out and the 2a keywords. Card effects are resolved by hand.

**Architecture:**
- `Game` (namespace `CromoBound.Engine.Rules`) is a partial class, split by rules area into focused files.
- It owns a task queue (CR 333). Each mandatory procedure is a small task object that tracks its own step, so it can pause for a decision and resume.
- `Submit` validates an action against the pending decision, applies it, then runs the loop until the next decision:
  1. tasks;
  2. the chain;
  3. the showdown;
  4. the Main phase.
- All state changes go through `Game` mutation helpers. They emit events and mark that a cleanup is needed.

**Tech Stack:** .NET 10, xUnit 2.9.3. No new dependencies.

**Design document:** `docs/engine-architecture.md`. This plan implements §6.1–6.4 and §7, plus the rule interpretations in §13.

## Global Constraints

- Everything in Plan A's Global Constraints still applies: `net10.0`, no NuGet packages in runtime projects, determinism (no `DateTime`/`Guid`/`System.Random`), no commits of `docs/*.md`.
- State changes during a game go only through `Game`'s mutation helpers (`MoveCard`, `SetStatus`, `DealDamage`, `GainPoints`, …). Tests may arrange a `GameState` directly before `Start`, and may poke state between actions to set up a situation.
- A decision handler validates **before** it mutates anything. A rejected action leaves the game unchanged.
- Victory Score is 8. Channel 2 runes per turn (3 on turn 2, the second player's first turn). Draw 1. Starting the game (setup, mulligan) is Plan C.

## Deliberate deviations from the spec (reviewers: these are intended)

1. **Play choices are one action.** `ChoosePlayOptions(Location, Accelerate)` replaces the spec's separate `ChooseLocation` and `ChooseAccelerate` (§6.2), saving a round trip.
2. **The pool is used automatically.** `PayCost` names only the runes to exhaust and to recycle. The engine takes whatever else is needed from the rune pool; anything left over stays there. This replaces the spec's "amount from pool".
3. **Immediate Burn Out win** (CR 431.3) is checked across consecutive burnouts within one draw instruction.

## Review Focus

1. **A player pays with an exhausted rune, an opponent's rune, or not enough runes.** Rejected, and nothing changes. Pinned in Task 4 (`An_exhausted_or_foreign_rune_cannot_pay`, `Insufficient_payment_is_rejected_and_nothing_changes`).
2. **The wrong player acts, or the right player sends an action that doesn't fit the decision.** Rejected with `NotYourDecision` / `UnexpectedAction`, game unchanged. Pinned in Task 3 (`Wrong_player_or_wrong_action_is_rejected_without_changes`).
3. **A deck runs out mid-game.** Burn Out: the trash is shuffled back, the opponent gains a point, and the game continues; it doesn't crash. Pinned in Task 3 (`Empty_deck_burns_out_giving_the_opponent_a_point`).
4. **A side with no damage to deal** (all its units stunned). It isn't asked to assign; combat still resolves. Pinned in Task 5 (`Surviving_attackers_are_recalled_and_a_side_with_no_damage_is_not_asked`).
5. **A long game played by simple scripted players.** Ends with a winner, never gets stuck waiting with no legal answer, and gives identical results for the same seed. Pinned in Task 8 (`A_scripted_game_reaches_a_winner_and_replays_identically`).

---

## File Structure

```
src/CromoBound.Engine/
  Results.cs                    RejectionCode, Rejection, SubmitResult, GameEndReason, GameOutcome
  Actions/PlayerAction.cs       PlayerAction and its records, RuneUse, DamageAssignment
  Decisions/Decisions.cs        PendingDecision and its records, TotalCost, PaymentSuggestion, DamageGroup, DamageTarget, MoveOption, HideOption, RuneOption
  Events/GameEvents.cs          GameEvent and its records, StatusKind, ScoreKind, CombatResult
  State/Place.cs                (modify) [JsonIgnore] on computed properties
  State/PlayerState.cs          (modify) RunePool.Clone/CopyFrom/AddPower
  State/ShowdownState.cs        showdown/combat in progress
  State/GameState.cs            (modify) Showdown, StagedShowdowns, StagedCombats
  State/ChainItem.cs            (modify) Location, Accelerate, SourceCardId, Text
  Rules/Payment.cs              RuneInfo, cost calculation and payment matching (pure)
  Rules/CombatDamage.cs         lethal damage, assignment validation and suggestion (pure)
  Rules/CardKeywords.cs         a card's own keywords, from the starts of its text lines (pure)
  Rules/GameTask.cs             GameTask, StepTask
  Rules/Game.cs                 core: Submit, loop, Ask, Emit, queries
  Rules/Game.Mutations.cs       MoveCard, statuses, damage, points, draw, channel, Burn Out, scoring, control
  Rules/Game.Turn.cs            turn structure tasks
  Rules/Game.Priority.cs        who acts, legal options, runes
  Rules/Game.Dispatch.cs        priority action dispatch (grows each task)
  Rules/Game.Cleanup.cs         CleanupTask (v1 in Task 3, full in Task 5)
  Rules/Game.Play.cs            playing cards, payment flow
  Rules/Game.Chain.cs           passing, resolving by hand
  Rules/Game.Movement.cs        standard move, contested
  Rules/Game.Showdown.cs        showdowns
  Rules/Game.Combat.cs          combat
  Rules/Game.Hidden.cs          hiding cards
  Rules/Game.TurnPoints.cs      pauses for start/end-of-turn card effects
tests/CromoBound.Engine.Tests/
  TestGame.cs                   (modify) Start, Runes, First, GameTestExtensions
  ActionJsonTests.cs, PaymentTests.cs, CombatDamageTests.cs, CardKeywordsTests.cs, TurnTests.cs, PlayTests.cs,
  BattlefieldTests.cs, HiddenTests.cs, TurnPointTests.cs, Bot.cs, ScriptedGameTests.cs
```

---

### Task 1: Engine types: actions, decisions, events, results

**Files:**
- Create: `src/CromoBound.Engine/Results.cs`, `Actions/PlayerAction.cs`, `Decisions/Decisions.cs`, `Events/GameEvents.cs`
- Modify: `src/CromoBound.Engine/State/Place.cs`
- Test: `tests/CromoBound.Engine.Tests/ActionJsonTests.cs`

**Interfaces:**
- Consumes: `PlayerId`, `ObjectId`, `Place`, `Phase` (Models), `TurnStep`, `Domain`, `PowerSymbol`.
- Produces: every type listed in the code below. Later tasks use these exact names.

- [ ] **Step 1: Write the failing test**

Create `tests/CromoBound.Engine.Tests/ActionJsonTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.State;
using CromoBound.Models.Json;

namespace CromoBound.Engine.Tests;

public class ActionJsonTests
{
    [Fact]
    public void Actions_round_trip_with_a_type_discriminator()
    {
        PlayerAction[] actions =
        [
            new PlayCard(new ObjectId(4)),
            new StandardMove { Units = [new ObjectId(1), new ObjectId(2)], Destination = Place.Battlefield(1) },
            new PayCost { Exhaust = [new ObjectId(7)] },
            new Pass(),
            new ChoosePlayOptions(Place.Base(new PlayerId(0)), true),
            new AssignDamage { Assignments = [new DamageAssignment(new ObjectId(3), 2)] },
        ];

        foreach (var action in actions)
        {
            var json = CromoJson.Serialize(action);
            var back = CromoJson.Deserialize<PlayerAction>(json);

            Assert.Contains("\"type\"", json);
            Assert.DoesNotContain("isBoard", json);
            Assert.Equal(json, CromoJson.Serialize(back));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~ActionJsonTests"`
Expected: FAIL (compilation error: namespace `CromoBound.Engine.Actions` not found).

- [ ] **Step 3: Hide Place's computed properties from JSON**

In `src/CromoBound.Engine/State/Place.cs`, add `using System.Text.Json.Serialization;` at the top. Then put `[JsonIgnore]` on the line above each of the four computed properties `IsBoard`, `IsLocation`, `IsOrdered` and `IsPlayerPile`. For example:
```csharp
    /// <summary>Board zones (CR 107). Moving into or out of anything else makes a new object.</summary>
    [JsonIgnore]
    public bool IsBoard => Kind is PlaceKind.LegendZone or PlaceKind.Base or PlaceKind.Battlefield or PlaceKind.Facedown or PlaceKind.BattlefieldCard;
```

- [ ] **Step 4: Create the types**

Create `src/CromoBound.Engine/Results.cs`:
```csharp
using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine;

public enum RejectionCode
{
    NotYourDecision, UnexpectedAction, WrongTiming, UnknownObject, IllegalLocation, InsufficientPayment,
    InvalidAssignment, InvalidSideboard, UndoNotAllowed, MatchOver,
}

/// <summary>Why an action was refused. A rejected action changes nothing.</summary>
public sealed record Rejection(RejectionCode Code, string Message);

public sealed record SubmitResult(bool Accepted, Rejection? Rejection, IReadOnlyList<GameEvent> Events)
{
    public static SubmitResult Reject(RejectionCode code, string message) => new(false, new Rejection(code, message), []);
}

public enum GameEndReason { Points, BurnOut, Concede }

public sealed record GameOutcome(PlayerId? Winner, GameEndReason Reason);
```

Create `src/CromoBound.Engine/Actions/PlayerAction.cs`:
```csharp
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
```

Create `src/CromoBound.Engine/Decisions/Decisions.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Decisions;

/// <summary>What the engine is waiting for and from whom.</summary>
public abstract record PendingDecision(IReadOnlyList<PlayerId> Players);

public sealed record RuneOption(ObjectId Rune, bool CanExhaust);

public sealed record MoveOption(ObjectId Unit, IReadOnlyList<Place> Destinations);

public sealed record HideOption(ObjectId Card, IReadOnlyList<int> Battlefields);

/// <summary>The priority (or focus) holder's options. Playable lists cards by timing only; payment is checked when paying.</summary>
public sealed record PriorityDecision(
    PlayerId Player,
    IReadOnlyList<ObjectId> Playable,
    IReadOnlyList<RuneOption> Runes,
    IReadOnlyList<MoveOption> Moves,
    IReadOnlyList<HideOption> Hides,
    bool CanPass,
    bool CanEndTurn) : PendingDecision([Player]);

public sealed record PlayChoicesDecision(PlayerId Player, ObjectId Card, IReadOnlyList<Place> Locations, bool AccelerateAvailable)
    : PendingDecision([Player]);

/// <summary>Energy plus one power symbol per entry.</summary>
public sealed record TotalCost(int Energy, IReadOnlyList<PowerSymbol> Power);

public sealed record PaymentSuggestion(IReadOnlyList<ObjectId> Exhaust, IReadOnlyList<ObjectId> Recycle);

/// <summary>Pay <see cref="Cost"/>. <see cref="Domains"/> are the card's domains (what [C]/Self accepts). The suggestion is only a shortcut.</summary>
public sealed record PayCostDecision(PlayerId Player, TotalCost Cost, IReadOnlyList<Domain> Domains, PaymentSuggestion? Suggested)
    : PendingDecision([Player]);

public sealed record ChooseShowdownDecision(PlayerId Player, IReadOnlyList<int> Battlefields, bool Combat) : PendingDecision([Player]);

public enum DamageGroup { Tank, Normal, Backline }

public sealed record DamageTarget(ObjectId Unit, int Lethal, DamageGroup Group);

public sealed record AssignDamageDecision(
    PlayerId Player, int Battlefield, int Total, IReadOnlyList<DamageTarget> Targets, IReadOnlyList<DamageAssignment> Suggested)
    : PendingDecision([Player]);

/// <summary>Carry out the item's effect with manual actions, then submit ResolveDone.</summary>
public sealed record ResolveManuallyDecision(PlayerId Player, int ChainItem, string CardId, string Text) : PendingDecision([Player]);

public enum TurnPoint { StartOfBeginning, StartOfMain, EndOfTurn }

/// <summary>Cards in play have an effect at this point of the turn: apply them with manual actions, then submit ContinueTurn.</summary>
public sealed record TurnPointDecision(PlayerId Player, TurnPoint Point, IReadOnlyList<ObjectId> Cards) : PendingDecision([Player]);
```

Create `src/CromoBound.Engine/Events/GameEvents.cs`:
```csharp
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
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests).

- [ ] **Step 6: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests/ActionJsonTests.cs
git commit -m "feat(engine): add actions, decisions, events and results"
```

---

### Task 2: Payment, combat damage and own-keyword rules

**Files:**
- Modify: `src/CromoBound.Engine/State/PlayerState.cs`
- Create: `src/CromoBound.Engine/Rules/Payment.cs`, `src/CromoBound.Engine/Rules/CombatDamage.cs`, `src/CromoBound.Engine/Rules/CardKeywords.cs`
- Test: `tests/CromoBound.Engine.Tests/PaymentTests.cs`, `tests/CromoBound.Engine.Tests/CombatDamageTests.cs`, `tests/CromoBound.Engine.Tests/CardKeywordsTests.cs`

**Interfaces:**
- Consumes: `RunePool`, `TotalCost`, `PaymentSuggestion`, `DamageTarget`, `DamageGroup`, `DamageAssignment` (Task 1).
- Produces:
  - On `RunePool`: `RunePool.Clone()`, `CopyFrom(RunePool)`, `AddPower(Domain, int amount = 1)`.
  - `record RuneInfo(ObjectId Id, Domain Domain, bool Exhausted)`.
  - In `Payment`:
    - `Payment.CostOf(Card, bool fromHidden, bool accelerate) : TotalCost`
    - `Payment.Adjust(TotalCost, int energy, IEnumerable<PowerSymbol> add, IEnumerable<PowerSymbol> remove) : TotalCost`
    - `Payment.TryPay(RunePool, TotalCost, IReadOnlyList<Domain> cardDomains) : bool` (mutates the pool only on success)
    - `Payment.Suggest(RunePool, TotalCost, IReadOnlyList<Domain>, IReadOnlyList<RuneInfo>) : PaymentSuggestion?`
  - In `CombatDamage`:
    - `CombatDamage.Lethal(int might, int damage) : int`
    - `CombatDamage.GroupOf(bool tank, bool backline) : DamageGroup`
    - `CombatDamage.Validate(IReadOnlyList<DamageTarget>, int total, IReadOnlyList<DamageAssignment>) : string?` (null = valid)
    - `CombatDamage.Suggest(IReadOnlyList<DamageTarget>, int total) : IReadOnlyList<DamageAssignment>`
  - `CardKeywords.Own(Card) : IReadOnlySet<DisplayKeyword>`: the card's own keywords, read from the starts of its text lines.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/PaymentTests.cs`:
```csharp
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Tests;

public class PaymentTests
{
    private static readonly Domain[] FuryChaos = [Domain.Fury, Domain.Chaos];

    private static RunePool Pool(int energy = 0, int universal = 0, params (Domain Domain, int Amount)[] power)
    {
        var pool = new RunePool { Energy = energy, UniversalPower = universal };
        foreach (var (domain, amount) in power) pool.AddPower(domain, amount);
        return pool;
    }

    [Fact]
    public void Cost_is_printed_or_zero_base_from_hidden_and_accelerate_adds_energy_and_self()
    {
        var card = EngineTestDb.Create().Cards["unit-3"];

        var printed = Payment.CostOf(card, fromHidden: false, accelerate: false);
        var hidden = Payment.CostOf(card, fromHidden: true, accelerate: true);

        Assert.Equal(3, printed.Energy);
        Assert.Equal(new[] { PowerSymbol.Chaos }, printed.Power);
        Assert.Equal(1, hidden.Energy);
        Assert.Equal(new[] { PowerSymbol.Self }, hidden.Power);
    }

    [Fact]
    public void Adjusting_never_goes_below_zero_energy()
    {
        var adjusted = Payment.Adjust(new TotalCost(2, [PowerSymbol.Fury]), -5, [PowerSymbol.Any], [PowerSymbol.Fury]);

        Assert.Equal(0, adjusted.Energy);
        Assert.Equal(new[] { PowerSymbol.Any }, adjusted.Power);
    }

    [Fact]
    public void Specific_domains_are_matched_before_self_and_any()
    {
        var pool = Pool(power: [(Domain.Fury, 1), (Domain.Chaos, 1)]);

        Assert.True(Payment.TryPay(pool, new TotalCost(0, [PowerSymbol.Self, PowerSymbol.Fury]), FuryChaos));
        Assert.True(pool.IsEmpty);
    }

    [Fact]
    public void A_failed_payment_leaves_the_pool_untouched()
    {
        var pool = Pool(energy: 1, power: [(Domain.Calm, 1)]);

        Assert.False(Payment.TryPay(pool, new TotalCost(1, [PowerSymbol.Fury]), FuryChaos));
        Assert.Equal(1, pool.Energy);
        Assert.Equal(1, pool.Power[Domain.Calm]);
    }

    [Fact]
    public void Universal_power_pays_any_symbol_and_any_takes_any_domain()
    {
        var pool = Pool(universal: 1, power: [(Domain.Calm, 1)]);

        Assert.True(Payment.TryPay(pool, new TotalCost(0, [PowerSymbol.Fury, PowerSymbol.Any]), FuryChaos));
        Assert.True(pool.IsEmpty);
    }

    [Fact]
    public void Suggestion_exhausts_for_energy_and_recycles_matching_runes_preferring_exhausted_ones()
    {
        RuneInfo[] runes =
        [
            new(new(1), Domain.Fury, false), new(new(2), Domain.Fury, false),
            new(new(3), Domain.Chaos, false), new(new(4), Domain.Fury, false),
        ];

        var suggestion = Payment.Suggest(new RunePool(), new TotalCost(3, [PowerSymbol.Chaos]), FuryChaos, runes)!;

        Assert.Equal(new[] { new ObjectId(1), new ObjectId(2), new ObjectId(3) }, suggestion.Exhaust);
        Assert.Equal(new[] { new ObjectId(3) }, suggestion.Recycle);
    }

    [Fact]
    public void Suggestion_uses_the_pool_first_and_is_null_when_impossible()
    {
        RuneInfo[] runes = [new(new(1), Domain.Fury, false)];

        var fromPool = Payment.Suggest(Pool(energy: 2), new TotalCost(2, []), FuryChaos, runes)!;

        Assert.Empty(fromPool.Exhaust);
        Assert.Null(Payment.Suggest(new RunePool(), new TotalCost(1, [PowerSymbol.Calm]), FuryChaos, runes));
    }
}
```

Create `tests/CromoBound.Engine.Tests/CombatDamageTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Tests;

public class CombatDamageTests
{
    private static readonly ObjectId A = new(1), B = new(2), C = new(3);

    private static DamageTarget T(ObjectId unit, int lethal, DamageGroup group = DamageGroup.Normal) => new(unit, lethal, group);

    private static DamageAssignment D(ObjectId unit, int amount) => new(unit, amount);

    [Theory]
    [InlineData(3, 0, 3)]
    [InlineData(3, 2, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(-2, 0, 1)]
    public void Lethal_is_at_least_one_and_counts_existing_damage(int might, int damage, int expected) =>
        Assert.Equal(expected, CombatDamage.Lethal(might, damage));

    [Fact]
    public void Lethal_in_order_with_one_partial_is_valid() =>
        Assert.Null(CombatDamage.Validate([T(A, 2), T(B, 3)], 4, [D(A, 2), D(B, 2)]));

    [Fact]
    public void Two_partially_damaged_units_are_invalid() =>
        Assert.NotNull(CombatDamage.Validate([T(A, 2), T(B, 3)], 3, [D(A, 1), D(B, 2)]));

    [Fact]
    public void Excess_damage_waits_until_every_unit_has_lethal()
    {
        Assert.NotNull(CombatDamage.Validate([T(A, 2), T(B, 3)], 4, [D(A, 4)]));
        Assert.Null(CombatDamage.Validate([T(A, 2), T(B, 3)], 7, [D(A, 4), D(B, 3)]));
    }

    [Fact]
    public void Tanks_first_and_backline_last()
    {
        DamageTarget[] targets = [T(A, 2, DamageGroup.Tank), T(B, 2), T(C, 2, DamageGroup.Backline)];

        Assert.NotNull(CombatDamage.Validate(targets, 2, [D(B, 2)]));
        Assert.NotNull(CombatDamage.Validate(targets, 4, [D(A, 2), D(C, 2)]));
        Assert.Null(CombatDamage.Validate(targets, 5, [D(A, 2), D(B, 2), D(C, 1)]));
    }

    [Fact]
    public void Total_must_match_and_targets_must_be_eligible()
    {
        Assert.NotNull(CombatDamage.Validate([T(A, 2)], 2, [D(A, 1)]));
        Assert.NotNull(CombatDamage.Validate([T(A, 2)], 2, [D(B, 2)]));
    }

    [Fact]
    public void Suggestion_fills_lethal_in_group_order_and_puts_overflow_on_the_last_unit() =>
        Assert.Equal(new[] { D(A, 1), D(B, 4) }, CombatDamage.Suggest([T(B, 2), T(A, 1, DamageGroup.Tank)], 5));
}
```

Create `tests/CromoBound.Engine.Tests/CardKeywordsTests.cs`:
```csharp
using CromoBound.Engine.Rules;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Tests;

public class CardKeywordsTests
{
    private static IReadOnlySet<DisplayKeyword> Own(string rich) =>
        CardKeywords.Own(new Card { Id = "x", Name = "x", Type = CardType.Unit, Text = new CardText { Rich = rich } });

    [Theory]
    [InlineData("<p>[Tank] (I must be assigned combat damage first.)</p>", new[] { DisplayKeyword.Tank })]
    [InlineData("<p>[Accelerate] (You may pay more.)<br />[Ganking]</p>", new[] { DisplayKeyword.Accelerate, DisplayKeyword.Ganking })]
    [InlineData("<p>[Shield 2] [Tank]</p>", new[] { DisplayKeyword.Shield, DisplayKeyword.Tank })]
    [InlineData("<p>[Quick-Draw]</p>", new[] { DisplayKeyword.QuickDraw })]
    [InlineData("<p>[Reaction] (Play any time.)<br />Deal 2 to a unit.</p>", new[] { DisplayKeyword.Reaction })]
    public void Keywords_starting_a_line_are_the_cards_own(string rich, DisplayKeyword[] expected) =>
        Assert.Equal(expected.Order(), Own(rich).Order());

    [Theory]
    [InlineData("<p>[Reaction][&gt;] :rb_exhaust:: [Add] :rb_energy_1:.</p>")]
    [InlineData("<p>:rb_exhaust:: [Reaction] — Pay any amount of Energy.</p>")]
    [InlineData("<p>Give a unit [Shield 3] and [Tank] this turn.</p>")]
    [InlineData("<p>While I'm buffed, I have [Ganking].</p>")]
    [InlineData("<p>Units here with [Temporary] have [Shield].</p>")]
    [InlineData("<p>[Add] :rb_energy_1:.</p>")]
    public void Granted_conditional_and_ability_keywords_are_not_the_cards_own(string rich) =>
        Assert.Empty(Own(rich));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~PaymentTests|FullyQualifiedName~CombatDamageTests|FullyQualifiedName~CardKeywordsTests"`
Expected: FAIL (compilation errors: `Payment`, `CombatDamage`, `CardKeywords`, `RuneInfo`, `RunePool.AddPower` not found).

- [ ] **Step 3: Implement**

In `src/CromoBound.Engine/State/PlayerState.cs`, add these members to `RunePool` after `Clear()`:
```csharp
    public void AddPower(Domain domain, int amount = 1) => Power[domain] = Power.GetValueOrDefault(domain) + amount;

    public RunePool Clone()
    {
        var copy = new RunePool { Energy = Energy, UniversalPower = UniversalPower };
        foreach (var (domain, amount) in Power) copy.Power[domain] = amount;
        return copy;
    }

    public void CopyFrom(RunePool other)
    {
        Energy = other.Energy;
        UniversalPower = other.UniversalPower;
        Power.Clear();
        foreach (var (domain, amount) in other.Power) Power[domain] = amount;
    }
```

Create `src/CromoBound.Engine/Rules/Payment.cs`:
```csharp
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

/// <summary>A rune in a player's Base, as payment sees it.</summary>
public sealed record RuneInfo(ObjectId Id, Domain Domain, bool Exhausted);

/// <summary>Cost calculation and payment matching (CR 356–357, spec §7.4).</summary>
public static class Payment
{
    private static readonly IReadOnlyList<Domain> AllDomains = [.. Enum.GetValues<Domain>().Where(d => d != Domain.Colorless)];

    /// <summary>The printed cost (0 base when played from Hidden), plus Accelerate: 1 energy and one [C] (Self) symbol.</summary>
    public static TotalCost CostOf(Card card, bool fromHidden, bool accelerate)
    {
        var energy = fromHidden ? 0 : card.Cost?.Energy ?? 0;
        var power = new List<PowerSymbol>();
        if (!fromHidden && card.Cost is { } cost) power.AddRange(cost.Power);
        if (accelerate)
        {
            energy += 1;
            power.Add(PowerSymbol.Self);
        }
        return new TotalCost(energy, power);
    }

    /// <summary>Applies a player's cost adjustment. Missing symbols to remove are ignored; energy never goes below 0.</summary>
    public static TotalCost Adjust(TotalCost cost, int energy, IEnumerable<PowerSymbol> add, IEnumerable<PowerSymbol> remove)
    {
        var power = cost.Power.ToList();
        foreach (var symbol in remove) power.Remove(symbol);
        power.AddRange(add);
        return new TotalCost(Math.Max(0, cost.Energy + energy), power);
    }

    /// <summary>Pays the cost from the pool. Returns false and leaves the pool untouched when it can't.</summary>
    public static bool TryPay(RunePool pool, TotalCost cost, IReadOnlyList<Domain> cardDomains)
    {
        var work = pool.Clone();
        if (work.Energy < cost.Energy) return false;
        work.Energy -= cost.Energy;
        foreach (var symbol in cost.Power.OrderBy(Rank))
            if (!TakePower(work, Accepts(symbol, cardDomains))) return false;
        pool.CopyFrom(work);
        return true;
    }

    /// <summary>A payment that works: the pool first, then ready runes exhausted for energy, then runes recycled for power
    /// (preferring runes already exhausted). Null when the runes can't cover the cost.</summary>
    public static PaymentSuggestion? Suggest(RunePool pool, TotalCost cost, IReadOnlyList<Domain> cardDomains, IReadOnlyList<RuneInfo> runes)
    {
        var work = pool.Clone();
        var energyNeeded = Math.Max(0, cost.Energy - work.Energy);
        var exhaust = runes.Where(r => !r.Exhausted).Take(energyNeeded).ToList();
        if (exhaust.Count < energyNeeded) return null;
        work.Energy += exhaust.Count - cost.Energy;

        var recycle = new List<RuneInfo>();
        foreach (var symbol in cost.Power.OrderBy(Rank))
        {
            var accepted = Accepts(symbol, cardDomains);
            if (TakePower(work, accepted)) continue;
            var rune = runes
                .Where(r => !recycle.Contains(r) && accepted.Contains(r.Domain))
                .OrderBy(r => r.Exhausted || exhaust.Contains(r) ? 0 : 1)
                .FirstOrDefault();
            if (rune is null) return null;
            recycle.Add(rune);
        }
        return new PaymentSuggestion([.. exhaust.Select(r => r.Id)], [.. recycle.Select(r => r.Id)]);
    }

    /// <summary>Specific domains first, then [C] (Self), then [A] (Any): the narrowest symbols claim power first.</summary>
    private static int Rank(PowerSymbol symbol) => symbol switch
    {
        PowerSymbol.Self => 1,
        PowerSymbol.Any => 2,
        _ => 0,
    };

    /// <summary>Domains a symbol accepts: its own; Self any of the card's domains (any domain if it has none, CR 135.2.e.6); Any every domain.</summary>
    private static IReadOnlyList<Domain> Accepts(PowerSymbol symbol, IReadOnlyList<Domain> cardDomains)
    {
        var colored = cardDomains.Where(d => d != Domain.Colorless).ToList();
        return symbol switch
        {
            PowerSymbol.Any => AllDomains,
            PowerSymbol.Self => colored.Count > 0 ? colored : AllDomains,
            _ => [Enum.Parse<Domain>(symbol.ToString())],
        };
    }

    /// <summary>Takes one power from the accepted domain with the most available, else one universal power.</summary>
    private static bool TakePower(RunePool pool, IReadOnlyList<Domain> accepted)
    {
        Domain? best = null;
        foreach (var domain in accepted)
            if (pool.Power.GetValueOrDefault(domain) > 0 && (best is null || pool.Power[domain] > pool.Power[best.Value]))
                best = domain;
        if (best is { } chosen)
        {
            pool.Power[chosen]--;
            return true;
        }
        if (pool.UniversalPower == 0) return false;
        pool.UniversalPower--;
        return true;
    }
}
```

Create `src/CromoBound.Engine/Rules/CombatDamage.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;

namespace CromoBound.Engine.Rules;

/// <summary>Combat damage assignment rules (CR 465.2.c, spec §7.8).</summary>
public static class CombatDamage
{
    /// <summary>Damage still needed to kill: at least 1, enough to reach Might (negative Might counts as 0).</summary>
    public static int Lethal(int might, int damage) => Math.Max(1, Math.Max(might, 0) - damage);

    public static DamageGroup GroupOf(bool tank, bool backline) => tank ? DamageGroup.Tank : backline ? DamageGroup.Backline : DamageGroup.Normal;

    /// <summary>Null when the assignment is legal; otherwise the reason.</summary>
    public static string? Validate(IReadOnlyList<DamageTarget> targets, int total, IReadOnlyList<DamageAssignment> assignments)
    {
        var amounts = targets.ToDictionary(t => t.Unit, _ => 0);
        foreach (var assignment in assignments)
        {
            if (!amounts.ContainsKey(assignment.Unit)) return $"{assignment.Unit} is not a unit you can assign damage to.";
            if (assignment.Amount < 0) return "Damage amounts can't be negative.";
            amounts[assignment.Unit] += assignment.Amount;
        }

        var sum = amounts.Values.Sum();
        if (sum != total) return $"Assign exactly {total} damage (assigned {sum}).";
        if (targets.All(t => amounts[t.Unit] >= t.Lethal)) return null;
        if (targets.Any(t => amounts[t.Unit] > t.Lethal))
            return "No unit may take more than lethal damage until every unit has lethal damage.";
        if (targets.Count(t => amounts[t.Unit] > 0 && amounts[t.Unit] < t.Lethal) > 1)
            return "Each unit must take lethal damage before the next one takes any.";
        foreach (var group in Enum.GetValues<DamageGroup>())
        {
            var laterHit = targets.Any(t => t.Group > group && amounts[t.Unit] > 0);
            var groupUnfinished = targets.Any(t => t.Group == group && amounts[t.Unit] < t.Lethal);
            if (laterHit && groupUnfinished)
                return group == DamageGroup.Tank
                    ? "Tank units must take lethal damage first."
                    : "Backline units take damage only after every other unit has lethal damage.";
        }
        return null;
    }

    /// <summary>Lethal damage in group order (keeping the given order within a group); any excess goes to the last unit.</summary>
    public static IReadOnlyList<DamageAssignment> Suggest(IReadOnlyList<DamageTarget> targets, int total)
    {
        var result = new List<DamageAssignment>();
        var remaining = total;
        foreach (var target in targets.OrderBy(t => t.Group))
        {
            if (remaining == 0) break;
            var amount = Math.Min(remaining, target.Lethal);
            result.Add(new DamageAssignment(target.Unit, amount));
            remaining -= amount;
        }
        if (remaining > 0 && result.Count > 0)
            result[^1] = result[^1] with { Amount = result[^1].Amount + remaining };
        return result;
    }
}
```

Create `src/CromoBound.Engine/Rules/CardKeywords.cs`:
```csharp
using System.Text.RegularExpressions;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Rules;

/// <summary>
/// The keywords a card itself has (spec §7.10): bracketed keywords that start a line of its text. Keywords it grants to others
/// ("give a unit [Tank]"), has only conditionally ("while buffed, I have [Ganking]"), or that time one of its abilities
/// ("[Reaction][>] …", "cost: [Reaction] — …") don't count. The importer's keyword list holds every bracketed term and is for search only.
/// </summary>
public static partial class CardKeywords
{
    public static IReadOnlySet<DisplayKeyword> Own(Card card)
    {
        var result = new HashSet<DisplayKeyword>();
        foreach (var line in RichText.Lines(card.Text.Rich))
        {
            var leading = LeadingKeywords().Match(line);
            if (!leading.Success) continue;
            var rest = line[leading.Length..];
            if (rest.StartsWith("[&gt;]", StringComparison.Ordinal) || rest.StartsWith("[>]", StringComparison.Ordinal)) continue;
            foreach (Capture name in leading.Groups["name"].Captures)
                if (Enum.TryParse<DisplayKeyword>(name.Value.Replace("-", ""), out var keyword) && Enum.IsDefined(keyword))
                    result.Add(keyword);
        }
        return result;
    }

    /// <summary>One or more bracketed terms at the start of a line, each optionally with a number ("[Shield 2]").</summary>
    [GeneratedRegex(@"^(?:\[(?<name>[A-Za-z-]+)(?:\s+\d+)?\]\s*)+")]
    private static partial Regex LeadingKeywords();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests/PaymentTests.cs tests/CromoBound.Engine.Tests/CombatDamageTests.cs tests/CromoBound.Engine.Tests/CardKeywordsTests.cs
git commit -m "feat(engine): add payment, combat damage and own-keyword rules"
```

---

### Task 3: Game core and turn structure

**Files:**
- Create: `src/CromoBound.Engine/State/ShowdownState.cs`
- Modify: `src/CromoBound.Engine/State/GameState.cs`
- Create: `src/CromoBound.Engine/Rules/GameTask.cs`, `Game.cs`, `Game.Mutations.cs`, `Game.Turn.cs`, `Game.Priority.cs`, `Game.Dispatch.cs`, `Game.Cleanup.cs`
- Modify: `tests/CromoBound.Engine.Tests/TestGame.cs`, `tests/CromoBound.Engine.Tests/EngineTestDb.cs`
- Test: `tests/CromoBound.Engine.Tests/TurnTests.cs`

**Interfaces:**
- Consumes: Tasks 1–2; Plan A's `GameState`, `TestGame`, `EngineTestDb`.
- Produces:
  - `ShowdownState(int battlefield) { Battlefield, IsCombat, Attacker, Defender, Passes }`.
  - `GameState.Showdown`, `GameState.StagedShowdowns`, `GameState.StagedCombats` (`SortedSet<int>`).
  - `Game` (public), `const VictoryScore = 8`:
    - `Game(GameState, CardDatabase)`; properties `State`, `Db`, `Pending`, `Outcome`;
    - `Start(PlayerId firstPlayer) : IReadOnlyList<GameEvent>`, `Continue() : IReadOnlyList<GameEvent>`, `Submit(PlayerId, PlayerAction) : SubmitResult`, `MightOf(ObjectId) : int`.
  - Internal, used by later tasks:
    - loop and decisions: `Ask(decision, handler)`, `Reject(code, message)`, `Emit(event)`, `Enqueue(task)`, `Push(task)`, `MarkDirty()`, `CleanupDone()`, `End(winner, reason)`;
    - queries: `CardOf(...)`, `Has(instance, keyword)`, `IsUnit(instance)`, `UnitsAt(place)`, `BoardUnits()`, `RunesOf(player)`, `IsClosed`, `PriorityOptions(player)`, `HasWon(player)`;
    - mutations: `MoveCard(id, to, position)`, `SetStatus(id, status, value)`, `DealDamage`, `HealAllUnits`, `GainPoints`, `Kill`, `Recall`, `Draw`, `BurnOut`, `Channel`, `Score`, `EstablishControl`, `LoseControl`, `ApplyRune`;
    - property `ResolvingManually`.
  - `GameTask { Started, Run(Game) }`, `StepTask(Action<Game>)`, `CleanupTask(CleanupMode)`, `enum CleanupMode { Normal, Ending, Combat }`.
  - Test helpers:
    - `TestGame.Start(PlayerId? first = null, int filler = 10) : Game` (adds `filler` × `unit-2` to each Main Deck), `TestGame.StartEvents`, `TestGame.Runes(player, runeId, count)`, `TestGame.First(place, cardId)`;
    - extensions `game.Accept(player, action) : SubmitResult`, `game.Decision<T>() : T`, `game.PayWithSuggestion(player)`.

- [ ] **Step 1: Extend the test helpers**

Replace all of `tests/CromoBound.Engine.Tests/TestGame.cs` with:
```csharp
using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Random;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Tests;

/// <summary>Builds an exact game situation for a rules test, without going through setup.</summary>
internal sealed class TestGame
{
    public static readonly PlayerId P1 = new(0);
    public static readonly PlayerId P2 = new(1);

    public TestGame(ulong seed = 1)
    {
        Db = EngineTestDb.Create();
        State = new GameState(2, new SeededRandom(seed));
        AddBattlefield("bf-a", P1);
        AddBattlefield("bf-b", P2);
    }

    public CardDatabase Db { get; }
    public GameState State { get; }

    /// <summary>The events returned by <see cref="Game.Start"/>.</summary>
    public IReadOnlyList<GameEvent> StartEvents { get; private set; } = [];

    /// <summary>Puts a card straight into a place. The owner defaults to the place's player, else P1.</summary>
    public ObjectId Put(string cardId, Place place, PlayerId? owner = null)
    {
        var isToken = Db.Cards[cardId].Supertype == Supertype.Token;
        return State.Create(cardId, isToken ? null : $"p-{cardId}", owner ?? place.Player ?? P1, place, isToken);
    }

    public void Runes(PlayerId player, string runeId, int count)
    {
        for (var i = 0; i < count; i++) Put(runeId, Place.Base(player));
    }

    /// <summary>The first object at a place with this card id.</summary>
    public ObjectId First(Place place, string cardId) => State.At(place).First(id => State[id].CardId == cardId);

    /// <summary>Gives each Main Deck <paramref name="filler"/> copies of unit-2 (so draws don't burn out) and starts with <paramref name="first"/>'s turn.</summary>
    public Game Start(PlayerId? first = null, int filler = 10)
    {
        foreach (var player in new[] { P1, P2 })
            for (var i = 0; i < filler; i++) Put("unit-2", Place.MainDeck(player));
        var game = new Game(State, Db);
        StartEvents = game.Start(first ?? P1);
        return game;
    }

    private void AddBattlefield(string cardId, PlayerId owner)
    {
        var index = State.Battlefields.Count;
        State.Battlefields.Add(new BattlefieldState(index, Put(cardId, Place.BattlefieldCard(index), owner)));
    }
}

internal static class GameTestExtensions
{
    /// <summary>Submits and asserts the action was accepted.</summary>
    public static SubmitResult Accept(this Game game, PlayerId player, PlayerAction action)
    {
        var result = game.Submit(player, action);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    public static T Decision<T>(this Game game) where T : PendingDecision => Assert.IsType<T>(game.Pending);

    public static SubmitResult PayWithSuggestion(this Game game, PlayerId player)
    {
        var suggestion = game.Decision<PayCostDecision>().Suggested;
        Assert.NotNull(suggestion);
        return game.Accept(player, new PayCost { Exhaust = suggestion.Exhaust, Recycle = suggestion.Recycle });
    }
}
```

Then, in `tests/CromoBound.Engine.Tests/EngineTestDb.cs`, write each test card's keywords into its text, because the engine reads a card's own keywords from its text lines (`CardKeywords`). Add this helper:
```csharp
    /// <summary>"<p>[Tank]<br />body</p>": keywords on their own lines first, as on real cards.</summary>
    private static string RichWith(string body, DisplayKeyword[]? keywords) =>
        $"<p>{string.Concat((keywords ?? []).Select(k => $"[{k}]<br />"))}{body}</p>";
```
In `Unit`, replace `Text = new CardText { Rich = $"<p>{id}</p>" },` with:
```csharp
        Text = new CardText { Rich = RichWith(id, keywords) },
```
In `Spell`, replace `Cost = new CardCost { Energy = 1 }, Text = new CardText { Rich = $"<p>{id} text</p>" },` with:
```csharp
        Cost = new CardCost { Energy = 1 }, Text = new CardText { Rich = RichWith($"{id} text", keywords) },
```

- [ ] **Step 2: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/TurnTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class TurnTests
{
    [Fact]
    public void First_turn_channels_two_runes_draws_one_and_gives_priority()
    {
        var game = new TestGame();
        for (var i = 0; i < 5; i++) game.Put("fury-rune", Place.RuneDeck(P1));

        var engine = game.Start();

        Assert.Equal(2, game.State.At(Place.Base(P1)).Count);
        Assert.Single(game.State.At(Place.Hand(P1)));
        Assert.Equal(9, game.State.At(Place.MainDeck(P1)).Count);
        Assert.Equal(Phase.Main, game.State.Turn.Phase);
        var priority = engine.Decision<PriorityDecision>();
        Assert.Equal(P1, priority.Player);
        Assert.True(priority.CanEndTurn);
        Assert.False(priority.CanPass);
    }

    [Fact]
    public void Second_player_channels_an_extra_rune_on_their_first_turn()
    {
        var game = new TestGame();
        for (var i = 0; i < 5; i++) game.Put("chaos-rune", Place.RuneDeck(P2));
        var engine = game.Start();

        engine.Accept(P1, new EndTurn());

        Assert.Equal(2, game.State.Turn.Number);
        Assert.Equal(P2, game.State.Turn.TurnPlayer);
        Assert.Equal(3, game.State.At(Place.Base(P2)).Count);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Runes_float_energy_pools_empty_at_end_of_turn_and_awaken_readies()
    {
        var game = new TestGame();
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();
        var rune = game.State.At(Place.Base(P1))[0];

        engine.Accept(P1, new UseRune(rune, RuneUse.Exhaust));
        Assert.Equal(1, game.State.Player(P1).Pool.Energy);
        Assert.True(game.State[rune].Exhausted);

        engine.Accept(P1, new EndTurn());
        Assert.True(game.State.Player(P1).Pool.IsEmpty);

        engine.Accept(P2, new EndTurn());
        Assert.False(game.State[rune].Exhausted);
    }

    [Fact]
    public void Wrong_player_or_wrong_action_is_rejected_without_changes()
    {
        var game = new TestGame();
        var engine = game.Start();

        var wrongPlayer = engine.Submit(P2, new EndTurn());
        var wrongAction = engine.Submit(P1, new Pass());

        Assert.Equal(RejectionCode.NotYourDecision, wrongPlayer.Rejection!.Code);
        Assert.Equal(RejectionCode.UnexpectedAction, wrongAction.Rejection!.Code);
        Assert.Equal(1, game.State.Turn.Number);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Empty_deck_burns_out_giving_the_opponent_a_point()
    {
        var game = new TestGame();
        game.Put("unit-3", Place.Trash(P1));
        game.Put("unit-3", Place.Trash(P1));

        game.Start(filler: 0);

        Assert.Equal(1, game.State.Player(P2).Points);
        Assert.Single(game.State.At(Place.Hand(P1)));
        Assert.Single(game.State.At(Place.MainDeck(P1)));
        Assert.Empty(game.State.At(Place.Trash(P1)));
        Assert.Contains(game.StartEvents, e => e is BurnedOut b && b.Player == P1);
    }

    [Fact]
    public void Reaching_eight_points_with_the_lead_wins_in_cleanup()
    {
        var game = new TestGame();
        game.State.Player(P2).Points = 7;

        var engine = game.Start(filler: 0);

        Assert.Equal(new GameOutcome(P2, GameEndReason.Points), engine.Outcome);
        Assert.Null(engine.Pending);
        Assert.Equal(RejectionCode.MatchOver, engine.Submit(P1, new EndTurn()).Rejection!.Code);
    }

    [Fact]
    public void A_tie_at_eight_continues()
    {
        var game = new TestGame();
        game.State.Player(P1).Points = 8;
        game.State.Player(P2).Points = 8;

        var engine = game.Start();

        Assert.Null(engine.Outcome);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Hold_scores_controlled_battlefields_at_the_start_of_turn()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        game.Put("unit-2", Place.Battlefield(0));

        game.Start();

        Assert.Equal(1, game.State.Player(P1).Points);
        Assert.Contains(game.StartEvents, e => e is BattlefieldScored { Kind: ScoreKind.Hold, Battlefield: 0, GainedPoint: true });
    }

    [Fact]
    public void Temporary_permanents_die_at_the_start_of_their_controllers_turn()
    {
        var game = new TestGame();
        game.Put("temp-1", Place.Base(P1));

        game.Start();

        Assert.Empty(game.State.At(Place.Base(P1)));
        Assert.Single(game.State.At(Place.Trash(P1)));
    }

    [Fact]
    public void Ending_kills_lethal_damage_then_heals_and_expires_this_turn_effects()
    {
        var game = new TestGame();
        var survivor = game.Put("unit-3", Place.Base(P1));
        var doomed = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        var unit = game.State[survivor];
        unit.Damage = 1;
        unit.Stunned = true;
        unit.Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        unit.Modifiers.Add(new MightModifier(1, Duration.Permanent));
        game.State[doomed].Damage = 2;

        engine.Accept(P1, new EndTurn());

        Assert.Equal(0, unit.Damage);
        Assert.False(unit.Stunned);
        Assert.Equal(4, engine.MightOf(survivor));
        Assert.False(game.State.Exists(doomed));
        Assert.Single(game.State.At(Place.Trash(P1)));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~TurnTests"`
Expected: FAIL (compilation errors: `Game` not found).

- [ ] **Step 4: Add showdown state**

Create `src/CromoBound.Engine/State/ShowdownState.cs`:
```csharp
namespace CromoBound.Engine.State;

/// <summary>The showdown in progress (CR 341–348). A combat showdown also has an attacker and a defender (CR 464).</summary>
public sealed class ShowdownState(int battlefield)
{
    public int Battlefield { get; } = battlefield;
    public bool IsCombat { get; set; }
    public PlayerId? Attacker { get; set; }
    public PlayerId? Defender { get; set; }

    /// <summary>Consecutive passes with an empty chain; it ends when every player has passed in a row.</summary>
    public int Passes { get; set; }
}
```

In `src/CromoBound.Engine/State/GameState.cs`, add after the `Chain` property:
```csharp
    /// <summary>The showdown or combat in progress, if any.</summary>
    public ShowdownState? Showdown { get; set; }

    /// <summary>Battlefields with a staged, not yet started showdown or combat (cleanup steps 6–7).</summary>
    public SortedSet<int> StagedShowdowns { get; } = [];
    public SortedSet<int> StagedCombats { get; } = [];
```

- [ ] **Step 5: Implement the task queue and the core**

Create `src/CromoBound.Engine/Rules/GameTask.cs`:
```csharp
namespace CromoBound.Engine.Rules;

/// <summary>A piece of mandatory rules procedure (CR 333). Tracks its own progress so it can pause for a decision.</summary>
internal abstract class GameTask
{
    /// <summary>True once the task has run; cleanups never interrupt a started task.</summary>
    public bool Started { get; set; }

    /// <summary>True when finished. False when not finished yet (usually after asking a player); it runs again later.</summary>
    public abstract bool Run(Game game);
}

internal sealed class StepTask(Action<Game> step) : GameTask
{
    public override bool Run(Game game)
    {
        step(game);
        return true;
    }
}
```

Create `src/CromoBound.Engine/Rules/Game.cs`:
```csharp
using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Rules;

/// <summary>Runs one game: validates actions against the pending decision and applies the rules until someone must decide.</summary>
public sealed partial class Game
{
    public const int VictoryScore = 8;

    private readonly List<GameTask> _tasks = [];
    private readonly List<GameEvent> _events = [];
    private Func<PlayerId, PlayerAction, Rejection?>? _handler;
    private int _nextSequence = 1;
    private bool _cleanupNeeded;
    private readonly Dictionary<string, IReadOnlySet<DisplayKeyword>> _ownKeywords = [];

    public Game(GameState state, CardDatabase db)
    {
        State = state;
        Db = db;
    }

    public GameState State { get; }
    public CardDatabase Db { get; }

    /// <summary>What the engine waits for. Null only when the game is over.</summary>
    public PendingDecision? Pending { get; private set; }

    public GameOutcome? Outcome { get; private set; }

    /// <summary>Counts board changes; a cleanup repeats until a pass leaves it unchanged (CR 322).</summary>
    internal int Changes { get; private set; }

    /// <summary>True while a chain item is resolved by hand; cleanups wait until it's done (CR 321).</summary>
    internal bool ResolvingManually { get; set; }

    /// <summary>Starts the first turn and runs until the first decision.</summary>
    public IReadOnlyList<GameEvent> Start(PlayerId firstPlayer)
    {
        Enqueue(new StepTask(g => g.StartTurn(firstPlayer)));
        return Continue();
    }

    /// <summary>Runs the rules until a decision is needed and returns the events produced since the last call.</summary>
    public IReadOnlyList<GameEvent> Continue()
    {
        RunLoop();
        var events = _events.ToList();
        _events.Clear();
        return events;
    }

    public SubmitResult Submit(PlayerId player, PlayerAction action)
    {
        if (Outcome is not null) return SubmitResult.Reject(RejectionCode.MatchOver, "The game is over.");
        if (Pending is null || _handler is null) throw new InvalidOperationException("The game is running but nothing is pending.");
        if (!Pending.Players.Contains(player)) return SubmitResult.Reject(RejectionCode.NotYourDecision, $"{player} is not the one deciding now.");

        var (decision, handler) = (Pending, _handler);
        Pending = null;
        _handler = null;
        if (handler(player, action) is { } rejection)
        {
            if (Pending is null) (Pending, _handler) = (decision, handler);
            return new SubmitResult(false, rejection, []);
        }
        return new SubmitResult(true, null, Continue());
    }

    /// <summary>Current Might: printed + buff + active modifiers (spec §5).</summary>
    public int MightOf(ObjectId id)
    {
        var unit = State[id];
        return (CardOf(unit).Might ?? 0) + (unit.Buffed ? 1 : 0) + unit.Modifiers.Sum(m => m.Amount);
    }

    /// <summary>Raises a decision. The handler validates the answer first, then applies it; it returns a rejection to keep waiting.</summary>
    internal void Ask(PendingDecision decision, Func<PlayerId, PlayerAction, Rejection?> handler)
    {
        Pending = decision;
        _handler = handler;
    }

    internal static Rejection Reject(RejectionCode code, string message) => new(code, message);

    internal void Emit(GameEvent gameEvent)
    {
        gameEvent.Sequence = _nextSequence++;
        _events.Add(gameEvent);
    }

    internal void Enqueue(GameTask task) => _tasks.Add(task);

    /// <summary>Runs <paramref name="task"/> before every other queued task.</summary>
    internal void Push(GameTask task) => _tasks.Insert(0, task);

    /// <summary>Records a board change: a cleanup is now outstanding (CR 319).</summary>
    internal void MarkDirty()
    {
        Changes++;
        _cleanupNeeded = true;
    }

    internal void CleanupDone() => _cleanupNeeded = false;

    internal void End(PlayerId? winner, GameEndReason reason)
    {
        if (Outcome is not null) return;
        Outcome = new GameOutcome(winner, reason);
        Pending = null;
        _handler = null;
        Emit(new GameEnded(winner, reason));
    }

    internal Card CardOf(CardInstance instance) => Db.Cards[instance.CardId];

    internal Card CardOf(ObjectId id) => CardOf(State[id]);

    /// <summary>Whether the card itself has the keyword (spec §7.10): only keywords starting a text line count, see <see cref="CardKeywords"/>.</summary>
    internal bool Has(CardInstance instance, DisplayKeyword keyword)
    {
        if (!_ownKeywords.TryGetValue(instance.CardId, out var own))
            _ownKeywords[instance.CardId] = own = CardKeywords.Own(CardOf(instance));
        return own.Contains(keyword);
    }

    internal bool IsUnit(CardInstance instance) => CardOf(instance).Type == CardType.Unit;

    internal List<CardInstance> UnitsAt(Place location) => [.. State.At(location).Select(id => State[id]).Where(IsUnit)];

    internal IEnumerable<CardInstance> BoardUnits() => State.Objects.Where(o => o.Place.IsLocation && IsUnit(o));

    internal List<RuneInfo> RunesOf(PlayerId player) =>
    [
        .. State.At(Place.Base(player)).Select(id => State[id])
            .Where(o => o.Controller == player && CardOf(o).Type == CardType.Rune)
            .Select(o => new RuneInfo(o.Id, CardOf(o).Domains[0], o.Exhausted)),
    ];

    internal bool HasWon(PlayerId player)
    {
        var points = State.Player(player).Points;
        return points >= VictoryScore && State.Players.All(p => p.Id == player || p.Points < points);
    }

    /// <summary>Handle outstanding tasks, then the chain, the showdown, the Main phase (CR 334–336).</summary>
    private void RunLoop()
    {
        while (Outcome is null && Pending is null)
        {
            if (_cleanupNeeded && !ResolvingManually && (_tasks.Count == 0 || !_tasks[0].Started))
            {
                _cleanupNeeded = false;
                Push(new CleanupTask(CleanupMode.Normal));
                continue;
            }
            if (_tasks.Count > 0)
            {
                var task = _tasks[0];
                task.Started = true;
                if (task.Run(this)) _tasks.Remove(task);
                continue;
            }
            AskPriority();
        }
    }
}
```

- [ ] **Step 6: Implement the mutations**

Create `src/CromoBound.Engine/Rules/Game.Mutations.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Moves a card. A move between two hidden places emits a private event for the owner (or the facedown card's
    /// controller) plus an anonymous public one; any other move is public.</summary>
    internal ObjectId? MoveCard(ObjectId id, Place to, DeckPosition position = DeckPosition.Top)
    {
        var instance = State[id];
        var from = instance.Place;
        var cardId = instance.CardId;
        var viewer = from.Kind == PlaceKind.Facedown ? instance.Controller : instance.Owner;
        var newId = State.Move(id, to, position);
        var landed = newId is { } moved ? State[moved].Place : to;
        if (IsHidden(from) && IsHidden(landed))
        {
            Emit(new CardMoved(cardId, id, newId, from, landed) { VisibleTo = viewer });
            Emit(new CardMoved(null, null, null, from, landed));
        }
        else
        {
            Emit(new CardMoved(cardId, id, newId, from, landed));
        }
        MarkDirty();
        return newId;
    }

    private static bool IsHidden(Place place) =>
        place.Kind is PlaceKind.Hand or PlaceKind.MainDeck or PlaceKind.RuneDeck or PlaceKind.Facedown;

    internal void SetStatus(ObjectId id, StatusKind status, bool value)
    {
        var instance = State[id];
        var current = status switch
        {
            StatusKind.Exhausted => instance.Exhausted,
            StatusKind.Stunned => instance.Stunned,
            StatusKind.Buffed => instance.Buffed,
            _ => instance.Empowered,
        };
        if (current == value) return;
        switch (status)
        {
            case StatusKind.Exhausted: instance.Exhausted = value; break;
            case StatusKind.Stunned: instance.Stunned = value; break;
            case StatusKind.Buffed: instance.Buffed = value; break;
            default: instance.Empowered = value; break;
        }
        Emit(new StatusChanged(id, status, value));
        MarkDirty();
    }

    internal void DealDamage(ObjectId unit, int amount)
    {
        if (amount <= 0) return;
        State[unit].Damage += amount;
        Emit(new DamageDealt(unit, amount));
        MarkDirty();
    }

    internal void HealAllUnits()
    {
        var damaged = BoardUnits().Where(u => u.Damage > 0).ToList();
        if (damaged.Count == 0) return;
        foreach (var unit in damaged) unit.Damage = 0;
        Emit(new UnitsHealed());
        MarkDirty();
    }

    /// <summary>Points never go below 0 (CR 194.4).</summary>
    internal void GainPoints(PlayerId player, int amount)
    {
        var state = State.Player(player);
        var points = Math.Max(0, state.Points + amount);
        if (points == state.Points) return;
        state.Points = points;
        Emit(new PointsChanged(player, points));
        MarkDirty();
    }

    internal void Kill(ObjectId unit)
    {
        var instance = State[unit];
        Emit(new UnitDied(unit, instance.CardId, instance.Controller));
        MoveCard(unit, Place.Trash(instance.Owner));
    }

    /// <summary>Returns a permanent to its controller's Base. Not a move (CR 454): damage and statuses stay.</summary>
    internal void Recall(ObjectId id) => MoveCard(id, Place.Base(State[id].Controller));

    /// <summary>Draws one card at a time; an empty Main Deck burns out first (CR 413, 431).</summary>
    internal void Draw(PlayerId player, int count)
    {
        var streak = 0;
        for (var i = 0; i < count && Outcome is null; i++)
        {
            if (State.At(Place.MainDeck(player)).Count == 0)
            {
                BurnOut(player, ++streak);
                if (State.At(Place.MainDeck(player)).Count == 0) continue;
            }
            MoveCard(State.At(Place.MainDeck(player))[0], Place.Hand(player));
            streak = 0;
        }
    }

    /// <summary>CR 431: the trash is shuffled into the Main Deck and the opponent gains 1 point. From the second burnout in a row,
    /// a point that reaches the Victory Score with the lead wins at once.</summary>
    internal void BurnOut(PlayerId player, int streak)
    {
        foreach (var card in State.At(Place.Trash(player)).ToList()) MoveCard(card, Place.MainDeck(player), DeckPosition.Bottom);
        State.Shuffle(Place.MainDeck(player));
        var opponent = State.Opponent(player);
        Emit(new BurnedOut(player, opponent));
        GainPoints(opponent, 1);
        if (streak >= 2 && HasWon(opponent)) End(opponent, GameEndReason.BurnOut);
    }

    /// <summary>Top runes of the Rune Deck to the Base, ready; as many as remain (CR 430).</summary>
    internal void Channel(PlayerId player, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var deck = State.At(Place.RuneDeck(player));
            if (deck.Count == 0) return;
            MoveCard(deck[0], Place.Base(player));
        }
    }

    /// <summary>A rune's Reaction Add (CR 429): exhaust for 1 energy, or recycle to the bottom of the Rune Deck for 1 power of its domain.</summary>
    internal void ApplyRune(PlayerId player, ObjectId rune, RuneUse use)
    {
        var pool = State.Player(player).Pool;
        if (use == RuneUse.Exhaust)
        {
            SetStatus(rune, StatusKind.Exhausted, true);
            pool.Energy++;
            Emit(new ResourcesAdded(player, 1, null));
            return;
        }
        var domain = CardOf(rune).Domains[0];
        MoveCard(rune, Place.RuneDeck(player), DeckPosition.Bottom);
        pool.AddPower(domain);
        Emit(new ResourcesAdded(player, 0, domain));
    }

    /// <summary>CR 470–471: one score per battlefield per player per turn. A Conquer for the final point needs every battlefield
    /// scored this turn (including this one); otherwise the player draws a card instead.</summary>
    internal void Score(PlayerId player, int battlefield, ScoreKind kind)
    {
        if (State.Turn.HasScored(player, battlefield)) return;
        State.Turn.MarkScored(player, battlefield);
        var finalPoint = State.Player(player).Points >= VictoryScore - 1;
        var allScored = State.Battlefields.All(b => State.Turn.HasScored(player, b.Index));
        var gains = kind == ScoreKind.Hold || !finalPoint || allScored;
        Emit(new BattlefieldScored(player, battlefield, kind, gains));
        if (gains) GainPoints(player, 1);
        else Draw(player, 1);
    }

    /// <summary>The player takes control (clearing Contested). Newly gaining control is a Conquer if not scored this turn.</summary>
    internal void EstablishControl(PlayerId player, int battlefield)
    {
        var state = State.Battlefields[battlefield];
        state.ContestedBy = null;
        MarkDirty();
        if (state.Controller == player) return;
        state.Controller = player;
        Emit(new ControlChanged(battlefield, player));
        Score(player, battlefield, ScoreKind.Conquer);
    }

    internal void LoseControl(int battlefield)
    {
        var state = State.Battlefields[battlefield];
        if (state.Controller is null) return;
        state.Controller = null;
        Emit(new ControlChanged(battlefield, null));
        MarkDirty();
    }
}
```

- [ ] **Step 7: Implement the turn structure**

Create `src/CromoBound.Engine/Rules/Game.Turn.cs`:
```csharp
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Awaken (ready everything the turn player controls), then queue Beginning, Scoring, Channel, Draw and Main (CR 315–316).</summary>
    internal void StartTurn(PlayerId player)
    {
        var turn = State.Turn;
        turn.Number++;
        turn.TurnPlayer = player;
        turn.Scored.Clear();
        turn.Priority = null;
        turn.Focus = null;
        Emit(new TurnStarted(player, turn.Number));
        EnterPhase(Phase.Awaken, TurnStep.None);
        foreach (var instance in State.Objects.Where(o => o.Controller == player && o.Place.IsBoard && o.Exhausted).ToList())
            SetStatus(instance.Id, StatusKind.Exhausted, false);
        Enqueue(new StepTask(g => g.BeginningStep()));
        Enqueue(new StepTask(g => g.ScoringStep()));
        Enqueue(new StepTask(g => g.ChannelPhase()));
        Enqueue(new StepTask(g => g.DrawPhase()));
        Enqueue(new StepTask(g => g.MainPhase()));
    }

    /// <summary>Temporary permanents the turn player controls are killed here, before scoring (CR 816.1.b).</summary>
    private void BeginningStep()
    {
        EnterPhase(Phase.Beginning, TurnStep.BeginningStep);
        var temporary = State.Objects
            .Where(o => o.Controller == State.Turn.TurnPlayer && o.Place.IsLocation && Has(o, DisplayKeyword.Temporary))
            .Select(o => o.Id)
            .ToList();
        foreach (var id in temporary) Kill(id);
    }

    private void ScoringStep()
    {
        EnterPhase(Phase.Beginning, TurnStep.ScoringStep);
        foreach (var battlefield in State.Battlefields.Where(b => b.Controller == State.Turn.TurnPlayer).ToList())
            Score(State.Turn.TurnPlayer, battlefield.Index, ScoreKind.Hold);
    }

    /// <summary>Channel 2 runes; the second player channels 3 on their first turn (turn 2, CR 485.7).</summary>
    private void ChannelPhase()
    {
        EnterPhase(Phase.Channel, TurnStep.None);
        Channel(State.Turn.TurnPlayer, State.Turn.Number == 2 ? 3 : 2);
    }

    private void DrawPhase()
    {
        EnterPhase(Phase.Draw, TurnStep.None);
        Draw(State.Turn.TurnPlayer, 1);
    }

    private void MainPhase()
    {
        EnterPhase(Phase.Main, TurnStep.None);
        foreach (var player in State.Players) player.Pool.Clear();
        State.Turn.Priority = State.Turn.TurnPlayer;
    }

    /// <summary>Ending step, Expiration step (Ending special cleanup), then the next player's turn (CR 317).</summary>
    private void EndTheTurn()
    {
        Enqueue(new StepTask(g => g.EnterPhase(Phase.Ending, TurnStep.EndingStep)));
        Enqueue(new StepTask(g =>
        {
            g.EnterPhase(Phase.Ending, TurnStep.ExpirationStep);
            g.Push(new CleanupTask(CleanupMode.Ending));
        }));
        Enqueue(new StepTask(g => g.StartTurn(g.State.Opponent(g.State.Turn.TurnPlayer))));
    }

    private void EnterPhase(Phase phase, TurnStep step)
    {
        State.Turn.Phase = phase;
        State.Turn.Step = step;
        Emit(new PhaseStarted(phase, step));
        MarkDirty();
    }
}
```

- [ ] **Step 8: Implement priority, dispatch and the first cleanup**

Create `src/CromoBound.Engine/Rules/Game.Priority.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Closed while a chain exists (CR 309).</summary>
    internal bool IsClosed => State.Chain.Count > 0;

    /// <summary>Asks whoever may act now (spec §7.2): the priority holder on a chain, the focus holder in a showdown,
    /// else the turn player in the Main phase.</summary>
    private void AskPriority()
    {
        var turn = State.Turn;
        PlayerId player;
        if (IsClosed) player = turn.Priority ?? throw new InvalidOperationException("A chain exists but nobody has priority.");
        else if (State.Showdown is not null) player = turn.Focus ?? throw new InvalidOperationException("A showdown is running but nobody has focus.");
        else if (turn.Phase == Phase.Main) player = turn.TurnPlayer;
        else throw new InvalidOperationException($"Nothing left to do in the {turn.Phase} phase.");
        turn.Priority = player;
        Ask(PriorityOptions(player), HandlePriority);
    }

    internal PriorityDecision PriorityOptions(PlayerId player)
    {
        var neutralOpenMain = IsNeutralOpenMain(player);
        return new PriorityDecision(
            player,
            [.. PlayableCards(player)],
            [.. RunesOf(player).Select(r => new RuneOption(r.Id, !r.Exhausted))],
            neutralOpenMain ? [.. MoveOptions(player)] : [],
            neutralOpenMain ? [.. HideOptions(player)] : [],
            CanPass: IsClosed || State.Showdown is not null,
            CanEndTurn: neutralOpenMain && State.StagedShowdowns.Count == 0 && State.StagedCombats.Count == 0);
    }

    private bool IsNeutralOpenMain(PlayerId player) =>
        !IsClosed && State.Showdown is null && State.Turn.Phase == Phase.Main && State.Turn.TurnPlayer == player;

    /// <summary>Cards the player may start playing now, by timing only (CR 310, 806, 811, 813). Payment is checked later.</summary>
    private IEnumerable<ObjectId> PlayableCards(PlayerId player)
    {
        var anything = IsNeutralOpenMain(player);
        foreach (var id in State.At(Place.Hand(player)).Concat(State.At(Place.ChampionZone(player))))
        {
            var card = State[id];
            var timing = anything
                || Has(card, DisplayKeyword.Reaction)
                || (!IsClosed && Has(card, DisplayKeyword.Action));
            if (timing) yield return id;
        }
        foreach (var battlefield in State.Battlefields)
            foreach (var id in State.At(Place.Facedown(battlefield.Index)))
                if (CanPlayFromHidden(State[id], player)) yield return id;
    }

    /// <summary>A hidden card gains Reaction from the turn after it was hidden (CR 811.1.b).</summary>
    private bool CanPlayFromHidden(CardInstance card, PlayerId player) =>
        card.Controller == player && card.HiddenOnTurn is { } hiddenOn && State.Turn.Number > hiddenOn;

    /// <summary>Standard Move (CR 144): a ready unit from Base to a battlefield, from a battlefield back to Base,
    /// or between battlefields with Ganking.</summary>
    private IEnumerable<MoveOption> MoveOptions(PlayerId player)
    {
        foreach (var unit in BoardUnits().Where(u => u.Controller == player && !u.Exhausted))
        {
            var destinations = new List<Place>();
            if (unit.Place.Kind == PlaceKind.Base)
            {
                destinations.AddRange(State.Battlefields.Select(b => Place.Battlefield(b.Index)));
            }
            else
            {
                destinations.Add(Place.Base(player));
                if (Has(unit, DisplayKeyword.Ganking))
                    destinations.AddRange(State.Battlefields.Where(b => b.Index != unit.Place.Index).Select(b => Place.Battlefield(b.Index)));
            }
            yield return new MoveOption(unit.Id, destinations);
        }
    }

    /// <summary>Hidden cards in hand, and battlefields the player controls with a free facedown slot (CR 421, 811).</summary>
    private IEnumerable<HideOption> HideOptions(PlayerId player)
    {
        var battlefields = State.Battlefields
            .Where(b => b.Controller == player && State.At(Place.Facedown(b.Index)).Count < BattlefieldState.FacedownCapacity)
            .Select(b => b.Index)
            .ToList();
        if (battlefields.Count == 0) yield break;
        foreach (var id in State.At(Place.Hand(player)))
            if (Has(State[id], DisplayKeyword.Hidden)) yield return new HideOption(id, battlefields);
    }

    private Rejection? UseRuneNow(PlayerId player, UseRune action)
    {
        var rune = RunesOf(player).FirstOrDefault(r => r.Id == action.Rune);
        if (rune is null) return Reject(RejectionCode.UnknownObject, $"{action.Rune} is not a rune in your Base.");
        if (action.Use == RuneUse.Exhaust && rune.Exhausted) return Reject(RejectionCode.WrongTiming, "That rune is already exhausted.");
        ApplyRune(player, action.Rune, action.Use);
        return null;
    }
}
```

Create `src/CromoBound.Engine/Rules/Game.Dispatch.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Applies an answer to a priority decision. Grows as later tasks add actions.</summary>
    private Rejection? HandlePriority(PlayerId player, PlayerAction action)
    {
        var options = PriorityOptions(player);
        switch (action)
        {
            case UseRune use:
                return UseRuneNow(player, use);
            case EndTurn when options.CanEndTurn:
                EndTheTurn();
                return null;
            default:
                return Reject(RejectionCode.UnexpectedAction, $"{action.GetType().Name} is not possible now.");
        }
    }
}
```

Create `src/CromoBound.Engine/Rules/Game.Cleanup.cs` (first version; Task 5 replaces it):
```csharp
using CromoBound.Engine.Events;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

internal enum CleanupMode { Normal, Ending, Combat }

/// <summary>CR 318–324. Runs its steps until a pass changes nothing.</summary>
internal sealed class CleanupTask(CleanupMode mode) : GameTask
{
    /// <summary>Set once the repeated steps are stable, so a later decision (Task 5) doesn't rerun them.</summary>
    public bool StableReached { get; set; }

    public override bool Run(Game game) => game.RunCleanup(mode, this);
}

public sealed partial class Game
{
    internal bool RunCleanup(CleanupMode mode, CleanupTask task)
    {
        var special = mode;
        while (true)
        {
            var before = Changes;
            if (CheckWin()) return true;
            KillLethallyDamagedUnits();
            if (special == CleanupMode.Ending) EndingSteps();
            special = CleanupMode.Normal;
            if (Changes == before) break;
        }
        task.StableReached = true;
        CleanupDone();
        return true;
    }

    /// <summary>Step 1: at least the Victory Score and more points than every opponent (CR 194.2, 323.1).</summary>
    private bool CheckWin()
    {
        foreach (var player in State.Players)
        {
            if (!HasWon(player.Id)) continue;
            End(player.Id, GameEndReason.Points);
            return true;
        }
        return false;
    }

    /// <summary>Steps 3a–3b: units with non-zero damage ≥ Might are killed.</summary>
    private void KillLethallyDamagedUnits()
    {
        foreach (var unit in BoardUnits().Where(u => u.Damage > 0 && u.Damage >= Math.Max(MightOf(u.Id), 0)).ToList())
            Kill(unit.Id);
    }

    /// <summary>Ending special cleanup, steps 3c–3e: heal all units, "this turn" effects and Stunned expire, rune pools empty (CR 317.2).</summary>
    private void EndingSteps()
    {
        HealAllUnits();
        foreach (var instance in State.Objects.ToList())
        {
            if (instance.Modifiers.RemoveAll(m => m.Duration == Duration.ThisTurn) > 0) MarkDirty();
            if (instance.Stunned) SetStatus(instance.Id, StatusKind.Stunned, false);
        }
        foreach (var player in State.Players) player.Pool.Clear();
    }
}
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests).

- [ ] **Step 10: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): add game loop and turn structure"
```

---

### Task 4: Playing cards, paying, the chain

**Files:**
- Modify: `src/CromoBound.Engine/State/ChainItem.cs`
- Create: `src/CromoBound.Engine/Rules/Game.Play.cs`, `src/CromoBound.Engine/Rules/Game.Chain.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Dispatch.cs`
- Test: `tests/CromoBound.Engine.Tests/PlayTests.cs`

**Interfaces:**
- Consumes: Task 3 internals; `Payment` (Task 2).
- Produces:
  - `ChainItem.Location`, `ChainItem.Accelerate`, `ChainItem.SourceCardId`, `ChainItem.Text`.
  - Internal, used by later tasks:
    - `AskPay(player, cost, domains, onPaid, onCancel, onAdjust)` (reused by Hide in Task 6);
    - `PlayCardTask`, `PlayStep`, `StartPlay(player, card)`;
    - `PassPriority(player)`, `AfterResolution()`, `ChainEmptied()`;
    - properties `ChainPasses`, `ChainStartedByTrigger`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/PlayTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class PlayTests
{
    private static (TestGame Game, Game Engine) Setup(Action<TestGame> arrange)
    {
        var game = new TestGame();
        arrange(game);
        return (game, game.Start());
    }

    [Fact]
    public void Playing_a_unit_pays_and_puts_it_exhausted_in_base()
    {
        var (game, engine) = Setup(g => { g.Put("unit-3", Place.Hand(P1)); g.Runes(P1, "chaos-rune", 3); });

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(3, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Chaos }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);

        var unit = game.First(Place.Base(P1), "unit-3");
        Assert.True(game.State[unit].Exhausted);
        Assert.Empty(game.State.Chain);
        Assert.Equal(2, game.State.At(Place.Base(P1)).Count(id => game.State[id].CardId == "chaos-rune"));
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void A_unit_may_enter_at_a_battlefield_you_control()
    {
        var (game, engine) = Setup(g =>
        {
            g.State.Battlefields[0].Controller = P1;
            g.Put("unit-2", Place.Battlefield(0));
            g.Put("ganker-2", Place.Hand(P1));
            g.Runes(P1, "chaos-rune", 2);
        });

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "ganker-2")));
        var choices = engine.Decision<PlayChoicesDecision>();
        Assert.Equal(new[] { Place.Base(P1), Place.Battlefield(0) }, choices.Locations);
        Assert.Equal(RejectionCode.IllegalLocation, engine.Submit(P1, new ChoosePlayOptions(Place.Battlefield(1), false)).Rejection!.Code);

        engine.Accept(P1, new ChoosePlayOptions(Place.Battlefield(0), false));
        engine.PayWithSuggestion(P1);

        Assert.Contains(game.State.At(Place.Battlefield(0)), id => game.State[id].CardId == "ganker-2");
    }

    [Fact]
    public void Insufficient_payment_is_rejected_and_nothing_changes()
    {
        var (game, engine) = Setup(g => { g.Put("unit-3", Place.Hand(P1)); g.Runes(P1, "fury-rune", 3); });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));
        var runes = game.State.At(Place.Base(P1)).ToList();

        var result = engine.Submit(P1, new PayCost { Exhaust = runes });

        Assert.Equal(RejectionCode.InsufficientPayment, result.Rejection!.Code);
        Assert.All(runes, id => Assert.False(game.State[id].Exhausted));
        Assert.Null(engine.Decision<PayCostDecision>().Suggested);
    }

    [Fact]
    public void An_exhausted_or_foreign_rune_cannot_pay()
    {
        var (game, engine) = Setup(g =>
        {
            g.Put("spell", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 1);
            g.Runes(P2, "fury-rune", 1);
        });
        var mine = game.State.At(Place.Base(P1))[0];
        var theirs = game.State.At(Place.Base(P2))[0];
        engine.Accept(P1, new UseRune(mine, RuneUse.Exhaust));
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));

        Assert.Equal(RejectionCode.InsufficientPayment, engine.Submit(P1, new PayCost { Exhaust = [mine] }).Rejection!.Code);
        Assert.Equal(RejectionCode.UnknownObject, engine.Submit(P1, new PayCost { Exhaust = [theirs] }).Rejection!.Code);

        engine.Accept(P1, new PayCost());
        Assert.Equal(0, game.State.Player(P1).Pool.Energy);
    }

    [Fact]
    public void Cancelling_returns_the_card_and_spends_nothing()
    {
        var (game, engine) = Setup(g => { g.Put("unit-3", Place.Hand(P1)); g.Runes(P1, "chaos-rune", 3); });

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));
        engine.Accept(P1, new CancelPlay());

        Assert.Contains(game.State.At(Place.Hand(P1)), id => game.State[id].CardId == "unit-3");
        Assert.Empty(game.State.Chain);
        Assert.All(game.State.At(Place.Base(P1)), id => Assert.False(game.State[id].Exhausted));
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Adjusting_the_cost_asks_again_with_the_new_cost()
    {
        var (game, engine) = Setup(g => { g.Put("unit-3", Place.Hand(P1)); g.Runes(P1, "chaos-rune", 1); });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));

        engine.Accept(P1, new AdjustCost { Energy = -3 });

        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(0, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Chaos }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);
        Assert.Contains(game.State.At(Place.Base(P1)), id => game.State[id].CardId == "unit-3");
    }

    [Fact]
    public void Accelerate_adds_its_cost_and_the_unit_enters_ready()
    {
        var (game, engine) = Setup(g => { g.Put("accel-3", Place.Hand(P1)); g.Runes(P1, "fury-rune", 4); });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "accel-3")));

        Assert.True(engine.Decision<PlayChoicesDecision>().AccelerateAvailable);
        engine.Accept(P1, new ChoosePlayOptions(Place.Base(P1), true));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(4, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Self }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);

        Assert.False(game.State[game.First(Place.Base(P1), "accel-3")].Exhausted);
    }

    [Fact]
    public void A_spell_waits_on_the_chain_and_is_resolved_by_hand()
    {
        var (game, engine) = Setup(g => { g.Put("spell", Place.Hand(P1)); g.Runes(P1, "fury-rune", 1); });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);

        Assert.Equal(ChainItemStatus.Finalized, Assert.Single(game.State.Chain).Status);
        Assert.True(engine.Decision<PriorityDecision>().CanPass);

        engine.Accept(P1, new Pass());
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
        engine.Accept(P2, new Pass());
        var resolve = engine.Decision<ResolveManuallyDecision>();
        Assert.Equal((P1, "spell"), (resolve.Player, resolve.CardId));

        engine.Accept(P1, new ResolveDone());

        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.Empty(game.State.Chain);
        Assert.False(engine.Decision<PriorityDecision>().CanPass);
    }

    [Fact]
    public void A_player_can_stack_reactions_on_their_own_spell_before_passing()
    {
        var (game, engine) = Setup(g =>
        {
            g.Put("spell", Place.Hand(P1));
            g.Put("spell", Place.Hand(P1));
            g.Put("reaction-spell", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 2);
        });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);

        var options = engine.Decision<PriorityDecision>();
        var reaction = game.First(Place.Hand(P1), "reaction-spell");
        Assert.Equal(P1, options.Player);
        Assert.Contains(reaction, options.Playable);
        Assert.DoesNotContain(game.First(Place.Hand(P1), "spell"), options.Playable);

        engine.Accept(P1, new PlayCard(reaction));
        engine.PayWithSuggestion(P1);
        Assert.Equal(2, game.State.Chain.Count);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        Assert.Equal(game.State.Chain[1].Id, engine.Decision<ResolveManuallyDecision>().ChainItem);

        engine.Accept(P1, new ResolveDone());
        Assert.Single(game.State.Chain);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void The_opponent_can_respond_and_their_reaction_resolves_first()
    {
        var (game, engine) = Setup(g =>
        {
            g.Put("spell", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 1);
            g.Put("reaction-spell", Place.Hand(P2));
            g.Runes(P2, "fury-rune", 1);
        });
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());

        engine.Accept(P2, new PlayCard(game.First(Place.Hand(P2), "reaction-spell")));
        engine.PayWithSuggestion(P2);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
        engine.Accept(P2, new Pass());
        engine.Accept(P1, new Pass());

        Assert.Equal(P2, engine.Decision<ResolveManuallyDecision>().Player);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~PlayTests"`
Expected: the project compiles (the actions exist since Task 1), and the tests FAIL: `PlayCard` is rejected with `UnexpectedAction`.

- [ ] **Step 3: Extend ChainItem**

In `src/CromoBound.Engine/State/ChainItem.cs`, add to `ChainItem` after `AbilityKind`:
```csharp
    /// <summary>Where a permanent will enter, and whether Accelerate was paid (choices made while playing).</summary>
    public Place? Location { get; set; }
    public bool Accelerate { get; set; }

    /// <summary>Ability items: the source card's id and the ability text, kept even if the source leaves play.</summary>
    public string? SourceCardId { get; init; }
    public string? Text { get; init; }
```

- [ ] **Step 4: Implement playing and paying**

Create `src/CromoBound.Engine/Rules/Game.Play.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

internal enum PlayStep { ToChain, Choices, Cost, Pay, Finalize, Cancelled }

/// <summary>Playing a card, CR 353–359: to the chain, choices, total cost, payment, finalize.</summary>
internal sealed class PlayCardTask(PlayerId player, ObjectId source) : GameTask
{
    public PlayerId Player { get; } = player;
    public ObjectId Source { get; } = source;
    public PlayStep Step { get; set; }
    public ChainItem? Item { get; set; }
    public Place Origin { get; set; }
    public int? HiddenOnTurn { get; set; }
    public TotalCost Cost { get; set; } = new(0, []);
    public bool FromHidden => Origin.Kind == PlaceKind.Facedown;

    public override bool Run(Game game) => game.RunPlay(this);
}

public sealed partial class Game
{
    private void StartPlay(PlayerId player, ObjectId card) => Push(new PlayCardTask(player, card));

    internal bool RunPlay(PlayCardTask task)
    {
        while (true)
        {
            switch (task.Step)
            {
                case PlayStep.ToChain:
                    PutOnChain(task);
                    task.Step = PlayStep.Choices;
                    break;
                case PlayStep.Choices:
                    if (AskPlayChoices(task)) return false;
                    task.Step = PlayStep.Cost;
                    break;
                case PlayStep.Cost:
                    task.Cost = Payment.CostOf(CardOf(task.Item!.Card!.Value), task.FromHidden, task.Item.Accelerate);
                    task.Step = PlayStep.Pay;
                    break;
                case PlayStep.Pay:
                    AskPay(task.Player, task.Cost, CardOf(task.Item!.Card!.Value).Domains,
                        onPaid: () => task.Step = PlayStep.Finalize,
                        onCancel: () => UndoPlay(task),
                        onAdjust: adjusted => task.Cost = adjusted);
                    return false;
                case PlayStep.Finalize:
                    FinishFinalizing(task);
                    return true;
                default:
                    return true;
            }
        }
    }

    private void PutOnChain(PlayCardTask task)
    {
        var source = State[task.Source];
        task.Origin = source.Place;
        task.HiddenOnTurn = source.HiddenOnTurn;
        if (State.Chain.Count == 0) ChainStartedByTrigger = false;
        var item = new ChainItem { Id = State.NextChainItemId(), Kind = ChainItemKind.Card, Controller = task.Player };
        item.Card = MoveCard(task.Source, Place.Chain, DeckPosition.Bottom);
        State[item.Card!.Value].Controller = task.Player;
        State.Chain.Add(item);
        Emit(new ChainItemAdded(item.Id, task.Player));
        task.Item = item;
    }

    /// <summary>Where the permanent may enter (CR 355.2.a, 811.1.d): a unit at your Base or a battlefield you control;
    /// gear at your Base; from Hidden, that card's battlefield. Spells have no location.</summary>
    private List<Place> PlayLocations(PlayCardTask task)
    {
        var card = CardOf(task.Item!.Card!.Value);
        if (card.Type == CardType.Spell) return [];
        if (task.FromHidden) return [Place.Battlefield(task.Origin.Index!.Value)];
        if (card.Type != CardType.Unit) return [Place.Base(task.Player)];
        return [Place.Base(task.Player), .. State.Battlefields.Where(b => b.Controller == task.Player).Select(b => Place.Battlefield(b.Index))];
    }

    /// <summary>Asks for the location and Accelerate when there is a real choice; returns true when it asked.</summary>
    private bool AskPlayChoices(PlayCardTask task)
    {
        var item = task.Item!;
        var locations = PlayLocations(task);
        var accelerate = CardOf(item.Card!.Value).Type == CardType.Unit && Has(State[item.Card.Value], DisplayKeyword.Accelerate);
        if (locations.Count <= 1 && !accelerate)
        {
            item.Location = locations.Count == 1 ? locations[0] : null;
            return false;
        }
        Ask(new PlayChoicesDecision(task.Player, item.Card.Value, locations, accelerate), (_, action) =>
        {
            if (action is CancelPlay)
            {
                UndoPlay(task);
                return null;
            }
            if (action is not ChoosePlayOptions choice)
                return Reject(RejectionCode.UnexpectedAction, "Choose where the card enters, or cancel.");
            var location = choice.Location ?? (locations.Count == 1 ? locations[0] : null);
            if (location is null || !locations.Contains(location.Value))
                return Reject(RejectionCode.IllegalLocation, "Choose one of the offered locations.");
            if (choice.Accelerate && !accelerate)
                return Reject(RejectionCode.UnexpectedAction, "This card has no Accelerate.");
            item.Location = location;
            item.Accelerate = choice.Accelerate;
            task.Step = PlayStep.Cost;
            return null;
        });
        return true;
    }

    /// <summary>Asks for payment (spec §7.4 step 4). AdjustCost changes the cost and asks again; CancelPlay undoes the play.</summary>
    internal void AskPay(PlayerId player, TotalCost cost, IReadOnlyList<Domain> domains, Action onPaid, Action onCancel, Action<TotalCost> onAdjust)
    {
        var suggestion = Payment.Suggest(State.Player(player).Pool, cost, domains, RunesOf(player));
        Ask(new PayCostDecision(player, cost, domains, suggestion), (_, action) =>
        {
            switch (action)
            {
                case CancelPlay:
                    onCancel();
                    return null;
                case AdjustCost adjust:
                    var adjusted = Payment.Adjust(cost, adjust.Energy, adjust.AddPower, adjust.RemovePower);
                    Emit(new CostAdjusted(player, adjusted));
                    onAdjust(adjusted);
                    return null;
                case PayCost pay:
                    if (ValidatePayment(player, pay, cost, domains) is { } rejection) return rejection;
                    foreach (var rune in pay.Exhaust) ApplyRune(player, rune, RuneUse.Exhaust);
                    foreach (var rune in pay.Recycle) ApplyRune(player, rune, RuneUse.Recycle);
                    if (!Payment.TryPay(State.Player(player).Pool, cost, domains))
                        throw new InvalidOperationException("A validated payment failed.");
                    onPaid();
                    return null;
                default:
                    return Reject(RejectionCode.UnexpectedAction, "Pay the cost, adjust it, or cancel.");
            }
        });
    }

    private Rejection? ValidatePayment(PlayerId player, PayCost pay, TotalCost cost, IReadOnlyList<Domain> domains)
    {
        if (pay.Exhaust.Distinct().Count() != pay.Exhaust.Count || pay.Recycle.Distinct().Count() != pay.Recycle.Count)
            return Reject(RejectionCode.InsufficientPayment, "A rune can be exhausted once and recycled once.");
        var runes = RunesOf(player).ToDictionary(r => r.Id);
        foreach (var id in pay.Exhaust.Concat(pay.Recycle))
            if (!runes.ContainsKey(id)) return Reject(RejectionCode.UnknownObject, $"{id} is not a rune in your Base.");
        if (pay.Exhaust.Any(id => runes[id].Exhausted))
            return Reject(RejectionCode.InsufficientPayment, "An exhausted rune can't be exhausted again.");
        var pool = State.Player(player).Pool.Clone();
        pool.Energy += pay.Exhaust.Count;
        foreach (var id in pay.Recycle) pool.AddPower(runes[id].Domain);
        return Payment.TryPay(pool, cost, domains) ? null : Reject(RejectionCode.InsufficientPayment, "Those runes and your pool don't cover the cost.");
    }

    /// <summary>The card goes back where it came from (face down again if it was hidden); nothing was spent.</summary>
    private void UndoPlay(PlayCardTask task)
    {
        var item = task.Item!;
        State.Chain.Remove(item);
        var back = MoveCard(item.Card!.Value, task.Origin, DeckPosition.Bottom);
        if (task.FromHidden && back is { } id)
        {
            var card = State[id];
            card.Facedown = true;
            card.Controller = task.Player;
            card.HiddenOnTurn = task.HiddenOnTurn;
        }
        Emit(new PlayCancelled(item.Id));
        task.Step = PlayStep.Cancelled;
    }

    /// <summary>CR 359: a permanent leaves the chain and enters the board (a unit exhausted unless Accelerated, gear ready);
    /// a spell stays on the chain, finalized, and its controller gets priority (CR 337.4).</summary>
    private void FinishFinalizing(PlayCardTask task)
    {
        var item = task.Item!;
        var card = CardOf(item.Card!.Value);
        item.Status = ChainItemStatus.Finalized;
        ChainPasses = 0;
        if (card.Type is CardType.Unit or CardType.Gear)
        {
            State.Chain.Remove(item);
            var id = MoveCard(item.Card.Value, item.Location!.Value)!.Value;
            State[id].Controller = task.Player;
            if (card.Type == CardType.Unit && !item.Accelerate) SetStatus(id, StatusKind.Exhausted, true);
            Emit(new ChainItemResolved(item.Id));
            AfterResolution();
            return;
        }
        State.Turn.Priority = item.Controller;
    }
}
```

- [ ] **Step 5: Implement passing and resolving by hand**

Create `src/CromoBound.Engine/Rules/Game.Chain.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Consecutive passes since the chain last changed (CR 339).</summary>
    internal int ChainPasses { get; set; }

    /// <summary>True when a triggered ability started the current chain; then focus doesn't pass when it closes (CR 346.1).</summary>
    internal bool ChainStartedByTrigger { get; set; }

    /// <summary>On a chain: when every player has passed in a row, the newest item resolves; otherwise priority moves on.</summary>
    private void PassPriority(PlayerId player)
    {
        ChainPasses++;
        if (ChainPasses >= State.Players.Count) ResolveTop();
        else State.Turn.Priority = State.Opponent(player);
    }

    /// <summary>2a: the newest finalized item is resolved by hand by its controller (spec §8).</summary>
    private void ResolveTop()
    {
        var item = State.Chain.Last(i => i.Status == ChainItemStatus.Finalized);
        var cardId = item.Card is { } card ? State[card].CardId : item.SourceCardId ?? "";
        var text = item.Text ?? (item.Card is { } c ? CardOf(c).Text.Rich : "");
        ResolvingManually = true;
        Ask(new ResolveManuallyDecision(item.Controller, item.Id, cardId, text), (_, action) =>
        {
            if (action is not ResolveDone)
                return Reject(RejectionCode.UnexpectedAction, "Carry out the effect with manual actions, then submit ResolveDone.");
            FinishResolution(item);
            return null;
        });
    }

    /// <summary>A resolved spell goes to its owner's trash; an ability is just removed.</summary>
    private void FinishResolution(ChainItem item)
    {
        State.Chain.Remove(item);
        if (item.Card is { } card && State.Exists(card) && State[card].Place.Kind == PlaceKind.Chain)
            MoveCard(card, Place.Trash(State[card].Owner));
        ResolvingManually = false;
        Emit(new ChainItemResolved(item.Id));
        MarkDirty();
        AfterResolution();
    }

    /// <summary>After an item resolves: the newest remaining item's controller gets priority, or the chain closes.</summary>
    private void AfterResolution()
    {
        ChainPasses = 0;
        if (State.Chain.Count > 0)
        {
            State.Turn.Priority = State.Chain[^1].Controller;
            return;
        }
        ChainEmptied();
    }

    /// <summary>Back to Open. In a showdown, focus passes unless a triggered ability started the chain (CR 346).</summary>
    private void ChainEmptied()
    {
        if (State.Showdown is { } showdown)
        {
            if (!ChainStartedByTrigger && State.Turn.Focus is { } focus) State.Turn.Focus = State.Opponent(focus);
            showdown.Passes = 0;
            State.Turn.Priority = State.Turn.Focus;
        }
        else
        {
            State.Turn.Priority = State.Turn.Phase == Phase.Main ? State.Turn.TurnPlayer : null;
        }
        ChainStartedByTrigger = false;
    }
}
```

- [ ] **Step 6: Dispatch PlayCard and Pass**

Replace all of `src/CromoBound.Engine/Rules/Game.Dispatch.cs` with:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Applies an answer to a priority decision. Grows as later tasks add actions.</summary>
    private Rejection? HandlePriority(PlayerId player, PlayerAction action)
    {
        var options = PriorityOptions(player);
        switch (action)
        {
            case UseRune use:
                return UseRuneNow(player, use);
            case PlayCard play when options.Playable.Contains(play.Card):
                StartPlay(player, play.Card);
                return null;
            case Pass when IsClosed:
                PassPriority(player);
                return null;
            case EndTurn when options.CanEndTurn:
                EndTheTurn();
                return null;
            default:
                return Reject(RejectionCode.UnexpectedAction, $"{action.GetType().Name} is not possible now.");
        }
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests).

- [ ] **Step 8: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests/PlayTests.cs
git commit -m "feat(engine): play cards, pay costs and resolve the chain"
```

---

### Task 5: Battlefields: movement, cleanup, showdowns, combat

**Files:**
- Create: `src/CromoBound.Engine/Rules/Game.Movement.cs`, `Game.Showdown.cs`, `Game.Combat.cs`
- Replace: `src/CromoBound.Engine/Rules/Game.Cleanup.cs` (full cleanup)
- Modify: `src/CromoBound.Engine/Rules/Game.Dispatch.cs`
- Test: `tests/CromoBound.Engine.Tests/BattlefieldTests.cs`

**Interfaces:**
- Consumes: Tasks 2–4 (`CombatDamage`, mutations, `ChainEmptied`, `PassPriority`).
- Produces (internal):
  - movement and showdowns: `MoveUnits(player, move, options)`, `ApplyContested(player, battlefield)`, `StartShowdown`, `PassFocus`, `EndShowdown`;
  - combat: `StartCombat`, `ConvertToCombat`, `CombatDamageTask`, `CombatResolutionTask`.
  - The full `RunCleanup` (CR 323 steps 1–10, plus the Ending and Combat special cleanups).

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/BattlefieldTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class BattlefieldTests
{
    private static StandardMove Move(ObjectId unit, Place to) => new() { Units = [unit], Destination = to };

    [Fact]
    public void Moving_to_an_empty_battlefield_starts_a_showdown_and_conquers_after_passes()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        engine.Accept(P1, Move(unit, Place.Battlefield(1)));

        Assert.True(game.State[unit].Exhausted);
        Assert.Equal(1, game.State.Showdown!.Battlefield);
        Assert.Equal(P1, game.State.Turn.Focus);
        var focus = engine.Decision<PriorityDecision>();
        Assert.True(focus.CanPass);
        Assert.False(focus.CanEndTurn);

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Null(game.State.Showdown);
        Assert.Equal(P1, game.State.Battlefields[1].Controller);
        Assert.Equal(1, game.State.Player(P1).Points);
        Assert.True(engine.Decision<PriorityDecision>().CanEndTurn);
    }

    [Fact]
    public void Units_can_only_move_where_the_rules_allow()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        var unit = game.Put("unit-2", Place.Battlefield(0));
        var ganker = game.Put("ganker-2", Place.Battlefield(0));
        var engine = game.Start();

        var moves = engine.Decision<PriorityDecision>().Moves;

        Assert.Equal(new[] { Place.Base(P1) }, moves.Single(m => m.Unit == unit).Destinations);
        Assert.Equal(new[] { Place.Base(P1), Place.Battlefield(1) }, moves.Single(m => m.Unit == ganker).Destinations);
        Assert.Equal(RejectionCode.IllegalLocation, engine.Submit(P1, Move(unit, Place.Battlefield(1))).Rejection!.Code);
    }

    [Fact]
    public void Leaving_a_battlefield_empty_loses_control()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        var unit = game.Put("unit-2", Place.Battlefield(0));
        var engine = game.Start();

        engine.Accept(P1, Move(unit, Place.Base(P1)));

        Assert.Null(game.State.Battlefields[0].Controller);
    }

    [Fact]
    public void Conquering_for_the_final_point_needs_every_battlefield_scored_this_turn()
    {
        var game = new TestGame();
        game.State.Player(P1).Points = 7;
        game.State.Battlefields[0].Controller = P2;
        game.Put("unit-2", Place.Battlefield(0), owner: P2);
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        var handSize = game.State.At(Place.Hand(P1)).Count;

        engine.Accept(P1, Move(unit, Place.Battlefield(1)));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Equal(P1, game.State.Battlefields[1].Controller);
        Assert.Equal(7, game.State.Player(P1).Points);
        Assert.Equal(handSize + 1, game.State.At(Place.Hand(P1)).Count);
        Assert.Null(engine.Outcome);
    }

    [Fact]
    public void Holding_one_battlefield_and_conquering_the_other_wins()
    {
        var game = new TestGame();
        game.State.Player(P1).Points = 6;
        game.State.Battlefields[0].Controller = P1;
        game.Put("unit-2", Place.Battlefield(0));
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        Assert.Equal(7, game.State.Player(P1).Points);

        engine.Accept(P1, Move(unit, Place.Battlefield(1)));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Equal(new GameOutcome(P1, GameEndReason.Points), engine.Outcome);
    }

    [Fact]
    public void Focus_passes_after_the_focus_holders_chain_closes()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        game.Put("action-spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();
        engine.Accept(P1, Move(unit, Place.Battlefield(1)));

        var spell = game.First(Place.Hand(P1), "action-spell");
        Assert.Contains(spell, engine.Decision<PriorityDecision>().Playable);
        engine.Accept(P1, new PlayCard(spell));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        engine.Accept(P1, new ResolveDone());

        Assert.Equal(P2, game.State.Turn.Focus);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Combat_attacker_assigns_first_damage_is_simultaneous_and_the_winner_conquers()
    {
        var game = new TestGame();
        game.State.Battlefields[1].Controller = P2;
        var defender = game.Put("unit-2", Place.Battlefield(1), owner: P2);
        var attacker = game.Put("unit-3", Place.Base(P1));
        game.State[attacker].Modifiers.Add(new MightModifier(1, Duration.ThisCombat));
        var engine = game.Start();

        engine.Accept(P1, Move(attacker, Place.Battlefield(1)));
        var combat = game.State.Showdown!;
        Assert.True(combat.IsCombat);
        Assert.Equal(P1, combat.Attacker);
        Assert.Equal(CombatRole.Attacker, game.State[attacker].Role);
        Assert.Equal(CombatRole.Defender, game.State[defender].Role);

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        var assign = engine.Decision<AssignDamageDecision>();
        Assert.Equal((P1, 4), (assign.Player, assign.Total));
        Assert.Equal(2, Assert.Single(assign.Targets).Lethal);
        engine.Accept(P1, new AssignDamage { Assignments = [new(defender, 4)] });
        Assert.Equal(P2, engine.Decision<AssignDamageDecision>().Player);
        engine.Accept(P2, new AssignDamage { Assignments = [new(attacker, 2)] });

        Assert.False(game.State.Exists(defender));
        Assert.Equal(0, game.State[attacker].Damage);
        Assert.Null(game.State[attacker].Role);
        Assert.Empty(game.State[attacker].Modifiers);
        Assert.Equal(P1, game.State.Battlefields[1].Controller);
        Assert.Equal(1, game.State.Player(P1).Points);
        Assert.Null(game.State.Showdown);
    }

    [Fact]
    public void Surviving_attackers_are_recalled_and_a_side_with_no_damage_is_not_asked()
    {
        var game = new TestGame();
        game.State.Battlefields[1].Controller = P2;
        var defender = game.Put("unit-3", Place.Battlefield(1), owner: P2);
        game.State[defender].Stunned = true;
        var attacker = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        engine.Accept(P1, Move(attacker, Place.Battlefield(1)));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        var result = engine.Accept(P1, new AssignDamage { Assignments = [new(defender, 2)] });

        Assert.Equal(Place.Base(P1), game.State[attacker].Place);
        Assert.True(game.State[attacker].Exhausted);
        Assert.Equal(0, game.State[defender].Damage);
        Assert.Equal(P2, game.State.Battlefields[1].Controller);
        Assert.Contains(result.Events, e => e is CombatEnded { Result: CombatResult.DefenderWon });
    }

    [Fact]
    public void Tank_units_must_take_lethal_damage_first()
    {
        var game = new TestGame();
        game.State.Battlefields[1].Controller = P2;
        var tank = game.Put("tank-2", Place.Battlefield(1), owner: P2);
        var plain = game.Put("unit-2", Place.Battlefield(1), owner: P2);
        var a = game.Put("unit-3", Place.Base(P1));
        var b = game.Put("unit-3", Place.Base(P1));
        var engine = game.Start();
        engine.Accept(P1, new StandardMove { Units = [a, b], Destination = Place.Battlefield(1) });
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        var wrong = engine.Submit(P1, new AssignDamage { Assignments = [new(plain, 6)] });
        var suggested = engine.Decision<AssignDamageDecision>().Suggested;

        Assert.Equal(RejectionCode.InvalidAssignment, wrong.Rejection!.Code);
        Assert.Equal(tank, suggested[0].Unit);
        engine.Accept(P1, new AssignDamage { Assignments = suggested });
        Assert.Equal(P2, engine.Decision<AssignDamageDecision>().Player);
    }

    [Fact]
    public void When_both_sides_die_the_battlefield_becomes_uncontrolled()
    {
        var game = new TestGame();
        game.State.Battlefields[1].Controller = P2;
        var defender = game.Put("unit-2", Place.Battlefield(1), owner: P2);
        var attacker = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        engine.Accept(P1, Move(attacker, Place.Battlefield(1)));
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        engine.Accept(P1, new AssignDamage { Assignments = [new(defender, 2)] });
        engine.Accept(P2, new AssignDamage { Assignments = [new(attacker, 2)] });

        Assert.Null(game.State.Battlefields[1].Controller);
        Assert.Null(game.State.Battlefields[1].ContestedBy);
        Assert.Equal(0, game.State.Player(P1).Points);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~BattlefieldTests"`
Expected: FAIL (`StandardMove` is rejected with `UnexpectedAction`).

- [ ] **Step 3: Implement movement**

Create `src/CromoBound.Engine/Rules/Game.Movement.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Standard Move (CR 144): each unit is exhausted as the cost and moves; arriving where you don't have control applies Contested.</summary>
    private Rejection? MoveUnits(PlayerId player, StandardMove move, PriorityDecision options)
    {
        if (move.Units.Count == 0 || move.Units.Distinct().Count() != move.Units.Count)
            return Reject(RejectionCode.UnexpectedAction, "Choose one or more different units.");
        foreach (var unit in move.Units)
        {
            var option = options.Moves.FirstOrDefault(m => m.Unit == unit);
            if (option is null) return Reject(RejectionCode.UnknownObject, $"{unit} can't move now.");
            if (!option.Destinations.Contains(move.Destination))
                return Reject(RejectionCode.IllegalLocation, $"{unit} can't move to {move.Destination}.");
        }
        foreach (var unit in move.Units)
        {
            SetStatus(unit, StatusKind.Exhausted, true);
            MoveCard(unit, move.Destination);
        }
        if (move.Destination.Kind == PlaceKind.Battlefield) ApplyContested(player, move.Destination.Index!.Value);
        return null;
    }

    /// <summary>CR 190.3: a unit arriving at a battlefield its controller doesn't control applies Contested, if not already applied.</summary>
    internal void ApplyContested(PlayerId player, int battlefield)
    {
        var state = State.Battlefields[battlefield];
        if (state.Controller == player || state.ContestedBy is not null) return;
        state.ContestedBy = player;
        MarkDirty();
    }
}
```

- [ ] **Step 4: Implement showdowns**

Create `src/CromoBound.Engine/Rules/Game.Showdown.cs`:
```csharp
using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>A non-combat showdown: the player who applied Contested gets focus and priority (CR 345).</summary>
    private void StartShowdown(int battlefield)
    {
        State.StagedShowdowns.Remove(battlefield);
        var focus = State.Battlefields[battlefield].ContestedBy ?? State.Turn.TurnPlayer;
        State.Showdown = new ShowdownState(battlefield);
        State.Turn.Focus = focus;
        State.Turn.Priority = focus;
        Emit(new ShowdownStarted(battlefield, focus));
        MarkDirty();
    }

    /// <summary>Passing in an open showdown hands focus on. When every player has passed in a row, it ends (CR 347–348).</summary>
    private void PassFocus(PlayerId player)
    {
        var showdown = State.Showdown!;
        showdown.Passes++;
        if (showdown.Passes >= State.Players.Count)
        {
            EndShowdown(showdown);
            return;
        }
        State.Turn.Focus = State.Opponent(player);
        State.Turn.Priority = State.Turn.Focus;
    }

    /// <summary>A combat showdown continues with combat damage. A non-combat showdown gives control to the only player with
    /// units there (a Conquer if not scored this turn); with no units left, the battlefield is left uncontested (spec §13 #4).</summary>
    private void EndShowdown(ShowdownState showdown)
    {
        if (showdown.IsCombat)
        {
            Enqueue(new CombatDamageTask(showdown.Battlefield));
            return;
        }
        State.Showdown = null;
        State.Turn.Focus = null;
        State.Turn.Priority = null;
        Emit(new ShowdownEnded(showdown.Battlefield));
        var battlefield = State.Battlefields[showdown.Battlefield];
        var present = UnitsAt(Place.Battlefield(showdown.Battlefield)).Select(u => u.Controller).Distinct().ToList();
        if (present.Count == 1)
        {
            EstablishControl(present[0], battlefield.Index);
            return;
        }
        battlefield.ContestedBy = null;
        MarkDirty();
    }
}
```

- [ ] **Step 5: Implement combat**

Create `src/CromoBound.Engine/Rules/Game.Combat.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

/// <summary>Combat step 2 (CR 465): the attacker assigns, then the defender, then all damage is dealt at once.</summary>
internal sealed class CombatDamageTask(int battlefield) : GameTask
{
    public int Battlefield { get; } = battlefield;

    /// <summary>0: attacker assigns; 1: defender assigns; 2: deal.</summary>
    public int Step { get; set; }

    public List<DamageAssignment> Assignments { get; } = [];

    public override bool Run(Game game) => game.RunCombatDamage(this);
}

/// <summary>Combat step 3 (CR 466): combat cleanup, result, control, end of combat.</summary>
internal sealed class CombatResolutionTask(int battlefield) : GameTask
{
    public int Battlefield { get; } = battlefield;
    public bool CleanedUp { get; set; }

    public override bool Run(Game game) => game.RunCombatResolution(this);
}

public sealed partial class Game
{
    /// <summary>Attacker = the player who applied Contested; they get focus (CR 464).</summary>
    private void StartCombat(int battlefield)
    {
        State.StagedCombats.Remove(battlefield);
        State.StagedShowdowns.Remove(battlefield);
        var attacker = State.Battlefields[battlefield].ContestedBy ?? State.Turn.TurnPlayer;
        var defender = State.Opponent(attacker);
        State.Showdown = new ShowdownState(battlefield) { IsCombat = true, Attacker = attacker, Defender = defender };
        State.Turn.Focus = attacker;
        State.Turn.Priority = attacker;
        Emit(new CombatStarted(battlefield, attacker, defender));
        MarkDirty();
    }

    /// <summary>Cleanup step 10a: enemy units arrived during a non-combat showdown; it becomes a combat showdown and focus stays put.</summary>
    private void ConvertToCombat(ShowdownState showdown)
    {
        State.StagedCombats.Remove(showdown.Battlefield);
        var attacker = State.Battlefields[showdown.Battlefield].ContestedBy ?? State.Turn.TurnPlayer;
        showdown.IsCombat = true;
        showdown.Attacker = attacker;
        showdown.Defender = State.Opponent(attacker);
        Emit(new CombatStarted(showdown.Battlefield, attacker, showdown.Defender.Value));
        MarkDirty();
    }

    internal bool RunCombatDamage(CombatDamageTask task)
    {
        var combat = State.Showdown!;
        var units = UnitsAt(Place.Battlefield(task.Battlefield));
        var attackers = units.Where(u => u.Controller == combat.Attacker).ToList();
        var defenders = units.Where(u => u.Controller == combat.Defender).ToList();
        if (task.Step == 0 && (attackers.Count == 0 || defenders.Count == 0)) task.Step = 2;
        if (task.Step == 0)
        {
            if (AskAssignment(task, combat.Attacker!.Value, attackers, defenders)) return false;
            task.Step = 1;
        }
        if (task.Step == 1)
        {
            if (AskAssignment(task, combat.Defender!.Value, defenders, attackers)) return false;
            task.Step = 2;
        }
        foreach (var assignment in task.Assignments) DealDamage(assignment.Unit, assignment.Amount);
        Push(new CombatResolutionTask(task.Battlefield));
        return true;
    }

    /// <summary>Asks a side to assign its damage; false when it has none to deal or nothing to hit. Stunned units deal 0 (CR 423.1.b).</summary>
    private bool AskAssignment(CombatDamageTask task, PlayerId player, List<CardInstance> own, List<CardInstance> enemies)
    {
        var total = own.Sum(u => u.Stunned ? 0 : Math.Max(MightOf(u.Id), 0));
        var targets = enemies
            .Select(u => new DamageTarget(
                u.Id,
                CombatDamage.Lethal(MightOf(u.Id), u.Damage),
                CombatDamage.GroupOf(Has(u, DisplayKeyword.Tank), Has(u, DisplayKeyword.Backline))))
            .ToList();
        if (total == 0 || targets.Count == 0) return false;
        Ask(new AssignDamageDecision(player, task.Battlefield, total, targets, CombatDamage.Suggest(targets, total)), (_, action) =>
        {
            if (action is not AssignDamage assign) return Reject(RejectionCode.UnexpectedAction, "Assign your combat damage.");
            if (CombatDamage.Validate(targets, total, assign.Assignments) is { } error) return Reject(RejectionCode.InvalidAssignment, error);
            task.Assignments.AddRange(assign.Assignments.Where(a => a.Amount > 0));
            task.Step++;
            return null;
        });
        return true;
    }

    internal bool RunCombatResolution(CombatResolutionTask task)
    {
        if (!task.CleanedUp)
        {
            task.CleanedUp = true;
            Push(new CleanupTask(CleanupMode.Combat));
            return false;
        }
        var combat = State.Showdown!;
        var units = UnitsAt(Place.Battlefield(task.Battlefield));
        var attackerLeft = units.Any(u => u.Controller == combat.Attacker);
        var defenderLeft = units.Any(u => u.Controller == combat.Defender);
        var result = attackerLeft && !defenderLeft ? CombatResult.AttackerWon
            : defenderLeft && !attackerLeft ? CombatResult.DefenderWon
            : CombatResult.NoResult;

        State.Showdown = null;
        State.Turn.Focus = null;
        State.Turn.Priority = null;
        foreach (var unit in BoardUnits().ToList())
        {
            unit.Role = null;
            unit.Modifiers.RemoveAll(m => m.Duration == Duration.ThisCombat);
        }
        Emit(new CombatEnded(task.Battlefield, result));

        var present = units.Select(u => u.Controller).Distinct().ToList();
        if (present.Count == 1)
        {
            EstablishControl(present[0], task.Battlefield);
        }
        else if (present.Count == 0)
        {
            LoseControl(task.Battlefield);
            State.Battlefields[task.Battlefield].ContestedBy = null;
        }
        MarkDirty();
        return true;
    }
}
```

- [ ] **Step 6: Replace the cleanup with the full version**

Replace all of `src/CromoBound.Engine/Rules/Game.Cleanup.cs` with:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

internal enum CleanupMode { Normal, Ending, Combat }

/// <summary>CR 318–324. Steps 1–8 repeat until a pass changes nothing; steps 9–10 may then start a staged showdown or combat.</summary>
internal sealed class CleanupTask(CleanupMode mode) : GameTask
{
    /// <summary>Set once steps 1–8 are stable, so answering the step 9–10 choice doesn't rerun them.</summary>
    public bool StableReached { get; set; }

    public override bool Run(Game game) => game.RunCleanup(mode, this);
}

public sealed partial class Game
{
    internal bool RunCleanup(CleanupMode mode, CleanupTask task)
    {
        if (!task.StableReached)
        {
            var special = mode;
            while (true)
            {
                var before = Changes;
                if (CheckWin()) return true;
                AssignCombatRoles();
                KillLethallyDamagedUnits();
                if (special == CleanupMode.Ending) EndingSteps();
                if (special == CleanupMode.Combat) CombatSteps();
                special = CleanupMode.Normal;
                LoseControlOfEmptyBattlefields();
                RecallStrays();
                StageShowdownsAndCombats();
                ClearContested();
                if (Changes == before) break;
            }
            task.StableReached = true;
            CleanupDone();
        }
        return StartStaged();
    }

    /// <summary>Step 1: at least the Victory Score and more points than every opponent (CR 194.2, 323.1).</summary>
    private bool CheckWin()
    {
        foreach (var player in State.Players)
        {
            if (!HasWon(player.Id)) continue;
            End(player.Id, GameEndReason.Points);
            return true;
        }
        return false;
    }

    /// <summary>Step 2: units at the combat battlefield take their side's role; units elsewhere lose it.</summary>
    private void AssignCombatRoles()
    {
        var combat = State.Showdown is { IsCombat: true } running ? running : null;
        foreach (var unit in BoardUnits().ToList())
        {
            CombatRole? role = combat is not null && unit.Place == Place.Battlefield(combat.Battlefield)
                ? unit.Controller == combat.Attacker ? CombatRole.Attacker : CombatRole.Defender
                : null;
            if (unit.Role == role) continue;
            unit.Role = role;
            MarkDirty();
        }
    }

    /// <summary>Steps 3a–3b: units with non-zero damage ≥ Might are killed.</summary>
    private void KillLethallyDamagedUnits()
    {
        foreach (var unit in BoardUnits().Where(u => u.Damage > 0 && u.Damage >= Math.Max(MightOf(u.Id), 0)).ToList())
            Kill(unit.Id);
    }

    /// <summary>Ending special cleanup, 3c–3e: heal all units; "this turn" effects and Stunned expire; rune pools empty (CR 317.2).</summary>
    private void EndingSteps()
    {
        HealAllUnits();
        foreach (var instance in State.Objects.ToList())
        {
            if (instance.Modifiers.RemoveAll(m => m.Duration == Duration.ThisTurn) > 0) MarkDirty();
            if (instance.Stunned) SetStatus(instance.Id, StatusKind.Stunned, false);
        }
        foreach (var player in State.Players) player.Pool.Clear();
    }

    /// <summary>Combat special cleanup, 3c–3d: heal all units; recall attackers if defenders are still there (CR 466.1).</summary>
    private void CombatSteps()
    {
        HealAllUnits();
        if (State.Showdown is not { IsCombat: true } combat) return;
        var units = UnitsAt(Place.Battlefield(combat.Battlefield));
        if (!units.Any(u => u.Controller == combat.Defender)) return;
        foreach (var attacker in units.Where(u => u.Controller == combat.Attacker)) Recall(attacker.Id);
    }

    /// <summary>Step 4: control is lost where the controller has no units, if Open and nothing is happening there (CR 190.4.c).</summary>
    private void LoseControlOfEmptyBattlefields()
    {
        if (IsClosed) return;
        foreach (var battlefield in State.Battlefields)
            if (battlefield.Controller is { } controller && !Busy(battlefield.Index)
                && !UnitsAt(Place.Battlefield(battlefield.Index)).Any(u => u.Controller == controller))
                LoseControl(battlefield.Index);
    }

    /// <summary>A showdown or combat is staged or running at this battlefield.</summary>
    private bool Busy(int battlefield) =>
        State.Showdown?.Battlefield == battlefield || State.StagedShowdowns.Contains(battlefield) || State.StagedCombats.Contains(battlefield);

    /// <summary>Step 5: unattached gear and runes at battlefields go to Base; permanents in another player's Base go home;
    /// facedown cards at battlefields their controller doesn't control go to their owner's trash.</summary>
    private void RecallStrays()
    {
        foreach (var battlefield in State.Battlefields)
        {
            foreach (var id in State.At(Place.Battlefield(battlefield.Index)).ToList())
                if (!IsUnit(State[id]) && State[id].AttachedTo is null) Recall(id);
            foreach (var id in State.At(Place.Facedown(battlefield.Index)).ToList())
                if (State[id].Controller != battlefield.Controller) MoveCard(id, Place.Trash(State[id].Owner));
        }
        foreach (var player in State.Players)
            foreach (var id in State.At(Place.Base(player.Id)).ToList())
                if (State[id].Controller != player.Id) Recall(id);
    }

    /// <summary>Steps 6–7: stage a showdown where Contested was applied and the applier has units there; stage a combat
    /// where two players have units. Un-stage what no longer applies.</summary>
    private void StageShowdownsAndCombats()
    {
        foreach (var battlefield in State.Battlefields)
        {
            var units = UnitsAt(Place.Battlefield(battlefield.Index));
            var running = State.Showdown?.Battlefield == battlefield.Index;
            var showdown = !running && battlefield.ContestedBy is { } applier && units.Any(u => u.Controller == applier);
            var combat = units.Select(u => u.Controller).Distinct().Count() > 1 && !(running && State.Showdown!.IsCombat);
            if (Toggle(State.StagedShowdowns, battlefield.Index, showdown) | Toggle(State.StagedCombats, battlefield.Index, combat))
                MarkDirty();
        }
    }

    private static bool Toggle(SortedSet<int> set, int value, bool on) => on ? set.Add(value) : set.Remove(value);

    /// <summary>Step 8: clear Contested where the applier has no units and nothing is happening; 8a: units left on a battlefield
    /// their controller doesn't control re-apply it.</summary>
    private void ClearContested()
    {
        foreach (var battlefield in State.Battlefields)
        {
            var units = UnitsAt(Place.Battlefield(battlefield.Index));
            if (battlefield.ContestedBy is { } applier && !units.Any(u => u.Controller == applier) && !Busy(battlefield.Index))
            {
                battlefield.ContestedBy = null;
                MarkDirty();
            }
            if (battlefield.ContestedBy is null && units.FirstOrDefault(u => u.Controller != battlefield.Controller) is { } stranger)
                ApplyContested(stranger.Controller, battlefield.Index);
        }
    }

    /// <summary>Steps 9–10: in Neutral Open, start a staged showdown (where no combat is staged), else a staged combat; the turn
    /// player chooses when there are several. 10a: during a non-combat showdown, a combat staged there converts it.</summary>
    private bool StartStaged()
    {
        if (IsClosed) return true;
        if (State.Showdown is { IsCombat: false } running && State.StagedCombats.Contains(running.Battlefield))
        {
            ConvertToCombat(running);
            return true;
        }
        if (State.Showdown is not null) return true;

        var showdowns = State.StagedShowdowns.Where(b => !State.StagedCombats.Contains(b)).ToList();
        var combat = showdowns.Count == 0;
        var candidates = combat ? State.StagedCombats.ToList() : showdowns;
        if (candidates.Count == 0) return true;
        if (candidates.Count == 1)
        {
            Begin(candidates[0], combat);
            return true;
        }
        Ask(new ChooseShowdownDecision(State.Turn.TurnPlayer, candidates, combat), (_, action) =>
        {
            if (action is not ChooseShowdown choice || !candidates.Contains(choice.Battlefield))
                return Reject(RejectionCode.UnexpectedAction, "Choose one of the staged battlefields.");
            Begin(choice.Battlefield, combat);
            return null;
        });
        return false;
    }

    private void Begin(int battlefield, bool combat)
    {
        if (combat) StartCombat(battlefield);
        else StartShowdown(battlefield);
    }
}
```

- [ ] **Step 7: Dispatch StandardMove and showdown passes**

Replace all of `src/CromoBound.Engine/Rules/Game.Dispatch.cs` with:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Applies an answer to a priority decision. Grows as later tasks add actions.</summary>
    private Rejection? HandlePriority(PlayerId player, PlayerAction action)
    {
        var options = PriorityOptions(player);
        switch (action)
        {
            case UseRune use:
                return UseRuneNow(player, use);
            case PlayCard play when options.Playable.Contains(play.Card):
                StartPlay(player, play.Card);
                return null;
            case StandardMove move when options.Moves.Count > 0:
                return MoveUnits(player, move, options);
            case Pass when IsClosed:
                PassPriority(player);
                return null;
            case Pass when State.Showdown is not null:
                PassFocus(player);
                return null;
            case EndTurn when options.CanEndTurn:
                EndTheTurn();
                return null;
            default:
                return Reject(RejectionCode.UnexpectedAction, $"{action.GetType().Name} is not possible now.");
        }
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests, including Tasks 3 and 4).

- [ ] **Step 9: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests/BattlefieldTests.cs
git commit -m "feat(engine): add movement, full cleanup, showdowns and combat"
```

---

### Task 6: Hidden

**Files:**
- Create: `src/CromoBound.Engine/Rules/Game.Hidden.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Dispatch.cs`
- Test: `tests/CromoBound.Engine.Tests/HiddenTests.cs`

**Interfaces:**
- Consumes: `AskPay` (Task 4); `HideOptions` (Task 3); play from facedown (`PlayCardTask.FromHidden`, Task 4); facedown removal in cleanup step 5 (Task 5).
- Produces: `HideTask`, `StartHide(player, card, battlefield)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/HiddenTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class HiddenTests
{
    /// <summary>P1 controls battlefield 0 (with a unit there), holds hidden-unit, and has one chaos rune.</summary>
    private static (TestGame Game, Game Engine, ObjectId Card) Setup()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        game.Put("unit-2", Place.Battlefield(0));
        game.Put("hidden-unit", Place.Hand(P1));
        game.Runes(P1, "chaos-rune", 1);
        var engine = game.Start();
        return (game, engine, game.First(Place.Hand(P1), "hidden-unit"));
    }

    [Fact]
    public void Hiding_pays_any_power_and_puts_the_card_face_down_privately()
    {
        var (game, engine, card) = Setup();
        var hide = Assert.Single(engine.Decision<PriorityDecision>().Hides);
        Assert.Equal(new[] { 0 }, hide.Battlefields);

        engine.Accept(P1, new Hide(card, 0));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(new[] { PowerSymbol.Any }, pay.Cost.Power);
        var result = engine.PayWithSuggestion(P1);

        var hidden = game.State[Assert.Single(game.State.At(Place.Facedown(0)))];
        Assert.True(hidden.Facedown);
        Assert.Equal(P1, hidden.Controller);
        Assert.Contains(result.Events, e => e is CardMoved { CardId: null, VisibleTo: null, ToPlace.Kind: PlaceKind.Facedown });
        Assert.Contains(result.Events, e => e is CardMoved { CardId: "hidden-unit", VisibleTo: { Index: 0 } });
    }

    [Fact]
    public void A_hidden_card_is_playable_from_the_next_turn_ignoring_its_base_cost()
    {
        var (game, engine, card) = Setup();
        engine.Accept(P1, new Hide(card, 0));
        engine.PayWithSuggestion(P1);
        var facedown = Assert.Single(game.State.At(Place.Facedown(0)));
        Assert.DoesNotContain(facedown, engine.Decision<PriorityDecision>().Playable);

        engine.Accept(P1, new EndTurn());
        engine.Accept(P2, new EndTurn());
        Assert.Contains(facedown, engine.Decision<PriorityDecision>().Playable);

        engine.Accept(P1, new PlayCard(facedown));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(0, pay.Cost.Energy);
        Assert.Empty(pay.Cost.Power);
        engine.Accept(P1, new PayCost());

        Assert.Contains(game.State.At(Place.Battlefield(0)), id => game.State[id].CardId == "hidden-unit");
        Assert.Empty(game.State.At(Place.Facedown(0)));
    }

    [Fact]
    public void A_facedown_card_is_trashed_when_control_of_its_battlefield_is_lost()
    {
        var (game, engine, card) = Setup();
        engine.Accept(P1, new Hide(card, 0));
        engine.PayWithSuggestion(P1);
        var unit = game.First(Place.Battlefield(0), "unit-2");

        engine.Accept(P1, new StandardMove { Units = [unit], Destination = Place.Base(P1) });

        Assert.Empty(game.State.At(Place.Facedown(0)));
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "hidden-unit");
    }

    [Fact]
    public void Hide_is_not_offered_without_a_controlled_battlefield()
    {
        var game = new TestGame();
        game.Put("hidden-unit", Place.Hand(P1));

        var engine = game.Start();

        Assert.Empty(engine.Decision<PriorityDecision>().Hides);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~HiddenTests"`
Expected: FAIL (`Hide` is rejected with `UnexpectedAction`). `Hide_is_not_offered_without_a_controlled_battlefield` already passes.

- [ ] **Step 3: Implement hiding**

Create `src/CromoBound.Engine/Rules/Game.Hidden.cs`:
```csharp
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

/// <summary>Hide (CR 421, 811): pay [A], then the card goes face down at a battlefield you control.</summary>
internal sealed class HideTask(PlayerId player, ObjectId card, int battlefield) : GameTask
{
    public PlayerId Player { get; } = player;
    public ObjectId Card { get; } = card;
    public int Battlefield { get; } = battlefield;
    public bool Paid { get; set; }
    public bool Cancelled { get; set; }
    public TotalCost Cost { get; set; } = new(0, [PowerSymbol.Any]);

    public override bool Run(Game game) => game.RunHide(this);
}

public sealed partial class Game
{
    private void StartHide(PlayerId player, ObjectId card, int battlefield) => Push(new HideTask(player, card, battlefield));

    internal bool RunHide(HideTask task)
    {
        if (task.Cancelled) return true;
        if (!task.Paid)
        {
            AskPay(task.Player, task.Cost, CardOf(task.Card).Domains,
                onPaid: () => task.Paid = true,
                onCancel: () => task.Cancelled = true,
                onAdjust: adjusted => task.Cost = adjusted);
            return false;
        }
        var id = MoveCard(task.Card, Place.Facedown(task.Battlefield))!.Value;
        var card = State[id];
        card.Facedown = true;
        card.Controller = task.Player;
        card.HiddenOnTurn = State.Turn.Number;
        return true;
    }
}
```

- [ ] **Step 4: Dispatch Hide**

In `src/CromoBound.Engine/Rules/Game.Dispatch.cs`, add this case after the `StandardMove` case:
```csharp
            case Hide hide when options.Hides.Any(h => h.Card == hide.Card && h.Battlefields.Contains(hide.Battlefield)):
                StartHide(player, hide.Card, hide.Battlefield);
                return null;
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests).

- [ ] **Step 6: Commit**

```bash
git add src/CromoBound.Engine/Rules tests/CromoBound.Engine.Tests/HiddenTests.cs
git commit -m "feat(engine): hide cards and play them from face down"
```

---

### Task 7: Turn points (start/end-of-turn effects)

About 26 real cards act "at the start of your Beginning phase", "at the start of your Main phase" or "at the end of your turn". In 2a these are applied by hand, so the engine must stop at the right moment. Pausing every turn would add clicks, so it pauses only when a card in play has text for that moment.

**Files:**
- Create: `src/CromoBound.Engine/Rules/Game.TurnPoints.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Turn.cs` (`StartTurn`, `EndTheTurn`)
- Modify: `tests/CromoBound.Engine.Tests/EngineTestDb.cs`
- Test: `tests/CromoBound.Engine.Tests/TurnPointTests.cs`

**Interfaces:**
- Consumes: `TurnPoint`, `TurnPointDecision`, `ContinueTurn` (Task 1); `RichText.StripReminders` (Phase 1).
- Produces: `TurnPointTask(TurnPoint)`, `Game.TurnPointCards(TurnPoint, PlayerId) : List<ObjectId>`. Plan C allows manual actions while a `TurnPointDecision` is pending.

**Rules of the pause:**
- **Where:**
  - `StartOfBeginning`: after the Beginning step (and its Temporary kills), before the Scoring step.
  - `StartOfMain`: after rune pools empty at the start of the Main phase, before priority.
  - `EndOfTurn`: after the Ending step, before the Expiration step (so before units heal and pools empty).
- **Which cards:** objects on the board (Base, battlefields, Legend Zone, battlefield cards; not facedown cards). Their rich text, with reminder text removed, must match the point:
  - `StartOfBeginning`: `at (the) start/beginning of … Beginning phase`
  - `StartOfMain`: `at (the) start/beginning of … Main phase`
  - `EndOfTurn`: `at (the) end of … turn`
- **"your":** if the matched text contains "your", it counts only on its controller's turn. Otherwise ("each player's", "at end of turn") it counts every turn.
- **Who decides:** the card's controller. For a battlefield card, its controller, or the turn player if nobody controls it (CR 190.6). The turn player goes first, then the opponent. Each answers with `ContinueTurn`.

- [ ] **Step 1: Add test cards**

In `tests/CromoBound.Engine.Tests/EngineTestDb.cs`:

Replace the `temp-1` line in `Cards` so its text carries Temporary's reminder:
```csharp
        Unit("temp-1", Domain.Fury, energy: 1, might: 1, [DisplayKeyword.Temporary]) with
        {
            Text = new CardText { Rich = "<p>[Temporary] (At the start of its controller's Beginning phase, before scoring, kill this.)</p>" },
        },
```

Add these entries after the `gear-1` line:
```csharp
        Relic("dawn-relic", "<p>At the start of your Beginning phase, deal 1 to a unit.</p>"),
        Relic("each-relic", "<p>At the start of each player's Beginning phase, deal 1 to each unit.</p>"),
        Relic("noon-relic", "<p>At the start of your Main phase, draw 1.</p>"),
        Relic("dusk-relic", "<p>At the end of your turn, ready 2 runes.</p>"),
```

Add this helper next to `Simple`:
```csharp
    private static Card Relic(string id, string rich) =>
        Simple(id, CardType.Gear, [Domain.Fury]) with { Cost = new CardCost { Energy = 1 }, Text = new CardText { Rich = rich } };
```

- [ ] **Step 2: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/TurnPointTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class TurnPointTests
{
    [Fact]
    public void No_pause_without_cards_for_that_moment()
    {
        var game = new TestGame();
        game.Put("unit-2", Place.Base(P1));
        game.Put("temp-1", Place.Base(P1));

        var engine = game.Start();
        engine.Accept(P1, new EndTurn());

        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Pauses_at_the_start_of_your_beginning_phase_before_scoring()
    {
        var game = new TestGame();
        var relic = game.Put("dawn-relic", Place.Base(P1));
        game.State.Battlefields[0].Controller = P1;
        game.Put("unit-2", Place.Battlefield(0));

        var engine = game.Start();

        var pause = engine.Decision<TurnPointDecision>();
        Assert.Equal((P1, TurnPoint.StartOfBeginning), (pause.Player, pause.Point));
        Assert.Equal(new[] { relic }, pause.Cards);
        Assert.Equal(0, game.State.Player(P1).Points);

        engine.Accept(P1, new ContinueTurn());

        Assert.Equal(1, game.State.Player(P1).Points);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Your_cards_dont_pause_on_the_opponents_turn_but_each_player_cards_do()
    {
        var mine = new TestGame();
        mine.Put("dawn-relic", Place.Base(P2));
        Assert.IsType<PriorityDecision>(mine.Start().Pending);

        var each = new TestGame();
        each.Put("each-relic", Place.Base(P2));
        Assert.Equal(P2, each.Start().Decision<TurnPointDecision>().Player);
    }

    [Fact]
    public void The_turn_player_continues_first_then_the_opponent()
    {
        var game = new TestGame();
        game.Put("dawn-relic", Place.Base(P1));
        game.Put("each-relic", Place.Base(P2));
        var engine = game.Start();

        Assert.Equal(P1, engine.Decision<TurnPointDecision>().Player);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new EndTurn()).Rejection!.Code);
        engine.Accept(P1, new ContinueTurn());
        Assert.Equal(P2, engine.Decision<TurnPointDecision>().Player);
        engine.Accept(P2, new ContinueTurn());

        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Pauses_at_the_start_of_main_and_at_end_of_turn_before_pools_empty()
    {
        var game = new TestGame();
        game.Put("noon-relic", Place.Base(P1));
        game.Put("dusk-relic", Place.Base(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();

        Assert.Equal(TurnPoint.StartOfMain, engine.Decision<TurnPointDecision>().Point);
        engine.Accept(P1, new ContinueTurn());
        var rune = game.First(Place.Base(P1), "fury-rune");
        engine.Accept(P1, new UseRune(rune, RuneUse.Exhaust));

        engine.Accept(P1, new EndTurn());

        Assert.Equal(TurnPoint.EndOfTurn, engine.Decision<TurnPointDecision>().Point);
        Assert.Equal(1, game.State.Player(P1).Pool.Energy);
        engine.Accept(P1, new ContinueTurn());
        Assert.True(game.State.Player(P1).Pool.IsEmpty);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~TurnPointTests"`
Expected: FAIL. The pause tests get a `PriorityDecision` instead of a `TurnPointDecision`. `No_pause_without_cards_for_that_moment` already passes.

- [ ] **Step 4: Implement the turn points**

Create `src/CromoBound.Engine/Rules/Game.TurnPoints.cs`:
```csharp
using System.Text.RegularExpressions;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Rules;

/// <summary>Pauses so start/end-of-turn effects can be applied by hand: the turn player first, then the opponent.</summary>
internal sealed class TurnPointTask(TurnPoint point) : GameTask
{
    public TurnPoint Point { get; } = point;
    public List<PlayerId>? Order { get; set; }
    public int Next { get; set; }

    public override bool Run(Game game) => game.RunTurnPoint(this);
}

public sealed partial class Game
{
    private static readonly Regex StartOfBeginningText = new(@"\bat (the )?(start|beginning) of\b[^.]*\bbeginning phase\b", RegexOptions.IgnoreCase);
    private static readonly Regex StartOfMainText = new(@"\bat (the )?(start|beginning) of\b[^.]*\bmain phase\b", RegexOptions.IgnoreCase);
    private static readonly Regex EndOfTurnText = new(@"\bat (the )?end of\b[^.]*\bturn\b", RegexOptions.IgnoreCase);
    private static readonly Regex Your = new(@"\byour\b", RegexOptions.IgnoreCase);

    internal bool RunTurnPoint(TurnPointTask task)
    {
        task.Order ??= [.. TurnOrder()];
        while (task.Next < task.Order.Count)
        {
            var player = task.Order[task.Next];
            var cards = TurnPointCards(task.Point, player);
            if (cards.Count == 0)
            {
                task.Next++;
                continue;
            }
            Ask(new TurnPointDecision(player, task.Point, cards), (_, action) =>
            {
                if (action is not ContinueTurn)
                    return Reject(RejectionCode.UnexpectedAction, "Apply the effects with manual actions, then continue the turn.");
                task.Next++;
                return null;
            });
            return false;
        }
        return true;
    }

    /// <summary>Cards in play handled by <paramref name="player"/> whose text acts at this point (reminder text ignored).
    /// Text saying "your" counts only on its controller's turn.</summary>
    internal List<ObjectId> TurnPointCards(TurnPoint point, PlayerId player)
    {
        var pattern = point switch
        {
            TurnPoint.StartOfBeginning => StartOfBeginningText,
            TurnPoint.StartOfMain => StartOfMainText,
            _ => EndOfTurnText,
        };
        var result = new List<ObjectId>();
        foreach (var instance in State.Objects.Where(o => o.Place.IsBoard && o.Place.Kind != PlaceKind.Facedown))
        {
            if (HandledBy(instance) != player) continue;
            var match = pattern.Match(RichText.StripReminders(CardOf(instance).Text.Rich));
            if (!match.Success) continue;
            if (Your.IsMatch(match.Value) && player != State.Turn.TurnPlayer) continue;
            result.Add(instance.Id);
        }
        return result;
    }

    /// <summary>A battlefield's abilities belong to its controller, or the turn player when uncontrolled (CR 190.6).</summary>
    private PlayerId HandledBy(CardInstance instance) =>
        instance.Place.Kind == PlaceKind.BattlefieldCard
            ? State.Battlefields[instance.Place.Index!.Value].Controller ?? State.Turn.TurnPlayer
            : instance.Controller;

    private IEnumerable<PlayerId> TurnOrder()
    {
        yield return State.Turn.TurnPlayer;
        foreach (var player in State.Players)
            if (player.Id != State.Turn.TurnPlayer) yield return player.Id;
    }
}
```

- [ ] **Step 5: Queue the turn points**

In `src/CromoBound.Engine/Rules/Game.Turn.cs`:

In `StartTurn`, replace the five `Enqueue` lines with:
```csharp
        Enqueue(new StepTask(g => g.BeginningStep()));
        Enqueue(new TurnPointTask(TurnPoint.StartOfBeginning));
        Enqueue(new StepTask(g => g.ScoringStep()));
        Enqueue(new StepTask(g => g.ChannelPhase()));
        Enqueue(new StepTask(g => g.DrawPhase()));
        Enqueue(new StepTask(g => g.MainPhase()));
        Enqueue(new TurnPointTask(TurnPoint.StartOfMain));
```

In `EndTheTurn`, insert this line after the first `Enqueue` (the Ending step):
```csharp
        Enqueue(new TurnPointTask(TurnPoint.EndOfTurn));
```

Add `using CromoBound.Engine.Decisions;` to the top of `Game.Turn.cs` (for `TurnPoint`).

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests).

- [ ] **Step 7: Check the real cards once**

The pattern is a heuristic, so check it against the real data once. Run:
```bash
python -c "import json,re; cards=json.load(open('data/cards.json',encoding='utf-8')); strip=lambda t: re.sub(r'\s*\([^)]*\)','',t); pats=[r'\bat (the )?(start|beginning) of\b[^.]*\bbeginning phase\b', r'\bat (the )?(start|beginning) of\b[^.]*\bmain phase\b', r'\bat (the )?end of\b[^.]*\bturn\b']; [print(c['id']) for c in cards if any(re.search(p, strip(c['text']['rich']), re.I) for p in pats)]"
```
Expected: about 26 ids (cards whose text, not their reminder text, acts at these points). Report the list to the project owner, and say if any card looks wrong (missed or matched by mistake).

- [ ] **Step 8: Commit**

```bash
git add src/CromoBound.Engine/Rules tests/CromoBound.Engine.Tests
git commit -m "feat(engine): pause at start/end of turn for card effects"
```

---

### Task 8: A scripted full game

**Files:**
- Create: `tests/CromoBound.Engine.Tests/Bot.cs`
- Test: `tests/CromoBound.Engine.Tests/ScriptedGameTests.cs`

**Interfaces:**
- Consumes: the whole public `Game` API (Tasks 1–7).
- Produces: `Bot` (test helper). It answers any pending decision with a simple, deterministic policy. Plan C reuses it for full matches.

- [ ] **Step 1: Write the test and the scripted player**

Create `tests/CromoBound.Engine.Tests/Bot.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Tests;

/// <summary>A deterministic scripted player: plays the first affordable unit to its Base, sends ready units from Base to a
/// battlefield it doesn't control, passes whenever it can, resolves by hand immediately and takes every suggestion.</summary>
internal sealed class Bot
{
    private readonly HashSet<string> _unaffordable = [];
    private int _turn;
    private string? _playing;

    public PlayerAction Choose(Game game)
    {
        if (game.State.Turn.Number != _turn)
        {
            _turn = game.State.Turn.Number;
            _unaffordable.Clear();
        }
        return game.Pending switch
        {
            PriorityDecision priority => ChoosePriority(game, priority),
            PlayChoicesDecision choices => new ChoosePlayOptions(choices.Locations.Count > 0 ? choices.Locations[0] : null, false),
            PayCostDecision { Suggested: { } s } => new PayCost { Exhaust = s.Exhaust, Recycle = s.Recycle },
            PayCostDecision => GiveUp(),
            AssignDamageDecision assign => new AssignDamage { Assignments = assign.Suggested },
            ChooseShowdownDecision showdown => new ChooseShowdown(showdown.Battlefields[0]),
            ResolveManuallyDecision => new ResolveDone(),
            TurnPointDecision => new ContinueTurn(),
            _ => throw new InvalidOperationException($"Unexpected decision {game.Pending}."),
        };
    }

    private PlayerAction GiveUp()
    {
        _unaffordable.Add(_playing!);
        return new CancelPlay();
    }

    private PlayerAction ChoosePriority(Game game, PriorityDecision priority)
    {
        if (priority.CanPass) return new Pass();
        var state = game.State;
        var unit = priority.Playable.FirstOrDefault(id =>
            game.Db.Cards[state[id].CardId].Type == CardType.Unit && !_unaffordable.Contains(state[id].CardId));
        if (unit != default)
        {
            _playing = state[unit].CardId;
            return new PlayCard(unit);
        }
        var target = state.Battlefields
            .Where(b => b.Controller != priority.Player)
            .Select(b => Place.Battlefield(b.Index))
            .FirstOrDefault(Place.Base(priority.Player));
        var movers = priority.Moves
            .Where(m => state[m.Unit].Place.Kind == PlaceKind.Base && m.Destinations.Contains(target))
            .Select(m => m.Unit)
            .ToList();
        if (target.Kind == PlaceKind.Battlefield && movers.Count > 0)
            return new StandardMove { Units = movers, Destination = target };
        return new EndTurn();
    }
}
```

Create `tests/CromoBound.Engine.Tests/ScriptedGameTests.cs`:
```csharp
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ScriptedGameTests
{
    [Fact]
    public void A_scripted_game_reaches_a_winner_and_replays_identically()
    {
        var first = Play(seed: 3);
        var second = Play(seed: 3);

        Assert.NotNull(first.Outcome);
        Assert.NotNull(first.Outcome!.Winner);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.State.Turn.Number, second.State.Turn.Number);
        Assert.Equal(first.State.Players.Select(p => p.Points), second.State.Players.Select(p => p.Points));
    }

    /// <summary>Two scripted players with 20 unit-2 and 12 fury runes each, until the game ends (or a safety cap).</summary>
    private static Game Play(ulong seed)
    {
        var game = new TestGame(seed);
        foreach (var player in new[] { P1, P2 })
        {
            for (var i = 0; i < 20; i++) game.Put("unit-2", Place.MainDeck(player));
            for (var i = 0; i < 12; i++) game.Put("fury-rune", Place.RuneDeck(player));
        }
        var engine = game.Start(filler: 0);
        var bots = new[] { new Bot(), new Bot() };
        for (var i = 0; i < 5000 && engine.Outcome is null; i++)
        {
            var player = engine.Pending!.Players[0];
            engine.Accept(player, bots[player.Index].Choose(engine));
        }
        return engine;
    }
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~ScriptedGameTests"`
Expected: PASS. The game ends with a winner well before the 5000-decision cap.

If it fails, find the first decision where the game stops progressing (for example, the same `PriorityDecision` repeating with no way forward). Fix the engine, not the test, unless the bot's policy is clearly wrong.

- [ ] **Step 3: Run all tests**

Run: `dotnet test CromoBound.slnx`
Expected: PASS (all tests).

- [ ] **Step 4: Commit**

```bash
git add tests/CromoBound.Engine.Tests/Bot.cs tests/CromoBound.Engine.Tests/ScriptedGameTests.cs
git commit -m "test(engine): play a scripted full game"
```

---

## Done criteria

- [ ] Engine types exist and actions round-trip through JSON (Task 1).
- [ ] Payment matching and combat damage rules are covered by unit tests (Task 2).
- [ ] Turns run: Awaken, Beginning (Temporary, Hold), Channel (+1 on turn 2), Draw (Burn Out), Main, Ending (heal, expire, empty pools) (Task 3).
- [ ] Cards are played and paid for; cancel and cost adjustment work; the chain resolves newest first and by hand; players can stack their own Reactions (Task 4).
- [ ] Standard moves, Contested, the full cleanup, showdowns, combat with Tank/Backline, conquer, the Final Point and control loss work (Task 5).
- [ ] Hide and play from face down work; facedown cards go to the trash on control loss (Task 6).
- [ ] The engine pauses at start/end-of-turn points only when a card in play has an effect there (Task 7).
- [ ] A scripted game reaches a winner and replays identically (Task 8).
- [ ] `dotnet test CromoBound.slnx` passes.
