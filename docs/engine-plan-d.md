# Engine Plan D: Effects Foundations and Spells

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. First of three plans for Phase 2b (D: foundations and spells; E: triggers and activations; F: modifiers, keywords and integration).

**Goal:** Mapped spells resolve automatically: targets are chosen while playing, the spell's steps run on resolution (Draw, Deal, Kill, Burn), Partial spells leave only their unmapped lines to the players, and every other card plays exactly as in 2a.

**Architecture:**
- A new `CromoBound.Engine.Effects` namespace holds the interpreter.
  - `CardEffects` says what a card does (status, abilities, keywords, manual lines).
  - `EffectsSupport` says whether the engine can run a file; a file it can't run yet is treated as Unmapped.
  - Resolvers turn selectors, players and values into objects, players and numbers.
  - `ResolveEffectTask` runs a step list with a step counter, like every 2a task.
  - One `IStepHandler` per step type, registered in `StepRegistry`.
- `Game` gains target selection in `PlayCardTask` and the Full/Partial/Unmapped switch in `ResolveTop`.

**Tech Stack:** .NET 10, xUnit 2.9.3. No new dependencies.

**Spec:** `docs/effects-engine.md` (sections 3, 4.1-4.5, 5.1, 5.6, 6.1-6.2, 6.4, 7, 9, 10 as far as spells go).

## Global Constraints

- Plans A, B and C's Global Constraints still apply: `net10.0`; no NuGet packages in runtime projects; no `DateTime`, `Guid`, `System.Random` or hash-order-dependent iteration in engine code; `CromoJson` for everything saved; every action and decision type round-trips through it.
- Public API stays limited to `Match`, `Game`, `PlayerView` and the action, decision, event and result records. Everything in `CromoBound.Engine.Effects` is `internal`. The test project gets access through `InternalsVisibleTo` (Task 1).
- Unmapped cards (no effects file, or a file the engine can't run yet) play exactly as in 2a. Every existing test keeps passing unchanged.
- Step handlers call the existing `Game` mutations (`Draw`, `DealDamage`, `Kill`, `MoveCard`, ...); no rule is duplicated.
- Owner rules: 0 build warnings and 0 errors at all times; conventional, title-only commit messages with no body, no co-author trailer and no mention of Claude/AI; no em dashes or en dashes in code, comments or strings (test strings that copy real card text may keep them); LF line endings; UTF-8 without BOM.
- Run dotnet with `export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && ` in Git Bash (the default dotnet on PATH is SDK 9).

## Deliberate deviations from the spec (reviewers: these are intended)

1. **A file the engine can't run yet falls back to Unmapped at runtime.** Spec §7 describes a load-time check; this plan adds a runtime fallback (`CardEffects` treats such a card as Unmapped and records why in `Unsupported`), so Plan D can ship while Plans E and F's constructs (triggers, activated and passive abilities, most keywords) are not implemented. The data test pins which sample cards run today; Plan F asserts every mapped card runs.
2. **Plan D's vocabulary is only what the spells need:** literal values; `You`/`Opponent`/`EachPlayer`/`EachOpponent` players; `Self` and `Unit`/`Gear`/`Permanent` selectors with `count` or `upTo` and `relation`/`type`/`token`/`other` filters; the steps Draw, Deal, Kill, Burn. A file with `overrides`, `additionalCosts` or `asYouPlay` is not run yet (no mapped card uses them). Conditions, variables in values, `ChoosePlayer`, `ChooseCards`, `Optional` and `OrderTriggers` decisions arrive in Plan E (spec decision 1, scope A).
3. **`Game.RunNow(task)`** is an internal seam that runs a task immediately, dropping the pending decision (whatever raised it asks again). Only tests use it.

## Review Focus

1. **A spell whose target dies or leaves before it resolves.** It resolves doing nothing to that target, still goes to its owner's trash, and the game goes on. Pinned in Task 2 (`Deal_to_a_target_that_left_does_nothing_and_stores_that_it_didnt_happen`) and Task 5 (`A_target_that_left_before_resolution_is_skipped`).
2. **A mapped spell with no legal target.** It isn't offered as playable and `PlayCard` is rejected, instead of getting stuck on an empty choice. Pinned in Task 4 (`A_spell_without_enough_legal_targets_is_not_playable`).
3. **A manual action while a target choice is pending.** The choice is asked again with fresh options, and slots already chosen are kept. Pinned in Task 4 (`A_manual_action_during_the_target_choice_asks_again_with_fresh_options`).
4. **A card whose file uses something the engine can't run yet** (Kharox, Garbage Grabber, any keyword beyond 2a's). It plays by hand exactly as in 2a, keywords from its text. Pinned in Task 3 (`A_file_the_engine_cant_run_yet_falls_back_to_unmapped_and_says_why`, `Cards_needing_later_plans_play_by_hand_for_now`).
5. **Undo and loading with target choices.** Undoing a target choice equals never making it, and a saved match with target choices loads into the same state. Pinned in Task 5 (`Undo_after_choosing_a_target_equals_never_choosing_it`, `A_saved_match_with_target_choices_loads_identically`).

---

## File Structure

```
src/CromoBound.Engine/
  CromoBound.Engine.csproj            (modify) InternalsVisibleTo the test project
  Effects/EffectContext.cs            EffectVar, EffectContext
  Effects/TargetSlots.cs              which selectors are targets, slot lookup by reference
  Effects/Resolvers/ObjectResolver.cs selector candidates, references -> objects
  Effects/Resolvers/PlayerResolver.cs player references -> players
  Effects/Resolvers/ValueResolver.cs  values -> numbers
  Effects/Steps/StepHandler.cs        StepOutcome, IStepHandler, StepHandler<TStep>
  Effects/Steps/StepRegistry.cs       step type -> handler
  Effects/Steps/CardStepHandlers.cs   Draw, Burn
  Effects/Steps/PermanentStepHandlers.cs  Deal, Kill
  Effects/ResolveEffectTask.cs        runs a step list with a step counter
  Effects/EffectsSupport.cs           what the engine can run
  Effects/CardEffects.cs              CardEffectInfo, CardEffects
  Rules/Game.cs                       (modify) Effects, Has, RunNow
  Rules/Game.Mutations.cs             (modify) Burn, shared top-card helper
  Rules/CardKeywords.cs               (modify) IsKeywordLine
  Rules/Game.Targets.cs               SpellSteps, HasTargetsFor, AskTargets
  Rules/Game.Play.cs                  (modify) Targets play step
  Rules/Game.Priority.cs              (modify) playable needs legal targets
  Rules/Game.Chain.cs                 (modify) automated resolution switch
  State/ChainItem.cs                  (modify) Effect context
  Results.cs                          (modify) RejectionCode.InvalidTarget
  Actions/PlayerAction.cs             (modify) ChooseTargets
  Actions/ActionShape.cs              (modify) ChooseTargets shape
  Decisions/Decisions.cs              (modify) ChooseTargetsDecision
  Events/GameEvents.cs                (modify) ChoiceMade
  Views/PlayerView.cs, ViewBuilder.cs (modify) CardView.Effects, CardView.ManualLines
tests/CromoBound.Engine.Tests/
  RepoPaths.cs                        finds data/
  EngineTestDb.cs                     (modify) effects, multi-spell, real cards
  TestGame.cs                         (modify) optional database
  Bot.cs                              (modify) ChooseTargetsDecision
  ResolverTests.cs, StepTests.cs, CardEffectsTests.cs, EffectsDataTests.cs, TargetTests.cs,
  SpellResolutionTests.cs, MatchEffectsTests.cs
```

---

### Task 1: Effect context, target slots and resolvers

**Files:**
- Modify: `src/CromoBound.Engine/CromoBound.Engine.csproj`
- Create: `src/CromoBound.Engine/Effects/EffectContext.cs`, `Effects/TargetSlots.cs`, `Effects/Resolvers/ObjectResolver.cs`, `Effects/Resolvers/PlayerResolver.cs`, `Effects/Resolvers/ValueResolver.cs`
- Test: `tests/CromoBound.Engine.Tests/ResolverTests.cs`

**Interfaces:**
- Consumes: Plan B's `Game` (`State`, internal `CardOf(CardInstance)`), `GameState` (`Objects`, `Exists`, `Opponent`, `Players`, `Turn`), `CardInstance`; Phase 1's `ObjectRef`, `PlayerRef`, `Value`, `Filter`, `SelectKind`, `Relation`, `PlayerKind`, `RefKind`, `Step`, `TargetStep`.
- Produces (namespace `CromoBound.Engine.Effects`):
  - `internal sealed record EffectVar(IReadOnlyList<ObjectId> Objects, IReadOnlyList<PlayerId> Players, int? Number, bool Happened)` with `static EffectVar Empty`.
  - `internal sealed class EffectContext { PlayerId Controller (required init); ObjectId? Source (init); string SourceCardId (required init); IReadOnlyList<ObjectRef> Slots (init); List<IReadOnlyList<ObjectId>> Targets; Dictionary<string, EffectVar> Vars }`.
  - `internal static class TargetSlots { IReadOnlyList<ObjectRef> Of(IReadOnlyList<Step>); bool IsTarget(ObjectRef); int IndexOf(IReadOnlyList<ObjectRef>, ObjectRef) }`.
- Produces (namespace `CromoBound.Engine.Effects.Resolvers`):
  - `ObjectResolver.Candidates(Game, EffectContext, ObjectRef selector) : List<ObjectId>` and `ObjectResolver.Resolve(Game, EffectContext, ObjectRef) : List<ObjectId>`.
  - `PlayerResolver.Resolve(Game, EffectContext, PlayerRef?) : List<PlayerId>`.
  - `ValueResolver.Resolve(Game, EffectContext, Value) : int`.

- [ ] **Step 1: Let the tests see internals**

In `src/CromoBound.Engine/CromoBound.Engine.csproj`, add this item group after the project references:
```xml
  <ItemGroup>
    <InternalsVisibleTo Include="CromoBound.Engine.Tests" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/ResolverTests.cs`:
