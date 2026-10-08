# CromoBound: Effects Engine Design (Phase 2b)

> Phase 2b of the game engine. Phase 2a (`docs/engine-architecture.md`, plans A to C) built the rules core with every card effect resolved by hand. Phase 2b runs the effects files of `docs/architecture.md` §7, so mapped cards resolve automatically.

## 1. Context and goals

- **Goal:** cards with an effects file resolve automatically.
  - `Full` file: the card is fully automated.
  - `Partial` file: its mapped abilities run automatically; its unmapped lines are resolved by hand, as in 2a.
  - `Unmapped` (no file): exactly the 2a behavior.
- **Scope (decision A):** the engine implements what the mapped cards in `data/effects/` need, and nothing more. The vocabulary grows card by card afterwards (the DSL's own rule, `architecture.md` §7.9). Each new step, trigger or modifier is one small, self-contained addition.
- **Today's mapped cards:** 49 files.
  - 12 hand-written samples (`architecture.md` §9).
  - 37 keyword-only or vanilla scaffolds. Keywords used: Accelerate, Assault, Shield, Tank, Ambush, Vision, Deflect, Hidden, Ganking, Deathknell, Empower, Hunt, Temporary, Weaponmaster, Reaction.

### Success criteria
1. The 12 sample cards run automatically, each pinned by a test using the real card data.
2. The 37 keyword cards behave as their keywords say.
3. Unmapped cards play exactly as in 2a; every 2a test still passes.
4. Manual actions, undo, saving, loading and per-player views keep their 2a guarantees.
5. Build: 0 warnings, 0 errors. The full test suite passes.

### Out of scope
- Steps, triggers, conditions and modifiers that no mapped card uses (`If`, `ForEach`, `ChooseOne`, `CreateDelayed`, `Replacement` abilities, `Heal`, `Move`, …).
- C# scripts: the `IStepScript` interface and the `[CardScript]` registry exist, but no script ships.
- Parsing Equip costs from card text (unmapped Equipment is attached by hand, §8.2).
- Mapping more cards. That is data work after 2b.

---

## 2. Decisions summary

| # | Topic | Decision |
|---|---|---|
| 1 | Scope | Only what the mapped cards need (§1). |
| 2 | Forced choices | A decision with exactly one legal answer and no way to decline is applied automatically and announced with a public event. "Up to N", optional blocks and anything with a "no" option always ask. |
| 3 | Suspension model | Effects run as `GameTask`s in 2a's task queue, with an explicit step counter. No iterators, no compiled closures. |
| 4 | Equip | Equip and attachment rules are part of 2b, because Weaponmaster needs them. |
| 5 | Modifiers | Passive abilities are evaluated live when a value is asked for. Nothing derived is stored, so replay and undo need no changes. |
| 6 | Basic runes | Keep 2a's built-in `UseRune`. Their effects files are checked to match, not executed. |

---

## 3. Layout

New folder `src/CromoBound.Engine/Effects/`:

```
Effects/
  CardEffects.cs          loads and caches effects per card id; status, abilities, keywords, manual lines
  EffectContext.cs        controller, source, targets, stored variables, paid costs of one resolution
  ResolveEffectTask.cs    runs a step list with a step counter; pauses for decisions
  Resolvers/              selectors, object/player/zone references, values, conditions (pure)
  Steps/                  one handler per step type: IStepHandler<TStep>
  StepRegistry.cs         step type -> handler
  Triggers/               TriggerWatcher, PendingTrigger, PutTriggersOnChainTask
  Activation.cs           activated abilities: options, costs, cost actions
  Modifiers.cs            live evaluation of passive abilities and keyword stats
  Attachment.cs           Equip, attach, detach, move-with-unit
  Scripts.cs              IStepScript, [CardScript], registry (empty)
```

`Game` gains a few partial-class files for the hooks (play targets, resolution switch, activation dispatch, attachment cleanup). Rules code is not duplicated: step handlers call the existing `Game` mutations (`Draw`, `DealDamage`, `Kill`, `BurnOut`, `Channel`, `MoveCard`, `SetStatus`, …).

---

## 4. Components

### 4.1 `CardEffects`
- Source: `CardDatabase.Effects` (already loaded and fingerprinted in Phase 1).
- Per card id: `Status`, `Abilities`, `Keywords`, and `ManualLines` (the card's text lines not covered by any ability's `line`; all lines for `Unmapped`).
- **Keywords:** for `Full` and `Partial` cards, the effects file's `keywords` are the card's own keywords. For `Unmapped` cards, 2a's `CardKeywords.Own` (bracketed keywords starting a text line) stays. `Game.Has` routes through `CardEffects`.
- `overrides.cost` replaces the printed cost when present.

### 4.2 `EffectContext`
- `Controller`, `Source` (object id, plus the source card id kept even if it leaves play), `Targets` (per target selector, in JSON order), `Vars` (`store` results: objects, players, numbers), `Paid` (additional cost ids), `TriggerSubject` and `TriggerSource` for triggered abilities.
- Built when an ability is put on the chain (or when a spell is played) and stored on its `ChainItem`.

### 4.3 `ResolveEffectTask`
- Fields: the context, the step list, a step index, and per-step progress for steps that ask more than once.
- `Run`: for each step from the index, call its handler. A handler returns `Done`, `DidNothing`, or `Asked` (it raised a decision and will be called again with the answer). `Asked` returns `false` from `Run`, exactly like 2a tasks.
- After a manual action the task is re-run and re-asks with fresh options (2a's guarantee).
- `store` on a step records its result in `Vars`; `{ "did": … }` is true only when the stored step actually happened.

### 4.4 Step handlers (scope A)
`Draw`, `Deal`, `Kill`, `Burn`, `Channel` (with `exhausted`), `Recycle`, `Add`, `ChoosePlayer`, `ChooseCard`, `Play` (with `cost: IgnoreAll`), `Optional` (with `reflexive`), `Predict`.

### 4.5 Resolvers
Pure functions over `GameState` and the context:
- `ObjectRef` → objects: `Self`, `Here`, `Controller`, `Owner`, `TriggerSubject`, `TriggerSource`, `{ var }`, and selectors (`select`, `count`/`upTo`/`all`, `filter`).
- `PlayerRef` → players: `You`, `Opponent`, `EachPlayer`, `EachOpponent`, `{ var }`, `{ controllerOf }`.
- `ZoneRef` → a `Place`.
- `Value` → number (literal, `count`, `prop`, `var`, arithmetic).
- `Condition` → bool (`all`/`any`/`not`, `exists`, `compare`, `paid`, `did`, `legion`, `empowered`, `turnOf`, `phase`).
- Filter fields needed now: `relation`, `type`, `zone`/`location`, `other`, `token`. Others are added when a card needs them.

### 4.6 `TriggerWatcher`
- Every `GameEvent` the engine emits passes through it. It maps events to trigger events:

| Trigger event | Engine event |
|---|---|
| `Hold`, `Conquer` | `BattlefieldScored` with `Kind` |
| `Dies` | `UnitDied` (emitted before the unit leaves, so the dying object's abilities are still known) |
| `BecameEmpowered` | `StatusChanged { Status: Empowered, Value: true }` |
| `Played` | new `CardPlayed` event, emitted when a play finalizes |

- For each match it records a `PendingTrigger` (ability, source, controller, subject). Abilities are matched on objects on the board (or in `activeIn`), plus the dying object for `Dies`.
- `Limit` (`per`, `times`) is tracked per object and ability.

### 4.7 Modifiers
- `Modifiers.MightOf(object)`: printed Might + buff + 2a modifiers + `Assault X` while attacker + `Shield X` while defender + `ModifyMight` passives. 2a's `Game.MightOf` delegates to it.
- `Modifiers.CostOf(card, play)`: applies the rule-356 order: base cost (or `overrides.cost`), additional costs, increases, reductions (`CostReduction` passives such as Noxus Hopeful), total changes, minimum 0. 2a's `Payment.CostOf` is the base; `AdjustCost` still applies on top.
- `Modifiers.PlayLocations(card)`: 2a's locations widened by `Permission` modifiers and Ambush.
- A passive applies while its `while`/`condition` holds and its source is in its active zone.

---

## 5. How effects run

### 5.1 Spells
1. **Targets** are a new step of 2a's `PlayCardTask`, after location and Accelerate, before the cost. Each selector with `count`/`upTo` over public objects is a target, chosen in JSON order. The same object may be chosen by two selectors (Falling Star).
2. Deflect raises the cost per target here (§8.1).
3. On resolution, the chain item checks the card's status:
   - `Full`: a `ResolveEffectTask` runs the spell ability's steps. No `ResolveManually`.
   - `Partial`: the mapped abilities run, then `ResolveManually` is raised with only the manual lines.
   - `Unmapped`: 2a's `ResolveManually`.
4. Targets no longer legal at resolution are dropped. A step whose targets were all dropped does nothing.

### 5.2 Triggered abilities
- Pending triggers go on the chain at the next point where the loop would ask a player anything or start a task, and never while a hand resolution or a started task is in progress (2a's cleanup rule applies to triggers too).
- Order: the turn player's triggers first, then the opponent's. A player with several triggers at once orders them (`OrderTriggersDecision`; forced when there is one).
- An intervening `if` is checked when the trigger fires and again on resolution.
- A leading `cost` is paid when it goes on the chain; an `optional` trigger asks on resolution.
- A trigger-started chain doesn't pass focus when it closes (2a's `ChainStartedByTrigger`).

### 5.3 Activated abilities
- A new priority action `ActivateAbility(Source, AbilityIndex)`. `PriorityDecision` lists `ActivateOption(Source, AbilityIndex)` for every ability whose timing allows it now (`timing` or the default: own turn, Neutral Open), whose `useOnlyIf` holds, whose limit isn't used up, and whose cost actions are possible.
- Cost: energy and power through 2a's `AskPay`; `exhaustSelf`; cost actions (e.g. "recycle 3 from your trash") run in **cost mode**: the player chooses the cards (`ChooseCardsDecision`), and the activation is illegal unless the action can be done in full (rules 416, 422).
- Then the ability goes on the chain as an ability item, like a trigger. An ability whose steps are only `Add` resolves immediately without the chain (rune-style Add, CR 429).

### 5.4 Reflexive blocks
`Optional` with `reflexive: true` ("you may do this:") asks yes/no; on yes it creates a new chain item with the remaining steps and the same context, controlled by the ability's controller. Players get priority on it, so the opponent can respond (Kharox).

### 5.5 Playing from an effect
`Play` (Kharox) starts a 2a `PlayCardTask` for the chosen card from its zone, with the step's cost mode (`IgnoreAll`). The player playing it controls it; the card still returns to its owner's piles (rule 056).

### 5.6 Determinism
Every player choice is a logged action. Forced choices are derived from the state, so replay recomputes them. Random steps use the match's single generator. Undo and loading need no change.

---

## 6. Decisions, actions and events

### 6.1 New decisions and actions

| Decision | Raised by | Answer |
|---|---|---|
| `ChooseTargetsDecision(Player, Selector index, Options, Min, Max)` | a target selector while playing or activating | `ChooseTargets(Ids)` |
| `ChoosePlayerDecision(Player, Options)` | `ChoosePlayer` | `ChoosePlayer(Id)` |
| `ChooseCardsDecision(Player, Options, Min, Max)` | `ChooseCard`, cost actions | `ChooseCards(Ids)` |
| `OptionalDecision(Player, Text)` | optional trigger, `Optional` step, reflexive block, Vision's recycle | `ChooseOptional(Yes)` |
| `OrderTriggersDecision(Player, Triggers)` | several simultaneous triggers of one player | `OrderTriggers(Order)` |
| (priority) | | `ActivateAbility(Source, AbilityIndex)` |

All are registered for JSON (actions log, decisions in views) and checked by `ActionShape`.

### 6.2 Forced choices
- If a decision has exactly one legal answer and nothing can be declined, the engine applies it without asking.
- It emits a public `ChoiceMade(Player, Kind, Chosen)` event. `Chosen` holds object ids only when they are public.
- Not logged: replay recomputes it from the same state.

### 6.3 New events
`CardPlayed(Card, CardId, Controller)`, `ChoiceMade`, `AbilityActivated(Source, AbilityIndex, Controller)`, `TriggerAdded(ItemId, Source, Controller)`, `Attached(Gear, Unit)`, `Detached(Gear)`, and a private `Predicted(Player, CardId)` with a public count-only copy.

### 6.4 Visibility (2a's rules, extended)
- A choice over a hidden zone (your hand, a deck): options only to the decider; others see the decision kind and who decides.
- Targets are public by rule.
- Cards chosen from hidden zones appear publicly only as a count, like 2a's `CardMoved` copies.
- `CardView` gains `Effects` (`MappingStatus`) and `ManualLines` (public card data).
- The view leak test is extended to the new decisions and events.

---

## 7. Errors and validation
- A step with nothing to act on (no legal target, empty trash) completes as "did nothing".
- Steps skip objects that no longer exist or have left the expected place (2a's stale-id rule).
- **Load-time check:** a new validator (run by a data test and at engine start) reports any step, trigger event, condition, filter field, modifier or keyword in `data/effects/` that has no engine support. A mapped card never silently falls back to manual play.
- A malformed file is rejected by Phase 1's loader and validator, never mid-game.

---

## 8. Keywords

### 8.1 Table

| Keyword | 2b behavior | Rule |
|---|---|---|
| Accelerate, Action, Reaction, Hidden, Ganking, Tank, Backline, Temporary | unchanged from 2a (keywords now read via `CardEffects`) | |
| Assault X / Shield X | +X Might while attacker / defender (`Modifiers.MightOf`) | 807, 814 |
| Deflect X | an opponent choosing it as a target of a spell or ability adds X `[A]` to that play's cost, per choice | 809 |
| Vision | triggered when played: predict 1 (look at the top of your Main Deck privately; you may recycle it) | 817 |
| Deathknell | triggered on its own death; steps from the keyword entry | 808 |
| Ambush | may be played to a battlefield where you control units, with Reaction timing for that play | 822 |
| Hunt X | when it conquers or holds (its controller scores its battlefield), gain X XP | 823 |
| Empower (cost) | activated: pay the cost, only if not Empowered; it becomes Empowered (`BecameEmpowered`) | 827 |
| Legion | condition: you played another card this turn. `TurnState` records cards played per player this turn | |
| Equip (cost) | activated: pay the cost, attach to a unit you control | 818 |
| Weaponmaster | when played, you may attach an Equipment you control by paying its Equip cost minus `[A]` | 821 |

### 8.2 Attachment
- `CardInstance.AttachedTo` (exists since Plan A) is set by Equip, Weaponmaster and the new `ManualAttach(Gear, Unit)` / `ManualDetach(Gear)` actions.
- Attached gear moves with its unit (standard move, recall), and is detached and recalled to its controller's Base when the unit leaves the board. This closes the dangling-`AttachedTo` issue carried over from Plan A.
- Cleanup step 5 keeps recalling unattached gear at battlefields, as in 2a.
- Weaponmaster only offers Equipment whose Equip cost is known (mapped gear). Unmapped Equipment is attached with `ManualAttach`.

---

## 9. Living alongside 2a
- **Unmapped:** unchanged. Hand resolution, `AddAbilityToChain`, text keywords.
- **Full:** no hand resolution, triggers automatic, and no turn-point pause. 2a's text-matching turn-point pause applies only to `Unmapped` cards and to the manual lines of `Partial` cards.
- **Partial:** mapped abilities automatic; manual lines as in 2a (hand resolution of those lines; pause only if a manual line matches).
- **Manual actions** work at every moment, including mid-resolution; the task re-asks with fresh options. Disagreements are fixed by manual actions or undo.
- **Saved matches:** the data fingerprint covers the effects files, so a match saved before an effects file changed is refused on load (2a policy).

---

## 10. Testing
- **Resolvers and handlers:** unit tests with hand-made cards in `EngineTestDb`; effects files written inline in tests.
- **The 12 sample cards:** one test each with the real `data/cards.json` and `data/effects/` via `CardRepository`. Examples: Vengeance kills its target; Falling Star can hit the same unit twice; Kharox: empower, the chosen opponent burns 3, then you may play a unit from their trash for free, and the opponent can respond to that block; Garbage Grabber can't be activated with fewer than 3 cards in your trash; Noxus Hopeful costs 2 less after another card was played; Rengar can be played to a battlefield with enemy units; Shadow Temple burns 3 on hold; Soaring Scout channels a rune exhausted on death; Mystic Poro predicts when played; Progress Day draws 4; Fury Rune's file matches `UseRune`; Vanguard Sergeant plays as a vanilla unit.
- **Data:** every construct used in `data/effects/` has engine support (§7).
- **Whole system:** the scripted Bo3 with mapped cards in both decks finishes and replays identically after loading; the view leak test covers the new decisions; undo during an automated resolution equals never taking the action.

---

## 11. Plans

| Plan | Content |
|---|---|
| D: foundations and spells | `CardEffects`, resolvers, `EffectContext`, `ResolveEffectTask`, step registry, target selection in `PlayCardTask`, the new decisions and forced choices, `Draw`/`Deal`/`Kill`/`Burn`, the Full/Partial/Unmapped resolution switch, `CardView` effects fields, the load-time support check |
| E: triggers and activations | `TriggerWatcher`, pending triggers and ordering, `CardPlayed`, activated abilities with cost actions, `ChoosePlayer`, `ChooseCard`, `Play`, reflexive `Optional`, `Channel`, `Recycle`, `Add`, `Predict`, and the keywords Deathknell, Vision, Hunt, Empower |
| F: modifiers, keywords and integration | `Modifiers` (Might, cost, locations), `CostReduction`, `Permission`, Assault, Shield, Deflect, Ambush, Legion, Equip and attachment with Weaponmaster and `ManualAttach`/`ManualDetach`, turn-point suppression, the 12 sample-card tests, the scripted Bo3 with mapped cards |

---

## 12. Rules interpretations

| # | Question | Reading |
|---|---|---|
| 1 | Targets illegal on resolution | Dropped; a step whose targets are all gone does nothing |
| 2 | Same object chosen by two target selectors | Allowed (Falling Star, `architecture.md` §9.4) |
| 3 | Add-only activated abilities | Resolve immediately, without the chain (CR 429) |
| 4 | Simultaneous triggers | Turn player's first; each player orders their own |
| 5 | Leaves-board triggers (Deathknell) | Look back at the dying object (`UnitDied` before the move) |
| 6 | Weaponmaster with unmapped Equipment | Not offered; attach by hand |
| 7 | Forced choices | Applied automatically, announced publicly, not logged |

---

## 13. Decisions log

| Decision | Choice | Reason |
|---|---|---|
| Scope | Only what mapped cards need | Finishable; every primitive is exercised by a real card |
| Forced choices | Auto-pick, public event | Fewer clicks; declinable choices still ask |
| Suspension | 2a task queue with step counters | Same model as 2a; manual actions, cleanup, undo and replay unchanged |
| Equip in 2b | Yes, with attachment rules | Weaponmaster needs it; fixes Plan A's dangling `AttachedTo` |
| Modifiers | Evaluated live | No derived state to keep in sync; replay stays trivial |
| Basic runes | Keep 2a `UseRune` | Rule-defined, already correct and tested |
