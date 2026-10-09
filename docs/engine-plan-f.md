# Engine Plan F: Modifiers, Keywords and Integration

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Last of three plans for Phase 2b (D: foundations and spells; E: triggers and activations; F: modifiers, keywords and integration).

**Goal:** Every mapped card in `data/effects` runs automatically. That means:
- Assault and Shield change Might in combat.
- Legion cost reductions and Deflect taxes change costs.
- Ambush and "play me to a battlefield with enemy units" widen where units enter.
- Equip, Weaponmaster and manual attach/detach handle gear, and gear follows its unit.
- Full cards no longer cause text-matched turn-point pauses.
- A scripted Bo3 with mapped cards in both decks finishes and replays identically.

**Architecture:**
- A new `Effects/Modifiers` class evaluates live values with nothing stored:
  - `MightOf`: printed Might, buff and manual modifiers, plus Assault while attacking and Shield while defending.
  - `CostOf`: Payment's base cost, plus Deflect's [A] per opposing target, minus the card's own cost reductions.
  - `Permits`: whether a passive grants a permission.
- `Game.MightOf` and the play's Cost step delegate to `Modifiers`. `PlayLocations` and `PlayableCards` learn Ambush and the battlefield permission.
- `CardEffectInfo` gains the file's `KeywordEntries`, so numbered keywords keep their values.
- Attachment lives in `Game.Attachment.cs`. `MoveCard` keeps attached gear with its unit, and detaches and recalls it when the unit leaves the board.
- The Equip keyword stands for an activated ability (attach to a unit you control). Weaponmaster stands for a trigger on being played, with an internal `WeaponmasterStep`.
- `EffectsSupport.Problems` takes the card type and closes the gaps Plan E left: trigger forms by card type, `player` on steps that ignore it, and parameters on keywords that take none.

**Tech Stack:** .NET 10, xUnit 2.9.3. No new dependencies.

**Spec:** `docs/effects-engine.md` (sections 4.1, 4.7, 7, 8.1 for Assault, Shield, Deflect, Ambush, Legion, Equip and Weaponmaster, 8.2, 9, 10, 11 row F).

## Global Constraints

- Plans A to E's Global Constraints still apply:
  - `net10.0`; no NuGet packages in runtime projects.
  - No `DateTime`, `Guid`, `System.Random` or hash-order-dependent iteration in engine code.
  - `CromoJson` for everything saved; every action and decision type round-trips through it.
- Public API stays limited to `Match`, `Game`, `PlayerView` and the action, decision, event and result records. Everything in `CromoBound.Engine.Effects` is `internal`; the test project sees it through `InternalsVisibleTo`.
- Unmapped cards play exactly as in 2a. Every existing test keeps passing; the only existing tests that change are the ones this plan names.
- Modifiers are evaluated live (spec §13 "Modifiers: Evaluated live"): no derived value is stored in `GameState`.
- Handlers validate an answer before they change anything, and a rejected answer leaves the decision pending.
- `Models.Effects.PlayStep` (the step) and `Rules.PlayStep` (the play-progress enum) share a name. A file that imports both namespaces and needs the step uses `using EffectPlayStep = CromoBound.Models.Effects.PlayStep;`.
- Owner rules:
  - 0 build warnings and 0 errors at all times.
  - Conventional, title-only commit messages: no body, no co-author trailer, no mention of Claude/AI.
  - No em dashes or en dashes in code, comments or strings. Test strings that copy real card text may keep them.
  - LF line endings; UTF-8 without BOM.
- Run dotnet with `export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && ` in Git Bash (the default dotnet on PATH is SDK 9).

## Deliberate deviations from the spec (reviewers: these are intended)

1. **Only the modifiers mapped cards use.**
   - `CostReduction`: a literal energy amount on the card itself, optionally under `{ "legion": true }` (Noxus Hopeful).
   - `Permission: PlayToBattlefieldWithEnemyUnits` on the card itself (Rengar, Trophy Hunter).
   - No mapped card uses `ModifyMight`, `GrantKeyword`, `CostIncrease`, `KeywordCostReduction`, `EnterReady`, `Untargetable` or `IgnoreCost` passives, or `overrides.cost`, so these stay unsupported. A file that uses one still falls back to manual play and names the construct.
2. **Legion means "you played (finalized) at least one card this turn".** It is read only while the card itself is being played, before its own play finalizes, so "another card" needs no exclusion. A cancelled play doesn't count (`architecture.md`: Legion looks at finalization).
3. **Equip chooses the unit on resolution.** It is not a target: activated abilities have no target selectors yet (Plan E deviation 1).
   - Weaponmaster's "minus [A]" removes one power symbol from the Equip cost; energy is unchanged.
   - No mapped Equipment exists yet, so Weaponmaster cards ship working but offer nothing with the real data. The Equip and Weaponmaster tests use inline effects files.
4. **The load-time validator (spec §7) is a data test.** The runtime fallback from Plan D stays for future files. `EffectsDataTests` asserts that every file in `data/effects` runs with its declared status and no unsupported construct.
5. **Ambush (CR 822, spec §8.1):**
   - It adds the battlefields where the player has units to a unit's locations.
   - It lets the unit be played at any time the player has priority, if such a battlefield exists. A play that only Ambush's timing allows may enter only there.
   - Ambush and the enemy-units permission apply to plays from hand and the Champion Zone; a hidden play keeps its own battlefield.
6. **`CardView` gains `AttachedTo`.** The spec is silent; clients need it to draw attached gear.
7. **Manual moves and Deathknell are unchanged.** A `ManualMoveCard` of a unit to the trash still emits no `UnitDied`. This is an open owner question, not decided here.

## Review Focus

1. **Deflect on a unit the player controls, and Deflect chosen twice.** Targeting your own Deflect unit costs nothing extra; targeting an enemy one with two selectors (Falling Star) pays the tax twice. Pinned in Task 4 (`Deflect_doesnt_tax_targeting_your_own_unit`, `Deflect_taxes_each_choice_of_an_enemy_target`).
2. **A cancelled play and Legion.** A play cancelled at payment doesn't make Noxus Hopeful cheaper. Pinned in Task 4 (`A_cancelled_play_doesnt_count_for_legion`).
3. **Attached gear when its unit moves, dies or the gear is detached.** It follows the unit; it is detached and goes to its controller's Base when the unit leaves the board; detached gear at a battlefield is recalled by cleanup. Pinned in Task 6 (`Attached_gear_joins_its_unit_and_follows_it`, `Gear_is_detached_and_recalled_when_its_unit_leaves_the_board`, `Detached_gear_at_a_battlefield_goes_back_to_base`).
4. **Turn-point pauses for Partial cards.** A Partial card pauses only when one of its manual lines matches, and a Full card never does. Pinned in Task 8 (`A_full_card_never_pauses`, `A_partial_card_pauses_only_for_its_manual_lines`).
5. **Replay with live modifiers, triggers and mapped keywords over a whole match.** A scripted Bo3 with mapped cards ends and its saved record replays into the same views. Pinned in Task 9 (`Scripted_players_finish_a_bo3_with_mapped_cards_and_the_saved_match_replays_identically`).

---

## File Structure

```
src/CromoBound.Engine/
  Effects/EffectsSupport.cs               (modify) card-type-aware triggers, player on steps, keyword parameters, new keywords,
                                          passive abilities and modifiers, Attach
  Effects/CardEffects.cs                  (modify) KeywordEntries, keyword ability text, Equip and Weaponmaster abilities
  Effects/Modifiers.cs                    MightOf, CostOf, Permits, KeywordValue
  Effects/Resolvers/ConditionResolver.cs  (modify) legion
  Effects/Steps/PlayStepHandler.cs        (modify) plays only from piles
  Effects/Steps/PermanentStepHandlers.cs  (modify) Attach
  Effects/Steps/WeaponmasterStep.cs       WeaponmasterStep, WeaponmasterHandler
  Effects/Steps/StepRegistry.cs           (modify) Attach, Weaponmaster
  State/TurnState.cs                      (modify) cards played this turn
  Rules/Game.cs                           (modify) MightOf delegates
  Rules/Game.Play.cs                      (modify) CostOf, ByAmbush, locations, Legion count
  Rules/Game.Priority.cs                  (modify) play timing with Ambush
  Rules/Game.Turn.cs                      (modify) reset cards played
  Rules/Game.Attachment.cs                Attach, Detach, attachments follow their unit, manual attach/detach
  Rules/Game.Mutations.cs                 (modify) MoveCard moves attachments
  Rules/Game.Manual.cs                    (modify) ManualAttach, ManualDetach
  Rules/Game.TurnPoints.cs                (modify) only unmapped text and manual lines pause
  Actions/ManualActions.cs, Actions/PlayerAction.cs  (modify) ManualAttach, ManualDetach
  Events/GameEvents.cs                    (modify) Attached, Detached
  Views/PlayerView.cs, Views/ViewBuilder.cs          (modify) CardView.AttachedTo
tests/CromoBound.Engine.Tests/
  CardEffectsTests.cs, EffectPlayTests.cs, TriggerTests.cs, TurnPointTests.cs, EffectsDataTests.cs, MatchEffectsTests.cs  (modify)
  ModifierTests.cs, CostTests.cs, LocationTests.cs, AttachmentTests.cs, EquipTests.cs
```

---

### Task 1: Support checks that never stay silent

**Files:**
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Modify: `src/CromoBound.Engine/Effects/CardEffects.cs`
- Modify: `tests/CromoBound.Engine.Tests/CardEffectsTests.cs`

**Interfaces:**
- Consumes: Plan E's `EffectsSupport` and `CardEffects`.
- Produces:
  - `EffectsSupport.Problems(EffectsFile file, CardType type) : IReadOnlyList<string>`.
  - `CardEffectInfo(MappingStatus Status, IReadOnlyList<Ability> Abilities, IReadOnlySet<DisplayKeyword> Keywords, IReadOnlyList<int> ManualLines, IReadOnlyList<string> Unsupported, IReadOnlyList<KeywordEntry> KeywordEntries)`. `KeywordEntries` is the file's keyword list for Full and Partial cards and empty otherwise. Tasks 3, 4 and 7 read it.

- [ ] **Step 1: Write the failing tests**

In `tests/CromoBound.Engine.Tests/CardEffectsTests.cs`:
- In `Unsupported_steps_targets_values_and_players_are_named`, change the call to `EffectsSupport.Problems(file, CardType.Spell)` and add these rows:

```csharp
    [InlineData("""{ "action": "Kill", "player": "Opponent", "target": { "select": "Unit", "count": 1 } }""", "abilities[0].steps[0]: player")]
    [InlineData("""{ "action": "Deal", "amount": 1, "player": "You", "target": { "select": "Unit", "count": 1 } }""", "abilities[0].steps[0]: player")]
```

- In `Unsupported_triggers_are_named` and `A_triggered_ability_cant_have_target_selectors_yet`, change the call to `EffectsSupport.Problems(file, CardType.Unit)`.
- Add these tests:

```csharp
    [Theory]
    [InlineData("""{ "keyword": "Tank", "value": 2 }""", "keywords[0]: value")]
    [InlineData("""{ "keyword": "Accelerate", "cost": { "energy": 1 } }""", "keywords[0]: cost")]
    [InlineData("""{ "keyword": "Vision", "steps": [ { "action": "Draw" } ] }""", "keywords[0]: steps")]
    [InlineData("""{ "keyword": "Hunt", "value": 1, "cost": { "energy": 1 } }""", "keywords[0]: cost")]
    [InlineData("""{ "keyword": "Empower", "cost": { "energy": 1, "exhaustSelf": true } }""", "keywords[0]: cost")]
    public void Keyword_parameters_the_engine_would_ignore_are_named(string keyword, string problem)
    {
        var file = CromoJson.Deserialize<EffectsFile>($$"""{ "cardId": "unit-3", "status": "Full", "keywords": [ {{keyword}} ] }""");

        Assert.Equal(new[] { problem }, EffectsSupport.Problems(file, CardType.Unit));
    }

    [Theory]
    [InlineData("""{ "event": "Hold", "subject": { "ref": "Self" } }""", CardType.Battlefield)]
    [InlineData("""{ "event": "Played", "subject": { "ref": "Self" } }""", CardType.Battlefield)]
    [InlineData("""{ "event": "Hold", "by": "You", "where": { "ref": "Here" } }""", CardType.Unit)]
    public void Trigger_forms_must_fit_the_card_type(string trigger, CardType type)
    {
        var file = CromoJson.Deserialize<EffectsFile>(
            $$"""{ "cardId": "unit-3", "status": "Full", "abilities": [ { "kind": "Triggered", "trigger": {{trigger}}, "steps": [] } ] }""");

        Assert.Equal(new[] { "abilities[0]: trigger" }, EffectsSupport.Problems(file, type));
    }

    [Fact]
    public void Every_mechanical_keyword_has_a_display_keyword()
    {
        Assert.All(Enum.GetValues<MechanicalKeyword>(),
            keyword => Assert.True(Enum.TryParse<DisplayKeyword>(keyword.ToString(), out _), $"{keyword} has no DisplayKeyword"));
    }

    [Fact]
    public void Mapped_cards_keep_their_keyword_entries_and_unmapped_ones_have_none()
    {
        var db = EngineTestDb.Create(("unit-2", FullTank));
        var effects = new CardEffects(db);

        Assert.Equal(MechanicalKeyword.Tank, Assert.Single(effects.For("unit-2").KeywordEntries).Keyword);
        Assert.Empty(effects.For("tank-2").KeywordEntries);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~CardEffectsTests"`