```csharp
using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ResolverTests
{
    private static EffectContext Context(PlayerId controller, ObjectId? source = null, params ObjectRef[] slots) =>
        new() { Controller = controller, Source = source, SourceCardId = "spell", Slots = slots };

    private static ObjectRef Select(SelectKind kind, Relation? relation = null, bool other = false) => new()
    {
        Select = kind,
        Count = 1,
        Filter = relation is null && !other ? null : new Filter { Relation = relation, Other = other ? true : null },
    };

    [Fact]
    public void Unit_candidates_are_board_units_in_id_order_filtered_by_relation()
    {
        var game = new TestGame();
        var mine = game.Put("unit-2", Place.Base(P1));
        var theirs = game.Put("unit-3", Place.Base(P2));
        game.Put("unit-2", Place.Trash(P1));
        game.Put("gear-1", Place.Base(P1));
        var engine = game.Start();
        var context = Context(P1);

        Assert.Equal(new[] { mine, theirs }, ObjectResolver.Candidates(engine, context, Select(SelectKind.Unit)));
        Assert.Equal(new[] { theirs }, ObjectResolver.Candidates(engine, context, Select(SelectKind.Unit, Relation.Enemy)));
        Assert.Equal(new[] { mine }, ObjectResolver.Candidates(engine, context, Select(SelectKind.Unit, Relation.Friendly)));
    }

    [Fact]
    public void Permanents_include_gear_and_other_leaves_out_the_source()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var gear = game.Put("gear-1", Place.Base(P1));
        var engine = game.Start();

        Assert.Equal(new[] { unit, gear }, ObjectResolver.Candidates(engine, Context(P1), Select(SelectKind.Permanent)));
        Assert.Equal(new[] { gear }, ObjectResolver.Candidates(engine, Context(P1, unit), Select(SelectKind.Permanent, other: true)));
    }

    [Fact]
    public void A_chosen_target_that_left_the_board_is_dropped()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var kept = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();
        var slot = new ObjectRef { Select = SelectKind.Unit, UpTo = 2 };
        var context = Context(P1, null, slot);
        context.Targets.Add([unit, kept]);

        engine.MoveCard(unit, Place.Trash(P1));

        Assert.Equal(new[] { kept }, ObjectResolver.Resolve(engine, context, slot));
    }

    [Fact]
    public void Self_means_the_source_while_it_exists()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Assert.Equal(new[] { unit }, ObjectResolver.Resolve(engine, Context(P1, unit), ObjectRef.Self));
        engine.MoveCard(unit, Place.Trash(P1));
        Assert.Empty(ObjectResolver.Resolve(engine, Context(P1, unit), ObjectRef.Self));
    }

    [Fact]
    public void Equal_looking_target_selectors_are_separate_slots()
    {
        var first = new ObjectRef { Select = SelectKind.Unit, Count = 1 };
        var second = new ObjectRef { Select = SelectKind.Unit, Count = 1 };
        Step[] steps =
        [
            new DealStep { Amount = 3, Target = first },
            new DrawStep { Amount = 1 },
            new KillStep { Target = ObjectRef.Self },
            new KillStep { Target = new ObjectRef { Select = SelectKind.Unit, All = true } },
            new DealStep { Amount = 3, Target = second },
        ];

        var slots = TargetSlots.Of(steps);

        Assert.Equal(2, slots.Count);
        Assert.Equal(0, TargetSlots.IndexOf(slots, first));
        Assert.Equal(1, TargetSlots.IndexOf(slots, second));
        Assert.Equal(-1, TargetSlots.IndexOf(slots, ObjectRef.Self));
    }

    [Fact]
    public void Players_resolve_in_turn_order()
    {
        var game = new TestGame();
        var engine = game.Start(first: P2);
        var context = Context(P1);

        Assert.Equal(new[] { P1 }, PlayerResolver.Resolve(engine, context, null));
        Assert.Equal(new[] { P1 }, PlayerResolver.Resolve(engine, context, PlayerRef.You));
        Assert.Equal(new[] { P2 }, PlayerResolver.Resolve(engine, context, new PlayerRef { Kind = PlayerKind.Opponent }));
        Assert.Equal(new[] { P2, P1 }, PlayerResolver.Resolve(engine, context, new PlayerRef { Kind = PlayerKind.EachPlayer }));
        Assert.Equal(new[] { P2 }, PlayerResolver.Resolve(engine, context, new PlayerRef { Kind = PlayerKind.EachOpponent }));
    }

    [Fact]
    public void Literal_values_resolve_to_themselves()
    {
        var engine = new TestGame().Start();

        Assert.Equal(4, ValueResolver.Resolve(engine, Context(P1), 4));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~ResolverTests"`
Expected: FAIL (compilation errors: namespace `CromoBound.Engine.Effects` not found).

- [ ] **Step 4: Implement the context and the target slots**

Create `src/CromoBound.Engine/Effects/EffectContext.cs`:
```csharp
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>A stored step result ("store"): the objects, players or number it produced, and whether the step happened.</summary>
internal sealed record EffectVar(IReadOnlyList<ObjectId> Objects, IReadOnlyList<PlayerId> Players, int? Number, bool Happened)
{
    public static EffectVar Empty { get; } = new([], [], null, false);
}

/// <summary>Everything one resolution carries (spec §4.2). Built when a card is played or an ability goes on the chain.</summary>
internal sealed class EffectContext
{
    public required PlayerId Controller { get; init; }

    /// <summary>The object the ability belongs to: the spell on the chain, or the permanent.</summary>
    public ObjectId? Source { get; init; }

    /// <summary>The source's card id, kept even after the source leaves play.</summary>
    public required string SourceCardId { get; init; }

    /// <summary>The target selectors (see <see cref="TargetSlots"/>), in JSON order.</summary>
    public IReadOnlyList<ObjectRef> Slots { get; init; } = [];

    /// <summary>The targets chosen so far; entry i belongs to <see cref="Slots"/>[i].</summary>
    public List<IReadOnlyList<ObjectId>> Targets { get; } = [];

    public Dictionary<string, EffectVar> Vars { get; } = new(StringComparer.Ordinal);
}
```

Create `src/CromoBound.Engine/Effects/TargetSlots.cs`:
```csharp
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>The target selectors of a step list (spec §5.1, rule 355): selectors with count or upTo over public board objects.</summary>
internal static class TargetSlots
{
    public static IReadOnlyList<ObjectRef> Of(IReadOnlyList<Step> steps) =>
        [.. steps.OfType<TargetStep>().Select(s => s.Target).Where(IsTarget)];

    public static bool IsTarget(ObjectRef reference) =>
        reference.Select is SelectKind.Unit or SelectKind.Gear or SelectKind.Permanent
        && (reference.Count is not null || reference.UpTo is not null);

    /// <summary>The slot of a selector, found by reference: two equal-looking selectors are two slots (Falling Star). -1 when it isn't a slot.</summary>
    public static int IndexOf(IReadOnlyList<ObjectRef> slots, ObjectRef reference)
    {
        for (var i = 0; i < slots.Count; i++)
            if (ReferenceEquals(slots[i], reference)) return i;
        return -1;
    }
}
```

- [ ] **Step 5: Implement the resolvers**

Create `src/CromoBound.Engine/Effects/Resolvers/ObjectResolver.cs`:
```csharp
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns object references into objects (spec §4.5). Pure: reads the game, changes nothing.</summary>
internal static class ObjectResolver
{
    /// <summary>The board objects a selector can pick now, in id order.</summary>
    public static List<ObjectId> Candidates(Game game, EffectContext context, ObjectRef selector) =>
    [
        .. game.State.Objects
            .Where(o => o.Place.IsLocation && Matches(game, context, selector.Select!.Value, selector.Filter, o))
            .Select(o => o.Id),
    ];

    /// <summary>The objects a reference means now: Self (while it exists), a stored variable, a target slot's chosen targets
    /// (dropping those no longer legal), or a selector's candidates.</summary>
    public static List<ObjectId> Resolve(Game game, EffectContext context, ObjectRef reference)
    {
        if (reference.Ref == RefKind.Self)
            return context.Source is { } source && game.State.Exists(source) ? [source] : [];
        if (reference.Var is { } name)
            return context.Vars.TryGetValue(name, out var stored) ? [.. stored.Objects.Where(game.State.Exists)] : [];
        var slot = TargetSlots.IndexOf(context.Slots, reference);
        if (slot < 0) return Candidates(game, context, reference);
        if (slot >= context.Targets.Count) return [];
        var legal = Candidates(game, context, reference);
        return [.. context.Targets[slot].Where(legal.Contains)];
    }

    private static bool Matches(Game game, EffectContext context, SelectKind kind, Filter? filter, CardInstance instance)
    {
        var type = game.CardOf(instance).Type;
        var kindMatches = kind switch
        {
            SelectKind.Unit => type == CardType.Unit,
            SelectKind.Gear => type == CardType.Gear,
            SelectKind.Permanent => type is CardType.Unit or CardType.Gear,
            _ => false,
        };
        if (!kindMatches || filter is null) return kindMatches;
        if (filter.Relation == Relation.Friendly && instance.Controller != context.Controller) return false;
        if (filter.Relation == Relation.Enemy && instance.Controller == context.Controller) return false;
        if (filter.Type is { } wanted && type != wanted) return false;
        if (filter.Token is { } token && instance.IsToken != token) return false;
        if (filter.Other == true && context.Source == instance.Id) return false;
        return true;
    }
}
```

Create `src/CromoBound.Engine/Effects/Resolvers/PlayerResolver.cs`:
```csharp
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns player references into players, in turn order (turn player first). A missing reference means "You".</summary>
internal static class PlayerResolver
{
    public static List<PlayerId> Resolve(Game game, EffectContext context, PlayerRef? reference)
    {
        var you = context.Controller;
        var target = reference ?? PlayerRef.You;
        return target.Kind switch
        {
            PlayerKind.You => [you],
            PlayerKind.Opponent => [game.State.Opponent(you)],
            PlayerKind.EachPlayer => [.. InTurnOrder(game)],
            PlayerKind.EachOpponent => [.. InTurnOrder(game).Where(p => p != you)],
            _ => target.Var is { } name && context.Vars.TryGetValue(name, out var stored) ? [.. stored.Players] : [],
        };
    }

    private static IEnumerable<PlayerId> InTurnOrder(Game game)
    {
        var turnPlayer = game.State.Turn.TurnPlayer;
        yield return turnPlayer;
        foreach (var player in game.State.Players)
            if (player.Id != turnPlayer) yield return player.Id;
    }
}
```

Create `src/CromoBound.Engine/Effects/Resolvers/ValueResolver.cs`:
```csharp
using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns values into numbers. Plan D runs literals; the support check keeps other forms out of the cards it runs.</summary>
internal static class ValueResolver
{
    public static int Resolve(Game game, EffectContext context, Value value) =>
        value.Literal ?? throw new InvalidOperationException("Only literal values are supported so far.");
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 7: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests/ResolverTests.cs
git commit -m "feat(engine): add effect context, target slots and resolvers"
```

