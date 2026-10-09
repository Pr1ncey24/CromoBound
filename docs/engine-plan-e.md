# Engine Plan E: Triggers and Activations

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Second of three plans for Phase 2b (D: foundations and spells; E: triggers and activations; F: modifiers, keywords and integration).

**Goal:** Triggered and activated abilities of mapped cards run automatically. Triggers are noticed from engine events, go on the chain in order and resolve through the interpreter. Activated abilities are offered at priority and their costs are paid. Effects can choose a player, cards or an optional part, play a card, channel, gain XP, empower and predict. Kharox, Garbage Grabber, Shadow Temple, Soaring Scout, Mystic Poro and Voracious Gromp run with the real card data.

**Architecture:**
- Ability chain items carry their steps and context (`ChainItem.Steps`, `ChainItem.Effect`); `ResolveTop` runs them through `ResolveEffectTask`, which is now tied to its chain item and stops if the item is countered.
- Steps that ask (`ChoosePlayer`, `ChooseCard`, reflexive `Optional`, `Predict`) store the answer on the task (`Answer`) and run again, exactly like 2a tasks. `Play` waits for a 2a `PlayCardTask` it starts.
- `TriggerWatcher` reads every public event as it is emitted and records `PendingTrigger`s. `RunLoop` puts them on the chain at the same points where it would start a cleanup (`PutTriggersOnChainTask`), the turn player's first, each player ordering their own.
- `PriorityDecision` lists `ActivateOption`s. `ActivateAbility` starts an `ActivationTask`: cost-action cards are chosen first, then energy and power are paid through 2a's `AskPay`, then the source is exhausted, the cards are recycled and the ability goes on the chain.
- `CardEffects` turns the keywords Deathknell, Vision, Hunt and Empower into the abilities they stand for. Runes get no abilities: basic runes keep 2a's `UseRune`.

**Tech Stack:** .NET 10, xUnit 2.9.3. No new dependencies.

**Spec:** `docs/effects-engine.md` (sections 4.2-4.6, 5.2-5.6, 6.1-6.4, 7, 8.1 for Deathknell, Vision, Hunt and Empower, 10, 11 row E, 12).

## Global Constraints

- Plans A to D's Global Constraints still apply: `net10.0`; no NuGet packages in runtime projects; no `DateTime`, `Guid`, `System.Random` or hash-order-dependent iteration in engine code; `CromoJson` for everything saved; every action and decision type round-trips through it.
- Public API stays limited to `Match`, `Game`, `PlayerView` and the action, decision, event and result records. Everything in `CromoBound.Engine.Effects` is `internal`; the test project sees it through `InternalsVisibleTo`.
- Unmapped cards (no effects file, or a file the engine can't run yet) play exactly as in 2a. Every existing test keeps passing; the only existing tests that change are the ones this plan names (support problems and data lists that move as cards become runnable).
- Step handlers call the existing `Game` mutations (`Draw`, `Burn`, `Channel`, `MoveCard`, `SetStatus`, ...); no rule is duplicated.
- Handlers validate an answer before they change anything, and a rejected answer leaves the decision pending (2a's `Submit` contract).
- `Models.Effects.PlayStep` (the step) and `Rules.PlayStep` (the play-progress enum) share a name. A file that imports both namespaces and needs the step uses the alias `using EffectPlayStep = CromoBound.Models.Effects.PlayStep;`; test files that use the step don't import `CromoBound.Engine.Rules`.
- Owner rules: 0 build warnings and 0 errors at all times; conventional, title-only commit messages with no body, no co-author trailer and no mention of Claude/AI; no em dashes or en dashes in code, comments or strings (test strings that copy real card text may keep them); LF line endings; UTF-8 without BOM.
- Run dotnet with `export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && ` in Git Bash (the default dotnet on PATH is SDK 9).

## Deliberate deviations from the spec (reviewers: these are intended)

1. **The runtime fallback stays (Plan D deviation 1), with a wider vocabulary.** Plan E runs:
   - triggers on the source's own `Dies`, `BecameEmpowered`, `Played`, `Hold` or `Conquer` (`"subject": { "ref": "Self" }`), and a battlefield's "when you hold (or conquer) here" (`"by": "You", "where": { "ref": "Here" }`);
   - activated abilities with any `timing`, costs of energy, power, `exhaustSelf` and at most one cost action, "recycle N from your trash";
   - the steps `Channel`, `GainXp`, `Empower`, `ChoosePlayer` (filter Opponent or Self), `ChooseCard` (from a trash), reflexive `Optional` without a cost, `Predict` 1, and `Play` of a stored card for its cost or `IgnoreAll`;
   - player references `{ "var": ... }`.
   Not yet: target selectors in triggered or activated abilities (no mapped one has them; when one does, it reuses `TargetsChosen`), trigger `if`/`optional`/`cost`/`limit`, `useOnlyIf`/`limit` in files (the Empower ability uses an internal condition), conditions beyond `all`/`any`/`not`/`empowered`.
2. **No `Add` step and no Add-only immediate resolution (spec §5.3, §12.3).** The only mapped card with `Add` is Fury Rune, and basic runes keep 2a's `UseRune` (spec §13). `CardEffects` gives runes no abilities, and a test checks that Fury Rune's file says what `UseRune` does. `Add` arrives with the first non-rune card that needs it.
3. **`Recycle` runs only as a cost action** ("recycle N from your trash", Garbage Grabber). No mapped card the engine runs uses it as a step.
4. **Effect decisions carry the source's `CardId`** (additive to spec §6.1), and the new public event `PlayerChosen(Player, Chosen)` announces player choices, since `ChoiceMade` carries object ids only.
5. **"The turn player's triggers first" (spec §5.2) means first onto the chain.** The opponent's go on top and resolve first.
6. **An activated ability goes on the chain once its costs are paid.** The cost-action cards are chosen before paying and recycled when the rest is paid; a chosen card a manual action moved meanwhile is skipped.
7. **"Ignoring its cost" skips the base cost like a hidden play** (`Payment.CostOf(card, fromHidden: true, accelerate)`); Accelerate is still offered and paid. A play started by an effect can be cancelled like any play; the step then did nothing.
8. **The play an effect starts is marked started**, so cleanups and triggers wait for the whole resolution (CR 321), as they do for any started task.
9. **Ability indices:** the file's abilities first, in JSON order, then the abilities keywords stand for, in keyword order. `ActivateOption.Ability` and `AbilityActivated.Ability` use these indices.

## Review Focus

1. **A chain item countered while its effect waits for a choice.** The effect stops: no later step runs, the spell goes to the trash, and the game goes on. Pinned in Task 3 (`Countering_a_spell_while_its_effect_waits_for_a_choice_stops_the_effect`).
2. **A manual action while an effect waits for an answer.** The same question is asked again with fresh options; nothing already done is redone. Pinned in Task 3 (`A_manual_action_during_an_effect_choice_asks_again`).
3. **Triggers that fire in the middle of a resolution or a play.** They wait until it has finished, then go on the chain. Pinned in Task 5 (`A_trigger_waits_until_the_resolution_that_caused_it_has_finished`).
4. **A cost action that can't be done in full.** The ability isn't offered (rules 416, 422), and becomes available as soon as it can be paid. Pinned in Task 6 (`Garbage_grabber_is_offered_only_with_three_cards_in_your_trash`).
5. **Undo, loading and views with triggers and effect choices.** Undoing an answer equals never giving it, a saved match loads identically, and the predicted card never reaches the opponent. Pinned in Task 7 (`Undo_after_answering_a_trigger_choice_equals_never_answering`, `A_saved_match_with_a_trigger_and_an_effect_choice_loads_identically`, `Only_the_predicting_player_sees_the_predicted_card`).

---

## File Structure

```
src/CromoBound.Engine/
  Decisions/Decisions.cs                  (modify) ActivateOption, PriorityDecision.Activations, ChoosePlayerDecision,
                                          ChooseCardsDecision, OptionalDecision, TriggerOption, OrderTriggersDecision
  Actions/PlayerAction.cs                 (modify) ChoosePlayer, ChooseCards, ChooseOptional, OrderTriggers, ActivateAbility
  Actions/ActionShape.cs                  (modify) ChooseCards and OrderTriggers shapes
  Events/GameEvents.cs                    (modify) CardPlayed, AbilityActivated, TriggerAdded, PlayerChosen, Predicted
  State/ChainItem.cs                      (modify) Steps
  Effects/ResolveEffectTask.cs            (modify) chain item, Answer, Progress
  Effects/Resolvers/ObjectResolver.cs     (modify) FilterMatches
  Effects/Resolvers/ZoneResolver.cs       zone references -> places
  Effects/Resolvers/ConditionResolver.cs  conditions -> true or false
  Effects/Steps/ResourceStepHandlers.cs   Channel, GainXp
  Effects/Steps/PermanentStepHandlers.cs  (modify) Empower
  Effects/Steps/ChoiceStepHandlers.cs     ChoosePlayer, ChooseCard, Optional
  Effects/Steps/CardStepHandlers.cs       (modify) Predict
  Effects/Steps/PlayStepHandler.cs        Play
  Effects/Steps/StepRegistry.cs           (modify) the new handlers
  Effects/TriggerWatcher.cs               PendingTrigger, events -> triggers
  Effects/EffectsSupport.cs               (modify) new steps, triggers, activated abilities, keywords
  Effects/CardEffects.cs                  (modify) keyword abilities, runes
  Rules/Game.cs                           (modify) PendingTriggers, Emit feeds the watcher, RunLoop puts triggers on the chain
  Rules/Game.Chain.cs                     (modify) ability items resolve automatically, AddAbilityItem, AbilityText
  Rules/Game.Mutations.cs                 (modify) Channel exhausted, GainXp, Recycle
  Rules/Game.Targets.cs                   (modify) CheckPick
  Rules/Game.Play.cs                      (modify) IgnoreCost, Finished, Played, CardPlayed
  Rules/Game.Triggers.cs                  PutTriggersOnChainTask, ordering
  Rules/Game.Activation.cs                ActivationTask, offered abilities, costs
  Rules/Game.Priority.cs                  (modify) Activations
  Rules/Game.Dispatch.cs                  (modify) ActivateAbility
  Rules/Game.TurnPoints.cs                (modify) HandledBy internal
tests/CromoBound.Engine.Tests/
  Bot.cs                                  (modify) the new decisions
  TestGame.cs                             (modify) battlefield card ids
  EffectsJsonTests.cs, ChoiceTests.cs, EffectPlayTests.cs, TriggerTests.cs, ActivationTests.cs
  StepTests.cs, CardEffectsTests.cs, EffectsDataTests.cs, MatchEffectsTests.cs   (modify)
```

---

### Task 1: Decisions, actions and events for effect choices

**Files:**
- Modify: `src/CromoBound.Engine/Decisions/Decisions.cs`
- Modify: `src/CromoBound.Engine/Actions/PlayerAction.cs`
- Modify: `src/CromoBound.Engine/Actions/ActionShape.cs`
- Modify: `src/CromoBound.Engine/Events/GameEvents.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Priority.cs`
- Modify: `tests/CromoBound.Engine.Tests/Bot.cs`
- Test: `tests/CromoBound.Engine.Tests/EffectsJsonTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces (all public records):
  - `ActivateOption(ObjectId Source, int Ability)`; `PriorityDecision(Player, Playable, Runes, Moves, Hides, IReadOnlyList<ActivateOption> Activations, bool CanPass, bool CanEndTurn)`.
  - `ChoosePlayerDecision(PlayerId Player, string CardId, IReadOnlyList<PlayerId> Options)`.
  - `ChooseCardsDecision(PlayerId Player, string CardId, IReadOnlyList<ObjectId> Options, int Min, int Max)`.
  - `OptionalDecision(PlayerId Player, string CardId, string Text)`.
  - `TriggerOption(ObjectId Source, string CardId, string? Text)`; `OrderTriggersDecision(PlayerId Player, IReadOnlyList<TriggerOption> Triggers)`.
  - Actions `ChoosePlayer(PlayerId Player)`, `ChooseCards { Cards }`, `ChooseOptional(bool Yes)`, `OrderTriggers { Order }`, `ActivateAbility(ObjectId Source, int Ability)`.
  - Events `CardPlayed(ObjectId Card, string CardId, PlayerId Controller)`, `AbilityActivated(ObjectId Source, int Ability, PlayerId Controller)`, `TriggerAdded(int ItemId, ObjectId Source, PlayerId Controller)`, `PlayerChosen(PlayerId Player, PlayerId Chosen)`, `Predicted(PlayerId Player, int Count, IReadOnlyList<string>? CardIds)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/EffectsJsonTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class EffectsJsonTests
{
    [Fact]
    public void Effect_actions_round_trip_through_json()
    {
        PlayerAction[] actions =
        [
            new ChoosePlayer(P2),
            new ChooseCards { Cards = [new ObjectId(3), new ObjectId(5)] },
            new ChooseOptional(true),
            new OrderTriggers { Order = [1, 0] },
            new ActivateAbility(new ObjectId(7), 1),
        ];

        foreach (var action in actions)
        {
            var json = CromoJson.Serialize(action);

            Assert.Contains($"\"{action.GetType().Name}\"", json);
            Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<PlayerAction>(json)));
        }
    }

    [Fact]
    public void Effect_decisions_round_trip_through_json()
    {
        PendingDecision[] decisions =
        [
            new PriorityDecision(P1, [], [], [], [], [new ActivateOption(new ObjectId(7), 1)], CanPass: false, CanEndTurn: true),
            new ChoosePlayerDecision(P1, "kharox", [P2]),
            new ChooseCardsDecision(P1, "kharox", [new ObjectId(3)], 1, 1),
            new OptionalDecision(P1, "kharox", "Do the rest of the ability?"),
            new OrderTriggersDecision(P1, [new TriggerOption(new ObjectId(4), "unit-3", null), new TriggerOption(new ObjectId(6), "unit-3", "text")]),
        ];

        foreach (var decision in decisions)
        {
            var json = CromoJson.Serialize(decision);

            Assert.Contains($"\"{decision.GetType().Name.Replace("Decision", "")}\"", json);
            Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<PendingDecision>(json)));
        }
    }

    [Fact]
    public void Effect_events_round_trip_through_json()
    {
        GameEvent[] events =
        [
            new CardPlayed(new ObjectId(4), "unit-2", P1),
            new AbilityActivated(new ObjectId(7), 1, P1),
            new TriggerAdded(3, new ObjectId(4), P2),
            new PlayerChosen(P1, P2),
            new Predicted(P1, 1, ["unit-2"]) { VisibleTo = P1 },
            new Predicted(P1, 1, null),
        ];

        foreach (var gameEvent in events)
        {
            var json = CromoJson.Serialize(gameEvent);

            Assert.Contains($"\"{gameEvent.GetType().Name}\"", json);
            Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<GameEvent>(json)));
        }
    }

    [Theory]
    [InlineData("""{ "type": "ChooseCards", "cards": null }""")]
    [InlineData("""{ "type": "OrderTriggers", "order": null }""")]
    public void A_null_list_in_an_effect_answer_is_rejected(string json)
    {
        var engine = new TestGame().Start();

        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, CromoJson.Deserialize<PlayerAction>(json)).Rejection!.Code);
    }

    [Fact]
    public void Priority_lists_no_activations_yet()
    {
        var engine = new TestGame().Start();

        Assert.Empty(engine.Decision<PriorityDecision>().Activations);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~EffectsJsonTests"`
Expected: FAIL (build errors: `ChoosePlayer`, `ActivateOption`, `CardPlayed` and the others don't exist).

- [ ] **Step 3: Write the implementation**

In `src/CromoBound.Engine/Decisions/Decisions.cs`, add these lines after `[JsonDerivedType(typeof(ChooseTargetsDecision), "ChooseTargets")]`:

```csharp
[JsonDerivedType(typeof(ChoosePlayerDecision), "ChoosePlayer")]
[JsonDerivedType(typeof(ChooseCardsDecision), "ChooseCards")]
[JsonDerivedType(typeof(OptionalDecision), "Optional")]
[JsonDerivedType(typeof(OrderTriggersDecision), "OrderTriggers")]
```

Replace the `PriorityDecision` record with:

```csharp
/// <summary>An activated ability the player may start now: its source and the ability's index in its card's abilities.</summary>
public sealed record ActivateOption(ObjectId Source, int Ability);

/// <summary>The priority (or focus) holder's options. Playable lists cards by timing only; payment is checked when paying.</summary>
public sealed record PriorityDecision(
    PlayerId Player,
    IReadOnlyList<ObjectId> Playable,
    IReadOnlyList<RuneOption> Runes,
    IReadOnlyList<MoveOption> Moves,
    IReadOnlyList<HideOption> Hides,
    IReadOnlyList<ActivateOption> Activations,
    bool CanPass,
    bool CanEndTurn) : PendingDecision([Player]);
```

Append at the end of the file:

```csharp
/// <summary>Choose one of <see cref="Options"/> for an effect of <see cref="CardId"/> (spec §6.1).</summary>
public sealed record ChoosePlayerDecision(PlayerId Player, string CardId, IReadOnlyList<PlayerId> Options) : PendingDecision([Player]);

/// <summary>Choose between <see cref="Min"/> and <see cref="Max"/> of <see cref="Options"/> for an effect or a cost of <see cref="CardId"/>.</summary>
public sealed record ChooseCardsDecision(PlayerId Player, string CardId, IReadOnlyList<ObjectId> Options, int Min, int Max)
    : PendingDecision([Player]);

/// <summary>Yes or no to an optional part of an effect of <see cref="CardId"/>; <see cref="Text"/> says what.</summary>
public sealed record OptionalDecision(PlayerId Player, string CardId, string Text) : PendingDecision([Player]);

/// <summary>A triggered ability waiting to go on the chain: its source (which may have left play), the source's card id and the ability's text, if known.</summary>
public sealed record TriggerOption(ObjectId Source, string CardId, string? Text);

/// <summary>Several triggered abilities of the player wait at once: choose the order they go on the chain (spec §5.2).</summary>
public sealed record OrderTriggersDecision(PlayerId Player, IReadOnlyList<TriggerOption> Triggers) : PendingDecision([Player]);
```

In `src/CromoBound.Engine/Actions/PlayerAction.cs`, add after `[JsonDerivedType(typeof(ChooseTargets), "ChooseTargets")]`:

```csharp
[JsonDerivedType(typeof(ChoosePlayer), "ChoosePlayer")]
[JsonDerivedType(typeof(ChooseCards), "ChooseCards")]
[JsonDerivedType(typeof(ChooseOptional), "ChooseOptional")]
[JsonDerivedType(typeof(OrderTriggers), "OrderTriggers")]
[JsonDerivedType(typeof(ActivateAbility), "ActivateAbility")]
```

Append at the end of the file:

```csharp
/// <summary>The player chosen for a ChoosePlayerDecision.</summary>
public sealed record ChoosePlayer(PlayerId Player) : PlayerAction;

/// <summary>The cards chosen for a ChooseCardsDecision.</summary>
public sealed record ChooseCards : PlayerAction
{
    public IReadOnlyList<ObjectId> Cards { get; init; } = [];
}

/// <summary>Yes or no to an OptionalDecision.</summary>
public sealed record ChooseOptional(bool Yes) : PlayerAction;

/// <summary>The offered triggers' indices in the order they go on the chain: the first goes on first, so the last resolves first.</summary>
public sealed record OrderTriggers : PlayerAction
{
    public IReadOnlyList<int> Order { get; init; } = [];
}

/// <summary>Start activating an ability the priority decision offers (spec §5.3).</summary>
public sealed record ActivateAbility(ObjectId Source, int Ability) : PlayerAction;
```

In `src/CromoBound.Engine/Actions/ActionShape.cs`, add before the `_ => null,` arm:

```csharp
        ChooseCards cards => cards.Cards is null ? Missing(nameof(cards.Cards)) : null,
        OrderTriggers order => order.Order is null ? Missing(nameof(order.Order)) : null,
```

In `src/CromoBound.Engine/Events/GameEvents.cs`, add after `[JsonDerivedType(typeof(TargetsChosen), "TargetsChosen")]`:

```csharp
[JsonDerivedType(typeof(CardPlayed), "CardPlayed")]
[JsonDerivedType(typeof(AbilityActivated), "AbilityActivated")]
[JsonDerivedType(typeof(TriggerAdded), "TriggerAdded")]
[JsonDerivedType(typeof(PlayerChosen), "PlayerChosen")]
[JsonDerivedType(typeof(Predicted), "Predicted")]
```

Append at the end of the file:

```csharp
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
```

In `src/CromoBound.Engine/Rules/Game.Priority.cs`, in `PriorityOptions`, add the activations argument after the hides (Task 6 fills it):

```csharp
        return new PriorityDecision(
            player,
            [.. PlayableCards(player)],
            [.. RunesOf(player).Select(r => new RuneOption(r.Id, !r.Exhausted))],
            neutralOpenMain ? [.. MoveOptions(player)] : [],
            neutralOpenMain ? [.. HideOptions(player)] : [],
            [],
            CanPass: IsClosed || State.Showdown is not null,
            CanEndTurn: neutralOpenMain && State.StagedShowdowns.Count == 0 && State.StagedCombats.Count == 0);
```

In `tests/CromoBound.Engine.Tests/Bot.cs`, change the class summary's end to "...resolves by hand immediately, takes every suggestion, picks the first offered player and cards, declines optional parts and keeps triggers in the offered order." and add these arms before the `_ => throw` arm of `Choose(Game game)`:

```csharp
            ChoosePlayerDecision choose => new ChoosePlayer(choose.Options[0]),
            ChooseCardsDecision cards => new ChooseCards { Cards = [.. cards.Options.Take(cards.Min)] },
            OptionalDecision => new ChooseOptional(false),
            OrderTriggersDecision order => new OrderTriggers { Order = [.. Enumerable.Range(0, order.Triggers.Count)] },
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): add effect choice decisions, actions and events"
```

---

### Task 2: Ability items resolve automatically; Channel, GainXp, Empower

**Files:**
- Modify: `src/CromoBound.Engine/State/ChainItem.cs`
- Modify: `src/CromoBound.Engine/Effects/ResolveEffectTask.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Chain.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Mutations.cs`
- Create: `src/CromoBound.Engine/Effects/Steps/ResourceStepHandlers.cs`
- Modify: `src/CromoBound.Engine/Effects/Steps/PermanentStepHandlers.cs`
- Modify: `src/CromoBound.Engine/Effects/Steps/StepRegistry.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Modify: `tests/CromoBound.Engine.Tests/CardEffectsTests.cs`
- Test: `tests/CromoBound.Engine.Tests/StepTests.cs`

**Interfaces:**
- Consumes: Plan D's `ResolveEffectTask`, `StepHandler<TStep>`, `StepRegistry`, `EffectContext`, `EffectVar`, `PlayerResolver`, `ObjectResolver`, `ValueResolver`.
- Produces:
  - `ChainItem.Steps` (`internal IReadOnlyList<Step>?`): the steps of an ability the engine runs.
  - `ResolveEffectTask(EffectContext context, IReadOnlyList<Step> steps, Action<Game> onDone, ChainItem? item = null)` with `Item`, `Answer` (`object?`) and `Progress` (`object?`), both cleared when a step finishes.
  - `Game.AddAbilityItem(PlayerId controller, ObjectId? source, string cardId, AbilityKind kind, string? text, IReadOnlyList<Step> steps, EffectContext context) : ChainItem` (internal): a finalized ability item; its controller gets priority.
  - `Game.Channel(PlayerId player, int count, bool exhausted = false) : List<ObjectId>` and `Game.GainXp(PlayerId player, int amount)` (internal).
  - Handlers for `ChannelStep`, `GainXpStep`, `EmpowerStep`.

- [ ] **Step 1: Write the failing tests**

Add to the usings of `tests/CromoBound.Engine.Tests/StepTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
```

Add these tests to the `StepTests` class:

```csharp
    [Fact]
    public void Channel_takes_runes_from_the_rune_deck_exhausted_when_the_step_says_so()
    {
        var game = new TestGame();
        for (var i = 0; i < 3; i++) game.Put("fury-rune", Place.RuneDeck(P1));
        var engine = game.Start();

        var context = Run(engine, [new ChannelStep { Count = 2, Exhausted = true, Store = "runes" }]);

        Assert.Empty(game.State.At(Place.RuneDeck(P1)));
        Assert.Equal(3, engine.RunesOf(P1).Count);
        Assert.Equal(1, engine.RunesOf(P1).Count(r => r.Exhausted));
        Assert.Single(context.Vars["runes"].Objects);
    }

    [Fact]
    public void Gain_xp_adds_experience_and_announces_it()
    {
        var game = new TestGame();
        var engine = game.Start();

        var events = engine.RunNow(new ResolveEffectTask(
            new EffectContext { Controller = P1, SourceCardId = "spell" }, [new GainXpStep { Amount = 3 }], _ => { }));

        Assert.Equal(3, game.State.Player(P1).Xp);
        Assert.Contains(events, e => e is XpChanged { Xp: 3 });
    }

    [Fact]
    public void Empower_sets_the_status_once()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        var context = new EffectContext { Controller = P1, Source = unit, SourceCardId = "unit-2" };

        engine.RunNow(new ResolveEffectTask(context,
            [new EmpowerStep { Target = ObjectRef.Self, Store = "first" }, new EmpowerStep { Target = ObjectRef.Self, Store = "again" }], _ => { }));

        Assert.True(game.State[unit].Empowered);
        Assert.True(context.Vars["first"].Happened);
        Assert.False(context.Vars["again"].Happened);
    }

    [Fact]
    public void A_game_won_by_burn_out_stops_the_remaining_steps()
    {
        var game = new TestGame();
        game.State.Player(P2).Points = 6;
        var engine = game.Start(filler: 1);
        var done = false;

        engine.RunNow(new ResolveEffectTask(new EffectContext { Controller = P1, SourceCardId = "spell" },
            [new BurnStep { Amount = 2 }, new DrawStep { Amount = 1 }], _ => done = true));

        Assert.Equal(new GameOutcome(P2, GameEndReason.BurnOut), engine.Outcome);
        Assert.Single(game.State.At(Place.Hand(P1)));
        Assert.False(done);
    }

    [Fact]
    public void An_ability_item_with_steps_resolves_automatically_once_both_players_pass()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        var hand = game.State.At(Place.Hand(P1)).Count;
        var context = new EffectContext { Controller = P1, Source = unit, SourceCardId = "unit-2" };

        engine.RunNow(new StepTask(g => g.AddAbilityItem(P1, unit, "unit-2", AbilityKind.Triggered, "draw text", [new DrawStep { Amount = 2 }], context)));

        var item = Assert.Single(game.State.Chain);
        Assert.Equal(ChainItemKind.Ability, item.Kind);
        Assert.Equal(ChainItemStatus.Finalized, item.Status);
        Assert.Equal("draw text", item.Text);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
        engine.Accept(P1, new Pass());
        var result = engine.Accept(P2, new Pass());

        Assert.Equal(hand + 2, game.State.At(Place.Hand(P1)).Count);
        Assert.Empty(game.State.Chain);
        Assert.Contains(result.Events, e => e is ChainItemResolved);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }
```

In `tests/CromoBound.Engine.Tests/CardEffectsTests.cs`, Channel is now supported: replace the first `InlineData` row of `Unsupported_steps_targets_values_and_players_are_named` with:

```csharp
    [InlineData("""{ "action": "ExtraTurn" }""", "abilities[0].steps[0]: step ExtraTurn")]
```

and add this row:

```csharp
    [InlineData("""{ "action": "GainXp", "amount": { "var": "x" } }""", "abilities[0].steps[0]: value")]
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~StepTests|FullyQualifiedName~CardEffectsTests"`
Expected: FAIL (build error: `Game.AddAbilityItem` doesn't exist).

- [ ] **Step 3: Write the implementation**

In `src/CromoBound.Engine/State/ChainItem.cs`, add `using CromoBound.Models.Effects;` and, after the `Effect` property:

```csharp
    /// <summary>The steps of an ability the engine runs (triggers, activations, reflexive blocks); null for abilities resolved by hand.</summary>
    internal IReadOnlyList<Step>? Steps { get; set; }
```

Replace `src/CromoBound.Engine/Effects/ResolveEffectTask.cs` with:

```csharp
using CromoBound.Engine.Effects.Steps;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>Runs an ability's steps in order with a step counter (spec §4.3). A step that asks pauses the task; after the answer
/// (or after a manual action rebuilt the decision) the same step runs again. <paramref name="onDone"/> runs once, after the last step.
/// The task stops when the game ends, and, when <paramref name="item"/> is given, as soon as that chain item leaves the chain
/// (countered while a step waited for an answer).</summary>
internal sealed class ResolveEffectTask(EffectContext context, IReadOnlyList<Step> steps, Action<Game> onDone, ChainItem? item = null) : GameTask
{
    public EffectContext Context { get; } = context;
    public IReadOnlyList<Step> Steps { get; } = steps;
    public ChainItem? Item { get; } = item;
    public int Index { get; set; }

    /// <summary>The current step's result, saved under its "store" name when the step finishes.</summary>
    public EffectVar? Result { get; set; }

    /// <summary>The answer to the current step's decision, set by the decision's handler; the step reads it when it runs again.</summary>
    public object? Answer { get; set; }

    /// <summary>What the current step did before asking, kept across reruns (Predict: the card it looked at).</summary>
    public object? Progress { get; set; }

    public override bool Run(Game game)
    {
        while (Index < Steps.Count)
        {
            if (Stopped(game)) return true;
            var step = Steps[Index];
            var outcome = StepRegistry.For(step).Run(game, this, step);
            if (outcome == StepOutcome.Asked) return false;
            if (step.Store is { } name) Context.Vars[name] = (Result ?? EffectVar.Empty) with { Happened = outcome == StepOutcome.Done };
            Result = null;
            Answer = null;
            Progress = null;
            Index++;
        }
        if (!Stopped(game)) onDone(game);
        return true;
    }

    private bool Stopped(Game game) => game.Outcome is not null || (Item is not null && !game.State.Chain.Contains(Item));
}
```

In `src/CromoBound.Engine/Rules/Game.Chain.cs`, replace `ResolveTop` with:

```csharp
    /// <summary>The newest finalized item resolves. An ability the engine runs and a spell it runs (Full or Partial) resolve
    /// automatically (spec §5.1-5.3); anything else is resolved by hand by its controller (spec §8).</summary>
    private void ResolveTop()
    {
        var item = State.Chain.Last(i => i.Status == ChainItemStatus.Finalized);
        if (item is { Kind: ChainItemKind.Ability, Steps: { } abilitySteps, Effect: { } abilityContext })
        {
            Push(new ResolveEffectTask(abilityContext, abilitySteps, g => g.FinishResolution(item), item));
            return;
        }
        if (item.Card is { } card && Effects.For(State[card].CardId) is { Status: not MappingStatus.Unmapped } effects)
        {
            var steps = SpellSteps(State[card].CardId);
            var context = item.Effect ?? new EffectContext
            {
                Controller = item.Controller,
                Source = card,
                SourceCardId = State[card].CardId,
                Slots = TargetSlots.Of(steps),
            };
            Push(new ResolveEffectTask(context, steps, g => g.AfterAutomatedResolution(item, effects), item));
            return;
        }
        ResolveByHand(item, null);
    }

    /// <summary>Puts an ability the engine runs on the chain, finalized (a trigger, an activation, a reflexive block); its
    /// controller gets priority.</summary>
    internal ChainItem AddAbilityItem(PlayerId controller, ObjectId? source, string cardId, AbilityKind kind, string? text,
        IReadOnlyList<Step> steps, EffectContext context)
    {
        var item = new ChainItem
        {
            Id = State.NextChainItemId(),
            Kind = ChainItemKind.Ability,
            Controller = controller,
            Status = ChainItemStatus.Finalized,
            Source = source,
            AbilityKind = kind,
            SourceCardId = cardId,
            Text = text,
            Effect = context,
            Steps = steps,
        };
        State.Chain.Add(item);
        Emit(new ChainItemAdded(item.Id, controller));
        MarkDirty();
        ChainPasses = 0;
        State.Turn.Priority = controller;
        return item;
    }
```

In `src/CromoBound.Engine/Rules/Game.Mutations.cs`, replace `Channel` with:

```csharp
    /// <summary>Top runes of the Rune Deck to the Base, as many as remain (CR 430): ready, or exhausted when an effect says so.
    /// Returns the channeled runes.</summary>
    internal List<ObjectId> Channel(PlayerId player, int count, bool exhausted = false)
    {
        var channeled = new List<ObjectId>();
        for (var i = 0; i < count; i++)
        {
            var deck = State.At(Place.RuneDeck(player));
            if (deck.Count == 0) break;
            var rune = MoveCard(deck[0], Place.Base(player))!.Value;
            if (exhausted) SetStatus(rune, StatusKind.Exhausted, true);
            channeled.Add(rune);
        }
        return channeled;
    }

    internal void GainXp(PlayerId player, int amount)
    {
        if (amount <= 0) return;
        var state = State.Player(player);
        state.Xp += amount;
        Emit(new XpChanged(player, state.Xp));
    }
```

Create `src/CromoBound.Engine/Effects/Steps/ResourceStepHandlers.cs`:

```csharp
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

internal sealed class ChannelHandler : StepHandler<ChannelStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, ChannelStep step)
    {
        var count = ValueResolver.Resolve(game, task.Context, step.Count);
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (count <= 0 || players.Count == 0) return StepOutcome.DidNothing;
        List<ObjectId> runes = [.. players.SelectMany(p => game.Channel(p, count, step.Exhausted == true))];
        if (runes.Count == 0) return StepOutcome.DidNothing;
        task.Result = new EffectVar(runes, players, runes.Count, true);
        return StepOutcome.Done;
    }
}

internal sealed class GainXpHandler : StepHandler<GainXpStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, GainXpStep step)
    {
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (amount <= 0 || players.Count == 0) return StepOutcome.DidNothing;
        foreach (var player in players) game.GainXp(player, amount);
        task.Result = new EffectVar([], players, amount, true);
        return StepOutcome.Done;
    }
}
```

In `src/CromoBound.Engine/Effects/Steps/PermanentStepHandlers.cs`, add `using CromoBound.Engine.Events;` and append:

```csharp
/// <summary>Empowers the board objects that aren't Empowered yet (Empower, spec §8.1).</summary>
internal sealed class EmpowerHandler : StepHandler<EmpowerStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, EmpowerStep step)
    {
        List<ObjectId> targets =
        [
            .. ObjectResolver.Resolve(game, task.Context, step.Target)
                .Where(id => game.State[id].Place.IsLocation && !game.State[id].Empowered),
        ];
        if (targets.Count == 0) return StepOutcome.DidNothing;
        foreach (var target in targets) game.SetStatus(target, StatusKind.Empowered, true);
        task.Result = new EffectVar(targets, [], null, true);
        return StepOutcome.Done;
    }
}
```

In `src/CromoBound.Engine/Effects/Steps/StepRegistry.cs`, add to the dictionary:

```csharp
        [typeof(ChannelStep)] = new ChannelHandler(),
        [typeof(GainXpStep)] = new GainXpHandler(),
        [typeof(EmpowerStep)] = new EmpowerHandler(),
```

In `src/CromoBound.Engine/Effects/EffectsSupport.cs`, add these arms to the `switch (step)` in `CheckStep`:

```csharp
            case ChannelStep channel:
                CheckValue(channel.Count, at, problems);
                break;
            case GainXpStep xp:
                CheckValue(xp.Amount, at, problems);
                break;
```

(`EmpowerStep` is a `TargetStep`, so its target is already checked: `Self` or a target selector.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): resolve engine-run abilities on the chain"
```

---
### Task 3: Choosing a player, cards and optional parts; Predict

**Files:**
- Create: `src/CromoBound.Engine/Effects/Resolvers/ZoneResolver.cs`
- Modify: `src/CromoBound.Engine/Effects/Resolvers/ObjectResolver.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Targets.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Mutations.cs`
- Create: `src/CromoBound.Engine/Effects/Steps/ChoiceStepHandlers.cs`
- Modify: `src/CromoBound.Engine/Effects/Steps/CardStepHandlers.cs`
- Modify: `src/CromoBound.Engine/Effects/Steps/StepRegistry.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Modify: `tests/CromoBound.Engine.Tests/CardEffectsTests.cs`
- Test: `tests/CromoBound.Engine.Tests/ChoiceTests.cs`

**Interfaces:**
- Consumes: Task 1's `ChoosePlayerDecision`, `ChooseCardsDecision`, `OptionalDecision`, `ChoosePlayer`, `ChooseCards`, `ChooseOptional`, `PlayerChosen`, `Predicted`; Task 2's `ResolveEffectTask.Item`/`Answer`/`Progress` and `Game.AddAbilityItem`.
- Produces:
  - `ZoneResolver.Resolve(Game, EffectContext, ZoneRef) : List<Place>` (one place per owner, in turn order).
  - `ObjectResolver.FilterMatches(Game, EffectContext, Filter?, CardInstance) : bool`.
  - `Game.CheckPick(IReadOnlyList<ObjectId> chosen, IReadOnlyList<ObjectId> options, int min, int max, string what) : Rejection?` (internal static), used for targets, `ChooseCard` and cost actions.
  - `Game.Recycle(ObjectId card)` (internal): to the bottom of its owner's deck.
  - Handlers for `ChoosePlayerStep`, `ChooseCardStep`, `OptionalStep` (reflexive) and `PredictStep`; `OptionalHandler.Question` and `PredictHandler.Question` (the `OptionalDecision` texts).
  - `EffectsSupport.CheckSteps(steps, at, problems, targets)` (private): target selectors only where `targets` is true (a spell's steps); player `var` references allowed.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/ChoiceTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Steps;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ChoiceTests
{
    private static readonly Filter Opponent = new() { Relation = Relation.Opponent };

    private const string ChooseThenDraw = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1, "steps": [
          { "action": "ChoosePlayer", "store": "who" },
          { "action": "Draw", "amount": 2, "player": { "var": "who" } } ] } ] }
        """;

    /// <summary>Starts running the steps for P1; they may stop at a decision. Returns the context and the events so far.</summary>
    private static (EffectContext Context, IReadOnlyList<GameEvent> Events) Run(Game engine, params Step[] steps)
    {
        var context = new EffectContext { Controller = P1, SourceCardId = "spell" };
        var events = engine.RunNow(new ResolveEffectTask(context, steps, _ => { }));
        return (context, events);
    }

    [Fact]
    public void Choosing_an_opponent_when_there_is_one_is_forced_and_announced()
    {
        var engine = new TestGame().Start();

        var (context, events) = Run(engine, new ChoosePlayerStep { Filter = Opponent, Store = "victim" });

        Assert.Equal(new[] { P2 }, context.Vars["victim"].Players);
        Assert.Contains(events, e => e is ChoiceMade { Kind: "Player" });
        Assert.Contains(events, e => e is PlayerChosen { Player.Index: 0, Chosen.Index: 1 });
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Choosing_among_several_players_asks_and_stores_the_answer()
    {
        var game = new TestGame();
        var engine = game.Start();

        var (context, _) = Run(engine,
            new ChoosePlayerStep { Store = "who" },
            new DrawStep { Amount = 1, Player = new PlayerRef { Var = "who" } });

        var choose = engine.Decision<ChoosePlayerDecision>();
        Assert.Equal(new[] { P1, P2 }, choose.Options);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new ChoosePlayer(new PlayerId(5))).Rejection!.Code);
        var result = engine.Accept(P1, new ChoosePlayer(P2));

        Assert.Equal(new[] { P2 }, context.Vars["who"].Players);
        Assert.Single(game.State.At(Place.Hand(P2)));
        Assert.Contains(result.Events, e => e is PlayerChosen { Chosen.Index: 1 });
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void A_manual_action_during_an_effect_choice_asks_again()
    {
        var engine = new TestGame().Start();
        Run(engine, new ChoosePlayerStep { Store = "who" });
        engine.Decision<ChoosePlayerDecision>();

        Assert.True(engine.SubmitManual(P1, new ManualAdjustXp(P1, 1)).Accepted);

        Assert.IsType<ChoosePlayerDecision>(engine.Pending);
    }

    [Fact]
    public void Countering_a_spell_while_its_effect_waits_for_a_choice_stops_the_effect()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", ChooseThenDraw)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        engine.Decision<ChoosePlayerDecision>();
        var item = Assert.Single(game.State.Chain);
        var hands = (game.State.At(Place.Hand(P1)).Count, game.State.At(Place.Hand(P2)).Count);

        Assert.True(engine.SubmitManual(P2, new ManualCounter(item.Id)).Accepted);

        Assert.Empty(game.State.Chain);
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.Equal(hands, (game.State.At(Place.Hand(P1)).Count, game.State.At(Place.Hand(P2)).Count));
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Choosing_cards_from_a_trash_offers_only_matching_cards()
    {
        var game = new TestGame();
        var units = new[] { game.Put("unit-2", Place.Trash(P2)), game.Put("unit-3", Place.Trash(P2)) };
        game.Put("spell", Place.Trash(P2));
        game.Put("unit-2", Place.Trash(P1));
        var engine = game.Start();
        var trash = new ZoneRef { Zone = Zone.Trash, Owner = new PlayerRef { Kind = PlayerKind.Opponent } };

        var (context, _) = Run(engine, new ChooseCardStep { From = trash, Filter = new Filter { Type = CardType.Unit }, Store = "picked" });

        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal(units.ToHashSet(), choose.Options.ToHashSet());
        Assert.Equal((1, 1), (choose.Min, choose.Max));
        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseCards { Cards = [units[0], units[1]] }).Rejection!.Code);
        engine.Accept(P1, new ChooseCards { Cards = [units[1]] });

        Assert.Equal(new[] { units[1] }, context.Vars["picked"].Objects);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Exactly_enough_cards_is_a_forced_choice_and_none_is_nothing()
    {
        var game = new TestGame();
        var only = game.Put("unit-2", Place.Trash(P1));
        var engine = game.Start();
        var mine = new ZoneRef { Zone = Zone.Trash };

        var (context, events) = Run(engine,
            new ChooseCardStep { From = mine, Store = "one" },
            new ChooseCardStep { From = mine, Filter = new Filter { Type = CardType.Spell }, Store = "none" });

        Assert.Equal(new[] { only }, context.Vars["one"].Objects);
        Assert.Contains(events, e => e is ChoiceMade { Kind: "Cards" });
        Assert.False(context.Vars["none"].Happened);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Saying_yes_to_a_reflexive_block_puts_it_on_the_chain_with_the_same_context()
    {
        var game = new TestGame();
        var engine = game.Start();
        var (context, _) = Run(engine,
            new ChoosePlayerStep { Filter = Opponent, Store = "victim" },
            new OptionalStep { Reflexive = true, Steps = [new BurnStep { Amount = 2, Player = new PlayerRef { Var = "victim" } }] });

        var ask = engine.Decision<OptionalDecision>();
        Assert.Equal(P1, ask.Player);
        Assert.Equal(OptionalHandler.Question, ask.Text);
        engine.Accept(P1, new ChooseOptional(true));

        var block = Assert.Single(game.State.Chain);
        Assert.Same(context, block.Effect);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
        engine.Accept(P1, new Pass());
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
        engine.Accept(P2, new Pass());
        Assert.Equal(2, game.State.At(Place.Trash(P2)).Count);
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void Saying_no_to_a_reflexive_block_skips_it()
    {
        var game = new TestGame();
        var engine = game.Start();
        var (context, _) = Run(engine, new OptionalStep { Reflexive = true, Steps = [new DrawStep { Amount = 1 }], Store = "did" });

        engine.Accept(P1, new ChooseOptional(false));

        Assert.Empty(game.State.Chain);
        Assert.False(context.Vars["did"].Happened);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Predicting_shows_the_top_card_only_to_the_player_who_may_recycle_it()
    {
        var game = new TestGame();
        var marked = game.Put("unit-3", Place.Trash(P1));
        var engine = game.Start();
        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(marked, Place.MainDeck(P1))).Accepted);

        var (_, events) = Run(engine, new PredictStep());

        var predicted = events.OfType<Predicted>().ToList();
        Assert.Equal(2, predicted.Count);
        Assert.Equal(new[] { "unit-3" }, predicted.Single(p => p.VisibleTo == P1).CardIds);
        Assert.Null(predicted.Single(p => p.VisibleTo is null).CardIds);
        Assert.Equal(PredictHandler.Question, engine.Decision<OptionalDecision>().Text);
        engine.Accept(P1, new ChooseOptional(true));

        var deck = game.State.At(Place.MainDeck(P1));
        Assert.Equal("unit-3", game.State[deck[^1]].CardId);
        Assert.Equal("unit-2", game.State[deck[0]].CardId);
    }

    [Fact]
    public void Declining_a_prediction_keeps_the_card_on_top()
    {
        var game = new TestGame();
        var marked = game.Put("unit-3", Place.Trash(P1));
        var engine = game.Start();
        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(marked, Place.MainDeck(P1))).Accepted);
        Run(engine, new PredictStep());

        engine.Accept(P1, new ChooseOptional(false));

        Assert.Equal("unit-3", game.State[game.State.At(Place.MainDeck(P1))[0]].CardId);
    }
}
```

In `tests/CromoBound.Engine.Tests/CardEffectsTests.cs`, variable players are now supported: replace the `InlineData` row that uses `{ "var": "victim" }` with:

```csharp
    [InlineData("""{ "action": "Draw", "player": { "controllerOf": { "ref": "Self" } } }""", "abilities[0].steps[0]: player")]
```

and add these rows to the same theory:

```csharp
    [InlineData("""{ "action": "Optional", "steps": [ { "action": "Draw" } ] }""", "abilities[0].steps[0]: optional")]
    [InlineData("""{ "action": "Optional", "reflexive": true, "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1 } } ] }""", "abilities[0].steps[0].steps[0]: target")]
    [InlineData("""{ "action": "ChooseCard", "from": { "zone": "Hand" } }""", "abilities[0].steps[0]: from")]
    [InlineData("""{ "action": "ChoosePlayer", "filter": { "relation": "Friendly" } }""", "abilities[0].steps[0]: filter")]
    [InlineData("""{ "action": "Predict", "amount": 2 }""", "abilities[0].steps[0]: value")]
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~ChoiceTests|FullyQualifiedName~CardEffectsTests"`
Expected: FAIL (build errors: `OptionalHandler` and `PredictHandler` don't exist).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Engine/Effects/Resolvers/ZoneResolver.cs`:

```csharp
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns zone references into places (spec §4.5): one per owner, in turn order. A missing owner means "You".</summary>
internal static class ZoneResolver
{
    public static List<Place> Resolve(Game game, EffectContext context, ZoneRef zone) =>
        [.. PlayerResolver.Resolve(game, context, zone.Owner).Select(player => PlaceOf(zone.Zone, player))];

    private static Place PlaceOf(Zone zone, PlayerId player) => zone switch
    {
        Zone.Trash => Place.Trash(player),
        Zone.Hand => Place.Hand(player),
        Zone.MainDeck => Place.MainDeck(player),
        Zone.RuneDeck => Place.RuneDeck(player),
        Zone.Base => Place.Base(player),
        Zone.Banishment => Place.Banishment(player),
        Zone.ChampionZone => Place.ChampionZone(player),
        _ => throw new InvalidOperationException($"The {zone} zone has no single place per player."),
    };
}
```

In `src/CromoBound.Engine/Effects/Resolvers/ObjectResolver.cs`, replace `Matches` with:

```csharp
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
        return kindMatches && FilterMatches(game, context, filter, instance);
    }

    /// <summary>Whether a card matches the filter's relation, type, token and other fields (the ones <see cref="EffectsSupport"/> lets through).</summary>
    public static bool FilterMatches(Game game, EffectContext context, Filter? filter, CardInstance instance)
    {
        if (filter is null) return true;
        if (filter.Relation == Relation.Friendly && instance.Controller != context.Controller) return false;
        if (filter.Relation == Relation.Enemy && instance.Controller == context.Controller) return false;
        if (filter.Type is { } wanted && game.CardOf(instance).Type != wanted) return false;
        if (filter.Token is { } token && instance.IsToken != token) return false;
        if (filter.Other == true && context.Source == instance.Id) return false;
        return true;
    }
```

In `src/CromoBound.Engine/Rules/Game.Targets.cs`, inside the `ChooseTargetsDecision` handler of `AskTargets`, replace:

```csharp
                var chosen = choose.Targets;
                if (chosen.Distinct().Count() != chosen.Count || chosen.Count < min || chosen.Count > max || !chosen.All(options.Contains))
                    return Reject(RejectionCode.InvalidTarget, $"Choose between {min} and {max} different targets among the offered ones.");
```

with:

```csharp
                var chosen = choose.Targets;
                if (CheckPick(chosen, options, min, max, "targets") is { } rejection) return rejection;
```

and add to the class:

```csharp
    /// <summary>Null when <paramref name="chosen"/> holds between min and max different ids, all among the options.</summary>
    internal static Rejection? CheckPick(IReadOnlyList<ObjectId> chosen, IReadOnlyList<ObjectId> options, int min, int max, string what) =>
        chosen.Distinct().Count() == chosen.Count && chosen.Count >= min && chosen.Count <= max && chosen.All(options.Contains)
            ? null
            : Reject(RejectionCode.InvalidTarget, $"Choose between {min} and {max} different {what} among the offered ones.");
```

In `src/CromoBound.Engine/Rules/Game.Mutations.cs`, add `using CromoBound.Models.Cards;` and, after `Recall`:

```csharp
    /// <summary>Puts a card on the bottom of its owner's deck: a rune's Rune Deck, any other card's Main Deck.</summary>
    internal void Recycle(ObjectId card)
    {
        var instance = State[card];
        var deck = CardOf(instance).Type == CardType.Rune ? Place.RuneDeck(instance.Owner) : Place.MainDeck(instance.Owner);
        MoveCard(card, deck, DeckPosition.Bottom);
    }
```

Create `src/CromoBound.Engine/Effects/Steps/ChoiceStepHandlers.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>The controller chooses a player the filter allows (Opponent, Self, or anyone when there is no filter), offered in
/// turn order. A single option is forced (spec §6.2); every choice is announced with <see cref="PlayerChosen"/>.</summary>
internal sealed class ChoosePlayerHandler : StepHandler<ChoosePlayerStep>
{
    private static readonly PlayerRef Everyone = new() { Kind = PlayerKind.EachPlayer };

    protected override StepOutcome Run(Game game, ResolveEffectTask task, ChoosePlayerStep step)
    {
        var chooser = task.Context.Controller;
        List<PlayerId> options = [.. PlayerResolver.Resolve(game, task.Context, Everyone).Where(p => Allowed(step.Filter, chooser, p))];
        if (options.Count == 0) return StepOutcome.DidNothing;
        if (task.Answer is PlayerId answered) return Chosen(task, answered);
        if (options.Count == 1)
        {
            game.Emit(new ChoiceMade(chooser, "Player", []));
            game.Emit(new PlayerChosen(chooser, options[0]));
            return Chosen(task, options[0]);
        }
        game.Ask(new ChoosePlayerDecision(chooser, task.Context.SourceCardId, options), (_, action) =>
        {
            if (action is not ChoosePlayer choice || !options.Contains(choice.Player))
                return Game.Reject(RejectionCode.UnexpectedAction, "Choose one of the offered players.");
            game.Emit(new PlayerChosen(chooser, choice.Player));
            task.Answer = choice.Player;
            return null;
        });
        return StepOutcome.Asked;
    }

    private static StepOutcome Chosen(ResolveEffectTask task, PlayerId player)
    {
        task.Result = new EffectVar([], [player], null, true);
        return StepOutcome.Done;
    }

    private static bool Allowed(Filter? filter, PlayerId chooser, PlayerId player) => filter?.Relation switch
    {
        Relation.Opponent => player != chooser,
        Relation.Self => player == chooser,
        _ => true,
    };
}

/// <summary>The controller chooses cards from a zone without targeting (spec §6.1): as many as the count, or all there are if fewer.
/// When the options are exactly that many the choice is forced.</summary>
internal sealed class ChooseCardHandler : StepHandler<ChooseCardStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, ChooseCardStep step)
    {
        var context = task.Context;
        List<ObjectId> options =
        [
            .. ZoneResolver.Resolve(game, context, step.From)
                .SelectMany(place => game.State.At(place))
                .Where(id => ObjectResolver.FilterMatches(game, context, step.Filter, game.State[id])),
        ];
        var count = Math.Min(ValueResolver.Resolve(game, context, step.Count), options.Count);
        if (count <= 0) return StepOutcome.DidNothing;
        if (task.Answer is IReadOnlyList<ObjectId> answered) return Chosen(task, answered);
        if (options.Count == count)
        {
            game.Emit(new ChoiceMade(context.Controller, "Cards", options));
            return Chosen(task, options);
        }
        game.Ask(new ChooseCardsDecision(context.Controller, context.SourceCardId, options, count, count), (_, action) =>
        {
            if (action is not ChooseCards choose) return Game.Reject(RejectionCode.UnexpectedAction, "Choose the cards.");
            if (Game.CheckPick(choose.Cards, options, count, count, "cards") is { } rejection) return rejection;
            List<ObjectId> picked = [.. choose.Cards];
            task.Answer = picked;
            return null;
        });
        return StepOutcome.Asked;
    }

    private static StepOutcome Chosen(ResolveEffectTask task, IReadOnlyList<ObjectId> cards)
    {
        task.Result = new EffectVar([.. cards], [], cards.Count, true);
        return StepOutcome.Done;
    }
}

/// <summary>"You may do this:" (spec §5.4). The controller answers yes or no; on yes the block becomes a new chain item with the
/// same context, so players get priority on it before it resolves.</summary>
internal sealed class OptionalHandler : StepHandler<OptionalStep>
{
    public const string Question = "Do the rest of the ability?";

    protected override StepOutcome Run(Game game, ResolveEffectTask task, OptionalStep step)
    {
        var context = task.Context;
        if (task.Answer is bool yes)
        {
            if (!yes) return StepOutcome.DidNothing;
            var parent = task.Item;
            game.AddAbilityItem(context.Controller, context.Source, context.SourceCardId, parent?.AbilityKind ?? AbilityKind.Triggered,
                parent?.Text, step.Steps, context);
            return StepOutcome.Done;
        }
        game.Ask(new OptionalDecision(context.Controller, context.SourceCardId, Question), (_, action) =>
        {
            if (action is not ChooseOptional choice) return Game.Reject(RejectionCode.UnexpectedAction, "Answer yes or no.");
            task.Answer = choice.Yes;
            return null;
        });
        return StepOutcome.Asked;
    }
}
```

In `src/CromoBound.Engine/Effects/Steps/CardStepHandlers.cs`, add these usings:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
```

and append:

```csharp
/// <summary>Predict 1 (spec §8.1, Vision): the player looks at the top card of their Main Deck (their own event has the card,
/// the public copy only the count) and may recycle it. After a manual action it looks again only if the top card changed.</summary>
internal sealed class PredictHandler : StepHandler<PredictStep>
{
    public const string Question = "Recycle the top card of your Main Deck?";

    protected override StepOutcome Run(Game game, ResolveEffectTask task, PredictStep step)
    {
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (players.Count != 1) return StepOutcome.DidNothing;
        var player = players[0];
        var deck = game.State.At(Place.MainDeck(player));
        if (deck.Count == 0) return StepOutcome.DidNothing;
        var top = deck[0];
        if (task.Answer is bool recycle && task.Progress is ObjectId seen && seen == top)
        {
            if (recycle) game.Recycle(top);
            task.Result = new EffectVar([], [player], 1, true);
            return StepOutcome.Done;
        }
        if (task.Progress is not ObjectId looked || looked != top)
        {
            task.Progress = top;
            game.Emit(new Predicted(player, 1, [game.State[top].CardId]) { VisibleTo = player });
            game.Emit(new Predicted(player, 1, null));
        }
        game.Ask(new OptionalDecision(player, task.Context.SourceCardId, Question), (_, action) =>
        {
            if (action is not ChooseOptional choice) return Game.Reject(RejectionCode.UnexpectedAction, "Answer yes or no.");
            task.Answer = choice.Yes;
            return null;
        });
        return StepOutcome.Asked;
    }
}
```

In `src/CromoBound.Engine/Effects/Steps/StepRegistry.cs`, add to the dictionary:

```csharp
        [typeof(ChoosePlayerStep)] = new ChoosePlayerHandler(),
        [typeof(ChooseCardStep)] = new ChooseCardHandler(),
        [typeof(OptionalStep)] = new OptionalHandler(),
        [typeof(PredictStep)] = new PredictHandler(),
```

Replace `src/CromoBound.Engine/Effects/EffectsSupport.cs` with:

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
        for (var i = 0; i < file.Abilities.Count; i++) CheckAbility(file.Abilities[i], $"abilities[{i}]", problems);
        return problems;
    }

    private static void CheckAbility(Ability ability, string at, List<string> problems)
    {
        if (ability is not SpellAbility spell)
        {
            problems.Add($"{at}: {ability.GetType().Name.Replace("Ability", "")} ability");
            return;
        }
        if (spell.Condition is not null || spell.ActiveIn is not null || spell.Script is not null)
            problems.Add($"{at}: condition, activeIn or script");
        CheckSteps(spell.Steps, at, problems, targets: true);
    }

    private static void CheckSteps(IReadOnlyList<Step> steps, string at, List<string> problems, bool targets)
    {
        for (var j = 0; j < steps.Count; j++) CheckStep(steps[j], $"{at}.steps[{j}]", problems, targets);
    }

    /// <summary><paramref name="targets"/>: target selectors are allowed (a spell's steps, chosen while playing); elsewhere only Self.</summary>
    private static void CheckStep(Step step, string at, List<string> problems, bool targets)
    {
        if (!StepRegistry.Supports(step.GetType()))
        {
            problems.Add($"{at}: step {step.GetType().Name.Replace("Step", "")}");
            return;
        }
        if (step.Script is not null || step.Chooser is not null) problems.Add($"{at}: script or chooser");
        if (step.Player is { Kind: null, Var: null }) problems.Add($"{at}: player");
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
            case ChannelStep channel:
                CheckValue(channel.Count, at, problems);
                break;
            case GainXpStep xp:
                CheckValue(xp.Amount, at, problems);
                break;
            case ChoosePlayerStep choose:
                if (choose.Filter is not null && !IsPlayerFilter(choose.Filter)) problems.Add($"{at}: filter");
                break;
            case ChooseCardStep card:
                if (card.From is not { Zone: Zone.Trash, Position: null } || card.From.Owner is { Kind: null, Var: null })
                    problems.Add($"{at}: from");
                if (!IsSupportedFilter(card.Filter)) problems.Add($"{at}: filter");
                CheckValue(card.Count, at, problems);
                break;
            case OptionalStep optional:
                if (optional.Reflexive != true || optional.Cost is not null) problems.Add($"{at}: optional");
                CheckSteps(optional.Steps, at, problems, targets: false);
                break;
            case PredictStep predict:
                if (predict.Amount.Literal != 1) problems.Add($"{at}: value");
                break;
        }
        if (step is TargetStep target && !IsSupportedTarget(target.Target, targets)) problems.Add($"{at}: target");
    }

    private static void CheckValue(Value value, string at, List<string> problems)
    {
        if (value.Literal is null) problems.Add($"{at}: value");
    }

    private static bool IsSupportedTarget(ObjectRef reference, bool targets) =>
        reference.Ref == RefKind.Self || (targets && TargetSlots.IsTarget(reference) && IsSupportedFilter(reference.Filter));

    /// <summary>Card filters may use relation (Friendly or Enemy), type, token and other; nothing else yet.</summary>
    private static bool IsSupportedFilter(Filter? filter) =>
        filter is null || (filter.Relation is null or Relation.Friendly or Relation.Enemy && HasOnlyBasicFields(filter));

    /// <summary>A ChoosePlayer filter: a relation of Opponent or Self and nothing else.</summary>
    private static bool IsPlayerFilter(Filter filter) =>
        filter.Relation is Relation.Opponent or Relation.Self && filter.Type is null && filter.Token is null && filter.Other is null
        && HasOnlyBasicFields(filter);

    /// <summary>None of the fields beyond relation, type, token and other is set.</summary>
    private static bool HasOnlyBasicFields(Filter filter) =>
        filter.Controller is null && filter.Owner is null && filter.Location is null && filter.Zone is null
        && filter.Supertype is null && filter.Tags.Count == 0 && filter.Domains.Count == 0 && filter.Name is null
        && filter.Might is null && filter.EnergyCost is null && filter.Status.Count == 0 && filter.Mighty is null
        && filter.Keyword is null && filter.Not is null;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): add player, card, optional and predict steps"
```

---

### Task 4: Playing a card from an effect

**Files:**
- Modify: `src/CromoBound.Engine/Rules/Game.Play.cs`
- Create: `src/CromoBound.Engine/Effects/Steps/PlayStepHandler.cs`
- Modify: `src/CromoBound.Engine/Effects/Steps/StepRegistry.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Test: `tests/CromoBound.Engine.Tests/EffectPlayTests.cs`

**Interfaces:**
- Consumes: Task 1's `CardPlayed`; Task 2's `ResolveEffectTask.Answer`; Task 3's `ChooseCardHandler`.
- Produces:
  - `PlayCardTask.IgnoreCost` (`init`), `PlayCardTask.Finished` and `PlayCardTask.Played` (`ObjectId?`, the card where it ended up).
  - Every finalized play emits `CardPlayed(card, cardId, player)` (Task 5's watcher maps it to `Played`).
  - A handler for `Models.Effects.PlayStep`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/EffectPlayTests.cs` (it doesn't import `CromoBound.Engine.Rules`, so `PlayStep` is the effects step):

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class EffectPlayTests
{
    private static readonly ZoneRef OpponentsTrash = new() { Zone = Zone.Trash, Owner = new PlayerRef { Kind = PlayerKind.Opponent } };

    /// <summary>Kharox's block: choose a unit in the opponent's trash, play it with the given cost mode, then draw 1.</summary>
    private static Step[] PickAndPlay(PlayCostMode? cost) =>
    [
        new ChooseCardStep { From = OpponentsTrash, Filter = new Filter { Type = CardType.Unit }, Store = "picked" },
        new PlayStep { Card = ObjectRef.Variable("picked"), Cost = cost, Store = "played" },
        new DrawStep { Amount = 1 },
    ];

    private static EffectContext Context() => new() { Controller = P1, SourceCardId = "spell" };

    [Fact]
    public void A_unit_from_the_opponents_trash_is_played_ignoring_its_cost()
    {
        var game = new TestGame();
        game.Put("unit-3", Place.Trash(P2));
        var engine = game.Start();
        var hand = game.State.At(Place.Hand(P1)).Count;
        var context = Context();

        var events = engine.RunNow(new ResolveEffectTask(context, PickAndPlay(PlayCostMode.IgnoreAll), _ => { }));

        var unit = game.First(Place.Base(P1), "unit-3");
        Assert.Equal(P1, game.State[unit].Controller);
        Assert.Equal(P2, game.State[unit].Owner);
        Assert.True(game.State[unit].Exhausted);
        Assert.Equal(new[] { unit }, context.Vars["played"].Objects);
        Assert.Contains(events, e => e is CardPlayed { CardId: "unit-3", Controller.Index: 0 });
        Assert.Equal(hand + 1, game.State.At(Place.Hand(P1)).Count);
        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void A_unit_played_ignoring_its_cost_can_still_be_accelerated()
    {
        var game = new TestGame();
        game.Put("accel-3", Place.Trash(P2));
        game.Runes(P1, "fury-rune", 2);
        var engine = game.Start();

        engine.RunNow(new ResolveEffectTask(Context(), PickAndPlay(PlayCostMode.IgnoreAll), _ => { }));

        Assert.True(engine.Decision<PlayChoicesDecision>().AccelerateAvailable);
        engine.Accept(P1, new ChoosePlayOptions(Place.Base(P1), true));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(1, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Self }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);
        Assert.False(game.State[game.First(Place.Base(P1), "accel-3")].Exhausted);
    }

    [Fact]
    public void A_play_from_an_effect_without_a_cost_mode_pays_the_cost()
    {
        var game = new TestGame();
        game.Put("unit-2", Place.Trash(P2));
        game.Runes(P1, "fury-rune", 2);
        var engine = game.Start();

        engine.RunNow(new ResolveEffectTask(Context(), PickAndPlay(null), _ => { }));

        Assert.Equal(2, engine.Decision<PayCostDecision>().Cost.Energy);
        engine.PayWithSuggestion(P1);
        Assert.Equal(P1, game.State[game.First(Place.Base(P1), "unit-2")].Controller);
    }

    [Fact]
    public void Cancelling_a_play_from_an_effect_puts_the_card_back_and_the_effect_goes_on()
    {
        var game = new TestGame();
        game.Put("accel-3", Place.Trash(P2));
        var engine = game.Start();
        var hand = game.State.At(Place.Hand(P1)).Count;
        var context = Context();
        engine.RunNow(new ResolveEffectTask(context, PickAndPlay(PlayCostMode.IgnoreAll), _ => { }));
        engine.Decision<PlayChoicesDecision>();

        engine.Accept(P1, new CancelPlay());

        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "accel-3");
        Assert.False(context.Vars["played"].Happened);
        Assert.Equal(hand + 1, game.State.At(Place.Hand(P1)).Count);
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void A_stored_card_that_is_gone_or_already_in_play_is_not_played()
    {
        var game = new TestGame();
        var inPlay = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        var context = Context();
        context.Vars["gone"] = new EffectVar([new ObjectId(999)], [], 1, true);
        context.Vars["here"] = new EffectVar([inPlay], [], 1, true);

        engine.RunNow(new ResolveEffectTask(context,
        [
            new PlayStep { Card = ObjectRef.Variable("gone"), Store = "first" },
            new PlayStep { Card = ObjectRef.Variable("here"), Store = "second" },
        ], _ => { }));

        Assert.False(context.Vars["first"].Happened);
        Assert.False(context.Vars["second"].Happened);
        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~EffectPlayTests"`
Expected: FAIL (the tests build, but `PlayStep` has no handler, so `StepRegistry.For` throws `KeyNotFoundException`).

- [ ] **Step 3: Write the implementation**

In `src/CromoBound.Engine/Rules/Game.Play.cs`, add to `PlayCardTask`:

```csharp
    /// <summary>Played by an effect "ignoring its cost" (spec §5.5): the base cost is skipped like a hidden play's; Accelerate still costs.</summary>
    public bool IgnoreCost { get; init; }

    /// <summary>Set when the play finalized; <see cref="Played"/> is the card where it ended up (the board, or the chain for a spell).</summary>
    public bool Finished { get; set; }
    public ObjectId? Played { get; set; }
```

In `RunPlay`, replace the `PlayStep.Cost` case with:

```csharp
                case PlayStep.Cost:
                    task.Cost = Payment.CostOf(CardOf(task.Item!.Card!.Value), task.FromHidden || task.IgnoreCost, task.Item.Accelerate);
                    task.Step = task.IgnoreCost && task.Cost.Energy == 0 && task.Cost.Power.Count == 0 ? PlayStep.Finalize : PlayStep.Pay;
                    break;
```

Replace `FinishFinalizing` with:

```csharp
    /// <summary>CR 359: a permanent leaves the chain and enters the board (a unit exhausted unless Accelerated, gear ready);
    /// a spell stays on the chain, finalized, and its controller gets priority (CR 337.4). Either way the play is announced.</summary>
    private void FinishFinalizing(PlayCardTask task)
    {
        var item = task.Item!;
        var card = CardOf(item.Card!.Value);
        item.Status = ChainItemStatus.Finalized;
        ChainPasses = 0;
        task.Finished = true;
        if (card.Type is CardType.Unit or CardType.Gear)
        {
            State.Chain.Remove(item);
            var id = MoveCard(item.Card.Value, item.Location!.Value)!.Value;
            State[id].Controller = task.Player;
            if (card.Type == CardType.Unit && !item.Accelerate) SetStatus(id, StatusKind.Exhausted, true);
            task.Played = id;
            Emit(new CardPlayed(id, card.Id, task.Player));
            Emit(new ChainItemResolved(item.Id));
            AfterResolution();
            return;
        }
        task.Played = item.Card;
        Emit(new CardPlayed(item.Card.Value, card.Id, task.Player));
        State.Turn.Priority = item.Controller;
    }
```

Create `src/CromoBound.Engine/Effects/Steps/PlayStepHandler.cs`:

```csharp
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using EffectPlayStep = CromoBound.Models.Effects.PlayStep;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>Plays a stored card (spec §5.5) through 2a's <see cref="PlayCardTask"/>, from wherever it is, for its cost or ignoring it.
/// The step asks nothing itself: it starts the play, waits until the play task is done, then runs again. A cancelled play did nothing.
/// The play task starts as started, so cleanups and triggers wait for the whole resolution (CR 321).</summary>
internal sealed class PlayHandler : StepHandler<EffectPlayStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, EffectPlayStep step)
    {
        if (task.Answer is PlayCardTask running)
        {
            if (!running.Finished) return StepOutcome.DidNothing;
            task.Result = new EffectVar([running.Played!.Value], [], null, true);
            return StepOutcome.Done;
        }
        List<ObjectId> cards =
        [
            .. ObjectResolver.Resolve(game, task.Context, step.Card)
                .Where(id => !game.State[id].Place.IsLocation && game.State[id].Place.Kind != PlaceKind.Chain),
        ];
        if (cards.Count == 0) return StepOutcome.DidNothing;
        var play = new PlayCardTask(task.Context.Controller, cards[0]) { IgnoreCost = step.Cost == PlayCostMode.IgnoreAll, Started = true };
        game.Push(play);
        task.Answer = play;
        return StepOutcome.Asked;
    }
}
```

In `src/CromoBound.Engine/Effects/Steps/StepRegistry.cs`, add to the dictionary:

```csharp
        [typeof(PlayStep)] = new PlayHandler(),
```

In `src/CromoBound.Engine/Effects/EffectsSupport.cs`, add this arm to the `switch (step)` in `CheckStep`:

```csharp
            case PlayStep play:
                if (play.Card.Var is null || play.From is not null || play.For is not null || play.Location is not null
                    || play.Exhausted is not null || play.Cost is not (null or PlayCostMode.IgnoreAll))
                    problems.Add($"{at}: play");
                break;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): play cards from effects"
```

---

### Task 5: Triggered abilities go on the chain; Deathknell, Vision, Hunt

**Files:**
- Create: `src/CromoBound.Engine/Effects/TriggerWatcher.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.cs`
- Create: `src/CromoBound.Engine/Rules/Game.Triggers.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Chain.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.TurnPoints.cs`
- Modify: `src/CromoBound.Engine/Effects/CardEffects.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Modify: `tests/CromoBound.Engine.Tests/TestGame.cs`
- Modify: `tests/CromoBound.Engine.Tests/CardEffectsTests.cs`
- Modify: `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`
- Test: `tests/CromoBound.Engine.Tests/TriggerTests.cs`

**Interfaces:**
- Consumes: Task 1's `OrderTriggersDecision`, `TriggerOption`, `OrderTriggers`, `TriggerAdded`, `CardPlayed`; Task 2's `AddAbilityItem` and automatic ability resolution; Task 4's `CardPlayed` emission.
- Produces:
  - `PendingTrigger(PlayerId Controller, ObjectId Source, string SourceCardId, TriggeredAbility Ability)` and `TriggerWatcher.Collect(Game, GameEvent) : List<PendingTrigger>`.
  - `Game.PendingTriggers` (internal), fed by `Emit` for every public event.
  - `PutTriggersOnChainTask` and `Game.PutTriggersOnChain() : bool` (internal).
  - `Game.AbilityText(string cardId, Ability ability) : string?` (internal), also used by Task 6.
  - `Game.HandledBy(CardInstance)` becomes internal.
  - `CardEffects` adds the keyword abilities after the file's own (`Deathknell`, `Vision`, `Hunt`; Task 6 adds `Empower`).
  - `TestGame(ulong seed = 1, CardDatabase? db = null, string firstBattlefield = "bf-a", string secondBattlefield = "bf-b")`.

- [ ] **Step 1: Write the failing tests**

In `tests/CromoBound.Engine.Tests/TestGame.cs`, replace the constructor with:

```csharp
    /// <summary>Two battlefields, the first owned by P1 and the second by P2. A real battlefield card can replace a test one.</summary>
    public TestGame(ulong seed = 1, CardDatabase? db = null, string firstBattlefield = "bf-a", string secondBattlefield = "bf-b")
    {
        Db = db ?? EngineTestDb.Create();
        State = new GameState(2, new SeededRandom(seed));
        AddBattlefield(firstBattlefield, P1);
        AddBattlefield(secondBattlefield, P2);
    }
```

Create `tests/CromoBound.Engine.Tests/TriggerTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class TriggerTests
{
    /// <summary>unit-3 with Deathknell: draw 1.</summary>
    private const string DrawOnDeath = """
        { "cardId": "unit-3", "status": "Full", "keywords": [ { "keyword": "Deathknell", "steps": [ { "action": "Draw", "amount": 1 } ] } ] }
        """;

    private const string KillThenDraw = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1, "steps": [
          { "action": "Kill", "target": { "select": "Unit", "count": 1 } }, { "action": "Draw", "amount": 1 } ] } ] }
        """;

    /// <summary>The priority holder passes, then the other player: the newest chain item resolves.</summary>
    private static SubmitResult BothPass(Game engine)
    {
        var first = engine.Decision<PriorityDecision>().Player;
        engine.Accept(first, new Pass());
        return engine.Accept(new PlayerId(1 - first.Index), new Pass());
    }

    [Fact]
    public void Soaring_scout_channels_a_rune_exhausted_when_it_dies()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("soaring-scout"));
        for (var i = 0; i < 3; i++) game.Put("fury-rune", Place.RuneDeck(P1));
        var scout = game.Put("soaring-scout", Place.Base(P1));
        var engine = game.Start();

        Assert.True(engine.SubmitManual(P1, new ManualDamage(scout, 1)).Accepted);

        var trigger = Assert.Single(game.State.Chain);
        Assert.Equal("soaring-scout", trigger.SourceCardId);
        Assert.Equal(AbilityKind.Triggered, trigger.AbilityKind);
        Assert.Equal(P1, trigger.Controller);
        BothPass(engine);
        Assert.Equal(3, engine.RunesOf(P1).Count);
        Assert.Equal(1, engine.RunesOf(P1).Count(r => r.Exhausted));
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void Mystic_poro_predicts_when_played()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("mystic-poro"));
        game.Put("mystic-poro", Place.Hand(P1));
        game.Runes(P1, "chaos-rune", 2);
        var marked = game.Put("unit-3", Place.Trash(P1));
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "mystic-poro")));
        engine.PayWithSuggestion(P1);

        Assert.Equal("mystic-poro", Assert.Single(game.State.Chain).SourceCardId);
        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(marked, Place.MainDeck(P1))).Accepted);
        var result = BothPass(engine);

        Assert.Equal(new[] { "unit-3" }, Assert.Single(result.Events.OfType<Predicted>(), p => p.VisibleTo == P1).CardIds);
        engine.Decision<OptionalDecision>();
        engine.Accept(P1, new ChooseOptional(true));
        Assert.Equal("unit-3", game.State[game.State.At(Place.MainDeck(P1))[^1]].CardId);
    }

    [Fact]
    public void Voracious_gromp_gains_3_xp_when_its_controller_holds_its_battlefield()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("voracious-gromp"));
        game.Put("voracious-gromp", Place.Battlefield(0));
        game.State.Battlefields[0].Controller = P1;
        var engine = game.Start();

        Assert.Equal("voracious-gromp", Assert.Single(game.State.Chain).SourceCardId);
        BothPass(engine);
        Assert.Equal(3, game.State.Player(P1).Xp);
        Assert.Equal(1, game.State.Player(P1).Points);
    }

    [Fact]
    public void Voracious_gromp_gains_3_xp_when_its_controller_conquers_its_battlefield()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("voracious-gromp"));
        game.Put("voracious-gromp", Place.Battlefield(0));
        game.State.Battlefields[0].Controller = P1;
        var engine = game.Start(first: P2);

        engine.RunNow(new StepTask(g => g.Score(P1, 0, ScoreKind.Conquer)));

        Assert.Equal("voracious-gromp", Assert.Single(game.State.Chain).SourceCardId);
        BothPass(engine);
        Assert.Equal(3, game.State.Player(P1).Xp);
    }

    [Fact]
    public void Shadow_temple_burns_3_when_you_hold_it()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("shadow-temple"), firstBattlefield: "shadow-temple");
        game.Put("unit-2", Place.Battlefield(0));
        game.State.Battlefields[0].Controller = P1;
        var engine = game.Start();

        var trigger = Assert.Single(game.State.Chain);
        Assert.Equal("shadow-temple", trigger.SourceCardId);
        Assert.Equal(P1, trigger.Controller);
        Assert.StartsWith("When you hold here", trigger.Text);
        BothPass(engine);
        Assert.Equal(3, game.State.At(Place.Trash(P1)).Count);
    }

    [Fact]
    public void A_player_orders_their_own_simultaneous_triggers()
    {
        var game = new TestGame(db: EngineTestDb.Create(("unit-3", DrawOnDeath)));
        var first = game.Put("unit-3", Place.Base(P1));
        var second = game.Put("unit-3", Place.Base(P1));
        game.State[first].Damage = 3;
        game.State[second].Damage = 3;
        var engine = game.Start();

        var order = engine.Decision<OrderTriggersDecision>();
        Assert.Equal(new[] { first, second }, order.Triggers.Select(t => t.Source));
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new OrderTriggers { Order = [0, 0] }).Rejection!.Code);
        engine.Accept(P1, new OrderTriggers { Order = [1, 0] });

        Assert.Equal(new ObjectId?[] { second, first }, game.State.Chain.Select(i => i.Source));
    }

    [Fact]
    public void The_turn_players_triggers_go_on_the_chain_first_so_the_opponents_resolve_first()
    {
        var game = new TestGame(db: EngineTestDb.Create(("unit-3", DrawOnDeath)));
        var mine = game.Put("unit-3", Place.Base(P1));
        var theirs = game.Put("unit-3", Place.Base(P2));
        game.State[mine].Damage = 3;
        game.State[theirs].Damage = 3;
        var engine = game.Start();

        Assert.Equal(new[] { P1, P2 }, game.State.Chain.Select(i => i.Controller));
        var hand = game.State.At(Place.Hand(P2)).Count;
        BothPass(engine);
        Assert.Equal(hand + 1, game.State.At(Place.Hand(P2)).Count);
        Assert.Equal(P1, Assert.Single(game.State.Chain).Controller);
    }

    [Fact]
    public void A_trigger_waits_until_the_resolution_that_caused_it_has_finished()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", KillThenDraw), ("unit-3", DrawOnDeath)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);
        var hand = game.State.At(Place.Hand(P1)).Count;

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Equal(hand + 1, game.State.At(Place.Hand(P1)).Count);
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "spell");
        Assert.Equal(P2, Assert.Single(game.State.Chain).Controller);
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
    }
}
```

In `tests/CromoBound.Engine.Tests/CardEffectsTests.cs`:
- In `A_file_the_engine_cant_run_yet_falls_back_to_unmapped_and_says_why`, a trigger with no subject is now named precisely; change the expected problems to `new[] { "keyword Shield", "abilities[0]: trigger" }`.
- Add these tests:

```csharp
    [Fact]
    public void Keywords_stand_for_triggered_abilities_listed_after_the_files_own()
    {
        var db = EngineTestDb.Create(("unit-3", """
            { "cardId": "unit-3", "status": "Full",
              "keywords": [ { "keyword": "Vision" }, { "keyword": "Hunt", "value": 2 },
                            { "keyword": "Deathknell", "steps": [ { "action": "Draw", "amount": 1 } ] } ],
              "abilities": [ { "kind": "Triggered", "trigger": { "event": "Dies", "subject": { "ref": "Self" } },
                               "steps": [ { "action": "Burn", "amount": 1 } ] } ] }
            """));

        var info = new CardEffects(db).For("unit-3");
        var abilities = info.Abilities.Cast<TriggeredAbility>().ToList();

        Assert.Equal(MappingStatus.Full, info.Status);
        Assert.Equal(
            new[] { TriggerEvent.Dies, TriggerEvent.Played, TriggerEvent.Conquer, TriggerEvent.Hold, TriggerEvent.Dies },
            abilities.Select(a => a.Trigger.Event));
        Assert.All(abilities, a => Assert.Equal(RefKind.Self, a.Trigger.Subject?.Ref));
        Assert.IsType<PredictStep>(Assert.Single(abilities[1].Steps));
        Assert.Equal(2, Assert.IsType<GainXpStep>(Assert.Single(abilities[3].Steps)).Amount.Literal);
        Assert.IsType<DrawStep>(Assert.Single(abilities[4].Steps));
    }

    [Theory]
    [InlineData("""{ "event": "Dies" }""")]
    [InlineData("""{ "event": "Attack", "subject": { "ref": "Self" } }""")]
    [InlineData("""{ "event": "Played", "by": "You", "where": { "ref": "Here" } }""")]
    public void Unsupported_triggers_are_named(string trigger)
    {
        var file = CromoJson.Deserialize<EffectsFile>(
            $$"""{ "cardId": "unit-3", "status": "Full", "abilities": [ { "kind": "Triggered", "trigger": {{trigger}}, "steps": [] } ] }""");

        Assert.Equal(new[] { "abilities[0]: trigger" }, EffectsSupport.Problems(file));
    }

    [Fact]
    public void A_triggered_ability_cant_have_target_selectors_yet()
    {
        var file = CromoJson.Deserialize<EffectsFile>("""
            { "cardId": "unit-3", "status": "Full", "abilities": [ { "kind": "Triggered",
              "trigger": { "event": "Dies", "subject": { "ref": "Self" } },
              "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1 } } ] } ] }
            """);

        Assert.Equal(new[] { "abilities[0].steps[0]: target" }, EffectsSupport.Problems(file));
    }
```

In `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`, replace both theories' data with:

```csharp
    [Theory]
    [InlineData("progress-day")]
    [InlineData("falling-star")]
    [InlineData("vengeance")]
    [InlineData("vanguard-sergeant")]
    [InlineData("horns-of-the-dragon")]
    [InlineData("token-sprite")]
    [InlineData("shadow-temple")]
    [InlineData("soaring-scout")]
    [InlineData("mystic-poro")]
    [InlineData("voracious-gromp")]
    public void Cards_the_engine_runs_today_are_full(string cardId) => Assert.Equal(MappingStatus.Full, Real.For(cardId).Status);

    [Theory]
    [InlineData("kharox", "keyword Empower")]
    [InlineData("garbage-grabber", "Activated ability")]
    [InlineData("noxus-hopeful", "Passive ability")]
    [InlineData("daring-poro", "keyword Assault")]
    [InlineData("jeweled-colossus", "keyword Shield")]
```

(the second theory's method stays as it is).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~TriggerTests|FullyQualifiedName~CardEffectsTests|FullyQualifiedName~EffectsDataTests"`
Expected: FAIL (the trigger tests find no chain item and no `OrderTriggersDecision`; the keyword expansion and data rows fail).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Engine/Effects/TriggerWatcher.cs`:

```csharp
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>A triggered ability waiting to go on the chain (spec §4.6). <see cref="Source"/> may have left play (Deathknell).</summary>
internal sealed record PendingTrigger(PlayerId Controller, ObjectId Source, string SourceCardId, TriggeredAbility Ability);

/// <summary>Maps engine events to trigger events (spec §4.6) and finds the abilities they trigger. Pure: reads the game, changes
/// nothing. It runs as each event is emitted, so a dying unit (UnitDied comes before the move) is still there to be read.</summary>
internal static class TriggerWatcher
{
    public static List<PendingTrigger> Collect(Game game, GameEvent gameEvent) => gameEvent switch
    {
        UnitDied died => Own(game, died.Unit, died.Controller, TriggerEvent.Dies),
        StatusChanged { Status: StatusKind.Empowered, Value: true } empowered =>
            Own(game, empowered.Object, game.State[empowered.Object].Controller, TriggerEvent.BecameEmpowered),
        CardPlayed played => Own(game, played.Card, played.Controller, TriggerEvent.Played),
        BattlefieldScored scored => Scored(game, scored),
        _ => [],
    };

    /// <summary>The object's abilities that trigger on its own event ("subject": Self).</summary>
    private static List<PendingTrigger> Own(Game game, ObjectId id, PlayerId controller, TriggerEvent kind) =>
        !game.State.Exists(id) ? [] :
        [
            .. Triggered(game, id, kind)
                .Where(a => a.Trigger.Subject?.Ref == RefKind.Self)
                .Select(a => new PendingTrigger(controller, id, game.State[id].CardId, a)),
        ];

    /// <summary>Hold and Conquer: the battlefield's own "when you hold (or conquer) here", when its controller is the scorer,
    /// and the scorer's units there whose own trigger it is (Hunt).</summary>
    private static List<PendingTrigger> Scored(Game game, BattlefieldScored scored)
    {
        var kind = scored.Kind == ScoreKind.Hold ? TriggerEvent.Hold : TriggerEvent.Conquer;
        var card = game.State.Battlefields[scored.Battlefield].Card;
        var found = new List<PendingTrigger>();
        if (game.HandledBy(game.State[card]) == scored.Player)
            found.AddRange(Triggered(game, card, kind)
                .Where(a => a.Trigger is { By.Kind: PlayerKind.You, Where.Ref: RefKind.Here })
                .Select(a => new PendingTrigger(scored.Player, card, game.State[card].CardId, a)));
        foreach (var unit in game.UnitsAt(Place.Battlefield(scored.Battlefield)).Where(u => u.Controller == scored.Player))
            found.AddRange(Own(game, unit.Id, unit.Controller, kind));
        return found;
    }

    private static IEnumerable<TriggeredAbility> Triggered(Game game, ObjectId id, TriggerEvent kind) =>
        game.Effects.For(game.State[id].CardId).Abilities.OfType<TriggeredAbility>().Where(a => a.Trigger.Event == kind);
}
```

In `src/CromoBound.Engine/Rules/Game.cs`:
- Add after the `Effects` property:

```csharp
    /// <summary>Triggered abilities that fired and wait to go on the chain (spec §5.2).</summary>
    internal List<PendingTrigger> PendingTriggers { get; } = [];
```

- Replace `Emit` with:

```csharp
    /// <summary>Records an event; every public one passes through the <see cref="TriggerWatcher"/> (spec §4.6).</summary>
    internal void Emit(GameEvent gameEvent)
    {
        gameEvent.Sequence = _nextSequence++;
        _events.Add(gameEvent);
        if (gameEvent.VisibleTo is null) PendingTriggers.AddRange(TriggerWatcher.Collect(this, gameEvent));
    }
```

- Replace `RunLoop` with:

```csharp
    /// <summary>Handle outstanding tasks, then the chain, the showdown, the Main phase (CR 334-336).
    /// A turn-structure task (<see cref="GameTask.WaitsForNeutralOpen"/>) waits while a chain or showdown is running,
    /// even after it started. A cleanup, then the pending triggers, run between tasks or while such a task is paused on its
    /// decision; never in the middle of another started task or of a hand resolution (CR 321, spec §5.2).</summary>
    private void RunLoop()
    {
        while (Outcome is null && Pending is null)
        {
            var head = _tasks.Count > 0 ? _tasks[0] : null;
            var paused = head is { WaitsForNeutralOpen: true } && (IsClosed || State.Showdown is not null);
            var between = !ResolvingManually && (head is null || !head.Started || head.WaitsForNeutralOpen);
            if (_cleanupNeeded && between)
            {
                _cleanupNeeded = false;
                Push(new CleanupTask(CleanupMode.Normal));
                continue;
            }
            if (PendingTriggers.Count > 0 && between && head is not PutTriggersOnChainTask)
            {
                Push(new PutTriggersOnChainTask());
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

Create `src/CromoBound.Engine/Rules/Game.Triggers.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Rules;

/// <summary>Puts the pending triggered abilities on the chain (spec §5.2). It reads <see cref="Game.PendingTriggers"/> each time it
/// runs, so a rerun after a manual action also sees triggers that fired meanwhile.</summary>
internal sealed class PutTriggersOnChainTask : GameTask
{
    public override bool Run(Game game) => game.PutTriggersOnChain();
}

public sealed partial class Game
{
    /// <summary>The turn player's triggers go on the chain first, then the opponent's, so the opponent's resolve first. A player
    /// with several orders them; one is put on directly. Returns false while a player is ordering.</summary>
    internal bool PutTriggersOnChain()
    {
        foreach (var player in TurnOrder())
        {
            var mine = PendingTriggers.Where(t => t.Controller == player).ToList();
            if (mine.Count == 0) continue;
            if (mine.Count == 1)
            {
                AddTriggers(mine);
                continue;
            }
            List<TriggerOption> options = [.. mine.Select(t => new TriggerOption(t.Source, t.SourceCardId, AbilityText(t.SourceCardId, t.Ability)))];
            Ask(new OrderTriggersDecision(player, options), (_, action) =>
            {
                if (action is not OrderTriggers order) return Reject(RejectionCode.UnexpectedAction, "Choose the order the triggers go on the chain.");
                if (order.Order.Count != mine.Count || order.Order.Distinct().Count() != mine.Count || order.Order.Any(i => i < 0 || i >= mine.Count))
                    return Reject(RejectionCode.UnexpectedAction, $"List each of the {mine.Count} triggers once, by its index.");
                AddTriggers([.. order.Order.Select(i => mine[i])]);
                return null;
            });
            return false;
        }
        return true;
    }

    /// <summary>Each trigger becomes a finalized ability item; a chain they start doesn't pass focus when it closes (CR 346.1).</summary>
    private void AddTriggers(List<PendingTrigger> triggers)
    {
        foreach (var trigger in triggers)
        {
            PendingTriggers.Remove(trigger);
            if (State.Chain.Count == 0) ChainStartedByTrigger = true;
            var context = new EffectContext { Controller = trigger.Controller, Source = trigger.Source, SourceCardId = trigger.SourceCardId };
            var item = AddAbilityItem(trigger.Controller, trigger.Source, trigger.SourceCardId, AbilityKind.Triggered,
                AbilityText(trigger.SourceCardId, trigger.Ability), trigger.Ability.Steps, context);
            Emit(new TriggerAdded(item.Id, trigger.Source, trigger.Controller));
        }
    }
}
```

In `src/CromoBound.Engine/Rules/Game.Chain.cs`, add after `AddAbilityItem`:

```csharp
    /// <summary>The card text lines an ability implements, or null when its file names none (the abilities keywords stand for).</summary>
    internal string? AbilityText(string cardId, Ability ability)
    {
        if (ability.Line is not { } line) return null;
        var lines = RichText.Lines(Db.Cards[cardId].Text.Rich);
        return string.Join("<br />", line.Lines.Where(n => n >= 1 && n <= lines.Count).Select(n => lines[n - 1]));
    }
```

In `src/CromoBound.Engine/Rules/Game.TurnPoints.cs`, make `HandledBy` internal:

```csharp
    internal PlayerId HandledBy(CardInstance instance) =>
```

In `src/CromoBound.Engine/Effects/CardEffects.cs`, in `Build`, replace the last four lines (from `IReadOnlySet<DisplayKeyword> keywords` to the Partial `return`) with:

```csharp
        IReadOnlySet<DisplayKeyword> keywords = file.Keywords.Select(k => Enum.Parse<DisplayKeyword>(k.Keyword.ToString())).ToHashSet();
        IReadOnlyList<Ability> abilities = [.. file.Abilities, .. KeywordAbilities(file)];
        if (file.Status == MappingStatus.Full) return new(MappingStatus.Full, abilities, keywords, [], []);
        var covered = file.Abilities.Where(a => a.Line is not null).SelectMany(a => a.Line!.Lines).ToHashSet();
        List<int> manual = [.. all.Where(n => !covered.Contains(n) && !CardKeywords.IsKeywordLine(lines[n - 1]))];
        return new(MappingStatus.Partial, abilities, keywords, manual, []);
```

and add to the class:

```csharp
    /// <summary>The abilities keywords stand for (spec §8.1), listed after the file's own so ability indices follow the JSON.</summary>
    private static IEnumerable<Ability> KeywordAbilities(EffectsFile file)
    {
        foreach (var entry in file.Keywords)
        {
            switch (entry.Keyword)
            {
                case MechanicalKeyword.Deathknell:
                    yield return OwnTrigger(TriggerEvent.Dies, entry.Steps);
                    break;
                case MechanicalKeyword.Vision:
                    yield return OwnTrigger(TriggerEvent.Played, [new PredictStep()]);
                    break;
                case MechanicalKeyword.Hunt:
                    IReadOnlyList<Step> gain = [new GainXpStep { Amount = entry.Value!.Value }];
                    yield return OwnTrigger(TriggerEvent.Conquer, gain);
                    yield return OwnTrigger(TriggerEvent.Hold, gain);
                    break;
            }
        }
    }

    /// <summary>"When I ...": a trigger on the card's own event.</summary>
    private static TriggeredAbility OwnTrigger(TriggerEvent kind, IReadOnlyList<Step> steps) =>
        new() { Trigger = new Trigger { Event = kind, Subject = ObjectRef.Self }, Steps = steps };
```

In `src/CromoBound.Engine/Effects/EffectsSupport.cs`:
- Change the `Keywords` summary to "Keywords the engine runs: those the 2a rules core enforces and the keyword abilities of Plan E. The others arrive with Plan F." and add `MechanicalKeyword.Deathknell, MechanicalKeyword.Vision, MechanicalKeyword.Hunt,` to the set.
- In `Problems`, replace the keyword `foreach` with:

```csharp
        for (var k = 0; k < file.Keywords.Count; k++) CheckKeyword(file.Keywords[k], $"keywords[{k}]", problems);
```

- Replace `CheckAbility` with:

```csharp
    /// <summary>Keyword abilities need their parameter: Deathknell's steps (they run like a trigger's), Hunt's value.</summary>
    private static void CheckKeyword(KeywordEntry entry, string at, List<string> problems)
    {
        if (!Keywords.Contains(entry.Keyword))
        {
            problems.Add($"keyword {entry.Keyword}");
            return;
        }
        if (entry.Keyword == MechanicalKeyword.Deathknell) CheckSteps(entry.Steps, at, problems, targets: false);
        if (entry.Keyword == MechanicalKeyword.Hunt && entry.Value is null) problems.Add($"{at}: value");
    }

    private static void CheckAbility(Ability ability, string at, List<string> problems)
    {
        if (ability is not (SpellAbility or TriggeredAbility))
        {
            problems.Add($"{at}: {ability.GetType().Name.Replace("Ability", "")} ability");
            return;
        }
        if (ability.Condition is not null || ability.ActiveIn is not null || ability.Script is not null)
            problems.Add($"{at}: condition, activeIn or script");
        switch (ability)
        {
            case SpellAbility spell:
                CheckSteps(spell.Steps, at, problems, targets: true);
                break;
            case TriggeredAbility triggered:
                if (!IsSupportedTrigger(triggered.Trigger)) problems.Add($"{at}: trigger");
                if (triggered.If is not null || triggered.Optional is not null || triggered.Cost is not null || triggered.Limit is not null)
                    problems.Add($"{at}: if, optional, cost or limit");
                CheckSteps(triggered.Steps, at, problems, targets: false);
                break;
        }
    }

    /// <summary>The forms the <see cref="TriggerWatcher"/> maps: the source's own Dies, BecameEmpowered, Played, Hold or Conquer,
    /// and a battlefield's "when you hold (or conquer) here".</summary>
    private static bool IsSupportedTrigger(Trigger trigger)
    {
        if (trigger.Filter is not null || trigger.Phase is not null) return false;
        var own = trigger.Subject?.Ref == RefKind.Self && trigger.By is null && trigger.Where is null;
        var here = trigger.Subject is null && trigger.By?.Kind == PlayerKind.You && trigger.Where?.Ref == RefKind.Here;
        return trigger.Event switch
        {
            TriggerEvent.Dies or TriggerEvent.BecameEmpowered or TriggerEvent.Played => own,
            TriggerEvent.Hold or TriggerEvent.Conquer => own || here,
            _ => false,
        };
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including Plan C's scripted Bo3 in `ViewTests`: its decks have no effects files, so no trigger ever fires there).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): put triggered abilities on the chain"
```

---

### Task 6: Activated abilities and Empower

**Files:**
- Create: `src/CromoBound.Engine/Effects/Resolvers/ConditionResolver.cs`
- Create: `src/CromoBound.Engine/Rules/Game.Activation.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Priority.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Dispatch.cs`
- Modify: `src/CromoBound.Engine/Effects/CardEffects.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Modify: `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`
- Test: `tests/CromoBound.Engine.Tests/ActivationTests.cs`

**Interfaces:**
- Consumes: Task 1's `ActivateOption`, `ActivateAbility`, `ChooseCardsDecision`, `ChooseCards`, `AbilityActivated`; Task 2's `AddAbilityItem`; Task 3's `ZoneResolver`, `Game.CheckPick`, `Game.Recycle`; Task 5's `AbilityText` and keyword abilities.
- Produces:
  - `ConditionResolver.Holds(Game, EffectContext, Condition) : bool` (`all`, `any`, `not`, `empowered`).
  - `ActivationTask(PlayerId player, ObjectId source, int ability)` with `ActivationStage { Choose, Pay, Finalize, Cancelled }`; `Game.RunActivation(ActivationTask) : bool` (internal).
  - `PriorityDecision.Activations` filled; `ActivateAbility` accepted for an offered option.
  - The Empower keyword stands for an activated ability (cost from the keyword, only if not Empowered, steps: Empower Self). Runes get no abilities.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/ActivationTests.cs`:

```csharp
using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ActivationTests
{
    /// <summary>gear-1 with a Reaction ability (exhaust it: draw 1) and a default-timing one (draw 1).</summary>
    private const string TwoAbilities = """
        { "cardId": "gear-1", "status": "Full", "abilities": [
          { "kind": "Activated", "timing": "Reaction", "cost": { "exhaustSelf": true }, "steps": [ { "action": "Draw", "amount": 1 } ] },
          { "kind": "Activated", "steps": [ { "action": "Draw", "amount": 1 } ] } ] }
        """;

    /// <summary>Garbage Grabber in P1's Base, one rune to pay with, and <paramref name="trash"/> cards in P1's trash.</summary>
    private static (TestGame Test, Game Engine, ObjectId Grabber) Grabber(int trash)
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("garbage-grabber"));
        var grabber = game.Put("garbage-grabber", Place.Base(P1));
        game.Runes(P1, "fury-rune", 1);
        for (var i = 0; i < trash; i++) game.Put("unit-2", Place.Trash(P1));
        return (game, game.Start(), grabber);
    }

    [Fact]
    public void Garbage_grabber_is_offered_only_with_three_cards_in_your_trash()
    {
        var (game, engine, grabber) = Grabber(trash: 2);
        Assert.Empty(engine.Decision<PriorityDecision>().Activations);

        Assert.True(engine.SubmitManual(P1, new ManualMoveCard(game.State.At(Place.Hand(P1))[0], Place.Trash(P1))).Accepted);

        Assert.Equal(new[] { new ActivateOption(grabber, 0) }, engine.Decision<PriorityDecision>().Activations);
    }

    [Fact]
    public void Garbage_grabber_recycles_three_pays_one_exhausts_and_draws_one()
    {
        var (game, engine, grabber) = Grabber(trash: 3);
        var deck = game.State.At(Place.MainDeck(P1)).Count;
        var hand = game.State.At(Place.Hand(P1)).Count;

        var started = engine.Accept(P1, new ActivateAbility(grabber, 0));

        Assert.Contains(started.Events, e => e is ChoiceMade { Kind: "Cards" });
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(1, pay.Cost.Energy);
        Assert.Empty(pay.Cost.Power);
        var paid = engine.PayWithSuggestion(P1);
        Assert.Contains(paid.Events, e => e is AbilityActivated { Ability: 0 });
        Assert.True(game.State[grabber].Exhausted);
        Assert.Empty(game.State.At(Place.Trash(P1)));
        Assert.Equal(deck + 3, game.State.At(Place.MainDeck(P1)).Count);
        var item = Assert.Single(game.State.Chain);
        Assert.Equal(AbilityKind.Activated, item.AbilityKind);
        Assert.Equal("garbage-grabber", item.SourceCardId);

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        Assert.Equal(hand + 1, game.State.At(Place.Hand(P1)).Count);
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void With_four_cards_in_the_trash_the_player_chooses_which_three_to_recycle()
    {
        var (game, engine, grabber) = Grabber(trash: 4);
        var trash = game.State.At(Place.Trash(P1)).ToList();

        engine.Accept(P1, new ActivateAbility(grabber, 0));
        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal((3, 3, 4), (choose.Min, choose.Max, choose.Options.Count));
        Assert.Equal(RejectionCode.InvalidTarget, engine.Submit(P1, new ChooseCards { Cards = [trash[0], trash[1]] }).Rejection!.Code);
        engine.Accept(P1, new ChooseCards { Cards = [trash[0], trash[1], trash[2]] });
        engine.PayWithSuggestion(P1);

        Assert.Equal(new[] { trash[3] }, game.State.At(Place.Trash(P1)));
    }

    [Fact]
    public void Cancelling_at_the_choice_or_at_the_payment_changes_nothing()
    {
        var (game, engine, grabber) = Grabber(trash: 4);
        var trash = game.State.At(Place.Trash(P1)).ToList();

        engine.Accept(P1, new ActivateAbility(grabber, 0));
        engine.Accept(P1, new CancelPlay());
        engine.Accept(P1, new ActivateAbility(grabber, 0));
        engine.Accept(P1, new ChooseCards { Cards = [trash[0], trash[1], trash[2]] });
        engine.Decision<PayCostDecision>();
        engine.Accept(P1, new CancelPlay());

        Assert.Equal(trash, game.State.At(Place.Trash(P1)));
        Assert.False(game.State[grabber].Exhausted);
        Assert.Empty(game.State.Chain);
        Assert.Single(engine.Decision<PriorityDecision>().Activations);
    }

    [Fact]
    public void An_exhausted_source_cant_activate_an_ability_that_exhausts_it()
    {
        var (_, engine, grabber) = Grabber(trash: 3);
        Assert.True(engine.SubmitManual(P1, new ManualSetStatus(grabber, StatusKind.Exhausted, true)).Accepted);

        Assert.Empty(engine.Decision<PriorityDecision>().Activations);
        Assert.Equal(RejectionCode.UnexpectedAction, engine.Submit(P1, new ActivateAbility(grabber, 0)).Rejection!.Code);
    }

    [Fact]
    public void Reaction_abilities_are_offered_on_a_chain_and_default_ones_only_in_your_neutral_open_main()
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", TwoAbilities)));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();
        Assert.Equal(new[] { new ActivateOption(gear, 0), new ActivateOption(gear, 1) }, engine.Decision<PriorityDecision>().Activations);

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);
        Assert.Equal(new[] { new ActivateOption(gear, 0) }, engine.Decision<PriorityDecision>().Activations);

        engine.Accept(P1, new ActivateAbility(gear, 0));

        Assert.True(game.State[gear].Exhausted);
        Assert.Equal(2, game.State.Chain.Count);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);
    }

    [Fact]
    public void Kharox_empowers_burns_the_opponent_and_may_play_a_unit_from_their_trash()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("kharox"));
        var kharox = game.Put("kharox", Place.Base(P1));
        game.Runes(P1, "chaos-rune", 8);
        var engine = game.Start();

        Assert.Equal(new[] { new ActivateOption(kharox, 1) }, engine.Decision<PriorityDecision>().Activations);
        engine.Accept(P1, new ActivateAbility(kharox, 1));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.True(game.State[kharox].Empowered);
        Assert.Equal(AbilityKind.Triggered, Assert.Single(game.State.Chain).AbilityKind);
        engine.Accept(P1, new Pass());
        var burned = engine.Accept(P2, new Pass());

        Assert.Contains(burned.Events, e => e is PlayerChosen { Chosen.Index: 1 });
        Assert.Equal(3, game.State.At(Place.Trash(P2)).Count);
        engine.Decision<OptionalDecision>();
        engine.Accept(P1, new ChooseOptional(true));
        Assert.Single(game.State.Chain);
        engine.Accept(P1, new Pass());
        Assert.Equal(P2, engine.Decision<PriorityDecision>().Player);
        engine.Accept(P2, new Pass());

        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal(3, choose.Options.Count);
        engine.Accept(P1, new ChooseCards { Cards = [choose.Options[0]] });

        var played = game.First(Place.Base(P1), "unit-2");
        Assert.Equal(P1, game.State[played].Controller);
        Assert.Equal(P2, game.State[played].Owner);
        Assert.Equal(2, game.State.At(Place.Trash(P2)).Count);
        Assert.Empty(game.State.Chain);
        Assert.Empty(engine.Decision<PriorityDecision>().Activations);
    }

    [Fact]
    public void Runes_offer_no_activations_and_keep_use_rune()
    {
        var game = new TestGame(db: EngineTestDb.Create(("fury-rune", """
            { "cardId": "fury-rune", "status": "Full", "abilities": [
              { "kind": "Activated", "timing": "Reaction", "cost": { "exhaustSelf": true }, "steps": [ { "action": "Draw", "amount": 1 } ] } ] }
            """)));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start();

        var priority = engine.Decision<PriorityDecision>();

        Assert.Empty(priority.Activations);
        Assert.Single(priority.Runes);
    }

    [Fact]
    public void Fury_runes_file_says_what_use_rune_does()
    {
        var real = CardRepository.Load(RepoPaths.Data);
        var file = real.Effects["fury-rune"].File;

        Assert.Equal(2, file.Abilities.Count);
        var exhaust = Assert.IsType<ActivatedAbility>(file.Abilities[0]);
        Assert.Equal(Timing.Reaction, exhaust.Timing);
        Assert.True(exhaust.Cost?.ExhaustSelf);
        Assert.Equal(1, Assert.IsType<AddStep>(Assert.Single(exhaust.Steps)).Energy?.Literal);
        var recycle = Assert.IsType<ActivatedAbility>(file.Abilities[1]);
        Assert.Equal(Timing.Reaction, recycle.Timing);
        Assert.Equal(RefKind.Self, Assert.IsType<RecycleStep>(Assert.Single(recycle.Cost!.Actions)).Target?.Ref);
        Assert.Equal(new[] { PowerSymbol.Fury }, Assert.IsType<AddStep>(Assert.Single(recycle.Steps)).Power);
        Assert.Equal(Domain.Fury, Assert.Single(real.Cards["fury-rune"].Domains));
        var info = new CardEffects(real).For("fury-rune");
        Assert.Equal(MappingStatus.Full, info.Status);
        Assert.Empty(info.Abilities);
    }
}
```

In `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`, add `kharox`, `garbage-grabber` and `fury-rune` to `Cards_the_engine_runs_today_are_full`:

```csharp
    [InlineData("kharox")]
    [InlineData("garbage-grabber")]
    [InlineData("fury-rune")]
```

and remove the `kharox` and `garbage-grabber` rows from `Cards_needing_later_plans_play_by_hand_for_now`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~ActivationTests|FullyQualifiedName~EffectsDataTests"`
Expected: FAIL (`Activations` is always empty and `ActivateAbility` is rejected; Kharox, Garbage Grabber and Fury Rune aren't Full yet).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Engine/Effects/Resolvers/ConditionResolver.cs`:

```csharp
using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns conditions into true or false (spec §4.5). Plan E evaluates the forms keyword abilities use: all, any, not and
/// empowered (the source is Empowered). <see cref="EffectsSupport"/> keeps every other condition out of the files it runs.</summary>
internal static class ConditionResolver
{
    public static bool Holds(Game game, EffectContext context, Condition condition)
    {
        if (condition.All is { } all) return all.All(c => Holds(game, context, c));
        if (condition.Any is { } any) return any.Any(c => Holds(game, context, c));
        if (condition.Not is { } not) return !Holds(game, context, not);
        if (condition.Empowered is { } empowered)
            return context.Source is { } source && game.State.Exists(source) && game.State[source].Empowered == empowered;
        throw new InvalidOperationException("Only all, any, not and empowered conditions are supported so far.");
    }
}
```

Create `src/CromoBound.Engine/Rules/Game.Activation.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

internal enum ActivationStage { Choose, Pay, Finalize, Cancelled }

/// <summary>Activating an ability (spec §5.3): choose the cost action's cards, pay energy and power, then pay the rest of the cost
/// and put the ability on the chain. Nothing is spent before the last stage, so cancelling leaves no trace.</summary>
internal sealed class ActivationTask(PlayerId player, ObjectId source, int ability) : GameTask
{
    public PlayerId Player { get; } = player;
    public ObjectId Source { get; } = source;
    public int Ability { get; } = ability;
    public ActivationStage Stage { get; set; }
    public IReadOnlyList<ObjectId> Chosen { get; set; } = [];
    public TotalCost? Cost { get; set; }

    public override bool Run(Game game) => game.RunActivation(this);
}

public sealed partial class Game
{
    /// <summary>The activated abilities of the player's permanents that can be started now. Runes have none (CardEffects leaves
    /// them to UseRune, CR 429).</summary>
    private IEnumerable<ActivateOption> ActivateOptions(PlayerId player)
    {
        foreach (var source in State.Objects.Where(o => o.Place.IsLocation && o.Controller == player))
        {
            var abilities = Effects.For(source.CardId).Abilities;
            for (var i = 0; i < abilities.Count; i++)
                if (abilities[i] is ActivatedAbility ability && CanActivate(player, source, ability)) yield return new ActivateOption(source.Id, i);
        }
    }

    /// <summary>Timing (Reaction: whenever you have priority; Action: while no chain exists; default: your own turn in Neutral Open),
    /// the ability's condition, a ready source when the cost exhausts it, and enough cards for its cost action (rules 416, 422).
    /// Energy and power are checked when paying, as for cards.</summary>
    private bool CanActivate(PlayerId player, CardInstance source, ActivatedAbility ability)
    {
        var timing = ability.Timing switch
        {
            Timing.Reaction => true,
            Timing.Action => !IsClosed,
            _ => IsNeutralOpenMain(player),
        };
        if (!timing) return false;
        var context = ActivationContext(player, source);
        if (ability.UseOnlyIf is { } condition && !ConditionResolver.Holds(this, context, condition)) return false;
        if (ability.Cost?.ExhaustSelf == true && source.Exhausted) return false;
        return CostAction(ability) is not { } recycle || CostCards(context, recycle).Count >= CostCount(context, recycle);
    }

    private static EffectContext ActivationContext(PlayerId player, CardInstance source) =>
        new() { Controller = player, Source = source.Id, SourceCardId = source.CardId };

    /// <summary>The ability's cost action, run in cost mode: "recycle N from your trash" is the only one EffectsSupport lets through.</summary>
    private static RecycleStep? CostAction(ActivatedAbility ability) => ability.Cost?.Actions is [RecycleStep recycle] ? recycle : null;

    private List<ObjectId> CostCards(EffectContext context, RecycleStep recycle) =>
        [.. ZoneResolver.Resolve(this, context, recycle.From!).SelectMany(place => State.At(place))];

    private int CostCount(EffectContext context, RecycleStep recycle) => ValueResolver.Resolve(this, context, recycle.Count!);

    internal bool RunActivation(ActivationTask task)
    {
        while (true)
        {
            if (task.Stage == ActivationStage.Cancelled) return true;
            if (OnBoard(task.Source) is not { } source) return true;
            var ability = (ActivatedAbility)Effects.For(source.CardId).Abilities[task.Ability];
            var context = ActivationContext(task.Player, source);
            switch (task.Stage)
            {
                case ActivationStage.Choose:
                    if (AskCostCards(task, ability, context)) return false;
                    break;
                case ActivationStage.Pay:
                    task.Cost ??= ability.Cost is { } cost ? new TotalCost(cost.Energy ?? 0, cost.Power) : new TotalCost(0, []);
                    if (task.Cost.Energy == 0 && task.Cost.Power.Count == 0)
                    {
                        task.Stage = ActivationStage.Finalize;
                        break;
                    }
                    AskPay(task.Player, task.Cost, CardOf(source).Domains,
                        onPaid: () => task.Stage = ActivationStage.Finalize,
                        onCancel: () => task.Stage = ActivationStage.Cancelled,
                        onAdjust: adjusted => task.Cost = adjusted);
                    return false;
                default:
                    FinishActivation(task, source, ability, context);
                    return true;
            }
        }
    }

    /// <summary>Cost mode: the cost action's cards are chosen before paying; with too few the activation ends, and exactly enough
    /// is a forced choice. Moves the task on to paying, or asks and returns true.</summary>
    private bool AskCostCards(ActivationTask task, ActivatedAbility ability, EffectContext context)
    {
        task.Stage = ActivationStage.Pay;
        if (CostAction(ability) is not { } recycle) return false;
        var options = CostCards(context, recycle);
        var count = CostCount(context, recycle);
        if (options.Count < count)
        {
            task.Stage = ActivationStage.Cancelled;
            return false;
        }
        if (options.Count == count)
        {
            task.Chosen = options;
            Emit(new ChoiceMade(task.Player, "Cards", options));
            return false;
        }
        task.Stage = ActivationStage.Choose;
        Ask(new ChooseCardsDecision(task.Player, context.SourceCardId, options, count, count), (_, action) =>
        {
            if (action is CancelPlay)
            {
                task.Stage = ActivationStage.Cancelled;
                return null;
            }
            if (action is not ChooseCards choose) return Reject(RejectionCode.UnexpectedAction, "Choose the cards to recycle, or cancel.");
            if (CheckPick(choose.Cards, options, count, count, "cards") is { } rejection) return rejection;
            task.Chosen = [.. choose.Cards];
            task.Stage = ActivationStage.Pay;
            return null;
        });
        return true;
    }

    /// <summary>The rest of the cost is paid (exhaust the source; recycle the chosen cards that are still there), then the ability
    /// goes on the chain and its controller gets priority.</summary>
    private void FinishActivation(ActivationTask task, CardInstance source, ActivatedAbility ability, EffectContext context)
    {
        if (ability.Cost?.ExhaustSelf == true) SetStatus(source.Id, StatusKind.Exhausted, true);
        foreach (var card in task.Chosen.Where(State.Exists).ToList()) Recycle(card);
        if (State.Chain.Count == 0) ChainStartedByTrigger = false;
        AddAbilityItem(task.Player, source.Id, source.CardId, AbilityKind.Activated, AbilityText(source.CardId, ability), ability.Steps, context);
        Emit(new AbilityActivated(source.Id, task.Ability, task.Player));
    }
}
```

In `src/CromoBound.Engine/Rules/Game.Priority.cs`, in `PriorityOptions`, replace the `[],` activations argument with:

```csharp
            [.. ActivateOptions(player)],
```

In `src/CromoBound.Engine/Rules/Game.Dispatch.cs`, add `using CromoBound.Engine.Decisions;` and this arm after the `PlayCard` arm of `HandlePriority`:

```csharp
            case ActivateAbility activate when options.Activations.Contains(new ActivateOption(activate.Source, activate.Ability)):
                Push(new ActivationTask(player, activate.Source, activate.Ability));
                return null;
```

In `src/CromoBound.Engine/Effects/CardEffects.cs`:
- In `Build`, after `var file = loaded.File;`, add:

```csharp
        if (card.Type == CardType.Rune) return new(file.Status, [], new HashSet<DisplayKeyword>(), [], []);
```

- Change the class summary to: "Effects per card id, cached. A file the engine can't fully run yet is treated as Unmapped, so the card plays by hand as in 2a. Runes get no abilities: basic runes keep 2a's UseRune (spec §13)."
- Add this case to the `switch` in `KeywordAbilities`:

```csharp
                case MechanicalKeyword.Empower:
                    yield return new ActivatedAbility
                    {
                        Cost = entry.Cost,
                        UseOnlyIf = new Condition { Not = new Condition { Empowered = true } },
                        Steps = [new EmpowerStep { Target = ObjectRef.Self }],
                    };
                    break;
```

In `src/CromoBound.Engine/Effects/EffectsSupport.cs`:
- Add `MechanicalKeyword.Empower` to `Keywords`.
- Change the `CheckKeyword` summary to "Keyword abilities need their parameter: Deathknell's steps (they run like a trigger's), Hunt's value, Empower's plain cost." and add at its end:

```csharp
        if (entry.Keyword == MechanicalKeyword.Empower && (entry.Cost is null || entry.Cost.Actions.Count > 0 || entry.Cost.ExhaustSelf is not null))
            problems.Add($"{at}: cost");
```

- In `CheckAbility`, change the first check to `if (ability is not (SpellAbility or TriggeredAbility or ActivatedAbility))` and add this case to its `switch`:

```csharp
            case ActivatedAbility activated:
                if (activated.UseOnlyIf is not null || activated.Limit is not null) problems.Add($"{at}: useOnlyIf or limit");
                if (activated.Cost is { } cost && !IsSupportedCost(cost)) problems.Add($"{at}: cost");
                CheckSteps(activated.Steps, at, problems, targets: false);
                break;
```

- Add:

```csharp
    /// <summary>Energy, power, exhausting the source, and at most one cost action: recycling a number of cards from your trash.</summary>
    private static bool IsSupportedCost(Cost cost) => cost.Actions switch
    {
        [] => true,
        [RecycleStep recycle] => recycle.Target is null && recycle.Count?.Literal is not null
            && recycle.From is { Zone: Zone.Trash, Position: null } from && (from.Owner is null || from.Owner.Kind == PlayerKind.You)
            && recycle.Player is null && recycle.Chooser is null && recycle.Script is null && recycle.Store is null,
        _ => false,
    };
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): activate abilities"
```

---

### Task 7: Triggers and effect choices in saved and undone matches

**Files:**
- Modify: `tests/CromoBound.Engine.Tests/MatchEffectsTests.cs`

**Interfaces:**
- Consumes: everything above, through `Match` only (logged actions, `ToRecord`, `Match.Load`, `ViewFor`).
- Produces: nothing new.

- [ ] **Step 1: Write the tests**

Add to `MatchEffectsTests`:

```csharp
    /// <summary>A legal Jinx deck with three Mystic Poros in place of filler-13.</summary>
    private static Deck PoroDeck(params string[] battlefields) => TestDecks.Jinx(battlefields) with
    {
        Main = [.. Enumerable.Range(1, 12).Select(i => new DeckEntry { Printing = $"p-filler-{i}", Count = 3 }), new DeckEntry { Printing = "p-mystic-poro", Count = 3 }],
    };

    private static CardDatabase PoroDb() => EngineTestDb.WithRealCards("mystic-poro");

    /// <summary>To play; the first player brings a Mystic Poro to hand, plays it with the suggested payment, and both players pass
    /// on its Vision trigger, stopping at the choice to recycle the predicted card. Everything goes through logged actions.</summary>
    private static (Match Match, PlayerId Player) AtPredictChoice()
    {
        var setup = new MatchSetup(MatchFormat.Bo1, PoroDeck("bf-a", "bf-b", "bf-c"), PoroDeck("bf-d", "bf-e", "bf-f"), 7);
        var match = Match.Create(setup, PoroDb()).Match!.ToPlay();
        var player = match.Decision<PriorityDecision>().Player;
        var state = match.Game!.State;
        if (!state.At(Place.Hand(player)).Any(id => state[id].CardId == "mystic-poro"))
            match.Accept(player, new ManualMoveCard(state.At(Place.MainDeck(player)).First(id => state[id].CardId == "mystic-poro"), Place.Hand(player)));
        match.Accept(player, new PlayCard(state.At(Place.Hand(player)).First(id => state[id].CardId == "mystic-poro")));
        var pay = match.Decision<PayCostDecision>().Suggested!;
        match.Accept(player, new PayCost { Exhaust = pay.Exhaust, Recycle = pay.Recycle });
        match.Accept(player, new Pass());
        match.Accept(Other(player), new Pass());
        match.Decision<OptionalDecision>();
        return (match, player);
    }

    [Fact]
    public void Undo_after_answering_a_trigger_choice_equals_never_answering()
    {
        var (undone, player) = AtPredictChoice();
        var (reference, _) = AtPredictChoice();
        undone.Accept(player, new ChooseOptional(true));

        undone.Accept(player, new RequestUndo());
        undone.Accept(Other(player), new AnswerUndo(true));

        Assert.IsType<OptionalDecision>(undone.Pending);
        Assert.Equal(reference.Snapshot(), undone.Snapshot());
    }

    [Fact]
    public void A_saved_match_with_a_trigger_and_an_effect_choice_loads_identically()
    {
        var (match, player) = AtPredictChoice();
        match.Accept(player, new ChooseOptional(true));

        var loaded = Match.Load(CromoJson.Deserialize<MatchRecord>(CromoJson.Serialize(match.ToRecord())), PoroDb());

        Assert.Equal(match.Snapshot(), loaded.Snapshot());
    }

    [Fact]
    public void Only_the_predicting_player_sees_the_predicted_card()
    {
        var (match, player) = AtPredictChoice();

        var mine = match.ViewFor(player);
        var theirs = match.ViewFor(Other(player));

        Assert.NotNull(Assert.Single(mine.Log.OfType<Predicted>(), p => p.CardIds is not null).CardIds);
        Assert.Null(Assert.Single(theirs.Log.OfType<Predicted>()).CardIds);
        Assert.IsType<OptionalDecision>(mine.Decision);
        Assert.Null(theirs.Decision);
        Assert.Equal("Optional", theirs.DecisionKind);
    }
```

Add `using CromoBound.Data;` to the file's usings (for `CardDatabase`).

- [ ] **Step 2: Run the tests**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests). These tests pin behavior Tasks 1-6 already provide (replay recomputes triggers and forced choices from the log, spec §5.6); if one fails, the defect is in the task that owns the behavior, not in the test.

- [ ] **Step 3: Commit**

```bash
git add tests/CromoBound.Engine.Tests
git commit -m "test(engine): cover triggers in saved and undone matches"
```

---

## Done criteria

- [ ] Effect choices, activations and the new events round-trip through JSON, and the scripted player answers every new decision (Task 1).
- [ ] Ability items the engine runs resolve automatically and stop when countered or when the game ends; Channel, GainXp and Empower work (Task 2).
- [ ] Effects choose players and cards (forced when there is one answer), ask about reflexive blocks that then go on the chain with the same context, and predict privately; a manual action re-asks and a counter stops the effect (Task 3).
- [ ] Effects play cards from any zone for their cost or ignoring it, with Accelerate still possible and cancelling allowed (Task 4).
- [ ] Triggers from deaths, empowering, plays and scoring go on the chain after the current resolution, the turn player's first, each player ordering their own; Soaring Scout, Mystic Poro, Voracious Gromp and Shadow Temple run with the real data (Task 5).
- [ ] Activated abilities are offered by timing, condition and cost actions, pay their costs, and go on the chain; Garbage Grabber and Kharox run with the real data; runes keep `UseRune` and Fury Rune's file matches it (Task 6).
- [ ] Undo, loading and views work with triggers and effect choices, and the predicted card stays private (Task 7).
- [ ] `dotnet build CromoBound.slnx --no-incremental` reports 0 warnings and 0 errors, and `dotnet test CromoBound.slnx` passes.