Expected: FAIL (build errors: `Problems` takes one argument; `KeywordEntries` doesn't exist).

- [ ] **Step 3: Write the implementation**

In `src/CromoBound.Engine/Effects/CardEffects.cs`:
- Replace the `CardEffectInfo` record with:

```csharp
/// <summary>What a card does as the engine runs it (spec §4.1). <see cref="ManualLines"/> are the 1-based text lines players
/// resolve by hand; <see cref="Unsupported"/> says why a mapped file is played as Unmapped. <see cref="KeywordEntries"/> are the
/// file's keywords with their values and costs (empty for Unmapped cards, whose keywords come from their text).</summary>
internal sealed record CardEffectInfo(
    MappingStatus Status,
    IReadOnlyList<Ability> Abilities,
    IReadOnlySet<DisplayKeyword> Keywords,
    IReadOnlyList<int> ManualLines,
    IReadOnlyList<string> Unsupported,
    IReadOnlyList<KeywordEntry> KeywordEntries);
```

- In `Build`, change the four constructions and the support call:

```csharp
        if (card.Type == CardType.Rune) return new(file.Status, [], new HashSet<DisplayKeyword>(), [], [], []);
        var unsupported = EffectsSupport.Problems(file, card.Type);
```

```csharp
        if (file.Status == MappingStatus.Full) return new(MappingStatus.Full, abilities, keywords, [], [], file.Keywords);
```

```csharp
        return new(MappingStatus.Partial, abilities, keywords, manual, [], file.Keywords);
```

```csharp
    private static CardEffectInfo Unmapped(Card card, List<int> lines, IReadOnlyList<string> unsupported) =>
        new(MappingStatus.Unmapped, [], CardKeywords.Own(card), lines, unsupported, []);
```

In `src/CromoBound.Engine/Effects/EffectsSupport.cs`:
- Add `using CromoBound.Models.Cards;`.
- Add after `Keywords`:

```csharp
    /// <summary>The keywords that take a value (Hunt 3, Assault 2), a cost (Empower, Equip) or steps (Deathknell). Any other
    /// parameter would be ignored by the engine, so it is reported (spec §7: never silent).</summary>
    private static readonly HashSet<MechanicalKeyword> WithValue =
        [MechanicalKeyword.Hunt, MechanicalKeyword.Assault, MechanicalKeyword.Shield, MechanicalKeyword.Deflect];
    private static readonly HashSet<MechanicalKeyword> WithCost = [MechanicalKeyword.Empower, MechanicalKeyword.Equip];
    private static readonly HashSet<MechanicalKeyword> WithSteps = [MechanicalKeyword.Deathknell];
```

- Replace `Problems` with:

```csharp
    /// <summary>One line per construct the engine can't run; empty when it runs the whole file. <paramref name="type"/> is the
    /// card's type: which trigger forms make sense depends on it.</summary>
    public static IReadOnlyList<string> Problems(EffectsFile file, CardType type)
    {
        var problems = new List<string>();
        if (file.Overrides is not null) problems.Add("overrides");
        if (file.AdditionalCosts.Count > 0) problems.Add("additionalCosts");
        if (file.AsYouPlay.Count > 0) problems.Add("asYouPlay");
        for (var k = 0; k < file.Keywords.Count; k++) CheckKeyword(file.Keywords[k], $"keywords[{k}]", problems);
        for (var i = 0; i < file.Abilities.Count; i++) CheckAbility(file.Abilities[i], type, $"abilities[{i}]", problems);
        return problems;
    }
```

- Replace `CheckKeyword` with:

```csharp
    /// <summary>A keyword's parameters must be ones it takes; Hunt needs its value, Empower and Equip a plain cost, and Deathknell's
    /// steps run like a trigger's.</summary>
    private static void CheckKeyword(KeywordEntry entry, string at, List<string> problems)
    {
        if (!Keywords.Contains(entry.Keyword))
        {
            problems.Add($"keyword {entry.Keyword}");
            return;
        }
        if (entry.Value is not null && !WithValue.Contains(entry.Keyword)) problems.Add($"{at}: value");
        if (entry.Cost is not null && !WithCost.Contains(entry.Keyword)) problems.Add($"{at}: cost");
        if (entry.Steps.Count > 0 && !WithSteps.Contains(entry.Keyword)) problems.Add($"{at}: steps");
        if (entry.Keyword == MechanicalKeyword.Deathknell) CheckSteps(entry.Steps, at, problems, targets: false);
        if (entry.Keyword == MechanicalKeyword.Hunt && entry.Value is null) problems.Add($"{at}: value");
        if (WithCost.Contains(entry.Keyword) && (entry.Cost is null || entry.Cost.Actions.Count > 0 || entry.Cost.ExhaustSelf is not null))
            problems.Add($"{at}: cost");
    }
```

- In `CheckAbility`, add the `CardType type` parameter after `ability` (`private static void CheckAbility(Ability ability, CardType type, string at, List<string> problems)`) and change the trigger check to `if (!IsSupportedTrigger(triggered.Trigger, type)) problems.Add($"{at}: trigger");`.
- Replace `IsSupportedTrigger` with:

```csharp
    /// <summary>The forms the <see cref="TriggerWatcher"/> maps: the source's own Dies, BecameEmpowered, Played, Hold or Conquer
    /// (a card that can be their subject, so not a battlefield), and a battlefield's "when you hold (or conquer) here".</summary>
    private static bool IsSupportedTrigger(Trigger trigger, CardType type)
    {
        if (trigger.Filter is not null || trigger.Phase is not null) return false;
        var own = type != CardType.Battlefield && trigger.Subject?.Ref == RefKind.Self && trigger.By is null && trigger.Where is null;
        var here = type == CardType.Battlefield && trigger.Subject is null && trigger.By?.Kind == PlayerKind.You
            && trigger.Where?.Ref == RefKind.Here;
        return trigger.Event switch
        {
            TriggerEvent.Dies or TriggerEvent.BecameEmpowered or TriggerEvent.Played => own,
            TriggerEvent.Hold or TriggerEvent.Conquer => own || here,
            _ => false,
        };
    }
```

- In `CheckStep`, replace the line `if (step.Player is { Kind: null, Var: null }) problems.Add($"{at}: player");` with:

```csharp
        if (step.Player is not null && !UsesPlayer(step)) problems.Add($"{at}: player");
        else if (step.Player is { Kind: null, Var: null }) problems.Add($"{at}: player");
```

  and add:

```csharp
    /// <summary>The steps that act for a player; on any other step a "player" would be ignored.</summary>
    private static bool UsesPlayer(Step step) => step is DrawStep or BurnStep or ChannelStep or GainXpStep or PredictStep;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests; `EffectsDataTests` is unchanged because no real file uses the newly rejected forms).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "fix(engine): report every mapping the engine would ignore"
```

---

### Task 2: Keyword ability text, plays from piles only, spells played by effects

**Files:**
- Modify: `src/CromoBound.Engine/Effects/CardEffects.cs`
- Modify: `src/CromoBound.Engine/Effects/Steps/PlayStepHandler.cs`
- Modify: `tests/CromoBound.Engine.Tests/TriggerTests.cs`
- Modify: `tests/CromoBound.Engine.Tests/EffectPlayTests.cs`

**Interfaces:**
- Consumes: Task 1's `CardEffects` (`KeywordEntries`), Plan E's `KeywordAbilities`, `PlayHandler`, `AbilityText`.
- Produces: the abilities keywords stand for carry `Line` = the text line starting with the keyword, so `Game.AbilityText` gives their chain items and trigger options text. `CardEffects.KeywordAbilities(EffectsFile file, IReadOnlyList<string> lines)` and `CardEffects.KeywordLine(IReadOnlyList<string> lines, MechanicalKeyword keyword) : LineRef?` (private) are used again by Task 7.

- [ ] **Step 1: Write the failing tests**

Add to `TriggerTests`:

```csharp
    [Fact]
    public void A_keyword_trigger_shows_its_keyword_line_on_the_chain()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("soaring-scout"));
        var scout = game.Put("soaring-scout", Place.Base(P1));
        var engine = game.Start();

        Assert.True(engine.SubmitManual(P1, new ManualDamage(scout, 1)).Accepted);

        Assert.StartsWith("[Deathknell]", Assert.Single(game.State.Chain).Text);
    }
```

Add to `EffectPlayTests`:

```csharp
    [Fact]
    public void A_spell_played_by_an_effect_waits_on_the_chain_and_the_effect_goes_on()
    {
        var game = new TestGame();
        game.Put("spell", Place.Trash(P2));
        var engine = game.Start();
        var hand = game.State.At(Place.Hand(P1)).Count;
        var context = Context();

        var events = engine.RunNow(new ResolveEffectTask(context,
        [
            new ChooseCardStep { From = OpponentsTrash, Filter = new Filter { Type = CardType.Spell }, Store = "picked" },
            new PlayStep { Card = ObjectRef.Variable("picked"), Cost = PlayCostMode.IgnoreAll, Store = "played" },
            new DrawStep { Amount = 1 },
        ], _ => { }));

        var item = Assert.Single(game.State.Chain);
        Assert.Equal(ChainItemStatus.Finalized, item.Status);
        Assert.Equal(new[] { item.Card!.Value }, context.Vars["played"].Objects);
        Assert.Contains(events, e => e is CardPlayed { CardId: "spell" });
        Assert.Equal(hand + 1, game.State.At(Place.Hand(P1)).Count);
        Assert.Equal(P1, engine.Decision<PriorityDecision>().Player);

        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());
        engine.Decision<ResolveManuallyDecision>();
        engine.Accept(P1, new ResolveDone());
        Assert.Contains(game.State.At(Place.Trash(P2)), id => game.State[id].CardId == "spell");
    }

    [Fact]
    public void A_facedown_card_is_never_played_by_an_effect()
    {
        var game = new TestGame();
        var hidden = game.Put("hidden-unit", Place.Facedown(0));
        var engine = game.Start();
        var context = Context();
        context.Vars["picked"] = new EffectVar([hidden], [], 1, true);

        engine.RunNow(new ResolveEffectTask(context, [new PlayStep { Card = ObjectRef.Variable("picked"), Store = "played" }], _ => { }));

        Assert.False(context.Vars["played"].Happened);
        Assert.Equal(PlaceKind.Facedown, game.State[hidden].Place.Kind);
        Assert.Empty(game.State.Chain);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~TriggerTests|FullyQualifiedName~EffectPlayTests"`
Expected: FAIL (the Deathknell item's text is null; the facedown card is played). The spell test pins Plan E behavior and may already pass.

- [ ] **Step 3: Write the implementation**

In `src/CromoBound.Engine/Effects/CardEffects.cs`:
- In `Build`, change the abilities line to `IReadOnlyList<Ability> abilities = [.. file.Abilities, .. KeywordAbilities(file, lines)];`.
- Replace `KeywordAbilities` and `OwnTrigger` with:

```csharp
    /// <summary>The abilities keywords stand for (spec §8.1), listed after the file's own so ability indices follow the JSON. Each
    /// carries the keyword's text line, so its chain item has text.</summary>
    private static IEnumerable<Ability> KeywordAbilities(EffectsFile file, IReadOnlyList<string> lines)
    {
        foreach (var entry in file.Keywords)
        {
            var line = KeywordLine(lines, entry.Keyword);
            switch (entry.Keyword)
            {
                case MechanicalKeyword.Deathknell:
                    yield return OwnTrigger(TriggerEvent.Dies, entry.Steps, line);
                    break;
                case MechanicalKeyword.Vision:
                    yield return OwnTrigger(TriggerEvent.Played, [new PredictStep()], line);
                    break;
                case MechanicalKeyword.Hunt:
                    IReadOnlyList<Step> gain = [new GainXpStep { Amount = entry.Value!.Value }];
                    yield return OwnTrigger(TriggerEvent.Conquer, gain, line);
                    yield return OwnTrigger(TriggerEvent.Hold, gain, line);
                    break;
                case MechanicalKeyword.Empower:
                    yield return new ActivatedAbility
                    {
                        Line = line,
                        Cost = entry.Cost,
                        UseOnlyIf = new Condition { Not = new Condition { Empowered = true } },
                        Steps = [new EmpowerStep { Target = ObjectRef.Self }],
                    };
                    break;
            }
        }
    }

    /// <summary>The first text line that starts with the keyword ("[Deathknell] ...", "[Hunt 3] ..."), or null.</summary>
    private static LineRef? KeywordLine(IReadOnlyList<string> lines, MechanicalKeyword keyword)
    {
        for (var n = 1; n <= lines.Count; n++)
            if (lines[n - 1].StartsWith($"[{keyword}", StringComparison.Ordinal)) return n;
        return null;
    }

    /// <summary>"When I ...": a trigger on the card's own event.</summary>
    private static TriggeredAbility OwnTrigger(TriggerEvent kind, IReadOnlyList<Step> steps, LineRef? line) =>
        new() { Line = line, Trigger = new Trigger { Event = kind, Subject = ObjectRef.Self }, Steps = steps };
```

(A Partial card's manual lines are still computed from the file's own abilities, and keyword lines are never manual, so this changes no manual lines.)

In `src/CromoBound.Engine/Effects/Steps/PlayStepHandler.cs`, replace the `.Where(...)` filter on the resolved cards with `.Where(id => IsPile(game.State[id].Place.Kind))`, change the class summary's "from wherever it is" to "from a pile (hand, trash, deck, banishment, Champion Zone)", and add:

```csharp
    /// <summary>An effect plays cards from piles only: never from the board, the chain or a facedown slot (hidden plays have their
    /// own rules).</summary>
    private static bool IsPile(PlaceKind kind) =>
        kind is PlaceKind.Hand or PlaceKind.Trash or PlaceKind.MainDeck or PlaceKind.Banishment or PlaceKind.ChampionZone;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "fix(engine): give keyword abilities text and play effects only from piles"
```

---

### Task 3: Assault and Shield

**Files:**
- Create: `src/CromoBound.Engine/Effects/Modifiers.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Modify: `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`
- Test: `tests/CromoBound.Engine.Tests/ModifierTests.cs`

**Interfaces:**
- Consumes: Task 1's `CardEffectInfo.KeywordEntries`.
- Produces: `Modifiers.MightOf(Game, CardInstance) : int` and `Modifiers.KeywordValue(Game, CardInstance, MechanicalKeyword) : int` (internal static, `CromoBound.Engine.Effects`). Tasks 4, 5 and 7 add to the same class. `Game.MightOf(ObjectId)` delegates to it.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/ModifierTests.cs`:

```csharp
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ModifierTests
{
    [Fact]
    public void Daring_poro_has_assault_1_only_while_attacking()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("daring-poro"));
        var poro = game.Put("daring-poro", Place.Base(P1));
        var engine = game.Start();

        Assert.Equal(2, engine.MightOf(poro));
        game.State[poro].Role = CombatRole.Attacker;
        Assert.Equal(3, engine.MightOf(poro));
        game.State[poro].Role = CombatRole.Defender;
        Assert.Equal(2, engine.MightOf(poro));
    }

    [Fact]
    public void Mutated_mouser_has_shield_2_only_while_defending()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("mutated-mouser"));
        var mouser = game.Put("mutated-mouser", Place.Base(P1));
        var engine = game.Start();

        game.State[mouser].Role = CombatRole.Attacker;
        Assert.Equal(1, engine.MightOf(mouser));
        game.State[mouser].Role = CombatRole.Defender;
        Assert.Equal(3, engine.MightOf(mouser));
    }

    [Fact]
    public void Several_instances_stack_and_a_missing_value_counts_as_1()
    {
        var game = new TestGame(db: EngineTestDb.Create(("unit-3", """
            { "cardId": "unit-3", "status": "Full", "keywords": [ { "keyword": "Assault", "value": 2 }, { "keyword": "Assault" } ] }
            """)));
        var unit = game.Put("unit-3", Place.Base(P1));
        var engine = game.Start();

        game.State[unit].Role = CombatRole.Attacker;

        Assert.Equal(6, engine.MightOf(unit));
    }

    [Fact]
    public void Buffs_and_manual_modifiers_still_count()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("daring-poro"));
        var poro = game.Put("daring-poro", Place.Base(P1));
        var engine = game.Start();
        game.State[poro].Buffed = true;
        game.State[poro].Modifiers.Add(new MightModifier(2, Duration.ThisTurn));
        game.State[poro].Role = CombatRole.Attacker;

        Assert.Equal(6, engine.MightOf(poro));
    }
}
```

In `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`, add `daring-poro`, `mutated-mouser` and `jeweled-colossus` to `Cards_the_engine_runs_today_are_full`, and replace the data rows of `Cards_needing_later_plans_play_by_hand_for_now` with:

```csharp
    [InlineData("noxus-hopeful", "Passive ability")]
    [InlineData("navori-scout", "keyword Deflect")]
    [InlineData("soulspinner", "keyword Ambush")]
    [InlineData("veteran-poro", "keyword Weaponmaster")]
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~ModifierTests|FullyQualifiedName~EffectsDataTests"`
Expected: FAIL (Assault and Shield add nothing; the three cards are Unmapped).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Engine/Effects/Modifiers.cs`:

```csharp
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>Values effects change while they apply (spec §4.7). Evaluated live: nothing is stored, so replay stays trivial.</summary>
internal static class Modifiers
{
    /// <summary>Printed Might, +1 while buffed, manual and effect Might modifiers, Assault X while attacking and Shield X while
    /// defending (CR 807, 814).</summary>
    public static int MightOf(Game game, CardInstance unit)
    {
        var combat = unit.Role switch
        {
            CombatRole.Attacker => KeywordValue(game, unit, MechanicalKeyword.Assault),
            CombatRole.Defender => KeywordValue(game, unit, MechanicalKeyword.Shield),
            _ => 0,
        };
        return (game.CardOf(unit).Might ?? 0) + (unit.Buffed ? 1 : 0) + unit.Modifiers.Sum(m => m.Amount) + combat;
    }

    /// <summary>The sum of a numbered keyword's values on the card: a missing value is 1, and several instances stack.</summary>
    public static int KeywordValue(Game game, CardInstance instance, MechanicalKeyword keyword) =>
        game.Effects.For(instance.CardId).KeywordEntries.Where(k => k.Keyword == keyword).Sum(k => k.Value ?? 1);
}
```

In `src/CromoBound.Engine/Rules/Game.cs`, replace `MightOf` with:

```csharp
    /// <summary>Current Might (spec §4.7): printed + buff + modifiers + Assault or Shield in combat.</summary>
    public int MightOf(ObjectId id) => Modifiers.MightOf(this, State[id]);
```

In `src/CromoBound.Engine/Effects/EffectsSupport.cs`, add `MechanicalKeyword.Assault, MechanicalKeyword.Shield,` to `Keywords` and change its summary to "Keywords the engine runs. The others arrive later in Plan F."

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including Plan B's combat tests: units with no mapped keyword keep their Might).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): add assault and shield"
```

---

### Task 4: Legion, Noxus Hopeful's cost reduction, Deflect

**Files:**
- Modify: `src/CromoBound.Engine/State/TurnState.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Turn.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Play.cs`
- Modify: `src/CromoBound.Engine/Effects/Resolvers/ConditionResolver.cs`
- Modify: `src/CromoBound.Engine/Effects/Modifiers.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Modify: `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`
- Test: `tests/CromoBound.Engine.Tests/CostTests.cs`

**Interfaces:**
- Consumes: Task 3's `Modifiers.KeywordValue`; Plan D's `ChainItem.Effect.Targets`; Plan E's `PlayCardTask` (`IgnoreCost`, `FromHidden`).
- Produces:
  - `TurnState.Played` (`Dictionary<PlayerId, int>`), `TurnState.PlayedThisTurn(PlayerId) : int`, `TurnState.MarkPlayed(PlayerId)`. They are cleared at each turn start and counted when a play finalizes.
  - `ConditionResolver` understands `{ "legion": true }`.
  - `Modifiers.CostOf(Game, PlayCardTask) : TotalCost` and `Modifiers.OwnPassives(Game, CardInstance, EffectContext) : IEnumerable<Modifier>` (private; Task 5 adds `Permits` next to it).
  - `EffectsSupport` accepts `Passive` abilities whose modifiers it supports (Task 5 adds `Permission`).

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/CostTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class CostTests
{
    private const string DeflectUnit = """{ "cardId": "unit-3", "status": "Full", "keywords": [ { "keyword": "Deflect" } ] }""";

    private const string DealTwice = """
        { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1, "steps": [
          { "action": "Deal", "amount": 1, "target": { "select": "Unit", "count": 1 } },
          { "action": "Deal", "amount": 1, "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    /// <summary>P1 holds Noxus Hopeful and a unit-2, with six runes.</summary>
    private static TestGame HopefulGame()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("noxus-hopeful"));
        game.Put("noxus-hopeful", Place.Hand(P1));
        game.Put("unit-2", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 6);
        return game;
    }

    [Fact]
    public void Noxus_hopeful_costs_2_less_after_you_played_another_card_this_turn()
    {
        var game = HopefulGame();
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "noxus-hopeful")));
        Assert.Equal(4, engine.Decision<PayCostDecision>().Cost.Energy);
        engine.Accept(P1, new CancelPlay());

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-2")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "noxus-hopeful")));

        Assert.Equal(2, engine.Decision<PayCostDecision>().Cost.Energy);
    }

    [Fact]
    public void A_cancelled_play_doesnt_count_for_legion()
    {
        var game = HopefulGame();
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-2")));
        engine.Accept(P1, new CancelPlay());

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "noxus-hopeful")));

        Assert.Equal(4, engine.Decision<PayCostDecision>().Cost.Energy);
    }

    [Fact]
    public void Legion_counts_only_this_turns_plays()
    {
        var game = HopefulGame();
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-2")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new EndTurn());
        engine.Accept(P2, new EndTurn());

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "noxus-hopeful")));

        Assert.Equal(4, engine.Decision<PayCostDecision>().Cost.Energy);
    }

    [Fact]
    public void Deflect_taxes_an_opponent_who_targets_it()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", TargetTests.KillAUnit), ("unit-3", DeflectUnit)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 2);
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));

        var cost = engine.Decision<PayCostDecision>().Cost;
        Assert.Equal(1, cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Any }, cost.Power);
    }

    [Fact]
    public void Deflect_doesnt_tax_targeting_your_own_unit()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", TargetTests.KillAUnit), ("unit-3", DeflectUnit)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 2);
        game.Put("unit-3", Place.Base(P1));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));

        Assert.Empty(engine.Decision<PayCostDecision>().Cost.Power);
    }

    [Fact]
    public void Deflect_taxes_each_choice_of_an_enemy_target()
    {
        var game = new TestGame(db: EngineTestDb.Create(("spell", DealTwice), ("unit-3", DeflectUnit)));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 3);
        game.Put("unit-3", Place.Base(P2));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));

        Assert.Equal(new[] { PowerSymbol.Any, PowerSymbol.Any }, engine.Decision<PayCostDecision>().Cost.Power);
    }
}
```

In `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`, add `noxus-hopeful`, `navori-scout`, `pouty-poro` and `token-bird` to `Cards_the_engine_runs_today_are_full`, and remove the `noxus-hopeful` and `navori-scout` rows from `Cards_needing_later_plans_play_by_hand_for_now`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~CostTests|FullyQualifiedName~EffectsDataTests"`
Expected: FAIL (Noxus Hopeful always costs 4; Deflect adds nothing; the four cards are Unmapped).

- [ ] **Step 3: Write the implementation**

In `src/CromoBound.Engine/State/TurnState.cs`, add to the class:

```csharp
    /// <summary>How many cards each player has played (finalized) this turn; Legion reads it.</summary>
    public Dictionary<PlayerId, int> Played { get; } = [];

    public int PlayedThisTurn(PlayerId player) => Played.GetValueOrDefault(player);

    public void MarkPlayed(PlayerId player) => Played[player] = PlayedThisTurn(player) + 1;
```

In `src/CromoBound.Engine/Rules/Game.Turn.cs`, in `StartTurn`, add `turn.Played.Clear();` after `turn.Scored.Clear();`.

In `src/CromoBound.Engine/Rules/Game.Play.cs`:
- Add `using CromoBound.Engine.Effects;`.
- Replace the `PlayStep.Cost` case with:

```csharp
                case PlayStep.Cost:
                    task.Cost = Modifiers.CostOf(this, task);
                    task.Step = task.IgnoreCost && task.Cost.Energy == 0 && task.Cost.Power.Count == 0 ? PlayStep.Finalize : PlayStep.Pay;
                    break;
```

- In `FinishFinalizing`, after `task.Finished = true;`, add `State.Turn.MarkPlayed(task.Player);`.

In `src/CromoBound.Engine/Effects/Resolvers/ConditionResolver.cs`, add before the `throw`:

```csharp
        if (condition.Legion is { } legion) return (game.State.Turn.PlayedThisTurn(context.Controller) > 0) == legion;
```

and change the summary to: "Turns conditions into true or false (spec §4.5): all, any, not, empowered (the source is Empowered) and legion (the controller played a card this turn; read while a card is being played, so its own play isn't counted yet). <see cref="EffectsSupport"/> keeps every other condition out of the files it runs." Change the exception message to "Only all, any, not, empowered and legion conditions are supported so far."

In `src/CromoBound.Engine/Effects/Modifiers.cs`:
- Add these usings:

```csharp
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects.Resolvers;
```

- Add these methods:

```csharp
    /// <summary>A play's total cost (spec §4.7, rule 356):
    /// - the base cost (none from Hidden or when an effect ignores it), plus Accelerate;
    /// - then one [A] per Deflect on each target an opponent of the player controls, counted per choice (CR 809);
    /// - then the card's own energy reductions (Legion), energy never below 0.</summary>
    public static TotalCost CostOf(Game game, PlayCardTask task)
    {
        var item = task.Item!;
        var card = game.State[item.Card!.Value];
        var cost = Payment.CostOf(game.CardOf(card), task.FromHidden || task.IgnoreCost, item.Accelerate);
        List<PowerSymbol> power = [.. cost.Power];
        IEnumerable<ObjectId> targets = item.Effect is { } effect ? effect.Targets.SelectMany(t => t) : [];
        foreach (var target in targets)
            if (game.State.Exists(target) && game.State[target].Controller != task.Player)
                power.AddRange(Enumerable.Repeat(PowerSymbol.Any, KeywordValue(game, game.State[target], MechanicalKeyword.Deflect)));
        var context = new EffectContext { Controller = task.Player, Source = card.Id, SourceCardId = card.CardId };
        var reduction = OwnPassives(game, card, context).OfType<CostReductionModifier>().Sum(m => ValueResolver.Resolve(game, context, m.Energy!));
        return new TotalCost(Math.Max(0, cost.Energy - reduction), power);
    }

    /// <summary>The modifiers of the card's passive abilities that apply to the card itself and whose condition holds now.</summary>
    private static IEnumerable<Modifier> OwnPassives(Game game, CardInstance card, EffectContext context) =>
        game.Effects.For(card.CardId).Abilities.OfType<PassiveAbility>()
            .Where(p => p.Condition is null || ConditionResolver.Holds(game, context, p.Condition))
            .SelectMany(p => p.Modifiers)
            .Where(m => m.AppliesTo?.Ref == RefKind.Self);
```

In `src/CromoBound.Engine/Effects/EffectsSupport.cs`:
- Add `MechanicalKeyword.Deflect,` to `Keywords`.
- Replace `CheckAbility` with:

```csharp
    private static void CheckAbility(Ability ability, CardType type, string at, List<string> problems)
    {
        if (ability is not (SpellAbility or TriggeredAbility or ActivatedAbility or PassiveAbility))
        {
            problems.Add($"{at}: {ability.GetType().Name.Replace("Ability", "")} ability");
            return;
        }
        var condition = ability.Condition is null || (ability is PassiveAbility && ability.Condition.Legion == true);
        if (!condition || ability.ActiveIn is not null || ability.Script is not null)
            problems.Add($"{at}: condition, activeIn or script");
        switch (ability)
        {
            case SpellAbility spell:
                CheckSteps(spell.Steps, at, problems, targets: true);
                break;
            case TriggeredAbility triggered:
                if (!IsSupportedTrigger(triggered.Trigger, type)) problems.Add($"{at}: trigger");
                if (triggered.If is not null || triggered.Optional is not null || triggered.Cost is not null || triggered.Limit is not null)
                    problems.Add($"{at}: if, optional, cost or limit");
                CheckSteps(triggered.Steps, at, problems, targets: false);
                break;
            case ActivatedAbility activated:
                if (activated.UseOnlyIf is not null || activated.Limit is not null) problems.Add($"{at}: useOnlyIf or limit");
                if (activated.Cost is { } cost && !IsSupportedCost(cost)) problems.Add($"{at}: cost");
                CheckSteps(activated.Steps, at, problems, targets: false);
                break;
            case PassiveAbility passive:
                if (passive.While is not null) problems.Add($"{at}: while");
                foreach (var modifier in passive.Modifiers)
                    if (!IsSupportedModifier(modifier)) problems.Add($"{at}: modifier {modifier.GetType().Name.Replace("Modifier", "")}");
                break;
        }
    }

    /// <summary>A modifier on the card itself, of a kind <see cref="Modifiers"/> evaluates: a literal energy cost reduction.</summary>
    private static bool IsSupportedModifier(Modifier modifier) => modifier.AppliesTo?.Ref == RefKind.Self && modifier switch
    {
        CostReductionModifier reduction => reduction.Energy?.Literal is not null && reduction.Power.Count == 0
            && reduction.Minimum is null && reduction.FromZone is null,
        _ => false,
    };
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including Plan B's `PaymentTests` and `PlayTests`: a card with no modifiers and no Deflect target costs what `Payment.CostOf` says).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): add legion cost reductions and deflect"
```

---

### Task 5: Ambush and playing to a battlefield with enemy units

**Files:**
- Modify: `src/CromoBound.Engine/Rules/Game.Play.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Priority.cs`
- Modify: `src/CromoBound.Engine/Effects/Modifiers.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Modify: `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`
- Test: `tests/CromoBound.Engine.Tests/LocationTests.cs`

**Interfaces:**
- Consumes: Task 4's `Modifiers.OwnPassives`.
- Produces:
  - `Modifiers.Permits(Game, CardInstance, PlayerId controller, Permission) : bool`.
  - `PlayCardTask.ByAmbush` (`init`).
  - `Game.HasPlayTiming(PlayerId, CardInstance) : bool` and `Game.AmbushBattlefields(PlayerId, CardInstance) : List<int>` (private).

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/LocationTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class LocationTests
{
    /// <summary>P1 holds Soulspinner (Ambush) and the unmapped "spell", with four runes. With <paramref name="unitThere"/>, P1
    /// controls the first battlefield and has a unit there.</summary>
    private static TestGame SoulspinnerGame(bool unitThere)
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("soulspinner"));
        if (unitThere)
        {
            game.State.Battlefields[0].Controller = P1;
            game.Put("unit-2", Place.Battlefield(0));
        }
        game.Put("soulspinner", Place.Hand(P1));
        game.Put("spell", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 4);
        return game;
    }

    [Fact]
    public void Ambush_lets_a_unit_be_played_as_a_reaction_to_a_battlefield_where_you_have_units()
    {
        var game = SoulspinnerGame(unitThere: true);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);

        var soulspinner = game.First(Place.Hand(P1), "soulspinner");
        Assert.Contains(soulspinner, engine.Decision<PriorityDecision>().Playable);
        engine.Accept(P1, new PlayCard(soulspinner));
        engine.PayWithSuggestion(P1);

        Assert.Contains(game.State.At(Place.Battlefield(0)), id => game.State[id].CardId == "soulspinner");
        Assert.Single(game.State.Chain);
    }

    [Fact]
    public void Without_units_on_a_battlefield_ambush_gives_no_reaction_timing()
    {
        var game = SoulspinnerGame(unitThere: false);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "spell")));
        engine.PayWithSuggestion(P1);

        Assert.DoesNotContain(game.First(Place.Hand(P1), "soulspinner"), engine.Decision<PriorityDecision>().Playable);
    }

    [Fact]
    public void In_your_main_phase_an_ambush_unit_may_still_enter_at_your_base()
    {
        var game = SoulspinnerGame(unitThere: true);
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "soulspinner")));

        Assert.Equal(new[] { Place.Base(P1), Place.Battlefield(0) }, engine.Decision<PlayChoicesDecision>().Locations);
    }

    [Fact]
    public void Rengar_trophy_hunter_can_be_played_to_a_battlefield_with_enemy_units()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("rengar-trophy-hunter"));
        game.State.Battlefields[1].Controller = P2;
        game.Put("unit-2", Place.Battlefield(1), P2);
        game.Put("rengar-trophy-hunter", Place.Hand(P1));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "rengar-trophy-hunter")));

        Assert.Equal(new[] { Place.Base(P1), Place.Battlefield(1) }, engine.Decision<PlayChoicesDecision>().Locations);
    }

    [Fact]
    public void An_ordinary_unit_isnt_offered_a_battlefield_with_enemy_units()
    {
        var game = new TestGame();
        game.State.Battlefields[1].Controller = P2;
        game.Put("unit-2", Place.Battlefield(1), P2);
        game.Put("unit-3", Place.Hand(P1));
        var engine = game.Start();

        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));

        Assert.IsType<PayCostDecision>(engine.Pending);
    }
}
```

In `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`, add `soulspinner`, `inferna`, `rengar-trophy-hunter` and `rengar-unseen` to `Cards_the_engine_runs_today_are_full`, and remove the `soulspinner` row from `Cards_needing_later_plans_play_by_hand_for_now`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~LocationTests|FullyQualifiedName~EffectsDataTests"`
Expected: FAIL (Soulspinner and Rengar are Unmapped, so no Ambush timing and no extra battlefield; `An_ordinary_unit...` already passes).

- [ ] **Step 3: Write the implementation**

In `src/CromoBound.Engine/Effects/Modifiers.cs`, add:

```csharp
    /// <summary>Whether one of the card's passive abilities gives the card the permission now (spec §4.7).</summary>
    public static bool Permits(Game game, CardInstance card, PlayerId controller, Permission permission) =>
        OwnPassives(game, card, new EffectContext { Controller = controller, Source = card.Id, SourceCardId = card.CardId })
            .OfType<PermissionModifier>()
            .Any(p => p.Permission == permission);
```

In `src/CromoBound.Engine/Rules/Game.Priority.cs`, replace the first loop of `PlayableCards` and add `HasPlayTiming`:

```csharp
    /// <summary>Cards the player may start playing now, by timing only (CR 310, 806, 811, 813, 822). Payment is checked later.</summary>
    private IEnumerable<ObjectId> PlayableCards(PlayerId player)
    {
        foreach (var id in State.At(Place.Hand(player)).Concat(State.At(Place.ChampionZone(player))))
        {
            var card = State[id];
            var timing = HasPlayTiming(player, card) || AmbushBattlefields(player, card).Count > 0;
            if (timing && HasTargetsFor(player, card)) yield return id;
        }
        foreach (var battlefield in State.Battlefields)
            foreach (var id in State.At(Place.Facedown(battlefield.Index)))
                if (CanPlayFromHidden(State[id], player) && HasTargetsFor(player, State[id])) yield return id;
    }

    /// <summary>The card's own timing: anything in your Neutral Open Main, Reaction any time, Action while no chain exists.</summary>
    private bool HasPlayTiming(PlayerId player, CardInstance card) =>
        IsNeutralOpenMain(player) || Has(card, DisplayKeyword.Reaction) || (!IsClosed && Has(card, DisplayKeyword.Action));
```

In `src/CromoBound.Engine/Rules/Game.Play.cs`:
- Add to `PlayCardTask`:

```csharp
    /// <summary>Started with Ambush's Reaction timing because the card's own timing didn't allow the play (CR 822): it may enter
    /// only a battlefield where the player has units.</summary>
    public bool ByAmbush { get; init; }
```

- Replace `StartPlay` with:

```csharp
    private void StartPlay(PlayerId player, ObjectId card) =>
        Push(new PlayCardTask(player, card) { ByAmbush = State[card].Place.Kind != PlaceKind.Facedown && !HasPlayTiming(player, State[card]) });
```

- In `RunPlay`, replace the `PlayStep.Choices` case with:

```csharp
                case PlayStep.Choices:
                    if (AskPlayChoices(task)) return false;
                    if (task.Step == PlayStep.Choices) task.Step = PlayStep.Targets;
                    break;
```

- Replace `PlayLocations` with:

```csharp
    /// <summary>Where the permanent may enter (CR 355.2.a, 811.1.d, 822): a unit at your Base or a battlefield you control, plus
    /// with Ambush a battlefield where you have units, plus with the permission a battlefield with enemy units; only the Ambush
    /// battlefields when Ambush gave the timing. Gear at your Base; from Hidden, that card's battlefield. Spells have no location.</summary>
    private List<Place> PlayLocations(PlayCardTask task)
    {
        var instance = State[task.Item!.Card!.Value];
        var card = CardOf(instance);
        if (card.Type == CardType.Spell) return [];
        if (task.FromHidden) return [Place.Battlefield(task.Origin.Index!.Value)];
        if (card.Type != CardType.Unit) return [Place.Base(task.Player)];
        var ambush = AmbushBattlefields(task.Player, instance);
        if (task.ByAmbush) return [.. ambush.Select(Place.Battlefield)];
        IEnumerable<int> enemies = Modifiers.Permits(this, instance, task.Player, Permission.PlayToBattlefieldWithEnemyUnits)
            ? State.Battlefields.Where(b => UnitsAt(Place.Battlefield(b.Index)).Any(u => u.Controller != task.Player)).Select(b => b.Index)
            : [];
        var battlefields = State.Battlefields.Where(b => b.Controller == task.Player).Select(b => b.Index)
            .Concat(ambush).Concat(enemies).Distinct().Order();
        return [Place.Base(task.Player), .. battlefields.Select(Place.Battlefield)];
    }

    /// <summary>Ambush (CR 822): the battlefields where the player has units, when the card has Ambush.</summary>
    private List<int> AmbushBattlefields(PlayerId player, CardInstance card) =>
        !Has(card, DisplayKeyword.Ambush) ? [] :
        [.. State.Battlefields.Where(b => UnitsAt(Place.Battlefield(b.Index)).Any(u => u.Controller == player)).Select(b => b.Index)];
```

- In `AskPlayChoices`, right after `var locations = PlayLocations(task);`, add:

```csharp
        if (locations.Count == 0 && CardOf(item.Card!.Value).Type != CardType.Spell)
        {
            UndoPlay(task);
            return false;
        }
```

  (A permanent with nowhere to enter, such as an Ambush play whose battlefield emptied, is cancelled instead of finalizing without a location.)

In `src/CromoBound.Engine/Effects/EffectsSupport.cs`:
- Add `MechanicalKeyword.Ambush,` to `Keywords`.
- In `IsSupportedModifier`, add this arm before `_ => false`:

```csharp
        PermissionModifier permission => permission.Permission == Permission.PlayToBattlefieldWithEnemyUnits,
```

  and change its summary to "A modifier on the card itself, of a kind <see cref="Modifiers"/> evaluates: a literal energy cost reduction, or the permission to be played to a battlefield with enemy units."

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including Plan B's `PlayTests` and `HiddenTests`: ordinary and hidden plays keep their locations and timing).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): add ambush and the enemy battlefield permission"
```

---

### Task 6: Attachment

**Files:**
- Create: `src/CromoBound.Engine/Rules/Game.Attachment.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Mutations.cs`
- Modify: `src/CromoBound.Engine/Rules/Game.Manual.cs`
- Modify: `src/CromoBound.Engine/Actions/ManualActions.cs`
- Modify: `src/CromoBound.Engine/Actions/PlayerAction.cs`
- Modify: `src/CromoBound.Engine/Events/GameEvents.cs`
- Modify: `src/CromoBound.Engine/Views/PlayerView.cs`
- Modify: `src/CromoBound.Engine/Views/ViewBuilder.cs`
- Test: `tests/CromoBound.Engine.Tests/AttachmentTests.cs`

**Interfaces:**
- Consumes: Plan A's `CardInstance.AttachedTo`, `MoveCard`, cleanup step 5 (unattached gear at a battlefield goes to Base).
- Produces:
  - `Game.Attach(ObjectId gear, ObjectId unit)` and `Game.Detach(ObjectId gear)` (internal); Task 7 uses `Attach`.
  - Manual actions `ManualAttach(ObjectId Gear, ObjectId Unit)` and `ManualDetach(ObjectId Gear)`.
  - Events `Attached(ObjectId Gear, ObjectId Unit)` and `Detached(ObjectId Gear)`.
  - `CardView.AttachedTo` (`ObjectId?`, last parameter).

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/AttachmentTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class AttachmentTests
{
    /// <summary>P1 controls the first battlefield with a ready unit-2 there, and has gear-1 in their Base.</summary>
    private static (TestGame Test, Game Engine, ObjectId Unit, ObjectId Gear) Setup()
    {
        var game = new TestGame();
        game.State.Battlefields[0].Controller = P1;
        var unit = game.Put("unit-2", Place.Battlefield(0));
        var gear = game.Put("gear-1", Place.Base(P1));
        return (game, game.Start(), unit, gear);
    }

    [Fact]
    public void Attached_gear_joins_its_unit_and_follows_it()
    {
        var (game, engine, unit, gear) = Setup();

        var attached = engine.SubmitManual(P1, new ManualAttach(gear, unit));

        Assert.True(attached.Accepted);
        Assert.Contains(attached.Events, e => e is Attached);
        Assert.Equal(unit, game.State[gear].AttachedTo);
        Assert.Equal(Place.Battlefield(0), game.State[gear].Place);
        engine.Accept(P1, new StandardMove { Units = [unit], Destination = Place.Base(P1) });
        Assert.Equal(Place.Base(P1), game.State[gear].Place);
        Assert.Equal(unit, game.State[gear].AttachedTo);
    }

    [Fact]
    public void Gear_is_detached_and_recalled_when_its_unit_leaves_the_board()
    {
        var (game, engine, unit, gear) = Setup();
        Assert.True(engine.SubmitManual(P1, new ManualAttach(gear, unit)).Accepted);

        var killed = engine.SubmitManual(P1, new ManualDamage(unit, 2));

        Assert.False(game.State.Exists(unit));
        Assert.Null(game.State[gear].AttachedTo);
        Assert.Equal(Place.Base(P1), game.State[gear].Place);
        Assert.Contains(killed.Events, e => e is Detached);
    }

    [Fact]
    public void Detached_gear_at_a_battlefield_goes_back_to_base()
    {
        var (game, engine, unit, gear) = Setup();
        Assert.True(engine.SubmitManual(P1, new ManualAttach(gear, unit)).Accepted);

        Assert.True(engine.SubmitManual(P1, new ManualDetach(gear)).Accepted);

        Assert.Null(game.State[gear].AttachedTo);
        Assert.Equal(Place.Base(P1), game.State[gear].Place);
        Assert.Equal(Place.Battlefield(0), game.State[unit].Place);
    }

    [Fact]
    public void Only_gear_in_play_attaches_to_a_unit_in_play()
    {
        var (game, engine, unit, gear) = Setup();
        var otherGear = game.Put("gear-1", Place.Base(P1));
        var inHand = game.Put("gear-1", Place.Hand(P1));

        Assert.Equal(RejectionCode.UnknownObject, engine.SubmitManual(P1, new ManualAttach(unit, unit)).Rejection!.Code);
        Assert.Equal(RejectionCode.UnknownObject, engine.SubmitManual(P1, new ManualAttach(gear, otherGear)).Rejection!.Code);
        Assert.Equal(RejectionCode.UnknownObject, engine.SubmitManual(P1, new ManualAttach(inHand, unit)).Rejection!.Code);
        Assert.Equal(RejectionCode.UnknownObject, engine.SubmitManual(P1, new ManualDetach(gear)).Rejection!.Code);
        Assert.Null(game.State[gear].AttachedTo);
    }

    [Fact]
    public void Attachment_actions_and_events_round_trip_through_json()
    {
        PlayerAction[] actions = [new ManualAttach(new ObjectId(3), new ObjectId(4)), new ManualDetach(new ObjectId(3))];
        GameEvent[] events = [new Attached(new ObjectId(3), new ObjectId(4)), new Detached(new ObjectId(3))];

        foreach (var action in actions)
        {
            var json = CromoJson.Serialize(action);
            Assert.Contains($"\"{action.GetType().Name}\"", json);
            Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<PlayerAction>(json)));
        }
        foreach (var gameEvent in events)
        {
            var json = CromoJson.Serialize(gameEvent);
            Assert.Contains($"\"{gameEvent.GetType().Name}\"", json);
            Assert.Equal(json, CromoJson.Serialize(CromoJson.Deserialize<GameEvent>(json)));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~AttachmentTests"`
Expected: FAIL (build errors: `ManualAttach`, `ManualDetach`, `Attached`, `Detached` don't exist).

- [ ] **Step 3: Write the implementation**

In `src/CromoBound.Engine/Actions/ManualActions.cs`, add after `ManualCounter`:

```csharp
/// <summary>Attaches gear in play to a unit in play (Equip or Weaponmaster resolved by hand, unmapped Equipment).</summary>
public sealed record ManualAttach(ObjectId Gear, ObjectId Unit) : ManualAction;

/// <summary>Detaches gear; at a battlefield, cleanup then recalls it to its controller's Base.</summary>
public sealed record ManualDetach(ObjectId Gear) : ManualAction;
```

In `src/CromoBound.Engine/Actions/PlayerAction.cs`, add after `[JsonDerivedType(typeof(AddAbilityToChain), "AddAbilityToChain")]`:

```csharp
[JsonDerivedType(typeof(ManualAttach), "ManualAttach")]
[JsonDerivedType(typeof(ManualDetach), "ManualDetach")]
```

In `src/CromoBound.Engine/Events/GameEvents.cs`, add after `[JsonDerivedType(typeof(Predicted), "Predicted")]`:

```csharp
[JsonDerivedType(typeof(Attached), "Attached")]
[JsonDerivedType(typeof(Detached), "Detached")]
```

and append:

```csharp
/// <summary>Gear was attached to a unit (spec §8.2).</summary>
public sealed record Attached(ObjectId Gear, ObjectId Unit) : GameEvent;

/// <summary>Gear was detached from its unit.</summary>
public sealed record Detached(ObjectId Gear) : GameEvent;
```

Create `src/CromoBound.Engine/Rules/Game.Attachment.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Attaches gear to a unit (spec §8.2, CR 818): the gear joins the unit's location.</summary>
    internal void Attach(ObjectId gear, ObjectId unit)
    {
        var instance = State[gear];
        instance.AttachedTo = unit;
        if (instance.Place != State[unit].Place) MoveCard(gear, State[unit].Place);
        Emit(new Attached(gear, unit));
        MarkDirty();
    }

    /// <summary>The gear stays where it is; at a battlefield, cleanup step 5 recalls it to its controller's Base.</summary>
    internal void Detach(ObjectId gear)
    {
        State[gear].AttachedTo = null;
        Emit(new Detached(gear));
        MarkDirty();
    }

    /// <summary>After a unit moved: gear attached to it follows when it stayed on the board, and is detached and recalled to its
    /// controller's Base when it left (spec §8.2).</summary>
    private void MoveAttachments(ObjectId unit, Place landed, bool stayed)
    {
        foreach (var gear in State.Objects.Where(o => o.AttachedTo == unit).Select(o => o.Id).ToList())
        {
            if (stayed)
            {
                if (State[gear].Place != landed) MoveCard(gear, landed);
                continue;
            }
            Detach(gear);
            var home = Place.Base(State[gear].Controller);
            if (State[gear].Place != home) MoveCard(gear, home);
        }
    }

    private Rejection? AttachByHand(ManualAttach attach)
    {
        if (OnBoard(attach.Gear) is not { } gear || CardOf(gear).Type != CardType.Gear || BoardUnit(attach.Unit) is null)
            return Reject(RejectionCode.UnknownObject, "Choose gear in play and a unit in play.");
        Attach(attach.Gear, attach.Unit);
        return null;
    }

    private Rejection? DetachByHand(ManualDetach detach)
    {
        if (OnBoard(detach.Gear) is not { AttachedTo: not null })
            return Reject(RejectionCode.UnknownObject, "Choose gear in play that is attached to a unit.");
        Detach(detach.Gear);
        return null;
    }
}
```

In `src/CromoBound.Engine/Rules/Game.Mutations.cs`, replace `MoveCard` with:

```csharp
    /// <summary>Moves a card. The public event hides the object id of any side that is a deck, a hand or a facedown slot, and
    /// the card's identity when both sides are hidden. If a side is a hand or a facedown slot, the owner (or the facedown card's
    /// controller) also gets a private copy showing hand and facedown ids, never deck ids (deck order is secret to everyone).
    /// Gear attached to a moving unit goes with it, or is detached when the unit leaves the board.</summary>
    internal ObjectId? MoveCard(ObjectId id, Place to, DeckPosition position = DeckPosition.Top)
    {
        var instance = State[id];
        var from = instance.Place;
        var cardId = instance.CardId;
        var carries = from.IsLocation && IsUnit(instance);
        var viewer = from.Kind == PlaceKind.Facedown ? instance.Controller : instance.Owner;
        var newId = State.Move(id, to, position);
        var landed = newId is { } moved ? State[moved].Place : to;
        var bothHidden = IsHidden(from) && IsHidden(landed);
        Emit(new CardMoved(bothHidden ? null : cardId, IsHidden(from) ? null : id, IsHidden(landed) ? null : newId, from, landed));
        if (IsPrivate(from) || IsPrivate(landed))
            Emit(new CardMoved(cardId, IsSecret(from) ? null : id, IsSecret(landed) ? null : newId, from, landed) { VisibleTo = viewer });
        MarkDirty();
        if (carries) MoveAttachments(id, landed, stayed: newId == id && landed.IsLocation);
        return newId;
    }
```

In `src/CromoBound.Engine/Rules/Game.Manual.cs`, add these cases before `default:` in `ApplyManual`:

```csharp
            case ManualAttach attach:
                return AttachByHand(attach);
            case ManualDetach detach:
                return DetachByHand(detach);
```

In `src/CromoBound.Engine/Views/PlayerView.cs`, replace `CardView` with:

```csharp
public sealed record CardView(
    ObjectId Id, string CardId, string? PrintingId, PlayerId Owner, PlayerId Controller,
    bool Exhausted, bool Stunned, bool Buffed, bool Empowered, int Damage, int? Might, CombatRole? Role,
    MappingStatus Effects, IReadOnlyList<int> ManualLines, ObjectId? AttachedTo);
```

In `src/CromoBound.Engine/Views/ViewBuilder.cs`, in `Card`, pass `effects.Status, effects.ManualLines, card.AttachedTo);` as the last arguments. (Keep `PlayerView.cs`'s existing summary comment above `CardView`, if any.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including Plan A's cleanup tests for unattached gear at battlefields).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): attach gear to units"
```

---

### Task 7: Equip and Weaponmaster; every mapped card runs

**Files:**
- Create: `src/CromoBound.Engine/Effects/Steps/WeaponmasterStep.cs`
- Modify: `src/CromoBound.Engine/Effects/Steps/PermanentStepHandlers.cs`
- Modify: `src/CromoBound.Engine/Effects/Steps/StepRegistry.cs`
- Modify: `src/CromoBound.Engine/Effects/CardEffects.cs`
- Modify: `src/CromoBound.Engine/Effects/EffectsSupport.cs`
- Modify: `tests/CromoBound.Engine.Tests/EffectsDataTests.cs`
- Test: `tests/CromoBound.Engine.Tests/EquipTests.cs`

**Interfaces:**
- Consumes:
  - Task 1's `CardEffectInfo.KeywordEntries`.
  - Task 2's `KeywordAbilities(file, lines)`, `KeywordLine` and `OwnTrigger(kind, steps, line)`.
  - Task 6's `Game.Attach`.
  - Plan E's activated abilities, `ChooseCardsDecision`, `Game.CheckPick`, `Game.AskPay`.
- Produces:
  - The Equip keyword stands for an activated ability. Its cost comes from the keyword; it resolves as `AttachStep { Target = Self, To = a friendly unit }`, and the unit is chosen on resolution.
  - The Weaponmaster keyword stands for a trigger on being played, with `WeaponmasterStep` (internal, `CromoBound.Engine.Effects.Steps`).
  - `AttachHandler` and `WeaponmasterHandler` are registered.
  - `EffectsDataTests.Every_mapped_card_runs_as_mapped` replaces the per-plan lists.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Engine.Tests/EquipTests.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class EquipTests
{
    private const string EquipGear = """
        { "cardId": "gear-1", "status": "Full", "keywords": [ { "keyword": "Equip", "cost": { "energy": 1, "power": ["Fury"] } } ] }
        """;

    private const string WeaponmasterUnit = """{ "cardId": "unit-3", "status": "Full", "keywords": [ { "keyword": "Weaponmaster" } ] }""";

    [Fact]
    public void Equip_attaches_the_gear_to_your_only_unit()
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", EquipGear)));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.State.Battlefields[0].Controller = P1;
        var unit = game.Put("unit-2", Place.Battlefield(0));
        game.Runes(P1, "fury-rune", 2);
        var engine = game.Start();

        Assert.Contains(new ActivateOption(gear, 0), engine.Decision<PriorityDecision>().Activations);
        engine.Accept(P1, new ActivateAbility(gear, 0));
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(1, pay.Cost.Energy);
        Assert.Equal(new[] { PowerSymbol.Fury }, pay.Cost.Power);
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Equal(unit, game.State[gear].AttachedTo);
        Assert.Equal(Place.Battlefield(0), game.State[gear].Place);
    }

    [Fact]
    public void With_several_units_the_player_chooses_which_to_equip()
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", EquipGear)));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.Put("unit-2", Place.Base(P1));
        var second = game.Put("unit-2", Place.Base(P1));
        game.Runes(P1, "fury-rune", 2);
        var engine = game.Start();
        engine.Accept(P1, new ActivateAbility(gear, 0));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal((2, 1, 1), (choose.Options.Count, choose.Min, choose.Max));
        engine.Accept(P1, new ChooseCards { Cards = [second] });

        Assert.Equal(second, game.State[gear].AttachedTo);
    }

    [Fact]
    public void Weaponmaster_may_attach_your_equipment_for_its_equip_cost_minus_a_power_symbol()
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", EquipGear), ("unit-3", WeaponmasterUnit)));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.Put("unit-3", Place.Hand(P1));
        game.Runes(P1, "chaos-rune", 5);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        var choose = engine.Decision<ChooseCardsDecision>();
        Assert.Equal((0, 1), (choose.Min, choose.Max));
        Assert.Equal(new[] { gear }, choose.Options);
        engine.Accept(P1, new ChooseCards { Cards = [gear] });
        var pay = engine.Decision<PayCostDecision>();
        Assert.Equal(1, pay.Cost.Energy);
        Assert.Empty(pay.Cost.Power);
        engine.PayWithSuggestion(P1);

        Assert.Equal(game.First(Place.Base(P1), "unit-3"), game.State[gear].AttachedTo);
    }

    [Fact]
    public void Declining_weaponmaster_attaches_nothing()
    {
        var game = new TestGame(db: EngineTestDb.Create(("gear-1", EquipGear), ("unit-3", WeaponmasterUnit)));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.Put("unit-3", Place.Hand(P1));
        game.Runes(P1, "chaos-rune", 5);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "unit-3")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        engine.Accept(P1, new ChooseCards { Cards = [] });

        Assert.Null(game.State[gear].AttachedTo);
        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void Veteran_poro_offers_no_equipment_whose_equip_cost_is_unknown()
    {
        var game = new TestGame(db: EngineTestDb.WithRealCards("veteran-poro"));
        var gear = game.Put("gear-1", Place.Base(P1));
        game.Put("veteran-poro", Place.Hand(P1));
        game.Runes(P1, "fury-rune", 2);
        var engine = game.Start();
        engine.Accept(P1, new PlayCard(game.First(Place.Hand(P1), "veteran-poro")));
        engine.PayWithSuggestion(P1);
        engine.Accept(P1, new Pass());
        engine.Accept(P2, new Pass());

        Assert.Null(game.State[gear].AttachedTo);
        Assert.Empty(game.State.Chain);
        Assert.IsType<PriorityDecision>(engine.Pending);
    }
}
```

Replace `tests/CromoBound.Engine.Tests/EffectsDataTests.cs` with:

```csharp
using CromoBound.Data;
using CromoBound.Engine.Effects;

namespace CromoBound.Engine.Tests;

/// <summary>The load-time check of spec §7, as a data test: every effects file in data/effects runs with the status it declares.</summary>
public class EffectsDataTests
{
    private static readonly CardDatabase Data = CardRepository.Load(RepoPaths.Data);
    private static readonly CardEffects Real = new(Data);

    [Fact]
    public void Every_mapped_card_runs_as_mapped()
    {
        Assert.NotEmpty(Data.Effects);
        Assert.All(Data.Effects, effects =>
        {
            var info = Real.For(effects.Key);
            Assert.True(info.Unsupported.Count == 0, $"{effects.Key}: {string.Join(", ", info.Unsupported)}");
            Assert.Equal(effects.Value.File.Status, info.Status);
        });
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~EquipTests|FullyQualifiedName~EffectsDataTests"`
Expected: FAIL (Equip offers no activation; Weaponmaster asks nothing; the Weaponmaster cards are Unmapped).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Engine/Effects/Steps/WeaponmasterStep.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>The step the Weaponmaster keyword stands for. It exists only in <see cref="CardEffects"/>, never in an effects file.</summary>
internal sealed record WeaponmasterStep : Step;

/// <summary>Weaponmaster (CR 821, spec §8.1): when the unit is played, its controller may attach an Equipment they control to it
/// by paying that Equipment's Equip cost minus [A] (one power symbol less). Only Equipment whose Equip cost the engine knows (a
/// mapped Equip keyword) is offered; other Equipment is attached by hand with ManualAttach.</summary>
internal sealed class WeaponmasterHandler : StepHandler<WeaponmasterStep>
{
    /// <summary>The chosen Equipment and the cost still to pay; Paid once the payment went through, Declined when it was cancelled.</summary>
    private sealed record Pick(ObjectId Gear, TotalCost Cost, bool Paid = false, bool Declined = false);

    protected override StepOutcome Run(Game game, ResolveEffectTask task, WeaponmasterStep step)
    {
        var context = task.Context;
        if (context.Source is not { } unit || !game.State.Exists(unit) || !game.State[unit].Place.IsLocation) return StepOutcome.DidNothing;
        switch (task.Progress)
        {
            case Pick { Declined: true }:
                return StepOutcome.DidNothing;
            case Pick { Paid: true } paid:
                return Attach(game, task, paid.Gear, unit);
            case Pick pick:
                game.AskPay(context.Controller, pick.Cost, game.CardOf(pick.Gear).Domains,
                    onPaid: () => task.Progress = pick with { Paid = true },
                    onCancel: () => task.Progress = pick with { Declined = true },
                    onAdjust: adjusted => task.Progress = pick with { Cost = adjusted });
                return StepOutcome.Asked;
        }
        var options = Equipment(game, context.Controller, unit);
        if (options.Count == 0) return StepOutcome.DidNothing;
        if (task.Answer is IReadOnlyList<ObjectId> chosen)
        {
            if (chosen.Count == 0) return StepOutcome.DidNothing;
            var cost = EquipCost(game, chosen[0]);
            if (cost.Energy == 0 && cost.Power.Count == 0) return Attach(game, task, chosen[0], unit);
            task.Progress = new Pick(chosen[0], cost);
            return Run(game, task, step);
        }
        game.Ask(new ChooseCardsDecision(context.Controller, context.SourceCardId, options, 0, 1), (_, action) =>
        {
            if (action is not ChooseCards choose) return Game.Reject(RejectionCode.UnexpectedAction, "Choose an Equipment to attach, or none.");
            if (Game.CheckPick(choose.Cards, options, 0, 1, "Equipment") is { } rejection) return rejection;
            List<ObjectId> picked = [.. choose.Cards];
            task.Answer = picked;
            return null;
        });
        return StepOutcome.Asked;
    }

    /// <summary>The player's gear in play, not already on this unit, whose Equip cost the engine knows.</summary>
    private static List<ObjectId> Equipment(Game game, PlayerId player, ObjectId unit) =>
    [
        .. game.State.Objects
            .Where(o => o.Place.IsLocation && o.Controller == player && o.AttachedTo != unit
                && game.CardOf(o).Type == CardType.Gear && EquipEntry(game, o) is not null)
            .Select(o => o.Id),
    ];

    private static KeywordEntry? EquipEntry(Game game, CardInstance gear) =>
        game.Effects.For(gear.CardId).KeywordEntries.FirstOrDefault(k => k.Keyword == MechanicalKeyword.Equip && k.Cost is not null);

    /// <summary>The Equip cost minus [A]: its first power symbol is dropped; energy is unchanged.</summary>
    private static TotalCost EquipCost(Game game, ObjectId gear)
    {
        var cost = EquipEntry(game, game.State[gear])!.Cost!;
        return new TotalCost(cost.Energy ?? 0, [.. cost.Power.Skip(1)]);
    }

    private static StepOutcome Attach(Game game, ResolveEffectTask task, ObjectId gear, ObjectId unit)
    {
        if (!game.State.Exists(gear) || !game.State[gear].Place.IsLocation) return StepOutcome.DidNothing;
        game.Attach(gear, unit);
        task.Result = new EffectVar([gear], [], null, true);
        return StepOutcome.Done;
    }
}
```

In `src/CromoBound.Engine/Effects/Steps/PermanentStepHandlers.cs`, add these usings:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
```

and append:

```csharp
/// <summary>Equip (CR 818, spec §8.1): attaches the gear (the step's target, Self) to a unit chosen on resolution among the "to"
/// selector's candidates. One candidate is a forced choice.</summary>
internal sealed class AttachHandler : StepHandler<AttachStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, AttachStep step)
    {
        var context = task.Context;
        List<ObjectId> gear = [.. ObjectResolver.Resolve(game, context, step.Target).Where(id => game.State[id].Place.IsLocation)];
        var hosts = ObjectResolver.Candidates(game, context, step.To);
        if (gear.Count == 0 || hosts.Count == 0) return StepOutcome.DidNothing;
        if (task.Answer is IReadOnlyList<ObjectId> [var chosen]) return Attached(game, task, gear[0], chosen);
        if (hosts.Count == 1)
        {
            game.Emit(new ChoiceMade(context.Controller, "Cards", hosts));
            return Attached(game, task, gear[0], hosts[0]);
        }
        game.Ask(new ChooseCardsDecision(context.Controller, context.SourceCardId, hosts, 1, 1), (_, action) =>
        {
            if (action is not ChooseCards choose) return Game.Reject(RejectionCode.UnexpectedAction, "Choose the unit to attach to.");
            if (Game.CheckPick(choose.Cards, hosts, 1, 1, "units") is { } rejection) return rejection;
            List<ObjectId> picked = [.. choose.Cards];
            task.Answer = picked;
            return null;
        });
        return StepOutcome.Asked;
    }

    private static StepOutcome Attached(Game game, ResolveEffectTask task, ObjectId gear, ObjectId unit)
    {
        game.Attach(gear, unit);
        task.Result = new EffectVar([gear], [], null, true);
        return StepOutcome.Done;
    }
}
```

In `src/CromoBound.Engine/Effects/Steps/StepRegistry.cs`, add to the dictionary:

```csharp
        [typeof(AttachStep)] = new AttachHandler(),
        [typeof(WeaponmasterStep)] = new WeaponmasterHandler(),
```

In `src/CromoBound.Engine/Effects/CardEffects.cs`, add `using CromoBound.Engine.Effects.Steps;` and these cases to the `switch` in `KeywordAbilities`:

```csharp
                case MechanicalKeyword.Equip:
                    yield return new ActivatedAbility
                    {
                        Line = line,
                        Cost = entry.Cost,
                        Steps =
                        [
                            new AttachStep
                            {
                                Target = ObjectRef.Self,
                                To = new ObjectRef { Select = SelectKind.Unit, Count = 1, Filter = new Filter { Relation = Relation.Friendly } },
                            },
                        ],
                    };
                    break;
                case MechanicalKeyword.Weaponmaster:
                    yield return OwnTrigger(TriggerEvent.Played, [new WeaponmasterStep()], line);
                    break;
```

In `src/CromoBound.Engine/Effects/EffectsSupport.cs`:
- Add `MechanicalKeyword.Equip, MechanicalKeyword.Weaponmaster,` to `Keywords`.
- Add this arm to the `switch (step)` in `CheckStep`:

```csharp
            case AttachStep:
                problems.Add($"{at}: attach");
                break;
```

  (Only the Equip keyword attaches; an Attach step in a file is not run yet.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests). If `Every_mapped_card_runs_as_mapped` names a card, the message lists what it still needs: report it rather than editing `data/effects`.

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): add equip and weaponmaster"
```

---

### Task 8: Turn-point pauses only for text the players resolve

**Files:**
- Modify: `src/CromoBound.Engine/Rules/Game.TurnPoints.cs`
- Modify: `tests/CromoBound.Engine.Tests/TurnPointTests.cs`

**Interfaces:**
- Consumes: Plan D's `CardEffectInfo.Status` and `ManualLines`.
- Produces: `TurnPointCards` reads all of an Unmapped card's text, only a Partial card's manual lines, and nothing of a Full card (spec §9).

- [ ] **Step 1: Write the failing tests**

Add to `TurnPointTests`:

```csharp
    [Fact]
    public void A_full_card_never_pauses()
    {
        var game = new TestGame(db: EngineTestDb.Create(("dawn-relic", """{ "cardId": "dawn-relic", "status": "Full" }""")));
        game.Put("dawn-relic", Place.Base(P1));

        var engine = game.Start();

        Assert.IsType<PriorityDecision>(engine.Pending);
    }

    [Fact]
    public void A_partial_card_pauses_only_for_its_manual_lines()
    {
        var manual = new TestGame(db: EngineTestDb.Create(("dawn-relic", """{ "cardId": "dawn-relic", "status": "Partial" }""")));
        manual.Put("dawn-relic", Place.Base(P1));
        var covered = new TestGame(db: EngineTestDb.Create(("dawn-relic", """
            { "cardId": "dawn-relic", "status": "Partial",
              "abilities": [ { "kind": "Activated", "line": 1, "steps": [ { "action": "Draw", "amount": 1 } ] } ] }
            """)));
        covered.Put("dawn-relic", Place.Base(P1));

        Assert.IsType<TurnPointDecision>(manual.Start().Pending);
        Assert.IsType<PriorityDecision>(covered.Start().Pending);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test CromoBound.slnx --filter "FullyQualifiedName~TurnPointTests"`
Expected: FAIL (the Full relic and the covered Partial relic still pause).

- [ ] **Step 3: Write the implementation**

In `src/CromoBound.Engine/Rules/Game.TurnPoints.cs`:
- Add `using CromoBound.Models.Effects;`.
- In `TurnPointCards`, replace

```csharp
            var match = pattern.Match(RichText.StripReminders(CardOf(instance).Text.Rich));
```

  with

```csharp
            if (TurnPointText(instance) is not { } text) continue;
            var match = pattern.Match(RichText.StripReminders(text));
```

- Change `TurnPointCards`' summary to "Cards in play handled by <paramref name="player"/> whose text the players still resolve (<see cref="TurnPointText"/>) acts at this point (reminder text ignored). Text saying "your" counts only on its controller's turn."
- Add:

```csharp
    /// <summary>The text that may need a turn-point pause (spec §9): all of an Unmapped card's text, only the manual lines of a
    /// Partial card, and none of a Full card's (the engine runs it).</summary>
    private string? TurnPointText(CardInstance instance)
    {
        var effects = Effects.For(instance.CardId);
        var text = CardOf(instance).Text.Rich;
        if (effects.Status == MappingStatus.Unmapped) return text;
        if (effects.Status == MappingStatus.Full || effects.ManualLines.Count == 0) return null;
        var lines = RichText.Lines(text);
        return string.Join("<br />", effects.ManualLines.Select(n => lines[n - 1]));
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including Plan C's turn-point tests for unmapped relics).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Engine tests/CromoBound.Engine.Tests
git commit -m "feat(engine): pause turn points only for text players resolve"
```

---

### Task 9: A scripted Bo3 with mapped cards

**Files:**
- Modify: `tests/CromoBound.Engine.Tests/MatchEffectsTests.cs`

**Interfaces:**
- Consumes: everything above, through `Match` only; the test `Bot`.
- Produces: nothing new.

- [ ] **Step 1: Write the test**

Add to `MatchEffectsTests`:

```csharp
    /// <summary>Mapped Fury and Chaos cards from data/: Accelerate, Ambush, Assault, Deflect, Legion, Vision and Weaponmaster.</summary>
    private static readonly string[] MappedFuryChaos =
    [
        "legion-rearguard", "inferna", "pouty-poro", "sharkling", "sentinel-adept",
        "mystic-poro", "shipyard-skulker", "noxus-hopeful", "blazing-scorcher",
    ];

    /// <summary>A legal Jinx deck: the nine mapped cards and filler-1 to filler-4, three copies each.</summary>
    private static Deck MappedDeck(params string[] battlefields) => TestDecks.Jinx(battlefields) with
    {
        Main =
        [
            .. MappedFuryChaos.Select(id => new DeckEntry { Printing = $"p-{id}", Count = 3 }),
            .. Enumerable.Range(1, 4).Select(i => new DeckEntry { Printing = $"p-filler-{i}", Count = 3 }),
        ],
    };

    [Fact]
    public void Scripted_players_finish_a_bo3_with_mapped_cards_and_the_saved_match_replays_identically()
    {
        var setup = new MatchSetup(MatchFormat.Bo3, MappedDeck("bf-a", "bf-b", "bf-c"), MappedDeck("bf-d", "bf-e", "bf-f"), 11);
        var match = Match.Create(setup, EngineTestDb.WithRealCards(MappedFuryChaos)).Match!;
        var bots = new[] { new Bot(), new Bot() };
        for (var i = 0; i < 20000 && match.Stage != MatchStage.Over; i++)
        {
            var player = match.Pending!.Players[0];
            match.Accept(player, bots[player.Index].Choose(match));
        }

        Assert.Equal(MatchStage.Over, match.Stage);
        Assert.Contains(2, match.Result.GameWins);
        Assert.Contains(match.Events, e => e is TriggerAdded);
        var loaded = Match.Load(match.ToRecord(), EngineTestDb.WithRealCards(MappedFuryChaos));
        var first = new PlayerId(0);
        Assert.Equal(CromoJson.Serialize(match.ViewFor(first)), CromoJson.Serialize(loaded.ViewFor(first)));
    }
```

- [ ] **Step 2: Run the test**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests). This test pins behavior Tasks 1-8 and Plans D-E already provide. If it fails, report which assertion failed and the last pending decision (for example a decision the `Bot` doesn't answer, or a game that never ends); fix the engine defect it reveals in the task that owns it, never by weakening the test.

- [ ] **Step 3: Commit**

```bash
git add tests/CromoBound.Engine.Tests
git commit -m "test(engine): play a scripted bo3 with mapped cards"
```

---

## Done criteria

- [ ] Every mapping the engine would ignore is reported: trigger forms that don't fit the card type, `player` on steps that don't use it, keyword parameters a keyword doesn't take (Task 1).
- [ ] Keyword abilities have text on the chain, effects play cards only from piles, and a spell played by an effect waits on the chain (Task 2).
- [ ] Assault and Shield add Might while attacking and defending, stacking, with a missing value as 1 (Task 3).
- [ ] Legion reductions (Noxus Hopeful) count only this turn's finalized plays, and Deflect taxes each opposing target choice (Task 4).
- [ ] Ambush gives Reaction timing to battlefields where you have units, and Rengar, Trophy Hunter can be played to a battlefield with enemy units (Task 5).
- [ ] Gear attaches to units by hand, follows its unit, and is detached and recalled when the unit leaves the board (Task 6).
- [ ] Equip and Weaponmaster attach gear, and every file in `data/effects` runs as mapped (Task 7).
- [ ] Full cards never cause a text-matched turn-point pause, and Partial cards pause only for their manual lines (Task 8).
- [ ] A scripted Bo3 with mapped cards finishes and replays identically (Task 9).
- [ ] `dotnet build CromoBound.slnx --no-incremental` reports 0 warnings and 0 errors, and `dotnet test CromoBound.slnx` passes.