---

### Task 2: Step handlers and the effect task

**Files:**
- Create: `src/CromoBound.Engine/Effects/Steps/StepHandler.cs`, `Effects/Steps/StepRegistry.cs`, `Effects/Steps/CardStepHandlers.cs`, `Effects/Steps/PermanentStepHandlers.cs`, `Effects/ResolveEffectTask.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.cs` (`RunNow`), `src/CromoBound.Engine/Rules/Game.Mutations.cs` (`Burn`)
- Test: `tests/CromoBound.Engine.Tests/StepTests.cs`

**Interfaces:**
- Consumes: Task 1 (`EffectContext`, `EffectVar`, `TargetSlots`, resolvers); Plan B's `GameTask`, `Game.Draw`, `DealDamage`, `Kill`, `MoveCard`, `IsUnit`, `BurnOut`, `Outcome`.
- Produces:
  - Namespace `CromoBound.Engine.Effects.Steps`: `internal enum StepOutcome { Done, DidNothing, Asked }`; `internal interface IStepHandler { StepOutcome Run(Game, ResolveEffectTask, Step) }`; `internal abstract class StepHandler<TStep> : IStepHandler`; `internal static class StepRegistry { bool Supports(Type stepType); IStepHandler For(Step) }`; handlers for `DrawStep`, `BurnStep`, `DealStep`, `KillStep`.
  - Namespace `CromoBound.Engine.Effects`: `internal sealed class ResolveEffectTask(EffectContext context, IReadOnlyList<Step> steps, Action<Game> onDone) : GameTask` with `Context`, `Steps`, `Index`, `Result`.
  - `Game.Burn(PlayerId, int)` (internal), `Game.RunNow(GameTask) : IReadOnlyList<GameEvent>` (internal, tests).

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/StepTests.cs`:
```csharp
using CromoBound.Engine.Effects;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class StepTests
{
    private static ObjectRef AUnit() => new() { Select = SelectKind.Unit, Count = 1 };

    /// <summary>Runs the steps for P1, with the given targets for each target slot, and returns the context afterwards.</summary>
    private static EffectContext Run(Game engine, IReadOnlyList<Step> steps, params ObjectId[][] targets)
    {
        var context = new EffectContext { Controller = P1, SourceCardId = "spell", Slots = TargetSlots.Of(steps) };
        foreach (var slot in targets) context.Targets.Add(slot);
        engine.RunNow(new ResolveEffectTask(context, steps, _ => { }));
        return context;
    }

    [Fact]
    public void Draw_draws_for_the_named_players()
    {
        var game = new TestGame();
        var engine = game.Start();

        Run(engine, [new DrawStep { Amount = 2 }]);
        Run(engine, [new DrawStep { Amount = 1, Player = new PlayerRef { Kind = PlayerKind.EachPlayer } }]);

        Assert.Equal(4, game.State.At(Place.Hand(P1)).Count);
        Assert.Single(game.State.At(Place.Hand(P2)));
    }

    [Fact]
    public void Burn_puts_the_top_cards_into_the_trash()
    {
        var game = new TestGame();
        var engine = game.Start();
        var top = game.State.At(Place.MainDeck(P1)).Take(3).Select(id => game.State[id].CardId).ToList();

        Run(engine, [new BurnStep { Amount = 3 }]);

        Assert.Equal(3, game.State.At(Place.Trash(P1)).Count);
        Assert.Equal(6, game.State.At(Place.MainDeck(P1)).Count);
        Assert.Equal(top, game.State.At(Place.Trash(P1)).Select(id => game.State[id].CardId).ToList());
    }

    [Fact]
    public void Burning_past_an_empty_deck_burns_out()
    {
        var game = new TestGame();
        var engine = game.Start(filler: 2);

        var events = engine.RunNow(new ResolveEffectTask(
            new EffectContext { Controller = P1, SourceCardId = "spell" }, [new BurnStep { Amount = 2 }], _ => { }));

        Assert.Equal(1, game.State.Player(P2).Points);
        Assert.Contains(events, e => e is BurnedOut { Player.Index: 0 });
        Assert.Single(game.State.At(Place.Trash(P1)));
        Assert.Empty(game.State.At(Place.MainDeck(P1)));
    }

    [Fact]
    public void Deal_damages_the_target_and_cleanup_kills_lethal_damage()
    {
        var game = new TestGame();
        var hurt = game.Put("unit-3", Place.Base(P2));
        var doomed = game.Put("unit-2", Place.Base(P2));
        var engine = game.Start();

        Run(engine, [new DealStep { Amount = 2, Target = AUnit() }], [hurt]);
        Run(engine, [new DealStep { Amount = 2, Target = AUnit() }], [doomed]);

        Assert.Equal(2, game.State[hurt].Damage);
        Assert.False(game.State.Exists(doomed));
    }

    [Fact]
    public void Deal_to_a_target_that_left_does_nothing_and_stores_that_it_didnt_happen()
    {
        var game = new TestGame();
        var unit = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();
        engine.MoveCard(unit, Place.Hand(P2));

        var context = Run(engine, [new DealStep { Amount = 2, Target = AUnit(), Store = "hit" }], [unit]);

        Assert.False(context.Vars["hit"].Happened);
        Assert.All(game.State.At(Place.Hand(P2)), id => Assert.Equal(0, game.State[id].Damage));
    }

    [Fact]
    public void Kill_moves_the_target_to_its_owners_trash()
    {
        var game = new TestGame();
        var unit = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        var context = Run(engine, [new KillStep { Target = AUnit(), Store = "killed" }], [unit]);

        Assert.False(game.State.Exists(unit));
        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "unit-3");
        Assert.True(context.Vars["killed"].Happened);
        Assert.Equal(new[] { unit }, context.Vars["killed"].Objects);
    }

    [Fact]
    public void Two_target_slots_may_hit_the_same_unit()
    {
        var game = new TestGame();
        var unit = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        Run(engine, [new DealStep { Amount = 2, Target = AUnit() }, new DealStep { Amount = 2, Target = AUnit() }], [unit], [unit]);

        Assert.False(game.State.Exists(unit));
    }

    [Fact]
    public void The_task_reports_done_once_after_the_last_step_and_the_game_asks_again()
    {
        var game = new TestGame();
        var engine = game.Start();
        var done = 0;

        engine.RunNow(new ResolveEffectTask(new EffectContext { Controller = P1, SourceCardId = "spell" },
            [new DrawStep { Amount = 1 }, new DrawStep { Amount = 1 }], _ => done++));

        Assert.Equal(1, done);
        Assert.Equal(3, game.State.At(Place.Hand(P1)).Count);
        Assert.IsType<Decisions.PriorityDecision>(engine.Pending);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~StepTests"`
Expected: FAIL (compilation errors: `ResolveEffectTask`, `Game.RunNow` not found).

- [ ] **Step 3: Add Burn and the test seam to Game**

In `src/CromoBound.Engine/Rules/Game.Mutations.cs`, replace the whole `Draw` method with:
```csharp
    /// <summary>Draws one card at a time; an empty Main Deck burns out first (CR 413, 431).</summary>
    internal void Draw(PlayerId player, int count) => MoveTopCards(player, count, Place.Hand(player));

    /// <summary>Burn: the top cards of the Main Deck go to the trash one at a time; an empty deck burns out first (CR 431).</summary>
    internal void Burn(PlayerId player, int count) => MoveTopCards(player, count, Place.Trash(player));

    private void MoveTopCards(PlayerId player, int count, Place to)
    {
        var streak = 0;
        for (var i = 0; i < count && Outcome is null; i++)
        {
            if (State.At(Place.MainDeck(player)).Count == 0)
            {
                BurnOut(player, ++streak);
                if (State.At(Place.MainDeck(player)).Count == 0) continue;
            }
            MoveCard(State.At(Place.MainDeck(player))[0], to);
            streak = 0;
        }
    }
```

In `src/CromoBound.Engine/Rules/Game.cs`, add after `Continue()`:
```csharp
    /// <summary>Runs <paramref name="task"/> before everything else, dropping the pending decision (whatever raised it asks again). For tests.</summary>
    internal IReadOnlyList<GameEvent> RunNow(GameTask task)
    {
        Pending = null;
        _handler = null;
        Push(task);
        return Continue();
    }
```

- [ ] **Step 4: Implement the handlers, the registry and the task**

Create `src/CromoBound.Engine/Effects/Steps/StepHandler.cs`:
```csharp
using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>Done: the step happened. DidNothing: there was nothing to act on. Asked: it raised a decision and runs again after the answer.</summary>
internal enum StepOutcome { Done, DidNothing, Asked }

/// <summary>Runs one kind of step (spec §4.4). Called again with the same task after it asked.</summary>
internal interface IStepHandler
{
    StepOutcome Run(Game game, ResolveEffectTask task, Step step);
}

internal abstract class StepHandler<TStep> : IStepHandler where TStep : Step
{
    public StepOutcome Run(Game game, ResolveEffectTask task, Step step) => Run(game, task, (TStep)step);

    protected abstract StepOutcome Run(Game game, ResolveEffectTask task, TStep step);
}
```

Create `src/CromoBound.Engine/Effects/Steps/StepRegistry.cs`:
```csharp
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>The step types the engine runs, each with its handler. A new step is one handler plus one line here.</summary>
internal static class StepRegistry
{
    private static readonly Dictionary<Type, IStepHandler> Handlers = new()
    {
        [typeof(DrawStep)] = new DrawHandler(),
        [typeof(BurnStep)] = new BurnHandler(),
        [typeof(DealStep)] = new DealHandler(),
        [typeof(KillStep)] = new KillHandler(),
    };

    public static bool Supports(Type stepType) => Handlers.ContainsKey(stepType);

    public static IStepHandler For(Step step) => Handlers[step.GetType()];
}
```

Create `src/CromoBound.Engine/Effects/Steps/CardStepHandlers.cs`:
```csharp
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

internal sealed class DrawHandler : StepHandler<DrawStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, DrawStep step)
    {
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (amount <= 0 || players.Count == 0) return StepOutcome.DidNothing;
        foreach (var player in players) game.Draw(player, amount);
        task.Result = new EffectVar([], players, amount, true);
        return StepOutcome.Done;
    }
}

internal sealed class BurnHandler : StepHandler<BurnStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, BurnStep step)
    {
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (amount <= 0 || players.Count == 0) return StepOutcome.DidNothing;
        foreach (var player in players) game.Burn(player, amount);
        task.Result = new EffectVar([], players, amount, true);
        return StepOutcome.Done;
    }
}
```

Create `src/CromoBound.Engine/Effects/Steps/PermanentStepHandlers.cs`:
```csharp
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

internal sealed class DealHandler : StepHandler<DealStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, DealStep step)
    {
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        List<ObjectId> units =
        [
            .. ObjectResolver.Resolve(game, task.Context, step.Target)
                .Where(id => game.State[id].Place.IsLocation && game.IsUnit(game.State[id])),
        ];
        if (amount <= 0 || units.Count == 0) return StepOutcome.DidNothing;
        foreach (var unit in units) game.DealDamage(unit, amount);
        task.Result = new EffectVar(units, [], amount, true);
        return StepOutcome.Done;
    }
}

internal sealed class KillHandler : StepHandler<KillStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, KillStep step)
    {
        List<ObjectId> targets = [.. ObjectResolver.Resolve(game, task.Context, step.Target).Where(id => game.State[id].Place.IsLocation)];
        if (targets.Count == 0) return StepOutcome.DidNothing;
        foreach (var target in targets) game.Kill(target);
        task.Result = new EffectVar(targets, [], null, true);
        return StepOutcome.Done;
    }
}
```

Create `src/CromoBound.Engine/Effects/ResolveEffectTask.cs`:
```csharp
using CromoBound.Engine.Effects.Steps;
using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>Runs an ability's steps in order with a step counter (spec §4.3). A step that asks pauses the task; after the answer
/// (or after a manual action rebuilt the decision) the same step runs again. <paramref name="onDone"/> runs once, after the last step.</summary>
internal sealed class ResolveEffectTask(EffectContext context, IReadOnlyList<Step> steps, Action<Game> onDone) : GameTask
{
    public EffectContext Context { get; } = context;
    public IReadOnlyList<Step> Steps { get; } = steps;
    public int Index { get; set; }

    /// <summary>The current step's result, saved under its "store" name when the step finishes.</summary>
    public EffectVar? Result { get; set; }

    public override bool Run(Game game)
    {
        while (Index < Steps.Count)
        {
            if (game.Outcome is not null) return true;
            var step = Steps[Index];
            var outcome = StepRegistry.For(step).Run(game, this, step);
            if (outcome == StepOutcome.Asked) return false;
            if (step.Store is { } name) Context.Vars[name] = (Result ?? EffectVar.Empty) with { Happened = outcome == StepOutcome.Done };
            Result = null;
            Index++;
        }
        if (game.Outcome is null) onDone(game);
        return true;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including Plan B's `TurnTests` that exercise `Draw` and Burn Out).

- [ ] **Step 6: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests/StepTests.cs
git commit -m "feat(engine): run effect steps for draw, burn, deal and kill"
```

---

### Task 3: What a card does, and what the engine can run

**Files:**
- Create: `src/CromoBound.Engine/Effects/EffectsSupport.cs`, `src/CromoBound.Engine/Effects/CardEffects.cs`
- Modify: `src/CromoBound.Engine/Effects/Resolvers/ValueResolver.cs` (summary now links `EffectsSupport`), `src/CromoBound.Engine/Rules/CardKeywords.cs`, `src/CromoBound.Engine/Rules/Game.cs`, `src/CromoBound.Engine/Views/PlayerView.cs`, `src/CromoBound.Engine/Views/ViewBuilder.cs`
- Modify: `tests/CromoBound.Engine.Tests/EngineTestDb.cs`, `tests/CromoBound.Engine.Tests/TestGame.cs`
- Create: `tests/CromoBound.Engine.Tests/RepoPaths.cs`
- Test: `tests/CromoBound.Engine.Tests/CardEffectsTests.cs`, `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`

**Interfaces:**
- Consumes: Task 1 (`TargetSlots.IsTarget`), Task 2 (`StepRegistry.Supports`); Phase 1's `CardDatabase.Effects`, `EffectsFile`, `RichText`; Plan B's `CardKeywords.Own`, `Game.Has`.
- Produces:
  - `internal static class EffectsSupport { IReadOnlyList<string> Problems(EffectsFile) }`.
  - `internal sealed record CardEffectInfo(MappingStatus Status, IReadOnlyList<Ability> Abilities, IReadOnlySet<DisplayKeyword> Keywords, IReadOnlyList<int> ManualLines, IReadOnlyList<string> Unsupported)`.
  - `internal sealed class CardEffects(CardDatabase db) { CardEffectInfo For(string cardId) }`.
  - `Game.Effects : CardEffects` (internal); `Game.Has` reads keywords from `CardEffects`.
  - `CardKeywords.IsKeywordLine(string line) : bool`.
  - `CardView` gains `MappingStatus Effects` and `IReadOnlyList<int> ManualLines` (last two parameters).
  - Test helpers: `EngineTestDb.Create(params (string CardId, string Json)[] effects)`, `EngineTestDb.WithRealCards(params string[] cardIds)`, the `multi-spell` test card, `TestGame(ulong seed = 1, CardDatabase? db = null)`, `RepoPaths.Data`.

- [ ] **Step 1: Add the test helpers**

Create `tests/CromoBound.Engine.Tests/RepoPaths.cs`:
```csharp
namespace CromoBound.Engine.Tests;

/// <summary>The repository root, found by walking up to CromoBound.slnx.</summary>
public static class RepoPaths
{
    public static string Root { get; } = FindRoot();
    public static string Data => Path.Combine(Root, "data");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "CromoBound.slnx")))
                return dir.FullName;
        throw new InvalidOperationException($"CromoBound.slnx not found above {AppContext.BaseDirectory}.");
    }
}
```

In `tests/CromoBound.Engine.Tests/EngineTestDb.cs`:
- Add `using CromoBound.Models.Json;` to the top.
- Add this entry to the end of the `Cards` list (before the closing `];`):
```csharp
        Simple("multi-spell", CardType.Spell, [Domain.Fury]) with
        {
            Cost = new CardCost { Energy = 1 },
            Text = new CardText { Rich = "<p>[Reaction] (Play any time.)<br />Deal 3 to a unit.<br />Draw 1.</p>" },
        },
```
- Replace the `Create()` method with:
```csharp
    /// <summary>The test pool, with effects files given as (card id, JSON) pairs.</summary>
    public static CardDatabase Create(params (string CardId, string Json)[] effects) => new()
    {
        Cards = Cards.ToDictionary(c => c.Id, StringComparer.Ordinal),
        Printings = Cards.Where(c => c.Supertype != Supertype.Token)
            .Select(c => new Printing { Id = $"p-{c.Id}", CardId = c.Id, Set = "TST" })
            .ToDictionary(p => p.Id, StringComparer.Ordinal),
        Sets = new Dictionary<string, CardSet>(),
        Effects = effects.ToDictionary(
            e => e.CardId,
            e => new LoadedEffects($"{e.CardId}.json", CromoJson.Deserialize<EffectsFile>(e.Json)),
            StringComparer.Ordinal),
    };

    /// <summary>The test pool plus the named real cards from data/ (their cards.json entries and effects files). Printing ids are "p-" + card id.</summary>
    public static CardDatabase WithRealCards(params string[] cardIds)
    {
        var real = RealData.Value;
        var test = Create();
        var cards = new Dictionary<string, Card>(test.Cards, StringComparer.Ordinal);
        var printings = new Dictionary<string, Printing>(test.Printings, StringComparer.Ordinal);
        var effects = new Dictionary<string, LoadedEffects>(StringComparer.Ordinal);
        foreach (var id in cardIds)
        {
            if (!cards.TryAdd(id, real.Cards[id])) throw new InvalidOperationException($"'{id}' is already a test card.");
            printings[$"p-{id}"] = new Printing { Id = $"p-{id}", CardId = id, Set = "TST" };
            if (real.Effects.TryGetValue(id, out var loaded)) effects[id] = loaded;
        }
        return new CardDatabase { Cards = cards, Printings = printings, Sets = test.Sets, Effects = effects };
    }

    private static readonly Lazy<CardDatabase> RealData = new(() => CardRepository.Load(RepoPaths.Data));
```

In `tests/CromoBound.Engine.Tests/TestGame.cs`, replace the constructor with:
```csharp
    public TestGame(ulong seed = 1, CardDatabase? db = null)
    {
        Db = db ?? EngineTestDb.Create();
        State = new GameState(2, new SeededRandom(seed));
        AddBattlefield("bf-a", P1);
        AddBattlefield("bf-b", P2);
    }
```

- [ ] **Step 2: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/CardEffectsTests.cs`:
```csharp
using CromoBound.Engine.Effects;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class CardEffectsTests
{
    private const string DealOnLineTwo = """
        { "cardId": "multi-spell", "status": "Partial", "keywords": [ { "keyword": "Reaction" } ],
          "abilities": [ { "kind": "Spell", "line": 2,
            "steps": [ { "action": "Deal", "amount": 3, "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    private const string FullTank = """{ "cardId": "unit-2", "status": "Full", "keywords": [ { "keyword": "Tank" } ] }""";

    [Fact]
    public void A_card_without_a_file_is_unmapped_with_text_keywords_and_every_line_manual()
    {
        var info = new CardEffects(EngineTestDb.Create()).For("tank-2");

        Assert.Equal(MappingStatus.Unmapped, info.Status);
        Assert.Contains(DisplayKeyword.Tank, info.Keywords);
        Assert.Equal(new[] { 1, 2 }, info.ManualLines);
        Assert.Empty(info.Abilities);
        Assert.Empty(info.Unsupported);
    }

    [Fact]
    public void A_full_file_brings_its_abilities_and_keywords_and_no_manual_lines()
    {
        var info = new CardEffects(EngineTestDb.Create(("unit-2", FullTank))).For("unit-2");

        Assert.Equal(MappingStatus.Full, info.Status);
        Assert.Equal(new[] { DisplayKeyword.Tank }, info.Keywords);
        Assert.Empty(info.ManualLines);
    }

    [Fact]
    public void A_partial_file_leaves_its_unmapped_lines_manual_but_not_keyword_lines()
    {
        var info = new CardEffects(EngineTestDb.Create(("multi-spell", DealOnLineTwo))).For("multi-spell");

        Assert.Equal(MappingStatus.Partial, info.Status);
        Assert.Equal(new[] { 3 }, info.ManualLines);
        Assert.Single(info.Abilities);
        Assert.Contains(DisplayKeyword.Reaction, info.Keywords);
    }

    [Fact]
    public void A_file_the_engine_cant_run_yet_falls_back_to_unmapped_and_says_why()
    {
        var db = EngineTestDb.Create(("tank-2", """
            { "cardId": "tank-2", "status": "Full", "keywords": [ { "keyword": "Shield", "value": 1 } ],
              "abilities": [ { "kind": "Triggered", "trigger": { "event": "Hold" }, "steps": [ { "action": "Draw" } ] } ] }
            """));

        var info = new CardEffects(db).For("tank-2");

        Assert.Equal(MappingStatus.Unmapped, info.Status);
        Assert.Contains(DisplayKeyword.Tank, info.Keywords);
        Assert.Equal(new[] { "keyword Shield", "abilities[0]: Triggered ability" }, info.Unsupported);
    }

    [Theory]
    [InlineData("""{ "action": "Channel", "count": 1 }""", "abilities[0].steps[0]: step Channel")]
    [InlineData("""{ "action": "Draw", "amount": { "var": "x" } }""", "abilities[0].steps[0]: value")]
    [InlineData("""{ "action": "Kill", "target": { "select": "Unit", "all": true } }""", "abilities[0].steps[0]: target")]
    [InlineData("""{ "action": "Kill", "target": { "select": "Unit", "count": 1, "filter": { "tags": ["Mech"] } } }""", "abilities[0].steps[0]: target")]
    [InlineData("""{ "action": "Draw", "player": { "var": "victim" } }""", "abilities[0].steps[0]: player")]
    [InlineData("""{ "action": "Deal", "amount": 2, "split": true, "target": { "select": "Unit", "count": 1 } }""", "abilities[0].steps[0]: split, bonus or source")]
    public void Unsupported_steps_targets_values_and_players_are_named(string step, string problem)
    {
        var file = CromoJson.Deserialize<EffectsFile>(
            $$"""{ "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "steps": [ {{step}} ] } ] }""");

        Assert.Equal(new[] { problem }, EffectsSupport.Problems(file));
    }

    [Fact]
    public void Mapped_keywords_replace_the_text_ones_in_play()
    {
        var game = new TestGame(db: EngineTestDb.Create(("unit-2", FullTank)));
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Assert.True(engine.Has(game.State[unit], DisplayKeyword.Tank));
    }
}
```

Create `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`:
```csharp
using CromoBound.Data;
using CromoBound.Engine.Effects;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Tests;

