# Engine Plan O: The Fiora Deck, Part 1 (Values, Targets, Costs, Passives, Empower)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. First of two plans for Phase 2c (O: values, targets, costs, passives, Empower; P: chain, new triggers, Mighty, lethal damage, the rest of the deck).

**Goal:** twelve cards of the Fiora sample deck resolve automatically: Harnessed Dragon, Punch First, Divining Shells, Doran's Blade, Shepherd's Heirloom, Rampage, Sacrifice, Kayle Justified, Fiora Victorious, Risen Altar, Sunken Temple and Amateur Recital. Each is pinned by a test that plays it with its real card data.

**Architecture:**
- **Resolvers** learn the values (`prop`/`of`, `var`, `sum`, `mul`), conditions (`exists`, `compare`, `paid`, `turnOf`) and filters (`mighty`, `location`, `not`) these cards use, plus the `Host` reference (the unit a gear is attached to).
- **Abilities choose targets** when they go on the chain, like spells:
  - a triggered ability right after it is added;
  - an activated ability before its cost is paid.
  A selector inside an optional block is chosen when that block runs. Target steps always record what they aimed at, so later steps can use `var`.
- **New steps:** `ModifyMight` (this turn, through 2a's might modifiers) and `Move` (to the unit's controller's base).
  - `Optional` also runs without `reflexive`: inline, with an optional cost.
- **Costs:**
  - Activated abilities may kill their source or spend XP.
  - Plays may have additional costs: an optional extra power, or a cost action that kills a chosen unit. Their payment is recorded as a variable.
- **Passives apply live:**
  - `ModifyMight` passives (on Self, or on the Host for equipment) join `Modifiers.MightOf`.
  - `GrantKeyword` passives join one `Game.Has`/`KeywordValue` read.
  - `KeywordCostReduction` lowers Empower costs (Risen Altar, the player's pick between energy and power).
- **Empower counts:** `CardInstance` counts Empowers. The Empower keyword's `value` is the most times it can be used (Kayle: 3). Views carry the count to the board's chip.
- **Triggers** may carry an `if` condition, checked when the event happens.

**Tech Stack:** .NET 10, xUnit 2.9.3, bUnit 2.11 (one client test). No new dependencies.

**Spec:** `docs/effects-fiora.md` (sections 1, 2, 3.1, 3.2, 3.4, 3.5, 4, 5, 6 row O). Phase 2b's `docs/effects-engine.md` stays the base design.

## Global Constraints

- Plans A to F's Global Constraints still apply:
  - `net10.0`; no NuGet packages in runtime projects.
  - No `DateTime`, `Guid`, `System.Random` or hash-order-dependent iteration in engine code.
  - `CromoJson` for everything saved; every action and decision type round-trips through it.
- Public API stays limited to `Match`, `Game`, `PlayerView` and the action, decision, event and result records. Everything in `CromoBound.Engine.Effects` is `internal`; the test project sees it through `InternalsVisibleTo`.
- Unmapped cards play exactly as in 2a. Every existing test keeps passing; the only existing tests that change are the ones this plan names.
- Modifiers are evaluated live: no derived value (might, keywords, costs) is stored in `GameState`. The Empower count is state, not a derived value.
- Handlers validate an answer before they change anything, and a rejected answer leaves the decision pending.
- A file that uses anything the engine can't run still plays by hand and names the construct (`EffectsSupport.Problems`). Every support check this plan widens stays exact: it lets through only the forms listed in its task.
- `Models.Effects.PlayStep` (the step) and `Rules.PlayStep` (the play-progress enum) share a name. A file that imports both namespaces and needs the step uses `using EffectPlayStep = CromoBound.Models.Effects.PlayStep;`.
- Owner rules:
  - 0 build warnings and 0 errors at all times.
  - Conventional, title-only commit messages: no body, no co-author trailer, no mention of Claude/AI.
  - No em dashes or en dashes in code, comments or strings. Test strings that copy real card text may keep them.
  - LF line endings; UTF-8 without BOM.
  - Commit on `develop` (the owner's standing permission); never push.
- Run dotnet with `export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && ` in Git Bash (the default dotnet on PATH is SDK 9). Output is in Italian: Avvisi = warnings, Errori = errors, Superato = passed.
- After changing anything in `src/CromoBound.Models/Effects`, regenerate the schema with `dotnet run --project tools/CromoBound.Importer -- schema` and commit `schema/effects.schema.json`.

## Deliberate deviations from the spec (reviewers: these are intended)

1. **Empower's limit is the keyword's `value`.** The spec's §3.5 says "an optional `limit`". The keyword entry already has a `value` field, so Kayle's file says `{ "keyword": "Empower", "value": 3, "cost": ... }`, with no new field. A missing value means 1, today's "only if not Empowered".
2. **Amateur Recital's destination is `"to": { "ref": "Controller" }`**: the base of the moved unit's controller. The spec's reading 7 (a move, not a recall) holds: the unit is moved with `MoveCard`, not exhausted, and no Contested applies.
3. **A trigger's `if` is checked once, when the event happens.** It isn't checked again on resolution (CR 386's "intervening if"). No Plan O card can lose its condition between the two moments.
4. **A trigger whose required target has no candidate is not put on the chain.** Nothing is announced. An "up to" target with no candidate goes on with none.
5. **Risen Altar asks with an `OptionalDecision`:** "Lower the Empower cost by 1 energy? No lowers one power instead." It only asks when the cost has both energy and power; otherwise the possible reduction applies without asking (owner reading 3).
6. **Rampage's "if you paid, +2"** is `amount: { "mul": [2, { "var": "body" }] }`. A paid additional cost is stored as a variable with number 1 and `Happened` true, an unpaid one with number 0. The `paid` condition reads the same variable. No conditional step is needed.
7. **Sacrifice's kill is chosen when the card is played and done after payment.** Cancelling before payment kills nothing. The unit dies through `Kill`, so its Deathknell triggers (spec reading 10).
8. **The board can't answer every new decision yet.** Target, card and yes/no choices are plan M's (4b-2). Until then the board shows its "comes in the next update" panel for them. The engine and its tests are complete regardless.

## Review Focus

1. **A trigger or activation with a target the player can't legally choose.** Harnessed Dragon with no enemy unit goes on no chain and asks nothing. Divining Shells can't be activated when no unit is on the board. Pinned in Task 2 (`A_trigger_with_no_legal_target_is_not_put_on_the_chain`) and Task 3 (`Divining_shells_needs_a_unit_to_target`).
2. **Equipment might when the gear is detached.** Doran's Blade adds +2 only while attached; a detached blade adds nothing (Plan F already detaches gear whose unit leaves the board). Pinned in Task 4 (`Dorans_blade_gives_two_might_only_while_attached`).
3. **Fiora, Victorious growing into Mighty mid-combat.** Her granted Shield applies once she is Mighty, and computing it doesn't loop on itself. Pinned in Task 4 (`Fiora_victorious_gets_her_keywords_only_while_mighty`).
4. **Cancelling a play with an additional cost.** Cancelling Sacrifice before paying kills nothing; cancelling Rampage after answering yes to the extra cost spends nothing. Pinned in Task 5 (`Cancelling_sacrifice_before_paying_kills_nothing`).
5. **Empowering past the limit and through a new object.** Kayle can't be Empowered a fourth time. A unit that goes to the trash and comes back starts at zero. Pinned in Task 6 (`Kayle_empowers_three_times_and_no_more`, `A_new_object_starts_unempowered`).

---

## File Structure

```
src/CromoBound.Models/Effects/
  ReferenceEnums.cs                       (modify) RefKind.Host, ValueProperty.EmpowerCount
  Modifiers.cs                            (modify) KeywordCostReductionModifier.OrPower
schema/effects.schema.json                (regenerate)
src/CromoBound.Engine/
  Effects/Resolvers/ValueResolver.cs      prop/of, var, sum, mul
  Effects/Resolvers/ConditionResolver.cs  exists, compare, paid, turnOf
  Effects/Resolvers/ObjectResolver.cs     Host; mighty, location and not filters
  Effects/ResolutionChoice.cs             targets for steps: slots, Self, vars, all, or a choice made now
  Effects/EffectsSupport.cs               (modify, every task) the forms each task adds
  Effects/CardEffects.cs                  (modify) Empower's limit, Equip with an XP cost
  Effects/Modifiers.cs                    (modify) ModifyMight and GrantKeyword passives, Host, KeywordsOf, Empower reductions
  Effects/TriggerWatcher.cs               (modify) if conditions
  Effects/Steps/MightStepHandlers.cs      ModifyMight, Move
  Effects/Steps/PermanentStepHandlers.cs  (modify) Deal and Kill record and choose targets; Empower counts
  Effects/Steps/ChoiceStepHandlers.cs     (modify) Optional inline, with a cost
  Effects/Steps/StepRegistry.cs           (modify) ModifyMight, Move
  State/CardInstance.cs                   (modify) EmpowerCount
  Rules/Game.cs                           (modify) Has goes through Modifiers; granting guard
  Rules/Game.Targets.cs                   (modify) AskSlotTargets shared by plays, triggers and activations
  Rules/Game.Triggers.cs                  (modify) trigger targets
  Rules/Game.Activation.cs                (modify) Targets and Reduce stages, kill-self and spend-XP costs
  Rules/Game.Play.cs                      (modify) Extra step: additional costs
  Rules/Game.Costs.cs                     additional costs: asking, recording, paying
  Rules/Game.Mutations.cs                 (modify) ModifyMight, SpendXp
  Rules/Game.Manual.cs                    (modify) manual might uses ModifyMight
  Rules/Game.Priority.cs                  (modify) playable needs additional-cost candidates
  Views/PlayerView.cs, Views/ViewBuilder.cs (modify) CardView.EmpowerCount
src/CromoBound.Client/Board/
  BoardParts.cs, BoardModel.cs            (modify) BoardCard.EmpowerCount
  Components/CardFace.razor, CardZoom.razor (modify) "Empowered x2"
data/effects/
  harnessed-dragon.json, punch-first.json, divining-shells.json, dorans-blade.json, shepherds-heirloom.json,
  rampage.json, sacrifice.json, kayle-justified.json, fiora-victorious.json, risen-altar.json, sunken-temple.json,
  amateur-recital.json
data/import-report.md                     (regenerate)
tests/CromoBound.Engine.Tests/
  ResolverTests.cs, TriggerTests.cs, ActivationTests.cs, ModifierTests.cs, CostTests.cs (modify)
  AbilityTargetTests.cs, FioraDeckTests.cs, AdditionalCostTests.cs, EmpowerTests.cs
tests/CromoBound.Client.Tests/BoardViewTests.cs (modify)
```

---
### Task 1: Values, conditions and filters

**Files:**
- Modify: `src/CromoBound.Models/Effects/ReferenceEnums.cs`, `src/CromoBound.Models/Effects/Modifiers.cs`
- Regenerate: `schema/effects.schema.json`
- Modify: `src/CromoBound.Engine/Effects/Resolvers/ValueResolver.cs`, `ConditionResolver.cs`, `ObjectResolver.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Test: `tests/CromoBound.Engine.Tests/ResolverTests.cs`

**Interfaces:**
- Consumes: Plan F's resolvers and `EffectsSupport`.
- Produces:
  - `RefKind.Host`; `ValueProperty.EmpowerCount`; `KeywordCostReductionModifier.OrPower : IReadOnlyList<PowerSymbol>` (Task 6 reads it).
  - `ValueResolver.Resolve` handles literal, `var` (the variable's `Number`, 0 when unset), `prop` + `of` (the first object's Might, printed EnergyCost, Damage or EmpowerCount; 0 when there is none), `sum` and `mul`.
  - `ConditionResolver.Holds` handles `exists`, `compare`, `paid` (the variable named exists and `Happened`) and `turnOf`.
  - `ObjectResolver.Resolve` handles `{ "ref": "Host" }`: the unit the source is attached to.
  - `ObjectResolver.FilterMatches` handles `mighty` (a unit with 5 or more might), `location` (`{ "ref": "Here" }`: the source's battlefield; `{ "select": "Battlefield" }`: any battlefield) and `not`.
  - `EffectsSupport.IsSupportedValue(Value)`, `IsSupportedCondition(Condition)` and the widened `IsSupportedFilter(Filter?)`, used by later tasks' checks.

- [ ] **Step 1: Write the failing tests**

Append to `tests/CromoBound.Engine.Tests/ResolverTests.cs`, inside the class:

```csharp
    [Fact]
    public void Values_read_properties_variables_sums_and_products()
    {
        var game = new TestGame();
        var unit = game.Put("unit-3", Place.Base(P1));
        game.State[unit].Damage = 1;
        var engine = game.Start();
        var context = Context(P1);
        context.Vars["picked"] = new EffectVar([unit], [], 2, true);
        var picked = ObjectRef.Variable("picked");

        Assert.Equal(3, ValueResolver.Resolve(engine, context, new Value { Prop = ValueProperty.Might, Of = picked }));
        Assert.Equal(3, ValueResolver.Resolve(engine, context, new Value { Prop = ValueProperty.EnergyCost, Of = picked }));
        Assert.Equal(1, ValueResolver.Resolve(engine, context, new Value { Prop = ValueProperty.Damage, Of = picked }));
        Assert.Equal(2, ValueResolver.Resolve(engine, context, new Value { Var = "picked" }));
        Assert.Equal(0, ValueResolver.Resolve(engine, context, new Value { Var = "missing" }));
        Assert.Equal(0, ValueResolver.Resolve(engine, context, new Value { Prop = ValueProperty.Might, Of = ObjectRef.Variable("missing") }));
        Assert.Equal(5, ValueResolver.Resolve(engine, context, new Value { Sum = [2, new Value { Var = "picked" }, 1] }));
        Assert.Equal(6, ValueResolver.Resolve(engine, context, new Value { Mul = [3, new Value { Var = "picked" }] }));
    }

    [Fact]
    public void Conditions_check_existence_comparisons_paid_costs_and_whose_turn_it_is()
    {
        var game = new TestGame();
        var unit = game.Put("unit-3", Place.Base(P1));
        var engine = game.Start();
        var context = Context(P1, unit);
        context.Vars["body"] = new EffectVar([], [], 1, true);
        context.Vars["skipped"] = new EffectVar([], [], 0, false);
        var self = new Value { Prop = ValueProperty.Might, Of = ObjectRef.Self };

        Assert.True(ConditionResolver.Holds(engine, context, new Condition { Exists = Select(SelectKind.Unit, Relation.Friendly) }));
        Assert.False(ConditionResolver.Holds(engine, context, new Condition { Exists = Select(SelectKind.Unit, Relation.Enemy) }));
        Assert.True(ConditionResolver.Holds(engine, context, new Condition { Compare = new Comparison(self, CompareOp.Gte, 3) }));
        Assert.False(ConditionResolver.Holds(engine, context, new Condition { Compare = new Comparison(self, CompareOp.Gt, 3) }));
        Assert.True(ConditionResolver.Holds(engine, context, new Condition { Paid = "body" }));
        Assert.False(ConditionResolver.Holds(engine, context, new Condition { Paid = "skipped" }));
        Assert.False(ConditionResolver.Holds(engine, context, new Condition { Paid = "missing" }));
        Assert.True(ConditionResolver.Holds(engine, context, new Condition { TurnOf = PlayerRef.You }));
        Assert.False(ConditionResolver.Holds(engine, context, new Condition { TurnOf = new PlayerRef { Kind = PlayerKind.Opponent } }));
    }

    [Fact]
    public void Filters_check_mighty_location_and_not()
    {
        var game = new TestGame();
        var small = game.Put("unit-2", Place.Battlefield(0));
        var big = game.Put("unit-3", Place.Battlefield(0));
        var home = game.Put("unit-3", Place.Base(P1));
        game.State[big].Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        var engine = game.Start();
        var here = Context(P1, game.State.Battlefields[0].Card);
        static ObjectRef Units(Filter filter) => new() { Select = SelectKind.Unit, All = true, Filter = filter };

        Assert.Equal(new[] { big }, ObjectResolver.Candidates(engine, here, Units(new Filter { Mighty = true })));
        Assert.Equal(new[] { small, big }, ObjectResolver.Candidates(engine, here, Units(new Filter { Location = new ObjectRef { Ref = RefKind.Here } })));
        Assert.Equal(new[] { small, big },
            ObjectResolver.Candidates(engine, here, Units(new Filter { Location = new ObjectRef { Select = SelectKind.Battlefield } })));
        Assert.Equal(new[] { small, home }, ObjectResolver.Candidates(engine, here, Units(new Filter { Not = new Filter { Mighty = true } })));
    }

    [Fact]
    public void Host_is_the_unit_the_source_is_attached_to()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.State[gear].AttachedTo = unit;
        var loose = game.Put("gear-1", Place.Base(P1));
        var engine = game.Start();
        var host = new ObjectRef { Ref = RefKind.Host };

        Assert.Equal(new[] { unit }, ObjectResolver.Resolve(engine, Context(P1, gear), host));
        Assert.Empty(ObjectResolver.Resolve(engine, Context(P1, loose), host));
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/CromoBound.Engine.Tests --filter "FullyQualifiedName~ResolverTests"`
Expected: build errors (`RefKind.Host`, `ValueProperty.EmpowerCount` don't exist), or, once Step 3's model lines are in, failures with "Only literal values are supported so far."

- [ ] **Step 3: Add the model words and regenerate the schema**

In `src/CromoBound.Models/Effects/ReferenceEnums.cs`:
- change `public enum RefKind { Self, Here, Controller, Owner, TriggerSubject, TriggerSource }` to `public enum RefKind { Self, Here, Controller, Owner, TriggerSubject, TriggerSource, Host }`;
- change `public enum ValueProperty { Might, EnergyCost, PowerCost, Damage }` to `public enum ValueProperty { Might, EnergyCost, PowerCost, Damage, EmpowerCount }`.

In `src/CromoBound.Models/Effects/Modifiers.cs`, replace `KeywordCostReductionModifier` with:

```csharp
/// <summary>Lowers a keyword's cost (Risen Altar: Empower). With both <see cref="Energy"/> and <see cref="OrPower"/>, the player
/// picks one of the two reductions.</summary>
public sealed record KeywordCostReductionModifier : Modifier
{
    public required MechanicalKeyword Keyword { get; init; }
    public Value? Energy { get; init; }
    public IReadOnlyList<PowerSymbol> OrPower { get; init; } = [];
}
```

Run: `dotnet run --project tools/CromoBound.Importer -- schema`
Expected: `Wrote schema/*.json.`; `git diff --stat schema` shows `effects.schema.json` changed.

- [ ] **Step 4: Implement the resolvers**

Replace the body of `src/CromoBound.Engine/Effects/Resolvers/ValueResolver.cs` (keep its usings, add `using CromoBound.Engine.State;` if missing) with:

```csharp
/// <summary>Turns values into numbers: literals, variables (their number, 0 when unset), an object's property (the first object;
/// 0 when there is none), sums and products. <see cref="EffectsSupport"/> keeps other forms out of the cards it runs.</summary>
internal static class ValueResolver
{
    public static int Resolve(Game game, EffectContext context, Value value)
    {
        if (value.Literal is { } literal) return literal;
        if (value.Var is { } name) return context.Vars.TryGetValue(name, out var stored) ? stored.Number ?? 0 : 0;
        if (value.Prop is { } prop)
            return ObjectResolver.Resolve(game, context, value.Of!).Select(id => Property(game, game.State[id], prop)).FirstOrDefault();
        if (value.Sum is { } sum) return sum.Sum(v => Resolve(game, context, v));
        if (value.Mul is { } mul) return mul.Aggregate(1, (total, v) => total * Resolve(game, context, v));
        throw new InvalidOperationException("Only literal, var, prop, sum and mul values are supported so far.");
    }

    private static int Property(Game game, CardInstance instance, ValueProperty prop) => prop switch
    {
        ValueProperty.Might => game.MightOf(instance.Id),
        ValueProperty.EnergyCost => game.CardOf(instance).Cost?.Energy ?? 0,
        ValueProperty.Damage => instance.Damage,
        ValueProperty.EmpowerCount => instance.Empowered ? 1 : 0,
        _ => throw new InvalidOperationException($"The {prop} property isn't supported so far."),
    };
}
```

(Task 6 changes the `EmpowerCount` line to `instance.EmpowerCount`.)

In `ConditionResolver.cs`, replace the summary and `Holds` with:

```csharp
/// <summary>Turns conditions into true or false (spec §4.5): all, any, not, empowered (the source is Empowered), legion (the
/// controller played a card this turn; read while a card is being played, so its own play isn't counted yet), exists (a reference
/// finds an object), compare, paid (an additional cost recorded as paid, Task 5) and turnOf.
/// <see cref="EffectsSupport"/> keeps every other condition out of the files it runs.</summary>
internal static class ConditionResolver
{
    public static bool Holds(Game game, EffectContext context, Condition condition)
    {
        if (condition.All is { } all) return all.All(c => Holds(game, context, c));
        if (condition.Any is { } any) return any.Any(c => Holds(game, context, c));
        if (condition.Not is { } not) return !Holds(game, context, not);
        if (condition.Empowered is { } empowered)
            return context.Source is { } source && game.State.Exists(source) && game.State[source].Empowered == empowered;
        if (condition.Legion is { } legion) return (game.State.Turn.PlayedThisTurn(context.Controller) > 0) == legion;
        if (condition.Exists is { } exists) return ObjectResolver.Resolve(game, context, exists).Count > 0;
        if (condition.Compare is { } compare)
            return Compare(ValueResolver.Resolve(game, context, compare.Left), compare.Op, ValueResolver.Resolve(game, context, compare.Right));
        if (condition.Paid is { } paid) return context.Vars.TryGetValue(paid, out var cost) && cost.Happened;
        if (condition.TurnOf is { } player) return PlayerResolver.Resolve(game, context, player).Contains(game.State.Turn.TurnPlayer);
        throw new InvalidOperationException("Only all, any, not, empowered, legion, exists, compare, paid and turnOf conditions are supported so far.");
    }

    private static bool Compare(int left, CompareOp op, int right) => op switch
    {
        CompareOp.Eq => left == right,
        CompareOp.Lte => left <= right,
        CompareOp.Gte => left >= right,
        CompareOp.Lt => left < right,
        _ => left > right,
    };
}
```

In `ObjectResolver.cs`:
- in `Resolve`, after the `RefKind.Self` line, add:

```csharp
        if (reference.Ref == RefKind.Host)
            return context.Source is { } gear && game.State.Exists(gear) && game.State[gear].AttachedTo is { } host && game.State.Exists(host)
                ? [host] : [];
```

- replace `FilterMatches` and add the two helpers:

```csharp
    /// <summary>Whether a card matches the filter's relation, type, token, other, mighty, location and not fields (the ones
    /// <see cref="EffectsSupport"/> lets through).</summary>
    public static bool FilterMatches(Game game, EffectContext context, Filter? filter, CardInstance instance)
    {
        if (filter is null) return true;
        if (filter.Relation == Relation.Friendly && instance.Controller != context.Controller) return false;
        if (filter.Relation == Relation.Enemy && instance.Controller == context.Controller) return false;
        if (filter.Type is { } wanted && game.CardOf(instance).Type != wanted) return false;
        if (filter.Token is { } token && instance.IsToken != token) return false;
        if (filter.Other == true && context.Source == instance.Id) return false;
        if (filter.Mighty is { } mighty && IsMighty(game, instance) != mighty) return false;
        if (filter.Location is { } location && !AtLocation(game, context, location, instance)) return false;
        if (filter.Not is { } not && FilterMatches(game, context, not, instance)) return false;
        return true;
    }

    /// <summary>A unit on the board with 5 or more might (CR 706-711).</summary>
    public static bool IsMighty(Game game, CardInstance instance) =>
        instance.Place.IsLocation && game.IsUnit(instance) && game.MightOf(instance.Id) >= 5;

    /// <summary>Here: the battlefield of the source (a battlefield card, or a permanent standing there). A battlefield selector:
    /// any battlefield.</summary>
    private static bool AtLocation(Game game, EffectContext context, ObjectRef location, CardInstance instance)
    {
        if (location.Select == SelectKind.Battlefield) return instance.Place.Kind == PlaceKind.Battlefield;
        if (location.Ref != RefKind.Here || context.Source is not { } source || !game.State.Exists(source)) return false;
        var at = game.State[source].Place;
        return at.Index is { } index && at.Kind is PlaceKind.BattlefieldCard or PlaceKind.Battlefield && instance.Place == Place.Battlefield(index);
    }
```

- [ ] **Step 5: Let the new forms through `EffectsSupport`**

In `src/CromoBound.Engine/Effects/EffectsSupport.cs`:
- replace `CheckValue` with:

```csharp
    private static void CheckValue(Value value, string at, List<string> problems)
    {
        if (!IsSupportedValue(value)) problems.Add($"{at}: value");
    }

    /// <summary>A literal, a variable, a property of Self, Host or a variable, or a sum or product of supported values.</summary>
    public static bool IsSupportedValue(Value value) =>
        value.Literal is not null
        || value.Var is not null
        || (value.Prop is ValueProperty.Might or ValueProperty.EnergyCost or ValueProperty.Damage or ValueProperty.EmpowerCount
            && value.Of is { } of && (of.Ref is RefKind.Self or RefKind.Host || of.Var is not null))
        || (value.Sum is { Count: > 0 } sum && sum.All(IsSupportedValue))
        || (value.Mul is { Count: > 0 } mul && mul.All(IsSupportedValue));

    /// <summary>all, any, not, empowered, legion, exists (Self, a variable, or a selector with a supported filter), compare of supported
    /// values, paid and turnOf (You or Opponent).</summary>
    public static bool IsSupportedCondition(Condition condition) =>
        (condition.All is { } all && all.All(IsSupportedCondition))
        || (condition.Any is { } any && any.All(IsSupportedCondition))
        || (condition.Not is { } not && IsSupportedCondition(not))
        || condition.Empowered is not null
        || condition.Legion is not null
        || (condition.Exists is { } exists && (exists.Ref == RefKind.Self || exists.Var is not null
            || (exists.Select is SelectKind.Unit or SelectKind.Gear or SelectKind.Permanent && IsSupportedFilter(exists.Filter))))
        || (condition.Compare is { } compare && IsSupportedValue(compare.Left) && IsSupportedValue(compare.Right))
        || condition.Paid is not null
        || condition.TurnOf is { Kind: PlayerKind.You or PlayerKind.Opponent };
```

- replace `IsSupportedFilter` and `HasOnlyBasicFields` with:

```csharp
    /// <summary>Card filters may use relation (Friendly or Enemy), type, token, other, mighty, location (Here, or any battlefield)
    /// and a supported not; nothing else yet.</summary>
    public static bool IsSupportedFilter(Filter? filter) =>
        filter is null
        || (filter.Relation is null or Relation.Friendly or Relation.Enemy && HasOnlyBasicFields(filter)
            && (filter.Location is null || filter.Location is { Ref: RefKind.Here } || filter.Location is { Select: SelectKind.Battlefield, Filter: null })
            && (filter.Not is null || IsSupportedFilter(filter.Not)));

    /// <summary>None of the fields beyond relation, type, token, other, mighty, location and not is set.</summary>
    private static bool HasOnlyBasicFields(Filter filter) =>
        filter.Controller is null && filter.Owner is null && filter.Zone is null
        && filter.Supertype is null && filter.Tags.Count == 0 && filter.Domains.Count == 0 && filter.Name is null
        && filter.Might is null && filter.EnergyCost is null && filter.Status.Count == 0 && filter.Keyword is null;
```

- `IsPlayerFilter` still requires no card fields: change its tail to `&& filter.Mighty is null && filter.Location is null && filter.Not is null && HasOnlyBasicFields(filter)`.

- [ ] **Step 6: Run the tests**

Run: `dotnet test tests/CromoBound.Engine.Tests`
Expected: all pass, including the four new ones.

- [ ] **Step 7: Build with 0 warnings, then commit**

Run: `dotnet build CromoBound.slnx --no-incremental` (0 Avvisi, 0 Errori), then:

```bash
git add src/CromoBound.Models/Effects schema/effects.schema.json src/CromoBound.Engine/Effects tests/CromoBound.Engine.Tests/ResolverTests.cs
git commit -m "feat(engine): resolve property values, comparisons and the mighty and location filters"
```

---

### Task 2: Abilities choose targets on the chain; steps choose on resolution

**Files:**
- Create: `src/CromoBound.Engine/Effects/ResolutionChoice.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Targets.cs`, `Game.Triggers.cs`, `Game.Activation.cs`
- Modify: `src/CromoBound.Engine/Effects/Steps/PermanentStepHandlers.cs` (Deal, Kill)
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Test: `tests/CromoBound.Engine.Tests/AbilityTargetTests.cs`

**Interfaces:**
- Consumes: Task 1's resolvers; Plan F's `TargetSlots`, `AskTargets`, `PutTriggersOnChain`, `ActivationTask`.
- Produces:
  - `Game.AskSlotTargets(PlayerId player, ObjectId card, EffectContext context, ChainItem? item, Action? onCancel) : bool` (`card`: the card being played, or the ability's source, as `ChooseTargetsDecision.Card`). It asks the next unfilled slot and returns true when it asked. A forced choice (exactly as many candidates as required) is applied and announced with `ChoiceMade`. `TargetsChosen` is emitted when `item` is given. `onCancel` (null: no cancel) runs on `CancelPlay`. `AskTargets` (plays) now calls it.
  - `Game.HasSlotCandidates(EffectContext context) : bool`: every slot has at least its required count.
  - Triggered abilities: their top-level target steps are slots, chosen right after the item is added (a `ChooseItemTargetsTask`). A trigger whose required slot has too few candidates is not added (deviation 4).
  - Activated abilities: `ActivationStage.Targets` comes first; targets are chosen before the cost, and the activation can be cancelled there. `CanActivate` requires `HasSlotCandidates`.
  - `ResolutionChoice.Targets(Game, ResolveEffectTask, ObjectRef) : IReadOnlyList<ObjectId>?`:
    - the objects for a step's target: a slot's chosen targets, Self, Host, a variable, or every candidate of an `all` selector;
    - a `count`/`upTo` selector that isn't a slot (inside an optional block) is chosen now with a `ChooseCardsDecision`, forced when exactly as many as required;
    - null means it asked.
  - `DealHandler` and `KillHandler` use it, and always record what they aimed at in `task.Result` (so `store` works even when the step did nothing).
  - `EffectsSupport.CheckSteps(..., targets: true)` for the top-level steps of triggered and activated abilities.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/AbilityTargetTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class AbilityTargetTests
{
    /// <summary>gear-1 played: deal 2 to an enemy unit. A trigger with a target.</summary>
    private const string Striker = """
        { "cardId": "gear-1", "status": "Full", "abilities": [
          { "kind": "Triggered", "trigger": { "event": "Played", "subject": { "ref": "Self" } },
            "steps": [ { "action": "Deal", "amount": 2, "target": { "select": "Unit", "count": 1, "filter": { "relation": "Enemy" } } } ] } ] }
        """;

    /// <summary>gear-1, exhaust: kill a unit. An activation with a target.</summary>
    private const string Slayer = """
        { "cardId": "gear-1", "status": "Full", "abilities": [
          { "kind": "Activated", "cost": { "exhaustSelf": true },
            "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    private static (TestGame Game, Rules.Game Engine) Setup(string json, Action<TestGame> arrange)
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", json)));
        arrange(game);
        return (game, game.Start());
    }

    [Fact]
    public void A_trigger_chooses_its_target_when_it_goes_on_the_chain()
    {
        var (game, engine) = Setup(Striker, g =>
        {
            g.Put("gear-1", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 1);
            g.Put("unit-2", Place.Base(P2));
            g.Put("unit-3", Place.Base(P2));
        });
        var big = game.First(Place.Base(P2), "unit-3");

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "gear-1")));
        engine.PayWithSuggestion(P1);
        var choose = engine.Decision<ChooseTargetsDecision>();
        Assert.Equal(2, choose.Options.Count);
        engine.Accept(P1, new ChooseTargets { Targets = [big] });
        Assert.Single(game.State.Chain);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Equal(2, game.State[big].Damage);
    }

    [Fact]
    public void A_trigger_with_one_legal_target_takes_it_without_asking()
    {
        var (game, engine) = Setup(Striker, g =>
        {
            g.Put("gear-1", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 1);
            g.Put("unit-2", Place.Base(P2));
        });

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "gear-1")));
        engine.PayWithSuggestion(P1);

        Assert.IsType<PriorityDecision>(engine.Pending);
        Assert.Single(game.State.Chain);
    }

    [Fact]
    public void A_trigger_with_no_legal_target_is_not_put_on_the_chain()
    {
        var (game, engine) = Setup(Striker, g =>
        {
            g.Put("gear-1", Place.Hand(P1));
            g.Runes(P1, "fury-rune", 1);
        });

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "gear-1")));
        engine.PayWithSuggestion(P1);

        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void An_activation_chooses_its_target_before_paying_and_can_be_cancelled_there()
    {
        var (game, engine) = Setup(Slayer, g =>
        {
            g.Put("gear-1", Place.Base(P1));
            g.Put("unit-2", Place.Base(P1));
            g.Put("unit-3", Place.Base(P2));
        });
        var gear = game.First(Place.Base(P1), "gear-1");
        var enemy = game.First(Place.Base(P2), "unit-3");

        engine.Accept(P1, new ActivateAbility(gear, 0));
        Assert.Equal(2, engine.Decision<ChooseTargetsDecision>().Options.Count);
        engine.Accept(P1, new CancelPlay());
        Assert.False(game.State[gear].Exhausted);
        Assert.Empty(game.State.Chain);

        engine.Accept(P1, new ActivateAbility(gear, 0));
        engine.Accept(P1, new ChooseTargets { Targets = [enemy] });
        Assert.True(game.State[gear].Exhausted);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "unit-3");
    }

    [Fact]
    public void An_activation_without_a_legal_target_is_not_offered()
    {
        var (game, engine) = Setup(Slayer, g => g.Put("gear-1", Place.Base(P1)));

        Assert.Empty(engine.Decision<PriorityDecision>().Activations);
    }

    [Fact]
    public void A_selector_that_isnt_a_slot_is_chosen_on_resolution_and_the_step_records_it()
    {
        var game = new TestGame();
        var a = game.Put("unit-2", Place.Base(P2));
        var b = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();
        var context = new EffectContext { Controller = P1, SourceCardId = "spell" };
        Step[] steps =
        [
            new DealStep { Amount = 0, Target = new ObjectRef { Select = SelectKind.Unit, Count = 1, Filter = new Filter { Relation = Relation.Enemy } }, Store = "aimed" },
        ];

        engine.RunNow(new ResolveEffectTask(context, steps, _ => { }));
        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal(new[] { a, b }, choose.Options);
        engine.Accept(P1, new ChooseCards { Cards = [b] });

        Assert.Equal(new[] { b }, context.Vars["aimed"].Objects);
        Assert.False(context.Vars["aimed"].Happened);
    }
}
```

`RunNow` is the existing test extension used by `EffectPlayTests`, and `PriorityDecision.Activations` is the list of `ActivateOption`s.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/CromoBound.Engine.Tests --filter "FullyQualifiedName~AbilityTargetTests"`
Expected: FAIL. The trigger and activation files are treated as Unmapped (a target in a triggered or activated ability is unsupported), so no `ChooseTargetsDecision` appears, and the last test deals 0 to every enemy unit instead of asking.

- [ ] **Step 3: Share the slot-asking code**

In `src/CromoBound.Engine/Rules/Game.Targets.cs`, replace `HasTargetsFor` and `AskTargets` with:

```csharp
    /// <summary>A spell the engine runs can be played only if every required target slot has enough candidates (rule 355).</summary>
    private bool HasTargetsFor(PlayerId player, CardInstance card) =>
        HasSlotCandidates(new EffectContext
        {
            Controller = player, Source = card.Id, SourceCardId = card.CardId, Slots = TargetSlots.Of(SpellSteps(card.CardId)),
        });

    /// <summary>Every slot has at least as many candidates as it requires.</summary>
    internal bool HasSlotCandidates(EffectContext context) =>
        context.Slots.All(slot => ObjectResolver.Candidates(this, context, slot).Count >= (slot.Count ?? 0));

    /// <summary>Spec §5.1: the spell's slots, chosen while it is played; cancelling undoes the play.</summary>
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
        if (!HasSlotCandidates(item.Effect))
        {
            UndoPlay(task);
            return false;
        }
        return AskSlotTargets(task.Player, card.Id, item.Effect, item, () => UndoPlay(task));
    }

    /// <summary>One decision per unfilled slot, in JSON order (spec §5.1). When the candidates are exactly as many as required the
    /// choice is forced: it is applied without asking and announced with <see cref="ChoiceMade"/>. Returns true when it asked.
    /// Callers check <see cref="HasSlotCandidates"/> first.</summary>
    internal bool AskSlotTargets(PlayerId player, ObjectId card, EffectContext context, ChainItem? item, Action? onCancel)
    {
        while (context.Targets.Count < context.Slots.Count)
        {
            var slot = context.Slots[context.Targets.Count];
            var options = ObjectResolver.Candidates(this, context, slot);
            var min = Math.Min(slot.Count ?? 0, options.Count);
            var max = Math.Min(slot.Count ?? slot.UpTo ?? 0, options.Count);
            if (options.Count == min)
            {
                context.Targets.Add(options);
                Emit(new ChoiceMade(player, "Targets", options));
                if (item is not null) Emit(new TargetsChosen(item.Id, context.Targets.Count - 1, options));
                continue;
            }
            Ask(new ChooseTargetsDecision(player, card, context.Targets.Count, options, min, max), (_, action) =>
            {
                if (action is CancelPlay && onCancel is not null)
                {
                    onCancel();
                    return null;
                }
                if (action is not ChooseTargets choose)
                    return Reject(RejectionCode.UnexpectedAction, onCancel is null ? "Choose the targets." : "Choose the targets, or cancel.");
                var chosen = choose.Targets;
                if (CheckPick(chosen, options, min, max, "targets") is { } rejection) return rejection;
                context.Targets.Add([.. chosen]);
                if (item is not null) Emit(new TargetsChosen(item.Id, context.Targets.Count - 1, [.. chosen]));
                return null;
            });
            return true;
        }
        return false;
    }
```

Note the old code undid the play when a slot had fewer than its count; that check now runs once, up front, in `AskTargets`.

- [ ] **Step 4: Triggers choose their targets**

In `src/CromoBound.Engine/Rules/Game.Triggers.cs`:
- add the task near `PutTriggersOnChainTask`:

```csharp
/// <summary>Chooses a triggered ability's targets right after it went on the chain (CR 382-388). Started at once, so cleanups and
/// other triggers wait for the choice.</summary>
internal sealed class ChooseItemTargetsTask(ChainItem item, PlayerId player) : GameTask
{
    public override bool Run(Game game) =>
        !game.State.Chain.Contains(item) || !game.AskSlotTargets(player, item.Source!.Value, item.Effect!, item, onCancel: null);
}
```

- replace `AddTriggers` with:

```csharp
    /// <summary>Each trigger becomes a finalized ability item; a chain they start doesn't pass focus when it closes (CR 346.1).
    /// A trigger with targets chooses them right after (one task per item, in the order they went on); one whose required target
    /// has no candidate is dropped.</summary>
    private void AddTriggers(List<PendingTrigger> triggers)
    {
        List<ChainItem> targeting = [];
        foreach (var trigger in triggers)
        {
            PendingTriggers.Remove(trigger);
            var context = new EffectContext
            {
                Controller = trigger.Controller,
                Source = trigger.Source,
                SourceCardId = trigger.SourceCardId,
                Slots = TargetSlots.Of(trigger.Ability.Steps),
            };
            if (!HasSlotCandidates(context)) continue;
            if (State.Chain.Count == 0) ChainStartedByTrigger = true;
            var item = AddAbilityItem(trigger.Controller, trigger.Source, trigger.SourceCardId, AbilityKind.Triggered,
                AbilityText(trigger.SourceCardId, trigger.Ability), trigger.Ability.Steps, context);
            Emit(new TriggerAdded(item.Id, trigger.Source, trigger.Controller));
            if (context.Slots.Count > 0) targeting.Add(item);
        }
        for (var i = targeting.Count - 1; i >= 0; i--) Push(new ChooseItemTargetsTask(targeting[i], targeting[i].Controller) { Started = true });
    }
```

`GameTask.Started` must be settable from here. It is already set by the run loop; if its setter isn't `internal` or wider, make it `internal set` (or `init`-compatible as `{ get; set; }`).

`ResolveEffectTask` for ability items already uses `item.Effect` as its context, so the chosen slots reach the steps with no other change.

- [ ] **Step 5: Activations choose their targets first**

In `src/CromoBound.Engine/Rules/Game.Activation.cs`:
- change the enum to `internal enum ActivationStage { Targets, Choose, Pay, Finalize, Cancelled }`.
- add a context the task keeps: in `ActivationTask`, add `public EffectContext? Context { get; set; }`.
- in `CanActivate`, before `return CostAction(...)`, add:

```csharp
        if (!HasSlotCandidates(ActivationContext(player, source, ability))) return false;
```

- replace `ActivationContext` with:

```csharp
    private static EffectContext ActivationContext(PlayerId player, CardInstance source, ActivatedAbility? ability = null) =>
        new() { Controller = player, Source = source.Id, SourceCardId = source.CardId, Slots = ability is null ? [] : TargetSlots.Of(ability.Steps) };
```

- in `RunActivation`, change `var context = ActivationContext(task.Player, source);` to `var context = task.Context ??= ActivationContext(task.Player, source, ability);`. Add a first case to the switch:

```csharp
                case ActivationStage.Targets:
                    if (AskSlotTargets(task.Player, source.Id, context, null, () => task.Stage = ActivationStage.Cancelled)) return false;
                    if (task.Stage == ActivationStage.Targets) task.Stage = ActivationStage.Choose;
                    break;
```

- in `FinishActivation`, after `AddAbilityItem(...)` returns, announce the chosen targets:

```csharp
        var item = AddAbilityItem(task.Player, source.Id, source.CardId, AbilityKind.Activated, AbilityText(source.CardId, ability), ability.Steps, context);
        for (var slot = 0; slot < context.Targets.Count; slot++) Emit(new TargetsChosen(item.Id, slot, context.Targets[slot]));
```

(replacing the existing bare `AddAbilityItem(...)` call).

- [ ] **Step 6: Steps choose on resolution and record their aim**

Create `src/CromoBound.Engine/Effects/ResolutionChoice.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>The objects a step acts on. Slots, Self, Host, variables and "all" selectors resolve as they always did; a count or
/// upTo selector that isn't a slot (a step inside an optional block) is chosen now by the controller, forced when there are
/// exactly as many candidates as required.</summary>
internal static class ResolutionChoice
{
    /// <summary>The objects, or null when it asked (the step returns Asked and runs again with the answer).</summary>
    public static IReadOnlyList<ObjectId>? Targets(Game game, ResolveEffectTask task, ObjectRef reference)
    {
        var context = task.Context;
        if (reference.Select is null || reference.All == true || TargetSlots.IndexOf(context.Slots, reference) >= 0
            || (reference.Count is null && reference.UpTo is null))
            return ObjectResolver.Resolve(game, context, reference);
        if (task.Answer is IReadOnlyList<ObjectId> chosen) return [.. chosen.Where(game.State.Exists)];
        var options = ObjectResolver.Candidates(game, context, reference);
        var min = Math.Min(reference.Count ?? 0, options.Count);
        var max = Math.Min(reference.Count ?? reference.UpTo ?? 0, options.Count);
        if (max == 0) return [];
        if (options.Count == min)
        {
            game.Emit(new ChoiceMade(context.Controller, "Cards", options));
            return options;
        }
        game.Ask(new ChooseCardsDecision(context.Controller, context.SourceCardId, options, min, max), (_, action) =>
        {
            if (action is not ChooseCards choose) return Game.Reject(RejectionCode.UnexpectedAction, "Choose the cards.");
            if (Game.CheckPick(choose.Cards, options, min, max, "cards") is { } rejection) return rejection;
            List<ObjectId> picked = [.. choose.Cards];
            task.Answer = picked;
            return null;
        });
        return null;
    }
}
```

In `PermanentStepHandlers.cs`, replace `DealHandler` and `KillHandler` with:

```csharp
internal sealed class DealHandler : StepHandler<DealStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, DealStep step)
    {
        if (ResolutionChoice.Targets(game, task, step.Target) is not { } aimed) return StepOutcome.Asked;
        List<ObjectId> units = [.. aimed.Where(id => game.State[id].Place.IsLocation && game.IsUnit(game.State[id]))];
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        task.Result = new EffectVar(units, [], amount, false);
        if (amount <= 0 || units.Count == 0) return StepOutcome.DidNothing;
        foreach (var unit in units) game.DealDamage(unit, amount);
        return StepOutcome.Done;
    }
}

internal sealed class KillHandler : StepHandler<KillStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, KillStep step)
    {
        if (ResolutionChoice.Targets(game, task, step.Target) is not { } aimed) return StepOutcome.Asked;
        List<ObjectId> targets = [.. aimed.Where(id => game.State[id].Place.IsLocation)];
        task.Result = new EffectVar(targets, [], null, false);
        if (targets.Count == 0) return StepOutcome.DidNothing;
        foreach (var target in targets) game.Kill(target);
        return StepOutcome.Done;
    }
}
```

(`ResolveEffectTask` overwrites `Happened` from the outcome when it stores the variable.)

- [ ] **Step 7: Let targets into triggered and activated abilities**

In `EffectsSupport.CheckAbility`, change the triggered and activated cases' `CheckSteps(..., targets: false)` to `targets: true`. Inner steps (inside an `Optional`) stay `targets: false`, but `IsSupportedTarget` must now allow a non-slot selector there, since `ResolutionChoice` chooses it. Replace `IsSupportedTarget` with:

```csharp
    /// <summary>Self, Host, a variable, a slot (top-level target steps of spells, triggers and activations) or, anywhere else, a
    /// unit, gear or permanent selector chosen on resolution; selectors need a supported filter.</summary>
    private static bool IsSupportedTarget(ObjectRef reference, bool targets) =>
        reference.Ref is RefKind.Self or RefKind.Host
        || reference.Var is not null
        || (reference.Select is SelectKind.Unit or SelectKind.Gear or SelectKind.Permanent && IsSupportedFilter(reference.Filter)
            && (targets ? TargetSlots.IsTarget(reference) || reference.All == true : true));
```

- [ ] **Step 8: Run all engine tests**

Run: `dotnet test tests/CromoBound.Engine.Tests`
Expected: all pass. Existing trigger and activation tests keep passing, because their abilities have no target selectors. If an existing test asserted that a target in a trigger made the file Unmapped, update that one assertion to the new support and say so in the report.

- [ ] **Step 9: Build with 0 warnings, then commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests/AbilityTargetTests.cs
git commit -m "feat(engine): choose targets for triggered and activated abilities"
```

---
### Task 3: ModifyMight, kill-this and spend-XP costs; Punch First, Divining Shells, Harnessed Dragon

**Files:**
- Create: `src/CromoBound.Engine/Effects/Steps/MightStepHandlers.cs`
- Modify: `src/CromoBound.Engine/Effects/Steps/StepRegistry.cs`, `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Mutations.cs`, `Game.Manual.cs`, `Game.Activation.cs`
- Create: `data/effects/punch-first.json`, `data/effects/divining-shells.json`, `data/effects/harnessed-dragon.json`
- Test: `tests/CromoBound.Engine.Tests/FioraDeckTests.cs`

**Interfaces:**
- Consumes: Task 1's values; Task 2's `ResolutionChoice.Targets`, ability targets.
- Produces:
  - `Game.ModifyMight(ObjectId unit, int amount, Duration duration)`: adds a `MightModifier`, emits `MightModified`, marks the board dirty. The manual might action uses it.
  - `Game.SpendXp(PlayerId player, int amount)`: lowers XP, emits `XpChanged`.
  - `ModifyMightHandler` (duration `ThisTurn` by default). It always records its targets and amount in `task.Result`.
  - Activated ability costs may be `{ "actions": [ { "action": "Kill", "target": { "ref": "Self" } } ] }` (the source is killed when the cost is paid) or `{ "actions": [ { "action": "SpendXp", "amount": N } ] }` (the player needs N XP to activate). Task 4's Equip uses the second.
  - `FioraDeckTests` (Tasks 3 to 7 add to it), with `Real(params string[] cardIds)` building a `TestGame` with those real cards.

- [ ] **Step 1: Write the card files**

`data/effects/punch-first.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "punch-first",
  "status": "Full",
  "keywords": [ { "keyword": "Action" } ],
  "abilities": [
    { "kind": "Spell", "line": 2,
      "steps": [ { "action": "ModifyMight", "target": { "select": "Unit", "count": 1 }, "amount": 5, "duration": "ThisTurn" } ] }
  ]
}
```

`data/effects/divining-shells.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "divining-shells",
  "status": "Full",
  "keywords": [ { "keyword": "Vision" } ],
  "abilities": [
    { "kind": "Activated", "line": 2, "timing": "Action",
      "cost": { "exhaustSelf": true, "actions": [ { "action": "Kill", "target": { "ref": "Self" } } ] },
      "steps": [ { "action": "ModifyMight", "target": { "select": "Unit", "count": 1 }, "amount": 2, "duration": "ThisTurn" } ] }
  ]
}
```

`data/effects/harnessed-dragon.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "harnessed-dragon",
  "status": "Full",
  "abilities": [
    { "kind": "Triggered", "line": 1,
      "trigger": { "event": "Played", "subject": { "ref": "Self" } },
      "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1, "filter": { "relation": "Enemy" } } } ] }
  ]
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/FioraDeckTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

/// <summary>The Fiora sample deck's cards, each played with its real card data and effects file (docs/effects-fiora.md §5).</summary>
public class FioraDeckTests
{
    private static TestGame Real(params string[] cardIds) => new(db: EngineTestDb.WithRealCards(cardIds));

    private static void PassBoth(Rules.Game engine)
    {
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
    }

    [Fact]
    public void Punch_first_gives_a_unit_five_might_this_turn_only()
    {
        var game = Real("punch-first", "body-rune");
        game.Put("punch-first", Place.Hand(P1));
        game.Runes(P1, "body-rune", 3);
        var unit = game.Put("unit-2", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "punch-first")));
        engine.PayWithSuggestion(P1);
        PassBoth(engine);

        Assert.Equal(7, engine.MightOf(unit));
        engine.Accept(P1, new EndTurn());
        Assert.Equal(2, engine.MightOf(unit));
    }

    [Fact]
    public void Divining_shells_kills_itself_to_give_a_unit_two_might()
    {
        var game = Real("divining-shells");
        var shells = game.Put("divining-shells", Place.Base(P1));
        var mine = game.Put("unit-2", Place.Base(P1));
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new ActivateAbility(shells, 0));
        engine.Accept(P1, new ChooseTargets { Targets = [mine] });
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "divining-shells");
        PassBoth(engine);

        Assert.Equal(4, engine.MightOf(mine));
    }

    [Fact]
    public void Divining_shells_needs_a_unit_to_target()
    {
        var game = Real("divining-shells");
        game.Put("divining-shells", Place.Base(P1));
        var engine = game.Start();

        Assert.Empty(engine.Decision<PriorityDecision>().Activations);
    }

    [Fact]
    public void Harnessed_dragon_kills_the_enemy_unit_its_player_picks()
    {
        var game = Real("harnessed-dragon", "order-rune");
        game.Put("harnessed-dragon", Place.Hand(P1));
        game.Runes(P1, "order-rune", 8);
        game.Put("unit-2", Place.Base(P2));
        var big = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "harnessed-dragon")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new ChooseTargets { Targets = [big] });
        PassBoth(engine);

        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "unit-3");
        Assert.Contains(game.State.At(Place.Base(P2)), id => game.State[id].CardId == "unit-2");
    }
}
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test tests/CromoBound.Engine.Tests --filter "FullyQualifiedName~FioraDeckTests"`
Expected: FAIL. `ModifyMight` isn't a supported step and the kill-self cost isn't supported, so Punch First and Divining Shells play by hand; Harnessed Dragon's trigger works through Task 2 and may already pass.

- [ ] **Step 4: Add the mutations**

In `src/CromoBound.Engine/Rules/Game.Mutations.cs`, after `GainXp`, add:

```csharp
    internal void SpendXp(PlayerId player, int amount)
    {
        if (amount <= 0) return;
        var state = State.Player(player);
        state.Xp -= amount;
        Emit(new XpChanged(player, state.Xp));
        MarkDirty();
    }

    /// <summary>A might change for a duration (2a's might modifiers: cleanup expires ThisTurn, combat's end ThisCombat).</summary>
    internal void ModifyMight(ObjectId unit, int amount, Duration duration)
    {
        State[unit].Modifiers.Add(new MightModifier(amount, duration));
        Emit(new MightModified(unit, amount, duration));
        MarkDirty();
    }
```

In `Game.Manual.cs`, replace the two lines

```csharp
                target.Modifiers.Add(new MightModifier(might.Amount, might.Duration));
                Emit(new MightModified(might.Unit, might.Amount, might.Duration));
```

with `ModifyMight(might.Unit, might.Amount, might.Duration);`. Keep any `MarkDirty()` that follows, or drop it if it becomes a duplicate.

- [ ] **Step 5: Add the step handler**

Create `src/CromoBound.Engine/Effects/Steps/MightStepHandlers.cs`:

```csharp
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>"Give a unit +N this turn": a might modifier on each unit aimed at (spec §3.2). It records its units and amount even
/// when the amount is 0, so a later step can read them (Rampage).</summary>
internal sealed class ModifyMightHandler : StepHandler<ModifyMightStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, ModifyMightStep step)
    {
        if (ResolutionChoice.Targets(game, task, step.Target) is not { } aimed) return StepOutcome.Asked;
        List<ObjectId> units = [.. aimed.Where(id => game.State[id].Place.IsLocation && game.IsUnit(game.State[id]))];
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        task.Result = new EffectVar(units, [], amount, false);
        if (units.Count == 0 || amount == 0) return StepOutcome.DidNothing;
        foreach (var unit in units) game.ModifyMight(unit, amount, step.Duration ?? Duration.ThisTurn);
        return StepOutcome.Done;
    }
}
```

In `StepRegistry.cs`, add `[typeof(ModifyMightStep)] = new ModifyMightHandler(),`.

- [ ] **Step 6: Support the step and the costs**

In `EffectsSupport.CheckStep`'s switch, add:

```csharp
            case ModifyMightStep might:
                CheckValue(might.Amount, at, problems);
                if (might.Duration is not (null or Duration.ThisTurn)) problems.Add($"{at}: duration");
                break;
```

Replace `IsSupportedCost` with:

```csharp
    /// <summary>Energy, power, exhausting the source, and at most one cost action: recycling a number of cards from your trash,
    /// killing the source, or spending a number of XP.</summary>
    private static bool IsSupportedCost(Cost cost) => cost.Actions switch
    {
        [] => true,
        [RecycleStep recycle] => recycle.Target is null && recycle.Count?.Literal is not null
            && recycle.From is { Zone: Zone.Trash, Position: null } from && (from.Owner is null || from.Owner.Kind == PlayerKind.You)
            && IsPlain(recycle),
        [KillStep kill] => kill.Target.Ref == RefKind.Self && IsPlain(kill),
        [SpendXpStep spend] => spend.Amount.Literal is not null && IsPlain(spend),
        _ => false,
    };

    /// <summary>A cost action acts for the ability's controller and names nothing else.</summary>
    private static bool IsPlain(Step step) => step.Player is null && step.Chooser is null && step.Script is null && step.Store is null;
```

- [ ] **Step 7: Pay the new costs**

In `src/CromoBound.Engine/Rules/Game.Activation.cs`:
- add after `CostCount`:

```csharp
    private static bool KillsSelf(ActivatedAbility ability) => ability.Cost?.Actions is [KillStep { Target.Ref: RefKind.Self }];

    private int XpCost(EffectContext context, ActivatedAbility ability) =>
        ability.Cost?.Actions is [SpendXpStep spend] ? ValueResolver.Resolve(this, context, spend.Amount) : 0;
```

- in `CanActivate`, after the exhaust check, add `if (State.Player(player).Xp < XpCost(context, ability)) return false;`.
- in `FinishActivation`, after the exhaust line, add:

```csharp
        if (XpCost(context, ability) is var xp and > 0) SpendXp(task.Player, xp);
        if (KillsSelf(ability)) Kill(source.Id);
```

The ability then goes on the chain with the source's old id, as a Deathknell's does; its steps don't read Self.

- [ ] **Step 8: Run the tests**

Run: `dotnet test tests/CromoBound.Engine.Tests`
Expected: all pass, including `FioraDeckTests` and `EffectsDataTests` (the three new files run as Full).

- [ ] **Step 9: Build with 0 warnings, then commit**

```bash
git add src/CromoBound.Engine data/effects/punch-first.json data/effects/divining-shells.json data/effects/harnessed-dragon.json tests/CromoBound.Engine.Tests/FioraDeckTests.cs
git commit -m "feat(engine): give might for a turn and pay costs by killing the source or spending xp"
```

---

### Task 4: Passive might and keywords; Doran's Blade, Shepherd's Heirloom, Fiora Victorious

**Files:**
- Modify: `src/CromoBound.Engine/Effects/Modifiers.cs`, `src/CromoBound.Engine/Rules/Game.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Create: `data/effects/dorans-blade.json`, `data/effects/shepherds-heirloom.json`, `data/effects/fiora-victorious.json`
- Test: `tests/CromoBound.Engine.Tests/FioraDeckTests.cs`

**Interfaces:**
- Consumes: Task 1's `Host`, `compare`, `IsSupportedValue`, `IsSupportedCondition`; Task 3's spend-XP cost.
- Produces:
  - `Modifiers.ActiveModifiers(Game game, CardInstance holder) : IEnumerable<(Modifier Modifier, EffectContext Context)>`. These are the modifiers that apply to the holder now:
    - its own passives with `appliesTo` Self;
    - the passives of gear attached to it with `appliesTo` Host.
    In both cases the passive's source must be on the board and its `condition` and `while` must hold. While one holder's are being evaluated, a nested might or keyword read of that same holder sees only printed values (`Game.EvaluatingPassives`), so conditions can't loop.
  - `Modifiers.MightOf` adds `ModifyMight` passives. `Modifiers.KeywordValue` and `Game.Has` add `GrantKeyword` passives.
  - Equip's cost may be a spend-XP action.

- [ ] **Step 1: Write the card files**

`data/effects/dorans-blade.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "dorans-blade",
  "status": "Full",
  "keywords": [ { "keyword": "Equip", "cost": { "power": ["Body"] } } ],
  "abilities": [
    { "kind": "Passive",
      "modifiers": [ { "type": "ModifyMight", "amount": 2, "appliesTo": { "ref": "Host" } } ] }
  ]
}
```

`data/effects/shepherds-heirloom.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "shepherds-heirloom",
  "status": "Full",
  "keywords": [ { "keyword": "Equip", "cost": { "actions": [ { "action": "SpendXp", "amount": 1 } ] } } ],
  "abilities": [
    { "kind": "Triggered", "line": 1,
      "trigger": { "event": "Played", "subject": { "ref": "Self" } },
      "steps": [ { "action": "GainXp", "amount": 1 } ] },
    { "kind": "Passive",
      "modifiers": [ { "type": "ModifyMight", "amount": 2, "appliesTo": { "ref": "Host" } } ] }
  ]
}
```

`data/effects/fiora-victorious.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "fiora-victorious",
  "status": "Full",
  "abilities": [
    { "kind": "Passive", "line": 1,
      "while": { "compare": [ { "prop": "Might", "of": { "ref": "Self" } }, "gte", 5 ] },
      "modifiers": [
        { "type": "GrantKeyword", "keyword": { "keyword": "Deflect" }, "appliesTo": { "ref": "Self" } },
        { "type": "GrantKeyword", "keyword": { "keyword": "Ganking" }, "appliesTo": { "ref": "Self" } },
        { "type": "GrantKeyword", "keyword": { "keyword": "Shield" }, "appliesTo": { "ref": "Self" } }
      ] }
  ]
}
```

- [ ] **Step 2: Write the failing tests**

Append to `FioraDeckTests`:

```csharp
    [Fact]
    public void Dorans_blade_gives_two_might_only_while_attached()
    {
        var game = Real("dorans-blade", "body-rune");
        var blade = game.Put("dorans-blade", Place.Base(P1));
        var unit = game.Put("unit-2", Place.Base(P1));
        game.Runes(P1, "body-rune", 1);
        var engine = game.Start();
        Assert.Equal(2, engine.MightOf(unit));

        engine.Accept(P1, new ActivateAbility(blade, 1));
        engine.PayWithSuggestion(P1);
        PassBoth(engine);

        Assert.Equal(unit, game.State[blade].AttachedTo);
        Assert.Equal(4, engine.MightOf(unit));
        game.State[blade].AttachedTo = null;
        Assert.Equal(2, engine.MightOf(unit));
    }

    [Fact]
    public void Shepherds_heirloom_gives_xp_when_played_and_spends_it_to_equip()
    {
        var game = Real("shepherds-heirloom");
        game.Put("shepherds-heirloom", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 2);
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "shepherds-heirloom")));
        engine.PayWithSuggestion(P1);
        PassBoth(engine);
        Assert.Equal(1, game.State.Player(P1).Xp);

        var heirloom = game.First(Place.Base(P1), "shepherds-heirloom");
        engine.Accept(P1, new ActivateAbility(heirloom, 2));
        Assert.Equal(0, game.State.Player(P1).Xp);
        PassBoth(engine);

        Assert.Equal(4, engine.MightOf(unit));
        Assert.DoesNotContain(engine.Decision<PriorityDecision>().Activations, a => a.Source == heirloom);
    }

    [Fact]
    public void Fiora_victorious_gets_her_keywords_only_while_mighty()
    {
        var game = Real("fiora-victorious");
        var fiora = game.Put("fiora-victorious", Place.Base(P1));
        var engine = game.Start();
        var instance = game.State[fiora];

        Assert.False(engine.Has(instance, DisplayKeyword.Ganking));
        Assert.Equal(0, Effects.Modifiers.KeywordValue(engine, instance, MechanicalKeyword.Deflect));

        instance.Modifiers.Add(new MightModifier(1, Duration.ThisTurn));

        Assert.True(engine.Has(instance, DisplayKeyword.Ganking));
        Assert.True(engine.Has(instance, DisplayKeyword.Shield));
        Assert.Equal(1, Effects.Modifiers.KeywordValue(engine, instance, MechanicalKeyword.Deflect));
        instance.Role = CombatRole.Defender;
        Assert.Equal(6, engine.MightOf(fiora));
    }
```

Add `using CromoBound.Models.Cards;` and `using CromoBound.Models.Effects;` at the top (`DisplayKeyword`, `MechanicalKeyword`, `Duration`).

The heirloom's abilities are listed as the file's own first (the trigger at 0, the passive at 1), then the keywords' (Equip at 2). The blade's are the passive (0) and Equip (1).

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test tests/CromoBound.Engine.Tests --filter "FullyQualifiedName~FioraDeckTests"`
Expected: the three new tests FAIL. The files are Unmapped: `ModifyMight` and `GrantKeyword` passives, `while` and an Equip cost action aren't supported.

- [ ] **Step 4: Evaluate the passives**

In `src/CromoBound.Engine/Rules/Game.cs`:
- add `internal HashSet<ObjectId> EvaluatingPassives { get; } = [];` next to `PendingTriggers`.
- replace `Has` with:

```csharp
    /// <summary>Whether the card has the keyword: printed (from its effects file when the engine runs it, else from the starts of its
    /// text lines, spec §7.10) or granted by one of its passives now.</summary>
    internal bool Has(CardInstance instance, DisplayKeyword keyword) =>
        Effects.For(instance.CardId).Keywords.Contains(keyword)
        || Modifiers.GrantedKeywords(this, instance).Any(k => k.Keyword.ToString() == keyword.ToString());
```

In `src/CromoBound.Engine/Effects/Modifiers.cs`:
- change `MightOf`'s return to add `+ PassiveMight(game, unit)` and its summary to mention "ModifyMight passives (its own and its gear's)".
- replace `KeywordValue` with:

```csharp
    /// <summary>The sum of a numbered keyword's values on the card, printed and granted: a missing value is 1, and several
    /// instances stack.</summary>
    public static int KeywordValue(Game game, CardInstance instance, MechanicalKeyword keyword) =>
        game.Effects.For(instance.CardId).KeywordEntries.Concat(GrantedKeywords(game, instance))
            .Where(k => k.Keyword == keyword).Sum(k => k.Value ?? 1);
```

- add:

```csharp
    /// <summary>The keyword entries the card's passives grant it now.</summary>
    public static IEnumerable<KeywordEntry> GrantedKeywords(Game game, CardInstance instance) =>
        ActiveModifiers(game, instance).Select(m => m.Modifier).OfType<GrantKeywordModifier>().Select(g => g.Keyword);

    private static int PassiveMight(Game game, CardInstance unit) =>
        ActiveModifiers(game, unit).Where(m => m.Modifier is ModifyMightModifier)
            .Sum(m => ValueResolver.Resolve(game, m.Context, ((ModifyMightModifier)m.Modifier).Amount));

    /// <summary>The modifiers that apply to the holder now:
    /// - its own passives with appliesTo Self;
    /// - the passives of gear attached to it with appliesTo Host.
    /// In both cases the source must be on the board and the passive's condition and while must hold. While a holder's are
    /// evaluated, a nested might or keyword read of it sees only printed values, so a condition that reads its might can't loop.</summary>
    public static IEnumerable<(Modifier Modifier, EffectContext Context)> ActiveModifiers(Game game, CardInstance holder)
    {
        if (!holder.Place.IsLocation || !game.EvaluatingPassives.Add(holder.Id)) return [];
        try
        {
            List<(Modifier, EffectContext)> found = [.. Applying(game, holder, holder, RefKind.Self)];
            foreach (var gear in game.State.Objects.Where(o => o.AttachedTo == holder.Id && o.Place.IsLocation))
                found.AddRange(Applying(game, gear, holder, RefKind.Host));
            return found;
        }
        finally
        {
            game.EvaluatingPassives.Remove(holder.Id);
        }
    }

    private static IEnumerable<(Modifier, EffectContext)> Applying(Game game, CardInstance source, CardInstance holder, RefKind appliesTo)
    {
        var context = new EffectContext { Controller = source.Controller, Source = source.Id, SourceCardId = source.CardId };
        return game.Effects.For(source.CardId).Abilities.OfType<PassiveAbility>()
            .Where(p => (p.Condition is null || ConditionResolver.Holds(game, context, p.Condition))
                && (p.While is null || ConditionResolver.Holds(game, context, p.While)))
            .SelectMany(p => p.Modifiers)
            .Where(m => m.AppliesTo?.Ref == appliesTo)
            .Select(m => (m, context));
    }
```

`OwnPassives` (used by `CostOf` and `Permits`) keeps its own code. Change its condition check to read `While` too:

```csharp
            .Where(p => (p.Condition is null || ConditionResolver.Holds(game, context, p.Condition))
                && (p.While is null || ConditionResolver.Holds(game, context, p.While)))
```

- [ ] **Step 5: Support the passives and the Equip cost**

In `EffectsSupport`:
- in `CheckAbility`, replace the `condition` line with

```csharp
        var condition = ability.Condition is null || (ability is PassiveAbility && IsSupportedCondition(ability.Condition));
```

- in the `PassiveAbility` case, replace `if (passive.While is not null) problems.Add($"{at}: while");` with `if (passive.While is not null && !IsSupportedCondition(passive.While)) problems.Add($"{at}: while");`.
- replace `IsSupportedModifier` with:

```csharp
    /// <summary>A modifier of a kind <see cref="Modifiers"/> evaluates: on the card itself, a literal energy cost reduction, the
    /// permission to be played to a battlefield with enemy units, or a granted Assault, Deflect, Ganking, Shield or Tank; on the
    /// card itself or its host, a might change.</summary>
    private static bool IsSupportedModifier(Modifier modifier) => modifier switch
    {
        CostReductionModifier reduction => modifier.AppliesTo?.Ref == RefKind.Self && reduction.Energy?.Literal is not null
            && reduction.Power.Count == 0 && reduction.Minimum is null && reduction.FromZone is null,
        PermissionModifier permission => modifier.AppliesTo?.Ref == RefKind.Self && permission.Permission == Permission.PlayToBattlefieldWithEnemyUnits,
        ModifyMightModifier might => modifier.AppliesTo?.Ref is RefKind.Self or RefKind.Host && IsSupportedValue(might.Amount),
        GrantKeywordModifier grant => modifier.AppliesTo?.Ref == RefKind.Self
            && grant.Keyword.Keyword is MechanicalKeyword.Assault or MechanicalKeyword.Deflect or MechanicalKeyword.Ganking
                or MechanicalKeyword.Shield or MechanicalKeyword.Tank
            && grant.Keyword.Cost is null && grant.Keyword.Steps.Count == 0,
        _ => false,
    };
```

- in `CheckKeyword`, replace the last `if` (the `WithCost` one) with:

```csharp
        if (WithCost.Contains(entry.Keyword) && (entry.Cost is null || entry.Cost.ExhaustSelf is not null
            || (entry.Cost.Actions.Count > 0 && !(entry.Keyword == MechanicalKeyword.Equip
                && entry.Cost.Actions is [SpendXpStep { Amount.Literal: not null } spend] && IsPlain(spend)))))
            problems.Add($"{at}: cost");
```

`CardEffects.KeywordAbilities` already gives the Equip ability the keyword's whole cost, actions included, so Task 3's activation code spends the XP.

- [ ] **Step 6: Run the tests**

Run: `dotnet test tests/CromoBound.Engine.Tests`
Expected: all pass.

- [ ] **Step 7: Build with 0 warnings, then commit**

```bash
git add src/CromoBound.Engine data/effects/dorans-blade.json data/effects/shepherds-heirloom.json data/effects/fiora-victorious.json tests/CromoBound.Engine.Tests/FioraDeckTests.cs
git commit -m "feat(engine): apply passive might and granted keywords, including equipment bonuses"
```

---
### Task 5: Additional costs; Rampage and Sacrifice

**Files:**
- Create: `src/CromoBound.Engine/Rules/Game.Costs.cs`
- Modify: `src/CromoBound.Engine/Effects/CardEffects.cs` (`CardEffectInfo.AdditionalCosts`)
- Modify: `src/CromoBound.Engine/Rules/Game.Play.cs`, `Game.Priority.cs`, `src/CromoBound.Engine/Effects/Modifiers.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Create: `data/effects/rampage.json`, `data/effects/sacrifice.json`
- Test: `tests/CromoBound.Engine.Tests/AdditionalCostTests.cs`

**Interfaces:**
- Consumes: Task 1's `paid` condition and `var` values; Task 2's recorded targets; Task 3's `ModifyMight`.
- Produces:
  - `CardEffectInfo` gains a last parameter, `IReadOnlyList<AdditionalCost> AdditionalCosts`: the file's additional costs for Full and Partial cards, empty otherwise. Update every `new CardEffectInfo(...)` (in `CardEffects`, and in tests if any build one).
  - `PlayStep.Extra`, between `Targets` and `Cost`. For each additional cost, in file order:
    - an optional one asks `OptionalDecision($"Pay the additional cost: {text}?")`;
    - a paid cost whose action is a `Kill` of a selector then chooses the units (forced when exactly enough; a `ChooseCardsDecision` otherwise).
    `CancelPlay` undoes the play at either question.
  - The play's `EffectContext.Vars[cost.Id]`: paid is `EffectVar(chosen units, [], 1, true)`, unpaid `EffectVar([], [], 0, false)`.
  - `Modifiers.CostOf` adds the paid costs' energy and power.
  - The chosen units are killed when the play finalizes, after payment (deviation 7).
  - A card whose mandatory kill cost has too few candidates isn't playable.

- [ ] **Step 1: Write the card files**

`data/effects/rampage.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "rampage",
  "status": "Full",
  "additionalCosts": [ { "id": "body", "optional": true, "cost": { "power": ["Body"] } } ],
  "abilities": [
    { "kind": "Spell", "line": [1, 2],
      "steps": [
        { "action": "ModifyMight", "target": { "select": "Unit", "count": 1, "filter": { "relation": "Friendly" } },
          "amount": { "mul": [2, { "var": "body" }] }, "duration": "ThisTurn", "store": "friend" },
        { "action": "Deal", "target": { "select": "Unit", "count": 1, "filter": { "relation": "Enemy" } },
          "amount": { "prop": "Might", "of": { "var": "friend" } }, "source": { "var": "friend" }, "store": "foe" },
        { "action": "Deal", "target": { "var": "friend" },
          "amount": { "prop": "Might", "of": { "var": "foe" } }, "source": { "var": "foe" } }
      ] }
  ]
}
```

`data/effects/sacrifice.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "sacrifice",
  "status": "Full",
  "keywords": [ { "keyword": "Reaction" } ],
  "additionalCosts": [
    { "id": "sacrifice",
      "cost": { "actions": [ { "action": "Kill", "target": { "select": "Unit", "count": 1, "filter": { "relation": "Friendly", "mighty": true } } } ] } }
  ],
  "abilities": [
    { "kind": "Spell", "line": 3,
      "steps": [ { "action": "Draw", "amount": 2 }, { "action": "Channel", "count": 1, "exhausted": true } ] }
  ]
}
```

(Rampage's two Deal steps read the units' might after the +2 and before any damage counts: damage kills only at cleanup.)

- [ ] **Step 2: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/AdditionalCostTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class AdditionalCostTests
{
    private static TestGame Real(params string[] cardIds) => new(db: EngineTestDb.WithRealCards(cardIds));

    private static void PassBoth(Rules.Game engine)
    {
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
    }

    [Fact]
    public void Rampage_with_the_extra_body_gives_two_might_and_the_units_hit_each_other()
    {
        var game = Real("rampage", "body-rune");
        game.Put("rampage", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 3);
        game.Runes(P1, "body-rune", 1);
        var friend = game.Put("unit-3", Place.Base(P1));
        var foe = game.Put("unit-2", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "rampage")));
        engine.Accept(P1, new ChooseOptional(true));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(3, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Body }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);
        PassBoth(engine);

        Assert.Equal(5, engine.MightOf(friend));
        Assert.Equal(2, game.State[friend].Damage);
        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "unit-2");
        Assert.False(game.State.Exists(foe) && game.State[foe].Place.IsLocation);
    }

    [Fact]
    public void Rampage_without_the_extra_cost_uses_the_printed_mights()
    {
        var game = Real("rampage");
        game.Put("rampage", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 3);
        game.Put("unit-2", Place.Base(P1));
        var foe = game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "rampage")));
        engine.Accept(P1, new ChooseOptional(false));
        Assert.Empty(engine.Decision<PayCostDecision>().Cost.Power);
        engine.PayWithSuggestion(P1);
        PassBoth(engine);

        Assert.Equal(2, game.State[foe].Damage);
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "unit-2");
    }

    [Fact]
    public void Cancelling_rampage_after_choosing_the_extra_cost_spends_nothing()
    {
        var game = Real("rampage", "body-rune");
        var card = game.Put("rampage", Place.Hand(P1));
        game.Runes(P1, "body-rune", 4);
        game.Put("unit-2", Place.Base(P1));
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(card));
        engine.Accept(P1, new ChooseOptional(true));
        engine.Accept(P1, new CancelPlay());

        Assert.Contains(game.State.At(Place.Hand(P1)), id => game.State[id].CardId == "rampage");
        Assert.All(game.State.At(Place.Base(P1)).Where(id => game.State[id].CardId == "body-rune"), id => Assert.False(game.State[id].Exhausted));
        Assert.Equal(4, game.State.At(Place.Base(P1)).Count(id => game.State[id].CardId == "body-rune"));
    }

    [Fact]
    public void Sacrifice_kills_a_friendly_mighty_unit_then_draws_two_and_channels_one_exhausted()
    {
        var game = Real("sacrifice");
        game.Put("sacrifice", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        game.Put("fury-rune", Place.RuneDeck(P1));
        var big = game.Put("unit-3", Place.Base(P1));
        game.State[big].Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();
        var hand = game.State.At(Place.Hand(P1)).Count;

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "sacrifice")));
        engine.PayWithSuggestion(P1);
        Assert.Contains(game.State.At(Place.Trash(P1)), id => game.State[id].CardId == "unit-3");
        PassBoth(engine);

        Assert.Equal(hand - 1 + 2, game.State.At(Place.Hand(P1)).Count);
        var runes = game.State.At(Place.Base(P1)).Where(id => game.State[id].CardId == "fury-rune").ToList();
        Assert.Equal(2, runes.Count);
        Assert.All(runes, id => Assert.True(game.State[id].Exhausted));
    }

    [Fact]
    public void Sacrifice_needs_a_friendly_mighty_unit()
    {
        var game = Real("sacrifice");
        var card = game.Put("sacrifice", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        game.Put("unit-3", Place.Base(P1));
        var engine = game.Start();

        Assert.DoesNotContain(card, engine.Decision<PriorityDecision>().Playable);
    }

    [Fact]
    public void Cancelling_sacrifice_before_paying_kills_nothing()
    {
        var game = Real("sacrifice");
        var card = game.Put("sacrifice", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 1);
        var a = game.Put("unit-3", Place.Base(P1));
        var b = game.Put("unit-3", Place.Base(P1));
        foreach (var unit in new[] { a, b }) game.State[unit].Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(card));
        Assert.Equal(new[] { a, b }, engine.Decision<ChooseCardsDecision>().Options);
        engine.Accept(P1, new ChooseCards { Cards = [a] });
        engine.Accept(P1, new CancelPlay());

        Assert.True(game.State[a].Place.IsLocation);
        Assert.Contains(game.State.At(Place.Hand(P1)), id => game.State[id].CardId == "sacrifice");
    }
}
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test tests/CromoBound.Engine.Tests --filter "FullyQualifiedName~AdditionalCostTests"`
Expected: FAIL. `additionalCosts` makes both files Unmapped, and `Deal` with a `source` is unsupported.

- [ ] **Step 4: Carry additional costs in `CardEffectInfo`**

In `CardEffects.cs`:
- add `IReadOnlyList<AdditionalCost> AdditionalCosts` as the last parameter of `CardEffectInfo`, with a doc line: "the file's additional costs (CR 356 step b) for Full and Partial cards".
- pass `file.AdditionalCosts` in the Full and Partial returns, and `[]` in the rune and `Unmapped` returns.

- [ ] **Step 5: Ask, record and pay the costs**

Create `src/CromoBound.Engine/Rules/Game.Costs.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

/// <summary>Additional costs (CR 356 step b, docs/effects-fiora.md §3.1): asked after the targets, recorded in the play's
/// variables, added to the total cost, and their actions done when the play finalizes.</summary>
public sealed partial class Game
{
    private static readonly EffectVar Unpaid = new([], [], 0, false);

    /// <summary>The kill action of a cost, when it has one.</summary>
    private static KillStep? KillAction(AdditionalCost cost) => cost.Cost.Actions is [KillStep kill] ? kill : null;

    /// <summary>A card can be played only if each mandatory cost action has enough candidates.</summary>
    private bool HasAdditionalCostCandidates(PlayerId player, CardInstance card)
    {
        var context = new EffectContext { Controller = player, Source = card.Id, SourceCardId = card.CardId };
        return Effects.For(card.CardId).AdditionalCosts
            .Where(c => c.Optional != true && KillAction(c) is not null)
            .All(c => ObjectResolver.Candidates(this, context, KillAction(c)!.Target).Count >= (KillAction(c)!.Target.Count ?? 1));
    }

    /// <summary>For each cost in file order: an optional one asks whether to pay; a paid one with a kill action chooses its units.
    /// Returns true when it asked; cancelling undoes the play.</summary>
    private bool AskAdditionalCosts(PlayCardTask task)
    {
        var item = task.Item!;
        var context = item.Effect!;
        var card = State[item.Card!.Value];
        foreach (var cost in Effects.For(card.CardId).AdditionalCosts)
        {
            if (!context.Vars.TryGetValue(cost.Id, out var recorded))
            {
                if (cost.Optional == true)
                {
                    Ask(new OptionalDecision(task.Player, card.CardId, $"Pay the additional cost: {Describe(cost.Cost)}?"), (_, action) =>
                    {
                        if (action is CancelPlay)
                        {
                            UndoPlay(task);
                            return null;
                        }
                        if (action is not ChooseOptional choice) return Reject(RejectionCode.UnexpectedAction, "Answer yes or no, or cancel.");
                        context.Vars[cost.Id] = choice.Yes ? new EffectVar([], [], 1, true) : Unpaid;
                        return null;
                    });
                    return true;
                }
                recorded = context.Vars[cost.Id] = new EffectVar([], [], 1, true);
            }
            if (!recorded.Happened || KillAction(cost) is not { } kill || recorded.Objects.Count > 0) continue;
            var options = ObjectResolver.Candidates(this, context, kill.Target);
            var count = kill.Target.Count ?? 1;
            if (options.Count < count)
            {
                UndoPlay(task);
                return false;
            }
            if (options.Count == count)
            {
                context.Vars[cost.Id] = recorded with { Objects = options };
                Emit(new ChoiceMade(task.Player, "Cards", options));
                continue;
            }
            Ask(new ChooseCardsDecision(task.Player, card.CardId, options, count, count), (_, action) =>
            {
                if (action is CancelPlay)
                {
                    UndoPlay(task);
                    return null;
                }
                if (action is not ChooseCards choose) return Reject(RejectionCode.UnexpectedAction, "Choose the units to kill, or cancel.");
                if (CheckPick(choose.Cards, options, count, count, "units") is { } rejection) return rejection;
                context.Vars[cost.Id] = recorded with { Objects = [.. choose.Cards] };
                return null;
            });
            return true;
        }
        return false;
    }

    /// <summary>After payment: the paid costs' actions happen (the chosen units still on the board are killed).</summary>
    private void PayAdditionalActions(PlayCardTask task)
    {
        var context = task.Item!.Effect!;
        foreach (var cost in Effects.For(State[task.Item.Card!.Value].CardId).AdditionalCosts)
            if (context.Vars.TryGetValue(cost.Id, out var paid) && paid.Happened && KillAction(cost) is not null)
                foreach (var unit in paid.Objects.Where(id => State.Exists(id) && State[id].Place.IsLocation).ToList())
                    Kill(unit);
    }

    /// <summary>"1 energy and Body": the cost in words for the question.</summary>
    private static string Describe(Cost cost)
    {
        List<string> parts = [];
        if (cost.Energy is > 0 and var energy) parts.Add($"{energy} energy");
        parts.AddRange(cost.Power.Select(p => p.ToString()));
        if (cost.Actions is [KillStep]) parts.Add("kill a unit");
        return parts.Count == 0 ? "nothing" : string.Join(" and ", parts);
    }
}
```

In `src/CromoBound.Engine/Rules/Game.Play.cs`:
- change the enum to `internal enum PlayStep { ToChain, Choices, Targets, Extra, Cost, Pay, Finalize, Cancelled }`.
- in `RunPlay`, change the Targets case's follow-up to `if (task.Step == PlayStep.Targets) task.Step = PlayStep.Extra;` and add:

```csharp
                case PlayStep.Extra:
                    if (AskAdditionalCosts(task)) return false;
                    if (task.Step == PlayStep.Extra) task.Step = PlayStep.Cost;
                    break;
```

- in the Finalize case, call `PayAdditionalActions(task);` before `FinishFinalizing(task);`.

In `Game.Priority.cs`'s `PlayableCards`, add `&& HasAdditionalCostCandidates(player, card)` next to both `HasTargetsFor(...)` checks (use `State[id]` where the loop has an id).

In `src/CromoBound.Engine/Effects/Modifiers.cs`'s `CostOf`, after `List<PowerSymbol> power = [.. cost.Power];`, add:

```csharp
        var extra = 0;
        foreach (var additional in game.Effects.For(card.CardId).AdditionalCosts)
            if (item.Effect is { } paidIn && paidIn.Vars.TryGetValue(additional.Id, out var paid) && paid.Happened)
            {
                extra += additional.Cost.Energy ?? 0;
                power.AddRange(additional.Cost.Power);
            }
```

and change the returned energy to `Math.Max(0, cost.Energy + extra - reduction)`. Add "plus the additional costs paid" to its summary.

- [ ] **Step 6: Support additional costs and `Deal`'s source**

In `EffectsSupport.Problems`, replace `if (file.AdditionalCosts.Count > 0) problems.Add("additionalCosts");` with:

```csharp
        for (var c = 0; c < file.AdditionalCosts.Count; c++)
            if (!IsSupportedAdditionalCost(file.AdditionalCosts[c])) problems.Add($"additionalCosts[{c}]");
```

and add:

```csharp
    /// <summary>Energy and power, or one kill of a unit selector with a supported filter; no steps on paying, no cost changes.</summary>
    private static bool IsSupportedAdditionalCost(AdditionalCost cost) =>
        cost.OnPaid.Count == 0 && cost.ModifiesCost is null && cost.Cost.ExhaustSelf is null
        && cost.Cost.Actions switch
        {
            [] => true,
            [KillStep kill] => kill.Target is { Select: SelectKind.Unit, Count: not null } target && IsSupportedFilter(target.Filter) && IsPlain(kill),
            _ => false,
        };
```

In `CheckStep`'s `DealStep` case, replace the split/bonus/source line with:

```csharp
                if (deal.Split is not null || deal.Bonus is not null) problems.Add($"{at}: split or bonus");
                if (deal.Source is { } source && source.Ref != RefKind.Self && source.Var is null) problems.Add($"{at}: source");
```

The source is recorded for Plan P (Elder Dragon's lethal damage); Plan O's handler doesn't read it.

- [ ] **Step 7: Run the tests**

Run: `dotnet test tests/CromoBound.Engine.Tests`
Expected: all pass.

- [ ] **Step 8: Build with 0 warnings, then commit**

```bash
git add src/CromoBound.Engine data/effects/rampage.json data/effects/sacrifice.json tests/CromoBound.Engine.Tests/AdditionalCostTests.cs
git commit -m "feat(engine): pay additional costs when playing a card"
```

---

### Task 6: Empower counts and cost reductions; Kayle, Risen Altar, the board's chip

**Files:**
- Modify: `src/CromoBound.Engine/State/CardInstance.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Mutations.cs` (`Empower`), `Game.Activation.cs` (Reduce stage)
- Modify: `src/CromoBound.Engine/Effects/Steps/PermanentStepHandlers.cs` (`EmpowerHandler`), `Effects/CardEffects.cs`, `Effects/Modifiers.cs`, `Effects/Resolvers/ValueResolver.cs`, `Effects/EffectsSupport.cs`
- Modify: `src/CromoBound.Engine/Views/PlayerView.cs`, `Views/ViewBuilder.cs`
- Modify: `src/CromoBound.Client/Board/BoardParts.cs`, `BoardModel.cs`, `Components/CardFace.razor`, `Components/CardZoom.razor`
- Create: `data/effects/kayle-justified.json`, `data/effects/risen-altar.json`
- Test: `tests/CromoBound.Engine.Tests/EmpowerTests.cs`, `tests/CromoBound.Client.Tests/BoardViewTests.cs`

**Interfaces:**
- Consumes: Task 1's `compare` and `EmpowerCount` property; Task 4's passives; Plan F's Empower keyword ability.
- Produces:
  - `CardInstance.EmpowerCount : int`. `Empowered` becomes `EmpowerCount > 0`: setting it true makes the count at least 1, false makes it 0.
  - `Game.Empower(ObjectId id)`: the first Empower goes through `SetStatus(Empowered, true)`, so `BecameEmpowered` triggers as before; later ones raise the count and mark the board dirty.
  - The Empower keyword's `value` is its limit (deviation 1). Its ability's `useOnlyIf` is `EmpowerCount < limit`. `Modifiers.EmpowerLimit(Game, CardInstance) : int`.
  - `ActivationStage.Reduce`, between `Choose` and `Pay`. An Empower activation's cost is lowered by each `KeywordCostReduction` for Empower that applies to the source (`Modifiers.KeywordReductions`), asking when both energy and power could go (deviation 5).
  - `CardView` gains a last parameter `int EmpowerCount = 0`; `BoardCard` gains a last parameter `int EmpowerCount = 0`. The board's chip reads "Empowered" for 1 and "Empowered x2" or "x3" above.

- [ ] **Step 1: Write the card files**

`data/effects/kayle-justified.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "kayle-justified",
  "status": "Full",
  "keywords": [ { "keyword": "Empower", "value": 3, "cost": { "energy": 3 } } ],
  "abilities": [
    { "kind": "Passive", "line": 3,
      "modifiers": [ { "type": "ModifyMight", "amount": { "mul": [2, { "prop": "EmpowerCount", "of": { "ref": "Self" } }] }, "appliesTo": { "ref": "Self" } } ] },
    { "kind": "Passive", "line": 4,
      "while": { "compare": [ { "prop": "EmpowerCount", "of": { "ref": "Self" } }, "gte", 3 ] },
      "modifiers": [
        { "type": "GrantKeyword", "keyword": { "keyword": "Deflect", "value": 3 }, "appliesTo": { "ref": "Self" } },
        { "type": "GrantKeyword", "keyword": { "keyword": "Ganking" }, "appliesTo": { "ref": "Self" } }
      ] }
  ]
}
```

`data/effects/risen-altar.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "risen-altar",
  "status": "Full",
  "abilities": [
    { "kind": "Passive", "line": 1,
      "modifiers": [
        { "type": "KeywordCostReduction", "keyword": "Empower", "energy": 1, "orPower": ["Any"],
          "appliesTo": { "select": "Unit", "all": true, "filter": { "relation": "Friendly", "location": { "ref": "Here" } } } }
      ] }
  ]
}
```

- [ ] **Step 2: Write the failing engine tests**

Create `tests/CromoBound.Engine.Tests/EmpowerTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class EmpowerTests
{
    private static void PassBoth(Rules.Game engine)
    {
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
    }

    /// <summary>Kayle's abilities: the two passives (0, 1), then the Empower keyword's activation (2).</summary>
    private const int KayleEmpower = 2;

    [Fact]
    public void Kayle_empowers_three_times_and_no_more()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("kayle-justified"));
        var kayle = game.Put("kayle-justified", Place.Base(P1));
        game.Runes(P1, "fury-rune", 9);
        var engine = game.Start();

        for (var i = 1; i <= 3; i++)
        {
            engine.Accept(P1, new ActivateAbility(kayle, KayleEmpower));
            engine.PayWithSuggestion(P1);
            PassBoth(engine);
            Assert.Equal(i, game.State[kayle].EmpowerCount);
            Assert.Equal(3 + 2 * i, engine.MightOf(kayle));
        }

        Assert.True(engine.Has(game.State[kayle], DisplayKeyword.Ganking));
        Assert.Equal(3, Effects.Modifiers.KeywordValue(engine, game.State[kayle], MechanicalKeyword.Deflect));
        Assert.DoesNotContain(engine.Decision<PriorityDecision>().Activations, a => a.Source == kayle);
    }

    [Fact]
    public void A_new_object_starts_unempowered()
    {
        var game = new TestGame();
        var unit = game.Put("unit-2", Place.Base(P1));
        game.State[unit].EmpowerCount = 2;
        var engine = game.Start();

        var moved = game.State.Move(unit, Place.Trash(P1), DeckPosition.Top)!.Value;

        Assert.Equal(0, game.State[moved].EmpowerCount);
        Assert.False(game.State[moved].Empowered);
    }

    [Fact]
    public void Risen_altar_lowers_kayles_empower_by_one_energy()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("kayle-justified", "risen-altar"), firstBattlefield: "risen-altar");
        game.State.Battlefields[0].Controller = P1;
        var kayle = game.Put("kayle-justified", Place.Battlefield(0));
        game.Runes(P1, "fury-rune", 3);
        var engine = game.Start();

        engine.Accept(P1, new ActivateAbility(kayle, KayleEmpower));

        var cost = engine.Decision<PayCostDecision>().Cost;
        Assert.Equal(2, cost.Energy);
        Assert.Empty(cost.Power);
    }

    [Fact]
    public void Risen_altar_asks_which_part_to_lower_when_the_cost_has_both()
    {
        const string altar = """
            { "cardId": "bf-a", "status": "Full", "abilities": [ { "kind": "Passive", "modifiers": [
              { "type": "KeywordCostReduction", "keyword": "Empower", "energy": 1, "orPower": ["Any"],
                "appliesTo": { "select": "Unit", "all": true, "filter": { "relation": "Friendly", "location": { "ref": "Here" } } } } ] } ] }
            """;
        const string unit = """
            { "cardId": "unit-3", "status": "Full", "keywords": [ { "keyword": "Empower", "cost": { "energy": 2, "power": ["Any"] } } ] }
            """;
        var game = new TestGame(db: EngineTestDb.Create(("bf-a", altar), ("unit-3", unit)));
        game.State.Battlefields[0].Controller = P1;
        var here = game.Put("unit-3", Place.Battlefield(0));
        var engine = game.Start();

        engine.Accept(P1, new ActivateAbility(here, 0));
        engine.Decision<OptionalDecision>();
        engine.Accept(P1, new ChooseOptional(false));

        var cost = engine.Decision<PayCostDecision>().Cost;
        Assert.Equal(2, cost.Energy);
        Assert.Empty(cost.Power);
    }

    [Fact]
    public void Away_from_risen_altar_the_cost_is_unchanged()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("kayle-justified", "risen-altar"), firstBattlefield: "risen-altar");
        game.State.Battlefields[0].Controller = P1;
        var kayle = game.Put("kayle-justified", Place.Base(P1));
        game.Runes(P1, "fury-rune", 3);
        var engine = game.Start();

        engine.Accept(P1, new ActivateAbility(kayle, KayleEmpower));

        Assert.Equal(3, engine.Decision<PayCostDecision>().Cost.Energy);
    }
}
```

`GameState.Move(id, place, position)` is the state-level move `MoveCard` calls; if its name differs, use the existing one.

- [ ] **Step 3: Write the failing client test**

In `tests/CromoBound.Client.Tests/BoardViewTests.cs`, add:

```csharp
    [Fact]
    public async Task An_empowered_card_shows_how_many_times()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        board.MyBase.Add(board.Card("unit-a", TestBoard.Me) with { Empowered = true, EmpowerCount = 2 });
        board.MyBase.Add(board.Card("unit-b", TestBoard.Me) with { Empowered = true, EmpowerCount = 1 });

        var cut = Render(ui, board.Model(), []);

        Assert.Equal(new[] { "Empowered x2", "Empowered" }, cut.FindAll(".cb-basezone .chip").Select(c => c.TextContent));
    }
```

- [ ] **Step 4: Run them to see them fail**

Run: `dotnet test tests/CromoBound.Engine.Tests --filter "FullyQualifiedName~EmpowerTests"` and `dotnet test tests/CromoBound.Client.Tests --filter "FullyQualifiedName~An_empowered_card"`
Expected: build errors (`EmpowerCount` doesn't exist).

- [ ] **Step 5: Count Empowers**

In `CardInstance.cs`, replace `public bool Empowered { get; set; }` with:

```csharp
    /// <summary>How many times it was Empowered (Kayle can be up to three times); 0 when it isn't.</summary>
    public int EmpowerCount { get; set; }

    public bool Empowered
    {
        get => EmpowerCount > 0;
        set => EmpowerCount = value ? Math.Max(EmpowerCount, 1) : 0;
    }
```

In `Game.Mutations.cs`, add:

```csharp
    /// <summary>The first Empower is a status change (BecameEmpowered triggers); each later one raises the count.</summary>
    internal void Empower(ObjectId id)
    {
        if (!State[id].Empowered)
        {
            SetStatus(id, StatusKind.Empowered, true);
            return;
        }
        State[id].EmpowerCount++;
        MarkDirty();
    }
```

In `PermanentStepHandlers.cs`, replace `EmpowerHandler` with:

```csharp
/// <summary>Empowers the board objects below their limit (Empower, spec §8.1; Kayle's three, docs/effects-fiora.md §3.5).</summary>
internal sealed class EmpowerHandler : StepHandler<EmpowerStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, EmpowerStep step)
    {
        List<ObjectId> targets =
        [
            .. ObjectResolver.Resolve(game, task.Context, step.Target)
                .Where(id => game.State[id].Place.IsLocation && game.State[id].EmpowerCount < Modifiers.EmpowerLimit(game, game.State[id])),
        ];
        if (targets.Count == 0) return StepOutcome.DidNothing;
        foreach (var target in targets) game.Empower(target);
        task.Result = new EffectVar(targets, [], null, true);
        return StepOutcome.Done;
    }
}
```

In `Modifiers.cs`, add:

```csharp
    /// <summary>How many times the object can be Empowered: its Empower keyword's value, 1 without one.</summary>
    public static int EmpowerLimit(Game game, CardInstance instance) =>
        game.Effects.For(instance.CardId).KeywordEntries.Where(k => k.Keyword == MechanicalKeyword.Empower).Select(k => k.Value ?? 1).DefaultIfEmpty(1).Max();
```

In `ValueResolver.Property`, change the `EmpowerCount` line to `ValueProperty.EmpowerCount => instance.EmpowerCount,`.

In `CardEffects.KeywordAbilities`, change the Empower ability's `UseOnlyIf` to:

```csharp
                        UseOnlyIf = new Condition
                        {
                            Compare = new Comparison(new Value { Prop = ValueProperty.EmpowerCount, Of = ObjectRef.Self }, CompareOp.Lt, entry.Value ?? 1),
                        },
```

In `EffectsSupport`, add `MechanicalKeyword.Empower` to `WithValue`.

- [ ] **Step 6: Lower Empower costs**

In `Modifiers.cs`, add:

```csharp
    /// <summary>The KeywordCostReduction modifiers for the keyword that apply to the holder now: from any card on the board or any
    /// battlefield, whose condition holds and whose appliesTo includes the holder. A battlefield's belong to its controller
    /// (CR 190.6). In id order.</summary>
    public static List<(KeywordCostReductionModifier Modifier, EffectContext Context)> KeywordReductions(Game game, CardInstance holder,
        MechanicalKeyword keyword)
    {
        List<(KeywordCostReductionModifier, EffectContext)> found = [];
        foreach (var source in game.State.Objects.Where(o => o.Place.IsLocation || o.Place.Kind == PlaceKind.BattlefieldCard))
        {
            var context = new EffectContext { Controller = game.HandledBy(source), Source = source.Id, SourceCardId = source.CardId };
            foreach (var passive in game.Effects.For(source.CardId).Abilities.OfType<PassiveAbility>())
            {
                if (passive.Condition is { } condition && !ConditionResolver.Holds(game, context, condition)) continue;
                if (passive.While is { } holds && !ConditionResolver.Holds(game, context, holds)) continue;
                foreach (var reduction in passive.Modifiers.OfType<KeywordCostReductionModifier>().Where(m => m.Keyword == keyword))
                    if (reduction.AppliesTo is { } applies && ObjectResolver.Resolve(game, context, applies).Contains(holder.Id))
                        found.Add((reduction, context));
            }
        }
        return found;
    }
```

In `Game.Activation.cs`:
- change the enum to `internal enum ActivationStage { Targets, Choose, Reduce, Pay, Finalize, Cancelled }`.
- add to `ActivationTask`: `public int Reductions { get; set; }` (how many reductions were applied).
- in `AskCostCards`, change the two `task.Stage = ActivationStage.Pay;` lines to `task.Stage = ActivationStage.Reduce;`.
- in `RunActivation`, add before the Pay case:

```csharp
                case ActivationStage.Reduce:
                    task.Cost ??= ability.Cost is { } cost ? new TotalCost(cost.Energy ?? 0, cost.Power) : new TotalCost(0, []);
                    if (AskEmpowerReduction(task, source, ability)) return false;
                    task.Stage = ActivationStage.Pay;
                    break;
```

  and in the Pay case delete the `task.Cost ??= ...` line (Reduce set it).
- add:

```csharp
    /// <summary>Risen Altar (docs/effects-fiora.md §4.3): each Empower reduction lowers an Empower activation's cost by its energy, or
    /// by one power symbol when it names orPower; the player picks when both are possible, otherwise the possible one applies.
    /// Returns true when it asked.</summary>
    private bool AskEmpowerReduction(ActivationTask task, CardInstance source, ActivatedAbility ability)
    {
        if (ability.Steps is not [EmpowerStep]) return false;
        var reductions = Modifiers.KeywordReductions(this, source, MechanicalKeyword.Empower);
        while (task.Reductions < reductions.Count)
        {
            var (reduction, context) = reductions[task.Reductions];
            var cost = task.Cost!;
            var energy = reduction.Energy is { } amount ? ValueResolver.Resolve(this, context, amount) : 0;
            var byEnergy = energy > 0 && cost.Energy > 0;
            var byPower = reduction.OrPower.Count > 0 && cost.Power.Count > 0;
            if (byEnergy && byPower)
            {
                Ask(new OptionalDecision(task.Player, source.CardId, "Lower the Empower cost by 1 energy? No lowers one power instead."), (_, action) =>
                {
                    if (action is not ChooseOptional choice) return Reject(RejectionCode.UnexpectedAction, "Answer yes or no.");
                    task.Cost = choice.Yes ? Lower(cost, energy) : WithoutOnePower(cost);
                    task.Reductions++;
                    return null;
                });
                return true;
            }
            if (byEnergy) task.Cost = Lower(cost, energy);
            else if (byPower) task.Cost = WithoutOnePower(cost);
            task.Reductions++;
        }
        return false;
    }

    private static TotalCost Lower(TotalCost cost, int energy) => cost with { Energy = Math.Max(0, cost.Energy - energy) };

    private static TotalCost WithoutOnePower(TotalCost cost) => cost with { Power = [.. cost.Power.Skip(1)] };
```

In `EffectsSupport.IsSupportedModifier`, add the case:

```csharp
        KeywordCostReductionModifier keyword => keyword.Keyword == MechanicalKeyword.Empower
            && (keyword.Energy is null || keyword.Energy.Literal is not null)
            && (modifier.AppliesTo?.Ref == RefKind.Self
                || modifier.AppliesTo is { Select: SelectKind.Unit, All: true } applies && IsSupportedFilter(applies.Filter)),
```

- [ ] **Step 7: Show the count**

In `src/CromoBound.Engine/Views/PlayerView.cs`, add `, int EmpowerCount = 0` as the last parameter of `CardView`. In `ViewBuilder.cs`, pass `instance.EmpowerCount` (named, `EmpowerCount: ...`) wherever a `CardView` is built from a `CardInstance`.

In `src/CromoBound.Client/Board/BoardParts.cs`, add `, int EmpowerCount = 0` as the last parameter of `BoardCard`. In `BoardModel.cs`'s `Build.Card`, add `EmpowerCount: card.EmpowerCount` as the last argument.

In `CardFace.razor`, change the Empowered chip to

```razor
                @if (Card.Empowered) { <span class="chip">@EmpowerText</span> }
```

and its label line to `if (Card.Empowered) parts.Add(EmpowerText.ToLowerInvariant());`. Add to its `@code`:

```csharp
    private string EmpowerText => Card.EmpowerCount > 1 ? $"Empowered x{Card.EmpowerCount}" : "Empowered";
```

In `CardZoom.razor`, use the same words for its Empowered line.

- [ ] **Step 8: Run all tests**

Run: `dotnet test CromoBound.slnx`
Expected: all pass. Existing Empower tests keep passing: the default limit is 1.

- [ ] **Step 9: Build with 0 warnings, then commit**

```bash
git add src data/effects/kayle-justified.json data/effects/risen-altar.json tests
git commit -m "feat(engine): empower up to a limit and lower empower costs at risen altar"
```

---
### Task 7: Trigger conditions, inline optional blocks, Move; Sunken Temple and Amateur Recital

**Files:**
- Modify: `src/CromoBound.Engine/Effects/TriggerWatcher.cs`
- Modify: `src/CromoBound.Engine/Effects/Steps/ChoiceStepHandlers.cs` (`OptionalHandler`), `Steps/MightStepHandlers.cs` (`MoveHandler`), `Steps/StepRegistry.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Create: `data/effects/sunken-temple.json`, `data/effects/amateur-recital.json`
- Test: `tests/CromoBound.Engine.Tests/FioraDeckTests.cs`

**Interfaces:**
- Consumes: Task 1's `exists`, `mighty` and `location`; Task 2's `ResolutionChoice`; Plan E's `Optional` (reflexive) and `Game.AskPay`.
- Produces:
  - A triggered ability's `if` is checked when the event happens, in its source's context (deviation 3).
  - `Optional` without `reflexive`: on yes it pays its optional `cost` (energy and power, through `AskPay`; cancelling the payment means no) and then runs its steps inline, as a child `ResolveEffectTask` with the same context.
  - `MoveHandler`: moves each aimed unit at a battlefield to its controller's base with `MoveCard`. No exhaust, no Contested (deviation 2). Only `"to": { "ref": "Controller" }` is supported.

- [ ] **Step 1: Write the card files**

`data/effects/sunken-temple.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "sunken-temple",
  "status": "Full",
  "abilities": [
    { "kind": "Triggered", "line": 1,
      "trigger": { "event": "Conquer", "by": "You", "where": { "ref": "Here" } },
      "if": { "exists": { "select": "Unit", "filter": { "relation": "Friendly", "mighty": true, "location": { "ref": "Here" } } } },
      "steps": [ { "action": "Optional", "cost": { "energy": 1 }, "steps": [ { "action": "Draw", "amount": 1 } ] } ] }
  ]
}
```

`data/effects/amateur-recital.json`:

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "amateur-recital",
  "status": "Full",
  "abilities": [
    { "kind": "Triggered", "line": 1,
      "trigger": { "event": "Hold", "by": "You", "where": { "ref": "Here" } },
      "steps": [
        { "action": "Optional",
          "steps": [ { "action": "Move", "target": { "select": "Unit", "count": 1, "filter": { "location": { "select": "Battlefield" } } },
                       "to": { "ref": "Controller" } } ] }
      ] }
  ]
}
```

- [ ] **Step 2: Write the failing tests**

Append to `FioraDeckTests`:

```csharp
    [Fact]
    public void Sunken_temple_lets_a_mighty_conqueror_pay_one_to_draw()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("sunken-temple"), firstBattlefield: "sunken-temple");
        game.State.Battlefields[0].Controller = P1;
        var big = game.Put("unit-3", Place.Battlefield(0));
        game.State[big].Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start(first: P2);
        var hand = game.State.At(Place.Hand(P1)).Count;

        engine.RunNow(new StepTask(g => g.Score(P1, 0, ScoreKind.Conquer)));
        Assert.Equal("sunken-temple", Assert.Single(game.State.Chain).SourceCardId);
        PassBoth(engine);
        engine.Accept(P1, new ChooseOptional(true));
        Assert.Equal(1, engine.Decision<PayCostDecision>().Cost.Energy);
        engine.PayWithSuggestion(P1);

        Assert.Equal(hand + 1, game.State.At(Place.Hand(P1)).Count);
    }

    [Fact]
    public void Sunken_temple_does_nothing_without_a_mighty_unit_there()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("sunken-temple"), firstBattlefield: "sunken-temple");
        game.State.Battlefields[0].Controller = P1;
        game.Put("unit-3", Place.Battlefield(0));
        var engine = game.Start(first: P2);

        engine.RunNow(new StepTask(g => g.Score(P1, 0, ScoreKind.Conquer)));

        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void Declining_to_pay_at_sunken_temple_draws_nothing()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("sunken-temple"), firstBattlefield: "sunken-temple");
        game.State.Battlefields[0].Controller = P1;
        var big = game.Put("unit-3", Place.Battlefield(0));
        game.State[big].Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        game.Runes(P1, "fury-rune", 1);
        var engine = game.Start(first: P2);
        var hand = game.State.At(Place.Hand(P1)).Count;

        engine.RunNow(new StepTask(g => g.Score(P1, 0, ScoreKind.Conquer)));
        PassBoth(engine);
        engine.Accept(P1, new ChooseOptional(true));
        engine.Accept(P1, new CancelPlay());

        Assert.Equal(hand, game.State.At(Place.Hand(P1)).Count);
        Assert.Empty(game.State.Chain);
    }

    [Fact]
    public void Amateur_recital_moves_a_unit_at_a_battlefield_to_its_base_when_you_hold_it()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("amateur-recital"), firstBattlefield: "amateur-recital");
        game.State.Battlefields[0].Controller = P1;
        game.State.Battlefields[1].Controller = P2;
        var mine = game.Put("unit-2", Place.Battlefield(0));
        var theirs = game.Put("unit-3", Place.Battlefield(1), owner: P2);
        var engine = game.Start();

        Assert.Equal("amateur-recital", Assert.Single(game.State.Chain).SourceCardId);
        PassBoth(engine);
        engine.Accept(P1, new ChooseOptional(true));
        Assert.Equal(new[] { mine, theirs }, engine.Decision<ChooseCardsDecision>().Options);
        engine.Accept(P1, new ChooseCards { Cards = [theirs] });

        Assert.Equal(Place.Base(P2), game.State[theirs].Place);
        Assert.False(game.State[theirs].Exhausted);
        Assert.Equal(Place.Battlefield(0), game.State[mine].Place);
    }
```

Add `using CromoBound.Engine.Events;` and `using CromoBound.Engine.Rules;` for `ScoreKind` and `StepTask` (use the namespaces the existing `TriggerTests` import for them).

`game.Put(..., Place.Battlefield(1), owner: P2)` gives the unit to P2 (a new card's controller is its owner).

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test tests/CromoBound.Engine.Tests --filter "FullyQualifiedName~FioraDeckTests"`
Expected: the four new tests FAIL. The files are Unmapped: a trigger `if`, an `Optional` without `reflexive` and the `Move` step aren't supported.

- [ ] **Step 4: Check trigger conditions**

In `TriggerWatcher.cs`:
- change `Own` to filter on the condition:

```csharp
    private static List<PendingTrigger> Own(Game game, ObjectId id, PlayerId controller, TriggerEvent kind) =>
        !game.State.Exists(id) ? [] :
        [
            .. Triggered(game, id, kind)
                .Where(a => a.Trigger.Subject?.Ref == RefKind.Self && Holds(game, a, controller, id))
                .Select(a => new PendingTrigger(controller, id, game.State[id].CardId, a)),
        ];
```

- in `Scored`, change the battlefield's `.Where(a => a.Trigger is { By.Kind: PlayerKind.You, Where.Ref: RefKind.Here })` to `.Where(a => a.Trigger is { By.Kind: PlayerKind.You, Where.Ref: RefKind.Here } && Holds(game, a, scored.Player, card))`.
- add:

```csharp
    /// <summary>A trigger's "if" (docs/effects-fiora.md, Plan O deviation 3), read when the event happens, in the source's context.</summary>
    private static bool Holds(Game game, TriggeredAbility ability, PlayerId controller, ObjectId source) =>
        ability.If is not { } condition
        || ConditionResolver.Holds(game, new EffectContext { Controller = controller, Source = source, SourceCardId = game.State[source].CardId }, condition);
```

Add `using CromoBound.Engine.Effects.Resolvers;` if missing.

- [ ] **Step 5: Run optional blocks inline, with a cost**

In `ChoiceStepHandlers.cs`, replace `OptionalHandler` with:

```csharp
/// <summary>"You may [pay a cost to] do X" (spec §5.4). The controller answers yes or no. A reflexive block ("do this:") becomes
/// a new chain item with the same context, so players get priority on it. Otherwise, on yes, its cost is paid (cancelling the
/// payment means no) and its steps run right away, as a child resolution with the same context.</summary>
internal sealed class OptionalHandler : StepHandler<OptionalStep>
{
    public const string Question = "Do the rest of the ability?";

    private enum Stage { Paying, Running, Waiting, Done, Cancelled }

    private sealed class Inline(TotalCost cost)
    {
        public Stage Stage { get; set; } = cost.Energy == 0 && cost.Power.Count == 0 ? Stage.Running : Stage.Paying;
        public TotalCost Cost { get; set; } = cost;
    }

    protected override StepOutcome Run(Game game, ResolveEffectTask task, OptionalStep step)
    {
        var context = task.Context;
        if (task.Progress is Inline inline) return Continue(game, task, step, inline);
        if (task.Answer is bool yes)
        {
            if (!yes) return StepOutcome.DidNothing;
            if (step.Reflexive == true)
            {
                var parent = task.Item;
                game.AddAbilityItem(context.Controller, context.Source, context.SourceCardId, parent?.AbilityKind ?? AbilityKind.Triggered,
                    parent?.Text, step.Steps, context);
                return StepOutcome.Done;
            }
            var started = new Inline(new TotalCost(step.Cost?.Energy ?? 0, step.Cost?.Power ?? []));
            task.Progress = started;
            return Continue(game, task, step, started);
        }
        game.Ask(new OptionalDecision(context.Controller, context.SourceCardId, Question), (_, action) =>
        {
            if (action is not ChooseOptional choice) return Game.Reject(RejectionCode.UnexpectedAction, "Answer yes or no.");
            task.Answer = choice.Yes;
            return null;
        });
        return StepOutcome.Asked;
    }

    private static StepOutcome Continue(Game game, ResolveEffectTask task, OptionalStep step, Inline inline)
    {
        switch (inline.Stage)
        {
            case Stage.Paying:
                game.AskPay(task.Context.Controller, inline.Cost, game.Db.Cards[task.Context.SourceCardId].Domains,
                    onPaid: () => inline.Stage = Stage.Running,
                    onCancel: () => inline.Stage = Stage.Cancelled,
                    onAdjust: adjusted => inline.Cost = adjusted);
                return StepOutcome.Asked;
            case Stage.Running:
                inline.Stage = Stage.Waiting;
                game.Push(new ResolveEffectTask(task.Context, step.Steps, _ => inline.Stage = Stage.Done));
                return StepOutcome.Asked;
            case Stage.Done:
                return StepOutcome.Done;
            default:
                return StepOutcome.DidNothing;
        }
    }
}
```

A `Waiting` rerun can't happen: the child sits in front of its parent until it is done. After an `AdjustCost`, the rerun finds `Paying` again and asks with the adjusted cost.

- [ ] **Step 6: Add the Move step**

In `MightStepHandlers.cs`, add:

```csharp
/// <summary>"Move a unit at a battlefield to its base" (Amateur Recital): each aimed unit at a battlefield goes to its controller's
/// base. Not a standard move: nothing is exhausted and no Contested applies (Plan O deviation 2).</summary>
internal sealed class MoveHandler : StepHandler<MoveStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, MoveStep step)
    {
        if (ResolutionChoice.Targets(game, task, step.Target) is not { } aimed) return StepOutcome.Asked;
        List<ObjectId> units = [.. aimed.Where(id => game.State[id].Place.Kind == PlaceKind.Battlefield && game.IsUnit(game.State[id]))];
        task.Result = new EffectVar(units, [], null, false);
        if (units.Count == 0) return StepOutcome.DidNothing;
        foreach (var unit in units) game.MoveCard(unit, Place.Base(game.State[unit].Controller));
        return StepOutcome.Done;
    }
}
```

In `StepRegistry.cs`, add `[typeof(MoveStep)] = new MoveHandler(),`.

- [ ] **Step 7: Support the new forms**

In `EffectsSupport`:
- in the triggered case, replace the `if, optional, cost or limit` check with:

```csharp
                if (triggered.If is { } condition && !IsSupportedCondition(condition)) problems.Add($"{at}: if");
                if (triggered.Optional is not null || triggered.Cost is not null || triggered.Limit is not null)
                    problems.Add($"{at}: optional, cost or limit");
```

- in `CheckStep`, replace the `OptionalStep` case with:

```csharp
            case OptionalStep optional:
                if (optional.Reflexive == true ? optional.Cost is not null
                    : optional.Cost is { } cost && (cost.ExhaustSelf is not null || cost.Actions.Count > 0))
                    problems.Add($"{at}: optional");
                CheckSteps(optional.Steps, at, problems, targets: false);
                break;
            case MoveStep move:
                if (move.To.Ref != RefKind.Controller) problems.Add($"{at}: to");
                break;
```

- [ ] **Step 8: Run all tests**

Run: `dotnet test tests/CromoBound.Engine.Tests`
Expected: all pass. Existing reflexive-optional tests keep passing, since `reflexive: true` keeps its old path.

- [ ] **Step 9: Build with 0 warnings, then commit**

```bash
git add src/CromoBound.Engine data/effects/sunken-temple.json data/effects/amateur-recital.json tests/CromoBound.Engine.Tests/FioraDeckTests.cs
git commit -m "feat(engine): check trigger conditions, run optional blocks inline and move units to base"
```

---

### Task 8: Count the deck's progress

**Files:**
- Regenerate: `data/import-report.md`
- Modify: `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`
- Modify: `docs/effects-fiora.md` (status line), `docs/client-manual-checks.md`

**Interfaces:**
- Consumes: every earlier task.
- Produces: a data test that pins the twelve Plan O cards (and Rengar) as running Full, and the regenerated mapping count.

- [ ] **Step 1: Write the test**

Append to `EffectsDataTests`:

```csharp
    [Theory]
    [InlineData("harnessed-dragon")]
    [InlineData("punch-first")]
    [InlineData("divining-shells")]
    [InlineData("dorans-blade")]
    [InlineData("shepherds-heirloom")]
    [InlineData("rampage")]
    [InlineData("sacrifice")]
    [InlineData("kayle-justified")]
    [InlineData("fiora-victorious")]
    [InlineData("risen-altar")]
    [InlineData("sunken-temple")]
    [InlineData("amateur-recital")]
    [InlineData("rengar-trophy-hunter")]
    public void The_fiora_decks_plan_o_cards_run_in_full(string cardId)
    {
        var info = Real.For(cardId);

        Assert.True(info.Unsupported.Count == 0, string.Join(", ", info.Unsupported));
        Assert.Equal(Models.Effects.MappingStatus.Full, info.Status);
    }
```

- [ ] **Step 2: Run it**

Run: `dotnet test tests/CromoBound.Engine.Tests --filter "FullyQualifiedName~EffectsDataTests"`
Expected: PASS (Tasks 3 to 7 made every one of them Full).

- [ ] **Step 3: Regenerate the report and update the docs**

Run: `dotnet run --project tools/CromoBound.Importer -- normalize`
Expected: "Validation passed."; `data/import-report.md` shows 66 Full.

In `docs/effects-fiora.md`, change the status line to: `- **Status:** Approved (2026-10-10), with the owner's rules readings in §4. Plan O done: 12 of the 19 cards run automatically.`

In `docs/client-manual-checks.md`, add to the board section:

```markdown
- Fiora deck (Plan O cards): Punch First on a unit shows +5 on the board this turn and is gone next turn; Doran's Blade
  equipped shows +2 on its unit; Kayle Empowered twice shows "Empowered x2" and 7 might. Cards that ask a target, a card or
  a yes/no show the "comes in the next update" panel until plan M.
```

- [ ] **Step 4: Run everything, then commit**

Run: `dotnet build CromoBound.slnx --no-incremental` (0 Avvisi, 0 Errori) and `dotnet test CromoBound.slnx` (all pass).

```bash
git add data/import-report.md tests/CromoBound.Engine.Tests/EffectsDataTests.cs docs/effects-fiora.md docs/client-manual-checks.md
git commit -m "test(data): pin the fiora deck's plan o cards as mapped"
```