/// <summary>Which real cards (data/effects) the engine runs today. Plans E and F move cards from the second list to the first.</summary>
public class EffectsDataTests
{
    private static readonly CardEffects Real = new(CardRepository.Load(RepoPaths.Data));

    [Theory]
    [InlineData("progress-day")]
    [InlineData("falling-star")]
    [InlineData("vengeance")]
    [InlineData("vanguard-sergeant")]
    [InlineData("horns-of-the-dragon")]
    [InlineData("token-sprite")]
    public void Cards_the_engine_runs_today_are_full(string cardId) => Assert.Equal(MappingStatus.Full, Real.For(cardId).Status);

    [Theory]
    [InlineData("kharox", "Triggered ability")]
    [InlineData("garbage-grabber", "Activated ability")]
    [InlineData("noxus-hopeful", "Passive ability")]
    [InlineData("daring-poro", "keyword Assault")]
    [InlineData("soaring-scout", "keyword Deathknell")]
    public void Cards_needing_later_plans_play_by_hand_for_now(string cardId, string missing)
    {
        var info = Real.For(cardId);

        Assert.Equal(MappingStatus.Unmapped, info.Status);
        Assert.Contains(info.Unsupported, p => p.Contains(missing, StringComparison.Ordinal));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~CardEffectsTests|FullyQualifiedName~EffectsDataTests"`
Expected: FAIL (compilation errors: `CardEffects`, `EffectsSupport` not found).

- [ ] **Step 4: Implement the support check**

Create `src/CromoBound.Engine/Effects/EffectsSupport.cs`:
```csharp
using CromoBound.Engine.Effects.Steps;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>What this engine can run (spec §7). A card whose file uses anything else plays by hand, like an Unmapped card.</summary>
internal static class EffectsSupport
{
    /// <summary>Keywords the 2a rules core already enforces. The others arrive with Plans E and F.</summary>
    private static readonly HashSet<MechanicalKeyword> Keywords =
    [
        MechanicalKeyword.Accelerate, MechanicalKeyword.Action, MechanicalKeyword.Reaction, MechanicalKeyword.Hidden,
        MechanicalKeyword.Ganking, MechanicalKeyword.Tank, MechanicalKeyword.Backline, MechanicalKeyword.Temporary,
        MechanicalKeyword.Unique,
    ];

    /// <summary>One line per construct the engine can't run; empty when it runs the whole file.</summary>
    public static IReadOnlyList<string> Problems(EffectsFile file)
    {
        var problems = new List<string>();
        if (file.Overrides is not null) problems.Add("overrides");
        if (file.AdditionalCosts.Count > 0) problems.Add("additionalCosts");
        if (file.AsYouPlay.Count > 0) problems.Add("asYouPlay");
        foreach (var entry in file.Keywords)
            if (!Keywords.Contains(entry.Keyword)) problems.Add($"keyword {entry.Keyword}");
        for (var i = 0; i < file.Abilities.Count; i++)
        {
            var at = $"abilities[{i}]";
            if (file.Abilities[i] is not SpellAbility spell)
            {
                problems.Add($"{at}: {file.Abilities[i].GetType().Name.Replace("Ability", "")} ability");
                continue;
            }
            if (spell.Condition is not null || spell.ActiveIn is not null || spell.Script is not null)
                problems.Add($"{at}: condition, activeIn or script");
            for (var j = 0; j < spell.Steps.Count; j++) CheckStep(spell.Steps[j], $"{at}.steps[{j}]", problems);
        }
        return problems;
    }

    private static void CheckStep(Step step, string at, List<string> problems)
    {
        if (!StepRegistry.Supports(step.GetType()))
        {
            problems.Add($"{at}: step {step.GetType().Name.Replace("Step", "")}");
            return;
        }
        if (step.Script is not null || step.Chooser is not null) problems.Add($"{at}: script or chooser");
        if (step.Player is { Kind: null }) problems.Add($"{at}: player");
        switch (step)
        {
            case DrawStep draw:
                CheckValue(draw.Amount, at, problems);
                break;
            case BurnStep burn:
                CheckValue(burn.Amount, at, problems);
                break;
            case DealStep deal:
                CheckValue(deal.Amount, at, problems);
                if (deal.Split is not null || deal.Bonus is not null || deal.Source is not null) problems.Add($"{at}: split, bonus or source");
                break;
        }
        if (step is TargetStep target && !IsSupportedTarget(target.Target)) problems.Add($"{at}: target");
    }

    private static void CheckValue(Value value, string at, List<string> problems)
    {
        if (value.Literal is null) problems.Add($"{at}: value");
    }

    private static bool IsSupportedTarget(ObjectRef reference) =>
        reference.Ref == RefKind.Self || (TargetSlots.IsTarget(reference) && IsSupportedFilter(reference.Filter));

    /// <summary>Filters may use relation (Friendly or Enemy), type, token and other; nothing else yet.</summary>
    private static bool IsSupportedFilter(Filter? filter) => filter is null
        || ((filter.Relation is null or Relation.Friendly or Relation.Enemy)
            && filter.Controller is null && filter.Owner is null && filter.Location is null && filter.Zone is null
            && filter.Supertype is null && filter.Tags.Count == 0 && filter.Domains.Count == 0 && filter.Name is null
            && filter.Might is null && filter.EnergyCost is null && filter.Status.Count == 0 && filter.Mighty is null
            && filter.Keyword is null && filter.Not is null);
}
```

In `src/CromoBound.Engine/Effects/Resolvers/ValueResolver.cs`, make the summary link the support check, now that it exists:
```csharp
/// <summary>Turns values into numbers. Plan D runs literals; <see cref="EffectsSupport"/> keeps other forms out of the cards it runs.</summary>
```

- [ ] **Step 5: Implement CardEffects and keyword lines**

In `src/CromoBound.Engine/Rules/CardKeywords.cs`, add this method to `CardKeywords` after `Own`:
```csharp
    /// <summary>True when the line is only the card's own keywords and their reminder text, e.g. "[Tank] (I must be assigned combat damage first.)".</summary>
    public static bool IsKeywordLine(string line)
    {
        var leading = LeadingKeywords().Match(line);
        return leading.Success && RichText.StripReminders(line[leading.Length..]).Length == 0;
    }
```

Create `src/CromoBound.Engine/Effects/CardEffects.cs`:
```csharp
using CromoBound.Data;
using CromoBound.Engine.Rules;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>What a card does as the engine runs it (spec §4.1). <see cref="ManualLines"/> are the 1-based text lines players
/// resolve by hand; <see cref="Unsupported"/> says why a mapped file is played as Unmapped.</summary>
internal sealed record CardEffectInfo(
    MappingStatus Status,
    IReadOnlyList<Ability> Abilities,
    IReadOnlySet<DisplayKeyword> Keywords,
    IReadOnlyList<int> ManualLines,
    IReadOnlyList<string> Unsupported);

/// <summary>Effects per card id, cached. A file the engine can't fully run yet is treated as Unmapped, so the card plays by hand as in 2a.</summary>
internal sealed class CardEffects(CardDatabase db)
{
    private readonly Dictionary<string, CardEffectInfo> _cache = new(StringComparer.Ordinal);

    public CardEffectInfo For(string cardId)
    {
        if (!_cache.TryGetValue(cardId, out var info)) _cache[cardId] = info = Build(db.Cards[cardId]);
        return info;
    }

    private CardEffectInfo Build(Card card)
    {
        var lines = RichText.Lines(card.Text.Rich);
        List<int> all = [.. Enumerable.Range(1, lines.Count)];
        if (!db.Effects.TryGetValue(card.Id, out var loaded) || loaded.File.Status == MappingStatus.Unmapped)
            return Unmapped(card, all, []);
        var file = loaded.File;
        var unsupported = EffectsSupport.Problems(file);
        if (unsupported.Count > 0) return Unmapped(card, all, unsupported);

        IReadOnlySet<DisplayKeyword> keywords = file.Keywords.Select(k => Enum.Parse<DisplayKeyword>(k.Keyword.ToString())).ToHashSet();
        if (file.Status == MappingStatus.Full) return new(MappingStatus.Full, file.Abilities, keywords, [], []);
        var covered = file.Abilities.Where(a => a.Line is not null).SelectMany(a => a.Line!.Lines).ToHashSet();
        List<int> manual = [.. all.Where(n => !covered.Contains(n) && !CardKeywords.IsKeywordLine(lines[n - 1]))];
        return new(MappingStatus.Partial, file.Abilities, keywords, manual, []);
    }

    private static CardEffectInfo Unmapped(Card card, List<int> lines, IReadOnlyList<string> unsupported) =>
        new(MappingStatus.Unmapped, [], CardKeywords.Own(card), lines, unsupported);
}
```

- [ ] **Step 6: Route Game's keywords through CardEffects**

In `src/CromoBound.Engine/Rules/Game.cs`:
- Add `using CromoBound.Engine.Effects;`.
- Delete the field `private readonly Dictionary<string, IReadOnlySet<DisplayKeyword>> _ownKeywords = [];`.
- In the constructor, add `Effects = new CardEffects(db);` after `Db = db;`.
- Add after the `Db` property:
```csharp
    /// <summary>What each card does as this engine runs it (spec §4.1).</summary>
    internal CardEffects Effects { get; }
```
- Replace the `Has` method (and its summary) with:
```csharp
    /// <summary>Whether the card itself has the keyword: from its effects file when the engine runs it, else from the starts of its text lines (spec §7.10).</summary>
    internal bool Has(CardInstance instance, DisplayKeyword keyword) => Effects.For(instance.CardId).Keywords.Contains(keyword);
```

- [ ] **Step 7: Show each card's effects status in views**

In `src/CromoBound.Engine/Views/PlayerView.cs`, replace the `CardView` record and its summary with:
```csharp
/// <summary>A card the viewer is allowed to see. Might is set for units only. <see cref="Effects"/> says whether the engine runs the card;
/// <see cref="ManualLines"/> are the text lines players resolve by hand (1-based).</summary>
public sealed record CardView(
    ObjectId Id, string CardId, string? PrintingId, PlayerId Owner, PlayerId Controller,
    bool Exhausted, bool Stunned, bool Buffed, bool Empowered, int Damage, int? Might, CombatRole? Role,
    MappingStatus Effects, IReadOnlyList<int> ManualLines);
```

In `src/CromoBound.Engine/Views/ViewBuilder.cs`, replace the `Card` method with:
```csharp
    private static CardView Card(Game game, CardInstance card)
    {
        var effects = game.Effects.For(card.CardId);
        return new(
            card.Id, card.CardId, card.PrintingId, card.Owner, card.Controller,
            card.Exhausted, card.Stunned, card.Buffed, card.Empowered, card.Damage,
            game.Db.Cards[card.CardId].Type == CardType.Unit ? game.MightOf(card.Id) : null, card.Role,
            effects.Status, effects.ManualLines);
    }
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests; every 2a test is unchanged because the test pool has no effects files).

- [ ] **Step 9: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): load card effects and fall back to manual play"
```

---

### Task 4: Choosing targets while playing

**Files:**
- Modify: `src/CromoBound.Engine/Results.cs`, `Actions/PlayerAction.cs`, `Actions/ActionShape.cs`, `Decisions/Decisions.cs`, `Events/GameEvents.cs`, `State/ChainItem.cs`, `Rules/Game.Play.cs`, `Rules/Game.Priority.cs`
- Create: `src/CromoBound.Engine/Rules/Game.Targets.cs`
- Modify: `tests/CromoBound.Engine.Tests/Bot.cs`
- Test: `tests/CromoBound.Engine.Tests/TargetTests.cs`

**Interfaces:**
- Consumes: Tasks 1 and 3 (`EffectContext`, `TargetSlots`, `ObjectResolver.Candidates`, `Game.Effects`); Plan B's `PlayCardTask`, `UndoPlay`, `Ask`, `Reject`, `Emit`, `PlayableCards`.
- Produces:
  - `RejectionCode.InvalidTarget` (added at the end of the enum).
  - `public sealed record ChooseTargets : PlayerAction { IReadOnlyList<ObjectId> Targets }` (JSON "ChooseTargets").
  - `public sealed record ChooseTargetsDecision(PlayerId Player, ObjectId Card, int Slot, IReadOnlyList<ObjectId> Options, int Min, int Max) : PendingDecision` (JSON "ChooseTargets").
  - `public sealed record ChoiceMade(PlayerId Player, string Kind, IReadOnlyList<ObjectId> Chosen) : GameEvent` (JSON "ChoiceMade").
  - `ChainItem.Effect : EffectContext?` (internal).
  - `PlayStep.Targets` between `Choices` and `Cost`.
  - `Game.SpellSteps(string cardId) : IReadOnlyList<Step>` (internal), `HasTargetsFor`, `AskTargets` (private).

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/TargetTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class TargetTests
{
    internal const string KillAUnit = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1,
          "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    private const string KillAnEnemy = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1,
          "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1, "filter": { "relation": "Enemy" } } } ] } ] }
        """;

    private const string DealTwice = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1, "steps": [
          { "action": "Deal", "amount": 1, "target": { "select": "Unit", "count": 1 } },
          { "action": "Deal", "amount": 1, "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    /// <summary>P1 holds "spell" (with the given effects) and one fury rune; P1 has unit-2 and P2 has unit-3 in Base unless arranged otherwise.</summary>
    private static (TestGame Game, Game Engine) Setup(string effects, bool ownUnit = true, bool enemyUnit = true)
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", effects)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        if (ownUnit) game.Put("unit-2", Place.Base(P1));
        if (enemyUnit) game.Put("unit-3", Place.Base(P2));
        return (game, game.Start());
    }

    private static ObjectId Spell(TestGame game) => game.First(Place.Hand(P1), "spell");

    [Fact]
    public void Targets_are_chosen_after_play_choices_and_before_the_cost()
    {
        var (game, engine) = Setup(KillAUnit);
        var enemy = game.First(Place.Base(P2), "unit-3");

        engine.Accept(P1, new PlayCard(Spell(game)));
        var choose = engine.Decision<ChooseTargetsDecision>();
        Assert.Equal((P1, 0, 1, 1), (choose.Player, choose.Slot, choose.Min, choose.Max));
        Assert.Equal(2, choose.Options.Count);
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });

        Assert.IsType<PayCostDecision>(engine.Pending);
        Assert.Equal(new[] { enemy }, Assert.Single(game.State.Chain).Effect!.Targets[0]);
    }

    [Fact]
    public void Invalid_target_choices_are_rejected_without_changes()
    {
        var (game, engine) = Setup(KillAUnit);
        var mine = game.First(Place.Base(P1), "unit-2");
        var enemy = game.First(Place.Base(P2), "unit-3");
        var inHand = game.First(Place.Hand(P1), "unit-2");
        engine.Accept(P1, new PlayCard(Spell(game)));

        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseTargets { Targets = [inHand] }).Rejection!.Code);
        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseTargets { Targets = [mine, enemy] }).Rejection!.Code);
        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseTargets { Targets = [enemy, enemy] }).Rejection!.Code);
        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseTargets()).Rejection!.Code);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new PayCost()).Rejection!.Code);
        Assert.Empty(Assert.Single(game.State.Chain).Effect!.Targets);
        Assert.IsType<ChooseTargetsDecision>(engine.Pending);
    }

    [Fact]
    public void A_single_legal_target_is_chosen_automatically_and_announced()
    {
        var (game, engine) = Setup(KillAnEnemy);
        var enemy = game.First(Place.Base(P2), "unit-3");

        var result = engine.Accept(P1, new PlayCard(Spell(game)));

        Assert.Contains(result.Events, e => e is ChoiceMade { Kind: "Targets" } made && made.Chosen.SequenceEqual(new[] { enemy }));
        Assert.IsType<PayCostDecision>(engine.Pending);
    }

    [Fact]
    public void A_spell_without_enough_legal_targets_is_not_playable()
    {
        var (game, engine) = Setup(KillAnEnemy, enemyUnit: false);

        Assert.DoesNotContain(Spell(game), engine.Decision<PriorityDecision>().Playable);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new PlayCard(Spell(game))).Rejection!.Code);
    }

    [Fact]
    public void Each_target_slot_is_asked_in_order_and_may_pick_the_same_unit()
    {
        var (game, engine) = Setup(DealTwice);
        var enemy = game.First(Place.Base(P2), "unit-3");
        engine.Accept(P1, new PlayCard(Spell(game)));

        Assert.Equal(0, engine.Decision<ChooseTargetsDecision>().Slot);
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });
        Assert.Equal(1, engine.Decision<ChooseTargetsDecision>().Slot);
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });

        var targets = Assert.Single(game.State.Chain).Effect!.Targets;
        Assert.Equal(new[] { enemy }, targets[0]);
        Assert.Equal(new[] { enemy }, targets[1]);
    }

    [Fact]
    public void Cancelling_at_the_target_choice_returns_the_card()
    {
        var (game, engine) = Setup(KillAUnit);
        engine.Accept(P1, new PlayCard(Spell(game)));

        engine.Accept(P1, new CancelPlay());

        Assert.Contains(game.State.At(Place.Hand(P1)), id => game.State[id].CardId == "spell");
        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void A_manual_action_during_the_target_choice_asks_again_with_fresh_options()
    {
        var (game, engine) = Setup(DealTwice);
        var mine = game.First(Place.Base(P1), "unit-2");
        var enemy = game.First(Place.Base(P2), "unit-3");
        engine.Accept(P1, new PlayCard(Spell(game)));
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });
        Assert.Equal(2, engine.Decision<ChooseTargetsDecision>().Options.Count);

        var result = engine.SubmitManual(P2, new ManualMoveCard(mine, Place.Trash(P1)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.IsType<PayCostDecision>(engine.Pending);
        var targets = Assert.Single(game.State.Chain).Effect!.Targets;
        Assert.Equal(new[] { enemy }, targets[0]);
        Assert.Equal(new[] { enemy }, targets[1]);
    }

    [Fact]
    public void Unmapped_spells_ask_no_targets()
    {
        var game = new TestGame();
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));

        Assert.IsType<PayCostDecision>(engine.Pending);
    }

    [Fact]
    public void Target_choices_round_trip_through_json_and_a_null_list_is_rejected()
    {
        PlayerAction action = new ChooseTargets { Targets = [new ObjectId(4)] };
        var json = CromoJson.Serialize(action);
        PendingDecision decision = new ChooseTargetsDecision(P1, new ObjectId(2), 0, [new ObjectId(4)], 1, 1);
        var (game, engine) = Setup(KillAUnit);
        engine.Accept(P1, new PlayCard(Spell(game)));

        Assert.Contains("\"ChooseTargets\"", json);
        Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<PlayerAction>(json)));
        Assert.Contains("\"ChooseTargets\"", CromoJson.Serialize(decision));
        var malformed = CromoJson.Deserialize<PlayerAction>("""{ "type": "ChooseTargets", "targets": null }""");
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, malformed).Rejection!.Code);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~TargetTests"`
Expected: FAIL (compilation errors: `ChooseTargets`, `ChooseTargetsDecision`, `ChoiceMade`, `RejectionCode.InvalidTarget` not found).

- [ ] **Step 3: Add the public types**

In `src/CromoBound.Engine/Results.cs`, add `InvalidTarget` as the last member of `RejectionCode` (after `MatchOver`).

In `src/CromoBound.Engine/Actions/PlayerAction.cs`:
- Add `[JsonDerivedType(typeof(ChooseTargets), "ChooseTargets")]` directly above `public abstract record PlayerAction;`.
- Add at the end of the file:
```csharp
/// <summary>The targets chosen for the target slot being asked (spec §6.1).</summary>
public sealed record ChooseTargets : PlayerAction
{
    public IReadOnlyList<ObjectId> Targets { get; init; } = [];
}
```

In `src/CromoBound.Engine/Actions/ActionShape.cs`, add this arm before `_ => null,`:
```csharp
        ChooseTargets choose => choose.Targets is null ? Missing(nameof(choose.Targets)) : null,
```

In `src/CromoBound.Engine/Decisions/Decisions.cs`:
- Add `[JsonDerivedType(typeof(ChooseTargetsDecision), "ChooseTargets")]` to the attribute list on `PendingDecision`.
- Add at the end of the file:
```csharp
/// <summary>Choose between <see cref="Min"/> and <see cref="Max"/> of <see cref="Options"/> as the targets of slot <see cref="Slot"/>
/// of the card being played (spec §5.1).</summary>
public sealed record ChooseTargetsDecision(PlayerId Player, ObjectId Card, int Slot, IReadOnlyList<ObjectId> Options, int Min, int Max)
    : PendingDecision([Player]);
```

In `src/CromoBound.Engine/Events/GameEvents.cs`:
- Add `[JsonDerivedType(typeof(ChoiceMade), "ChoiceMade")]` to the attribute list on `GameEvent`.
- Add at the end of the file:
```csharp
/// <summary>The engine made a forced choice for the player (exactly one legal answer, nothing to decline; spec §6.2).</summary>
public sealed record ChoiceMade(PlayerId Player, string Kind, IReadOnlyList<ObjectId> Chosen) : GameEvent;
```

In `src/CromoBound.Engine/State/ChainItem.cs`, add `using CromoBound.Engine.Effects;` and this property at the end of `ChainItem`:
```csharp
    /// <summary>The resolution context of a card the engine runs: its targets, chosen while playing (spec §4.2).</summary>
    internal EffectContext? Effect { get; set; }
```

- [ ] **Step 4: Add the target step to playing**

Create `src/CromoBound.Engine/Rules/Game.Targets.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>The steps of the card's spell abilities when the engine runs it (Full or Partial), in printed order; empty otherwise.</summary>
    internal IReadOnlyList<Step> SpellSteps(string cardId) =>
        [.. Effects.For(cardId).Abilities.OfType<SpellAbility>().SelectMany(a => a.Steps)];

    /// <summary>A spell the engine runs can be played only if every required target slot has enough candidates (rule 355).</summary>
    private bool HasTargetsFor(PlayerId player, CardInstance card)
    {
        var slots = TargetSlots.Of(SpellSteps(card.CardId));
        if (slots.Count == 0) return true;
        var context = new EffectContext { Controller = player, Source = card.Id, SourceCardId = card.CardId, Slots = slots };
        return slots.All(slot => ObjectResolver.Candidates(this, context, slot).Count >= (slot.Count ?? 0));
    }

    /// <summary>Spec §5.1: one decision per target slot, in JSON order. When the candidates are exactly as many as required the
    /// choice is forced: it is applied without asking and announced with <see cref="ChoiceMade"/>. Returns true when it asked.</summary>
    private bool AskTargets(PlayCardTask task)
    {
        var item = task.Item!;
        var card = State[item.Card!.Value];
        item.Effect ??= new EffectContext
        {
            Controller = task.Player,
            Source = card.Id,
            SourceCardId = card.CardId,
            Slots = TargetSlots.Of(SpellSteps(card.CardId)),
        };
        var context = item.Effect;
        while (context.Targets.Count < context.Slots.Count)
        {
            var slot = context.Slots[context.Targets.Count];
            var options = ObjectResolver.Candidates(this, context, slot);
            var min = slot.Count ?? 0;
            var max = Math.Min(slot.Count ?? slot.UpTo ?? 0, options.Count);
            if (options.Count < min)
            {
                UndoPlay(task);
                return false;
            }
            if (options.Count == min)
            {
                context.Targets.Add(options);
                Emit(new ChoiceMade(task.Player, "Targets", options));
                continue;
            }
            Ask(new ChooseTargetsDecision(task.Player, card.Id, context.Targets.Count, options, min, max), (_, action) =>
            {
                if (action is CancelPlay)
                {
                    UndoPlay(task);
                    return null;
                }
                if (action is not ChooseTargets choose) return Reject(RejectionCode.UnexpectedAction, "Choose the targets, or cancel.");
                var chosen = choose.Targets;
                if (chosen.Distinct().Count() != chosen.Count || chosen.Count < min || chosen.Count > max || !chosen.All(options.Contains))
                    return Reject(RejectionCode.InvalidTarget, $"Choose between {min} and {max} different targets among the offered ones.");
                context.Targets.Add([.. chosen]);
                return null;
            });
            return true;
        }
        return false;
    }
}
```

In `src/CromoBound.Engine/Rules/Game.Play.cs`:
- Replace the `PlayStep` enum with:
```csharp
internal enum PlayStep { ToChain, Choices, Targets, Cost, Pay, Finalize, Cancelled }
```
- In `RunPlay`, replace the `case PlayStep.Choices:` block with:
```csharp
                case PlayStep.Choices:
                    if (AskPlayChoices(task)) return false;
                    task.Step = PlayStep.Targets;
                    break;
                case PlayStep.Targets:
                    if (AskTargets(task)) return false;
                    if (task.Step == PlayStep.Targets) task.Step = PlayStep.Cost;
                    break;
```
- In `AskPlayChoices`, inside the handler, replace `task.Step = PlayStep.Cost;` with `task.Step = PlayStep.Targets;`.

In `src/CromoBound.Engine/Rules/Game.Priority.cs`, in `PlayableCards`:
- Replace `if (timing) yield return id;` with `if (timing && HasTargetsFor(player, card)) yield return id;`.
- Replace `if (CanPlayFromHidden(State[id], player)) yield return id;` with `if (CanPlayFromHidden(State[id], player) && HasTargetsFor(player, State[id])) yield return id;`.

- [ ] **Step 5: Teach the scripted player to choose targets**

In `tests/CromoBound.Engine.Tests/Bot.cs`, add this arm to the `switch` in `Choose(Game game)`, before the `_ =>` arm:
```csharp
            ChooseTargetsDecision targets => new ChooseTargets { Targets = [.. targets.Options.Take(Math.Max(targets.Min, Math.Min(1, targets.Max)))] },
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including Plan B's `PlayTests` and `HiddenTests`).

- [ ] **Step 7: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): choose spell targets while playing"
```

---

### Task 5: Resolving mapped spells automatically

**Files:**
- Modify: `src/CromoBound.Engine/Rules/Game.Chain.cs`
- Test: `tests/CromoBound.Engine.Tests/SpellResolutionTests.cs`, `tests/CromoBound.Engine.Tests/MatchEffectsTests.cs`

**Interfaces:**
- Consumes: Tasks 2-4 (`ResolveEffectTask`, `CardEffects`, `ChainItem.Effect`, `SpellSteps`); Plan B's `ResolveTop`, `FinishResolution`, `ResolveManuallyDecision`; Plan C's `Match`, `MatchTestExtensions.Snapshot`, `TestDecks`.
- Produces: the resolution switch of spec §5.1 step 3 (`ResolveTop` runs Full and Partial spells; `ResolveByHand(item, text)`; `AfterAutomatedResolution`).

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/SpellResolutionTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class SpellResolutionTests
{
    private const string DealOnLineTwo = """
        { "cardId": "multi-spell", "status": "Partial", "keywords": [ { "keyword": "Reaction" } ],
          "abilities": [ { "kind": "Spell", "line": 2,
            "steps": [ { "action": "Deal", "amount": 3, "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    /// <summary>Plays the card from P1's hand with the given targets (one list per slot), pays with the suggestion, and both players pass.</summary>
    private static SubmitResult PlayAndResolve(TestGame game, Game engine, string cardId, params ObjectId[][] targets)
    {
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), cardId)));
        foreach (var slot in targets) engine.Accept(P1, new ChooseTargets { Targets = slot });
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        return engine.Accept(P2, new Pass());
    }

    [Fact]
    public void A_full_spell_resolves_automatically()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", TargetTests.KillAUnit)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        game.Put("unit-2", Place.Base(P1));
        var enemy = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        var result = PlayAndResolve(game, engine, "spell", [enemy]);

        Assert.False(game.State.Exists(enemy));
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.Empty(game.State.Chain);
        Assert.Contains(result.Events, e => e is ChainItemResolved);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void A_partial_spell_runs_its_mapped_lines_then_asks_for_the_rest()
    {
        var game = new TestGame(db: EngineTestDb.Create(("multi-spell", DealOnLineTwo)));
        game.Put("multi-spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        game.Put("unit-2", Place.Base(P1));
        var enemy = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        PlayAndResolve(game, engine, "multi-spell", [enemy]);

        var resolve = engine.Decision<ResolveManuallyDecision>();
        Assert.Equal("<p>Draw 1.</p>", resolve.Text);
        Assert.Equal(3, game.State[enemy].Damage);
        engine.Accept(P1, new ResolveDone());
        Assert.False(game.State.Exists(enemy));
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "multi-spell");
    }

    [Fact]
    public void A_target_that_left_before_resolution_is_skipped()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", TargetTests.KillAUnit)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var mine = game.Put("unit-2", Place.Base(P1));
        var enemy = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());

        Assert.True(engine.SubmitManual(P2, new ManualMoveCard(enemy, Place.Hand(P2))).Accepted);
        engine.Accept(P2, new Pass());

        Assert.True(game.State.Exists(mine));
        Assert.Contains(game.State.At(Place.Hand(P2)), id => game.State[id].CardId == "unit-3");
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Progress_day_draws_four()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("progress-day", "mind-rune"));
        game.Put("progress-day", Place.Hand(P1));
        game.Runes(P1, "mind-rune", 7);
        var engine = game.Start();
        var hand = game.State.At(Place.Hand(P1)).Count;

        PlayAndResolve(game, engine, "progress-day");

        Assert.Equal(hand - 1 + 4, game.State.At(Place.Hand(P1)).Count);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Vengeance_kills_the_chosen_unit()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("vengeance", "order-rune"));
        game.Put("vengeance", Place.Hand(P1));
        game.Runes(P1, "order-rune", 6);
        var mine = game.Put("unit-2", Place.Base(P1));
        var enemy = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        PlayAndResolve(game, engine, "vengeance", [enemy]);

        Assert.False(game.State.Exists(enemy));
        Assert.True(game.State.Exists(mine));
    }

    [Fact]
    public void Falling_star_can_hit_the_same_unit_twice()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("falling-star"));
        game.Put("falling-star", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 4);
        game.Put("unit-2", Place.Base(P1));
        var enemy = game.Put("unit-3", Place.Base(P2));
        game.State[enemy].Modifiers.Add(new MightModifier(3, Duration.Permanent));
        var engine = game.Start();

        PlayAndResolve(game, engine, "falling-star", [enemy], [enemy]);

        Assert.False(game.State.Exists(enemy));
    }

    [Fact]
    public void Vanguard_sergeant_plays_as_a_vanilla_unit()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("vanguard-sergeant", "order-rune"));
        game.Put("vanguard-sergeant", Place.Hand(P1));
        game.Runes(P1, "order-rune", 4);
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "vanguard-sergeant")));
        engine.PayWithSuggestion(P1);

        var sergeant = game.First(Place.Base(P1), "vanguard-sergeant");
        Assert.True(game.State[sergeant].Exhausted);
        Assert.Empty(game.State.Chain);
        Assert.Equal(MappingStatus.Full, engine.Effects.For("vanguard-sergeant").Status);
    }
}
```

Create `tests/CromoBound.Engine.Tests/MatchEffectsTests.cs`:
```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Engine.Tests;

public class MatchEffectsTests
{
    private const string DealOne = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1,
          "steps": [ { "action": "Deal", "amount": 1, "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    private static PlayerId Other(PlayerId player) => new(1 - player.Index);

    /// <summary>A legal Jinx deck with three copies of the mapped "spell" in place of filler-13.</summary>
    private static Deck SpellDeck(params string[] battlefields) => TestDecks.Jinx(battlefields) with
    {
        Main = [.. Enumerable.Range(1, 12).Select(i => new DeckEntry { Printing = $"p-filler-{i}", Count = 3 }), new DeckEntry { Printing = "p-spell", Count = 3 }],
    };

    /// <summary>To play; the first player brings a spell to hand, both players get a Recruit token, and the spell is played,
    /// stopping at its target choice. Everything goes through logged actions.</summary>
    private static (Match Match, PlayerId Player) SpellAtTargetChoice()
    {
        var db = EngineTestDb.Create(("spell", DealOne));
        var setup = new MatchSetup(MatchFormat.Bo1, SpellDeck("bf-a", "bf-b", "bf-c"), SpellDeck("bf-d", "bf-e", "bf-f"), 7);
        var match = Match.Create(setup, db).Match!.ToPlay();
        var player = match.Decision<PriorityDecision>().Player;
        var state = match.Game!.State;
        if (!state.At(Place.Hand(player)).Any(id => state[id].CardId == "spell"))
            match.Accept(player, new ManualMoveCard(state.At(Place.MainDeck(player)).First(id => state[id].CardId == "spell"), Place.Hand(player)));
        match.Accept(player, new ManualCreateToken("token-recruit", Place.Base(player), player));
        match.Accept(player, new ManualCreateToken("token-recruit", Place.Base(Other(player)), Other(player)));
        match.Accept(player, new PlayCard(state.At(Place.Hand(player)).First(id => state[id].CardId == "spell")));
        return (match, player);
    }

    [Fact]
    public void Undo_after_choosing_a_target_equals_never_choosing_it()
    {
        var (undone, player) = SpellAtTargetChoice();
        var (reference, _) = SpellAtTargetChoice();
        var choose = undone.Decision<ChooseTargetsDecision>();
        undone.Accept(player, new ChooseTargets { Targets = [choose.Options[0]] });

        undone.Accept(player, new RequestUndo());
        undone.Accept(Other(player), new AnswerUndo(true));

        Assert.IsType<ChooseTargetsDecision>(undone.Pending);
        Assert.Equal(reference.Snapshot(), undone.Snapshot());
    }

    [Fact]
    public void A_saved_match_with_target_choices_loads_identically()
    {
        var (match, player) = SpellAtTargetChoice();
        match.Accept(player, new ChooseTargets { Targets = [match.Decision<ChooseTargetsDecision>().Options[1]] });

        var json = CromoJson.Serialize(match.ToRecord());
        var loaded = Match.Load(CromoJson.Deserialize<MatchRecord>(json), EngineTestDb.Create(("spell", DealOne)));

        Assert.Equal(match.Snapshot(), loaded.Snapshot());
    }

    [Fact]
    public void Views_show_the_target_choice_only_to_the_chooser_and_each_cards_effects_status()
    {
        var (match, player) = SpellAtTargetChoice();

        var mine = match.ViewFor(player);
        var theirs = match.ViewFor(Other(player));

        Assert.IsType<ChooseTargetsDecision>(mine.Decision);
        Assert.Null(theirs.Decision);
        Assert.Equal(new[] { player }, theirs.Deciding);
        Assert.Equal(MappingStatus.Full, Assert.Single(theirs.Chain).Card!.Effects);
        Assert.Empty(Assert.Single(theirs.Chain).Card!.ManualLines);
        Assert.All(theirs.Players[player.Index].Base, card => Assert.Equal(MappingStatus.Unmapped, card.Effects));
        Assert.Contains("\"ChooseTargets\"", CromoJson.Serialize(mine));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~SpellResolutionTests|FullyQualifiedName~MatchEffectsTests"`
Expected: FAIL. The project compiles; the resolution tests get a `ResolveManuallyDecision` where they expect the spell to have resolved.

- [ ] **Step 3: Implement the resolution switch**

In `src/CromoBound.Engine/Rules/Game.Chain.cs`:
- Add `using CromoBound.Engine.Effects;` and `using CromoBound.Models.Cards;` to the top.
- Replace the `ResolveTop` method (and its summary) with:
```csharp
    /// <summary>The newest finalized item resolves. A spell the engine runs (Full or Partial) resolves automatically (spec §5.1);
    /// anything else is resolved by hand by its controller (spec §8).</summary>
    private void ResolveTop()
    {
        var item = State.Chain.Last(i => i.Status == ChainItemStatus.Finalized);
        if (item.Card is { } card && Effects.For(State[card].CardId) is { Status: not MappingStatus.Unmapped } effects)
        {
            var context = item.Effect ?? new EffectContext { Controller = item.Controller, Source = card, SourceCardId = State[card].CardId };
            Push(new ResolveEffectTask(context, SpellSteps(context.SourceCardId), g => g.AfterAutomatedResolution(item, effects)));
            return;
        }
        ResolveByHand(item, null);
    }

    /// <summary>2a hand resolution of the item, or of just the lines in <paramref name="text"/> (a Partial spell's manual lines).</summary>
    private void ResolveByHand(ChainItem item, string? text)
    {
        var cardId = item.Card is { } card ? State[card].CardId : item.SourceCardId ?? "";
        text ??= item.Text ?? (item.Card is { } c ? CardOf(c).Text.Rich : "");
        ResolvingManually = true;
        Ask(new ResolveManuallyDecision(item.Controller, item.Id, cardId, text), (_, action) =>
        {
            if (action is not ResolveDone)
                return Reject(RejectionCode.UnexpectedAction, "Carry out the effect with manual actions, then submit ResolveDone.");
            FinishResolution(item);
            return null;
        });
    }

    /// <summary>After the automated part: a Partial spell's manual lines are resolved by hand; then it finishes like any spell.</summary>
    private void AfterAutomatedResolution(ChainItem item, CardEffectInfo effects)
    {
        if (!State.Chain.Contains(item)) return;
        if (effects.ManualLines.Count > 0)
        {
            ResolveByHand(item, ManualText(item.Card!.Value, effects.ManualLines));
            return;
        }
        FinishResolution(item);
    }

    private string ManualText(ObjectId card, IReadOnlyList<int> lines)
    {
        var all = RichText.Lines(CardOf(card).Text.Rich);
        return $"<p>{string.Join("<br />", lines.Select(n => all[n - 1]))}</p>";
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including Plan B's `PlayTests` for hand resolution of unmapped spells and Plan C's `ViewTests` scripted Bo3).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): resolve mapped spells automatically"
```

---

## Done criteria

- [ ] Resolvers, target slots and the effect context work as pinned by `ResolverTests` (Task 1).
- [ ] Draw, Burn, Deal and Kill run through `ResolveEffectTask`, and Burn Out applies to Burn (Task 2).
- [ ] `CardEffects` reports Full, Partial and Unmapped with keywords and manual lines; files the engine can't run yet play by hand and say why; views carry each card's effects status (Task 3).
- [ ] Spells the engine runs ask for targets while being played, auto-pick forced targets, can't be played without legal targets, and keep their targets across manual actions (Task 4).
- [ ] Full spells resolve automatically, Partial spells hand only their unmapped lines to the players, and Progress Day, Falling Star, Vengeance and Vanguard Sergeant work with the real card data; undo, loading and views work with target choices (Task 5).
- [ ] `dotnet build CromoBound.slnx --no-incremental` reports 0 warnings and 0 errors, and `dotnet test CromoBound.slnx` passes.
