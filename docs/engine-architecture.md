# CromoBound — Game Engine Design (Phase 2a: Rules Core)

- **Date:** 2026-10-08
- **Status:** Draft for review
- **Scope:** Phase 2a of the CromoBound Riftbound simulator: a rules-enforcing game engine for 1v1 games and Bo1/Bo3 matches, with deck validation, manual actions, an action log with undo, and per-player views. Card effects are resolved by hand in this phase. The effects interpreter is Phase 2b.
- **Builds on:** `docs/architecture.md` (Phase 1: card data, effects DSL, `CardDatabase`).

---

## 1. Context and goals

CromoBound is a Riftbound simulator for two friends. It will be a web app with a .NET backend that hosts the game engine. Automation is **hybrid**:
- The engine automates the rules core: turns, chain, showdowns, combat, scoring and keywords.
- It also runs card effects that have a structured definition (Phase 2b).
- Cards without one show their text, and players resolve them by hand.

Phase 2 is split in two:
- **2a: rules core** (this document).
  - The full game structure is rules-enforced.
  - Every card effect is resolved by hand through manual actions, as if every card were unmapped.
- **2b: effects interpreter** (later).
  - Runs effects files.
  - Done when the 12 Phase 1 sample cards run automatically.

Later phases: **Phase 3 server** (accounts, friends, saved decks, lobbies, SignalR) and **Phase 4 web UI** (deck builder, card list, play screen). Phase 2a is designed so they can reuse its pieces, e.g. `DeckValidator` in the deck builder.

### Success criteria
- `DeckValidator` reports every construction problem with a code, a severity and the cards involved.
- A Bo1 and a Bo3 match can be created from two legal decks and played to completion through `Submit`, covering:
  - the pre-game steps (battlefields, d20 roll-off, play order, sideboarding, mulligan);
  - turns, the chain, showdowns, combat, scoring, the Final Point and Burn Out.
- Card effects can be carried out with manual actions, and abilities can be put on the chain by hand.
- The same setup and action log always rebuild the same state. Undo by agreement works.
- A player's view never contains the identity of a card they're not allowed to see.
- `dotnet test CromoBound.slnx` passes.

### Out of scope
- The effects interpreter (2b): steps, targeting, triggered, activated and passive abilities, modifiers, numbered keywords.
- Server, persistence of saved decks, accounts, friends, UI.
- Multiplayer modes (2v2, free-for-all). The design must not block them: players are a list, not "me and you".
- Tournament time limits and loop-shortcut procedures.
- Deck codes and deck statistics. Both are easy additions on top of `Deck` later.
- Match history. A result summary per match (players, decks, winner, score per game, date) belongs to Phase 3 storage and must not depend on replaying the move log (see §6.7).

---

## 2. Decisions summary

| # | Topic | Decision |
|---|---|---|
| 1 | Strictness | Rules-enforcing, with manual actions as logged escape hatches |
| 2 | Game modes | 1v1 only; players modelled as a list so multiplayer can be added later |
| 3 | History | Action log; state rebuilt by replay; undo by agreement |
| 4 | Scope | 2a rules core now, 2b effects interpreter later |
| 5 | Hidden information | Strict per-player views; secret and opponent-private identities never leave the engine |
| 6 | Deck legality | Validated at match creation and after sideboarding; Main Deck exactly 40 including the champion |
| 7 | Formats | Bo1 and Bo3; sideboard up to 10 cards |
| 8 | Who chooses play order | d20 roll-off (reroll ties) in the first game; the previous game's loser afterwards |
| 9 | Architecture | Explicit state machine (task queue, chain loop, cleanup) plus action log |

---

## 3. Solution layout

```
src/
  CromoBound.Models/        (Phase 1) Deck gains an optional `sideboard`.
  CromoBound.Data/          (Phase 1) + DeckValidator, + CardDatabase.Fingerprint.
  CromoBound.Engine/        NEW. Depends on Models and Data. No NuGet packages.
    State/                  Game state: players, zones, card instances, battlefields, chain, turn.
    Actions/                Player actions (what can be submitted).
    Decisions/              Pending decisions (what the engine asks).
    Events/                 Game events (what happened), with visibility.
    Rules/                  Game loop, tasks, turn structure, cleanup, chain, showdowns, combat, scoring, payment.
    Match/                  Bo1/Bo3, pre-game steps, sideboarding, results.
    Views/                  PlayerView projection.
    Random/                 Seeded random generator.
tests/
  CromoBound.Models.Tests/  (Phase 1) + DeckValidator tests.
  CromoBound.Engine.Tests/  NEW. xUnit, same package versions as Phase 1.
```

**Dependency rules**
- `CromoBound.Engine` → `CromoBound.Data` → `CromoBound.Models`.
- The engine never references the importer.
- Only the .NET base library is used, as in Phase 1.
- JSON (saved matches, logged actions) uses `CromoJson` from Phase 1.

---

## 4. Public API

```csharp
// Creating a match. Returns the deck reports when either deck is illegal.
MatchCreateResult Match.Create(MatchSetup setup, CardDatabase db);
record MatchSetup(MatchFormat Format, Deck Player1Deck, Deck Player2Deck, ulong Seed);
enum MatchFormat { Bo1, Bo3 }
record MatchCreateResult(Match? Match, IReadOnlyList<DeckReport> Reports);

// Driving it.
SubmitResult match.Submit(PlayerId player, PlayerAction action);
record SubmitResult(bool Accepted, Rejection? Rejection, IReadOnlyList<GameEvent> Events);
PendingDecision? match.Pending { get; }          // null only when the match is over
PlayerView match.ViewFor(PlayerId player);
MatchResult? match.Result { get; }               // game wins, winner

// Saving and loading.
MatchRecord match.ToRecord();                    // versions + setup + action log
Match Match.Load(MatchRecord record, CardDatabase db);   // checks versions, then replays; fails naming the bad log entry
record MatchRecord(string EngineVersion, string DataFingerprint, MatchSetup Setup, IReadOnlyList<LoggedAction> Log);
```

- `PlayerId` is a small value type wrapping the player's index (0 or 1).
- Players are always handled as a list internally.

---

## 5. Game state

**Card instances**
- Every physical card in a game is a `CardInstance` with:
  - an `ObjectId` (sequential int);
  - `CardId` and `PrintingId`; owner and controller;
  - statuses: exhausted, stunned, buffed, empowered, facedown, attacking, defending;
  - damage;
  - attachments;
  - temporary Might modifiers, each with a duration (this turn, this combat, permanent).
- **New object on zone change (CR 124).** Moving into or out of a non-board zone gives the card a new `ObjectId` and clears damage, statuses and modifiers. Anything that referenced the old id (e.g. a pending choice) stops matching.
- **Tokens** are instances with no printing. They stop existing when they leave the board for any zone other than the chain.
- **Runes** are instances in the Base.

**Players**
- Points, XP, and a rune pool: energy, power by domain, universal power.
- Zones:
  - Main Deck and Rune Deck (ordered, secret);
  - Hand (private);
  - Trash, Banishment, Champion Zone, Legend Zone (public);
  - Base (on the board).
- The player's current deck (after sideboarding) is kept in the match state, not the game state.

**Board locations**
- A board object stores its `Location`: `Base(player)` or `Battlefield(index)`.
- A **battlefield** holds:
  - its card (the instance);
  - its controller (or none);
  - contested status, with who applied it;
  - a facedown slot (capacity 1).

**Turn state**
- Turn player, phase and step, priority holder, focus holder.
- Battlefields scored this turn per player.
- Whether the second player has had their first turn (for the extra rune).
- The task queue.
- Staged showdowns and combats; the active one, with attacker and defender.

**Chain**
- An ordered list of items. Each item is a card or an ability, with a controller and a status: `Pending` or `Finalized`.
- Each item also records the choices made: location, Accelerate paid, cost adjustment, and for manually added abilities, the source card and text line.

**Derived, never stored**
- Current Might = printed + buff + active modifiers. In 2b, effects plug into the same calculation (the rules' three layers, CR 473–480).
- Open/Closed comes from whether the chain is empty.
- Neutral/Showdown comes from whether a showdown or combat is active.

**Random generator state** is part of the game state.

---

## 6. Driving the engine

### 6.1 Submit

`Submit(player, action)`:
1. **Validate** the action against the current pending decision and the rules. If it's illegal, return a `Rejection`; nothing changes.
2. **Accept:** append a `LoggedAction` (player + action) to the log, apply it, then run the game loop (§7.1) until a decision is needed or the match ends.
3. **Return the events** produced.

While a match is running, there's always exactly one `PendingDecision`.
- It names the player(s) who must answer, its kind, and its options.
- For simultaneous decisions it lists every player who still has to answer.

### 6.2 Actions

All actions are records under a polymorphic base `PlayerAction`, serialized with a `type` discriminator (the same JSON conventions as Phase 1).

| Group | Actions |
|---|---|
| Pre-game | `PickBattlefield`, `ChoosePlayOrder` (first or last), `SubmitSideboard` (swaps, optional champion switch, or no changes), `Mulligan` (cards to set aside, 0–2) |
| Priority | `PlayCard`, `UseRune` (exhaust or recycle), `StandardMove` (units, destination), `Hide` (card, battlefield), `Pass`, `EndTurn` |
| Playing a card | `ChooseLocation`, `ChooseAccelerate`, `AdjustCost`, `PayCost` (runes to exhaust, runes to recycle, amount from pool), `CancelPlay` |
| Rules choices | `ChooseShowdown` (which staged showdown or combat starts), `AssignDamage` |
| Hand resolution | `ResolveDone` |
| Manual (§8) | `ManualMoveCard`, `ManualDamage`, `ManualHeal`, `ManualSetStatus`, `ManualModifyMight`, `ManualAdjustPoints`, `ManualAdjustXp`, `ManualAdjustPool`, `ManualCreateToken`, `ManualGainControl`, `ManualShuffle`, `ManualLookAtTop`, `ManualReveal`, `ManualCounter`, `AddAbilityToChain` |
| Match | `RequestUndo`, `AnswerUndo` (accept or decline), `Concede` |

### 6.3 Decisions

| Kind | Who | Options include |
|---|---|---|
| `PickBattlefield` | both, simultaneous (Bo3) | that player's unused battlefields |
| `ChoosePlayOrder` | roll-off winner or previous loser | first, last |
| `Sideboard` | both, simultaneous | current main, sideboard, champion candidates |
| `Mulligan` | each player in turn order | their hand |
| `Priority` | priority or focus holder | the list of legal actions with their parameters, e.g. playable cards, legal move destinations, hide targets |
| `PlayChoices` | the player playing | legal locations, Accelerate available, cancel |
| `PayCost` | the player playing | total cost, available runes and pool, a **suggested payment** |
| `ChooseShowdown` | turn player | staged showdowns and combats |
| `AssignDamage` | attacker, then defender | damage total, eligible units in the required order, a **suggested assignment** |
| `ResolveManually` | the item's controller | the card or ability text; `ResolveDone` |
| `TurnPoint` | controller of a card with a start/end-of-turn effect | the cards; `ContinueTurn` (§7.3) |
| `ConfirmUndo` | the opponent of the requester | accept, decline |

**Suggestions are shortcuts only.** Players can always pay with exactly the runes they choose, and assign damage exactly as they choose. Rune management matters.

### 6.4 Events

- Immutable records with a sequence number. Examples: `CardMoved`, `RuneUsed`, `DamageDealt`, `UnitKilled`, `PointsChanged`, `BattlefieldScored`, `ControlChanged`, `ShowdownStarted`, `CombatEnded`, `D20Rolled`, `TurnStarted`, `ManualActionTaken`, `GameEnded`.
- Each event carries its **visibility**: public, or private to one player.
  - When one event has a private and a public form, both are emitted. "Drew a card": the drawer sees the card; the opponent sees only that a card was drawn.
- Manual actions emit `ManualActionTaken` (highlighted in the log) as well as their normal events.

### 6.5 Log, replay, saving

- A match is fully defined by its `MatchSetup` (decks, format, seed) and its action log.
- **Saving** serializes a `MatchRecord`. **Loading** first checks versions (§6.7), then replays it; if a logged action is rejected during replay, loading fails and names the log entry.
- **Determinism:**
  - no clock, no GUIDs, sequential ids;
  - iteration order is always defined;
  - all randomness (shuffles, battlefield selection, d20 rolls, random recycle order) comes from the game's generator.
- **Random generator:** xoshiro256**, seeded via SplitMix64 from the 64-bit seed.
  - Shuffles use Fisher–Yates.
  - Bounded integers use rejection sampling, so there's no bias.
  - It's implemented in the engine, so results never depend on the .NET version.

### 6.6 Undo

- `RequestUndo` asks to roll back to just before the requester's most recent logged action. The opponent answers with `AnswerUndo`.
- If accepted, the engine rebuilds by replaying the log without that action and everything after it. The generator replays too, so shuffles and rolls come out identical.
- Undo is available only during play (after both mulligans). Pre-game choices (battlefields, play order, sideboarding, mulligan) are final. Undo never crosses the start of the current game, and it's rejected if the requester has no action since play began.
- While an undo request is pending, the only legal actions are `AnswerUndo` (opponent) and `Concede`.


### 6.7 Engine updates and saved matches

Replay only reproduces a match if the engine and the card data behave exactly as they did when the moves were made. Changing a rule, fixing a bug, or editing `cards.json` or an effects file can make an old move come out differently or be rejected.

**Policy: matches don't survive engine updates.**
- Running matches are finished (or conceded) before a new version is deployed.
- In Phase 3, the server can offer a maintenance switch that blocks new matches until the running ones end.

**Safeguard: records carry their versions.** A forgotten match must fail loudly, not silently replay into a different state.
- `EngineVersion`: the engine assembly's informational version. The .NET SDK includes the git commit in it, so every deployed commit counts as a new version.
- `DataFingerprint`: a SHA-256 over the card data files loaded into the `CardDatabase` (cards, tokens, printings, sets and effects files, in a fixed order), computed by `CardRepository` when loading and exposed as `CardDatabase.Fingerprint`.
- `Match.Load` compares both with the current values and throws `MatchVersionMismatchException` on any difference, without replaying.

**History doesn't depend on replay.** Anything meant to outlive an update (match history: players, decks, winner, score per game, date) is stored as a result summary by the Phase 3 server, separately from the move log. Re-watching an old match works only on the version it was played on.

---

## 7. Rules machinery

### 7.1 Game loop and tasks

After each accepted action, the loop repeats until a decision is needed or the match ends:
1. **Tasks waiting?** Run the next one. A task can finish, add tasks, or stop and raise a decision.
2. **Pending chain items?** Finalize the oldest (§7.4).
3. **Chain not empty?** The priority holder decides. When every player has passed in a row, the newest finalized item resolves (§7.5).
4. **Showdown or combat showdown running?** The focus holder decides (§7.7).
5. **Main phase, Neutral Open?** The turn player decides.
6. **Otherwise:** advance to the next phase or step.

This is the rules' "handle tasks, then finalize, execute, pass, resolve" procedure (CR 332–340).

**Tasks**
- Each piece of mandatory rules procedure is a small task object that tracks its own progress, so it can pause for a decision and resume.
- Turn steps and combat steps are tasks.

**Cleanup scheduling**
- Any board change marks the game as needing a cleanup: zone change, status change, chain change, completed move, phase change.
- The cleanup runs at the next task boundary, never while an item is resolving or a hand resolution is in progress.

### 7.2 Timing state, priority and focus

- **States:** Open/Closed and Neutral/Showdown are derived (§5). Priority and focus holders are stored.
- **Legal actions:** one function lists a player's legal actions for the current state. The `Priority` decision shows it, and `Submit` validates against it.

**Permissions**

| State | Who may act | What |
|---|---|---|
| Neutral Open, Main phase | turn player | play cards, use runes, standard move, hide, end turn |
| Showdown Open | focus holder | cards with Action or Reaction, runes, pass |
| Closed (any) | priority holder | cards with Reaction, runes, pass |

- In 2a, Action and Reaction are the card's own keywords (§7.10).
- **Responding to your own items.** After an item is finalized, priority goes to the controller of the **newest** item (CR 337.4). That player may add any number of Reactions on top of their own items before passing. Priority moves to the opponent only on a pass.
- **Focus in a showdown** passes only once the chain started by the focus holder has fully closed. A chain started by a triggered ability (including one added by hand with kind `Triggered`) or by a rune Add doesn't pass focus (CR 346.1).
- "End turn" is legal only in the Main phase, Neutral Open, with no showdown or combat staged. "Pass" is legal only while there's a chain or a showdown.

### 7.3 Turn structure

| Phase / step | Engine task |
|---|---|
| Awaken | Ready everything the turn player controls |
| Beginning step | Kill Temporary permanents the turn player controls; then the **start of Beginning** turn point |
| Scoring step | Hold: the turn player scores every battlefield they control (§7.9) |
| Channel | Channel 2 runes, ready, or as many as remain; +1 on the second player's first turn |
| Draw | Draw 1 (Burn Out if the deck is empty) |
| Main | Empty all rune pools; then the turn player acts |
| Ending step | The **end of turn** turn point |
| Expiration step | Ending special cleanup (heal all units, "this turn" modifiers and Stunned expire, rune pools empty); repeat the step while chain activity happens |
| Next turn | The other player becomes turn player |

**Turn points.** About 26 cards act at the start of the Beginning phase, at the start of the Main phase or at the end of the turn. In 2a they're applied by hand, so the engine pauses at those three moments, but only when a card in play has text for that moment.
- **When:**
  - after the Beginning step (before scoring);
  - after rune pools empty at the start of the Main phase (before priority);
  - after the Ending step (before units heal and pools empty).
- **Which cards:** board objects (not facedown cards) whose text, with reminder text removed, matches "at the start/beginning of … Beginning phase", "… Main phase" or "at the end of … turn". Text saying "your" counts only on its controller's turn.
- **Who decides:** each card's controller (a battlefield's controller, else the turn player) gets a `TurnPoint` decision, turn player first. They apply the effects with manual actions and answer `ContinueTurn`.
- **In 2b:** cards with a Full effects file trigger automatically and no longer cause a pause.

### 7.4 Playing a card

A card can be played from the hand, from the Champion Zone, or from face down (Hidden, from the turn after it was hidden).

1. **To the chain** as a `Pending` item.
2. **Choices** (`PlayChoices`):
   - a unit's location: your Base or a battlefield you control; a card played from face down must go to its battlefield;
   - whether to pay Accelerate.

   Targets and modes in card text are handled at resolution (§8).
3. **Total cost:**
   - the printed cost (or 0 base cost when played from Hidden);
   - plus Accelerate (1 energy + 1 power of the card's domain, i.e. `[C]`);
   - plus any `AdjustCost` the player applies. Adjustments stand in for text-based changes the engine doesn't know in 2a (discounts, Deflect taxes); they're logged like manual actions.
   - Energy and power never go below 0.
4. **Pay** (`PayCost`): one atomic action naming runes to exhaust (1 energy each), runes to recycle (1 power of that rune's domain each), and what to take from the pool.
   - Rune Add abilities resolve immediately (no priority pass).
   - The total must cover the cost; extra floats in the pool. An insufficient payment is rejected, so a play is never half-paid.
   - **Power matching:**
     1. domain symbols take matching power;
     2. `Self` takes power of any of the card's domains;
     3. `[A]` takes any power;
     4. universal power fills the rest.
5. **Check legality.** Can't fail at this point, because every step was validated when submitted.
6. **Finalize:**
   - Units enter the board **exhausted** (or ready with Accelerate) at the chosen location.
   - Gear enters **ready** in Base.
   - Spells stay on the chain, finalized.

**Cancel:** `CancelPlay` is legal until payment is submitted. The card returns where it came from; nothing is spent.

**Runes as abilities:** `UseRune` is a Reaction-speed Add that resolves immediately. It's legal whenever the player holds priority (to float resources) and during payment.

### 7.5 The chain

- **Finalize:** pending items finalize oldest first. Units, gear and rune Adds resolve on finalize.
- **Priority:** after finalizing, the controller of the newest item gets priority (§7.2). When every player has passed in a row with nothing added, the **newest finalized** item resolves (LIFO).
- **Resolving in 2a:**
  - A spell, or an ability added by hand, raises `ResolveManually` for its controller (§8). On `ResolveDone`, a spell goes to its owner's trash; an ability is removed.
- **After resolution:** an empty chain returns to Open. If items are still pending, they're finalized; otherwise the controller of the newest remaining item gets priority.

### 7.6 Cleanup

Runs CR 323 in order, repeating until a full pass changes nothing:
1. **Win check:** a player with 8 or more points *and* more than the opponent wins.
2. **Combat roles:** units at the combat battlefield take their controller's role; units elsewhere lose it.
3. **Lethal damage:** units with damage ≥ Might (and damage > 0) are killed and go to their owners' trash. An event records each death so death triggers can be added by hand.
   - Special cleanups insert their extra steps here:
     - **Ending:** heal all units; "this turn" effects and Stunned expire; rune pools empty.
     - **Combat:** heal all units; recall attackers if defenders remain.
4. **Battlefield control:** a player loses control of a battlefield where they have no units, if the state is Open and no showdown or combat is there.
5. **Recalls:**
   - non-unit gear and runes at battlefields return to Base;
   - permanents in a Base not their controller's return to their controller's Base;
   - a facedown card at a battlefield its controller doesn't control goes to its owner's trash.
6. **Stage showdowns** where Contested was applied.
7. **Stage combats** where both players have units.
8. **Clear Contested** where the applier has no units and nothing is ongoing; re-apply it for units left on a battlefield their controller doesn't control.
9. **Start a staged showdown** at a battlefield where no combat is staged (Neutral Open). With several, the turn player chooses (`ChooseShowdown`); with one, it starts automatically.
10. **Start a staged combat** (Neutral Open), same choice rule. In Showdown Open, a combat staged at the current non-combat showdown's battlefield converts it into a combat showdown.

### 7.7 Movement and showdowns

**Standard move**
- Discretionary: Main phase, Neutral Open, no showdown.
- One or more of your ready units, possibly from different places, to one destination. Each moving unit is exhausted as the cost.
- Destinations: Base → battlefield; battlefield → your Base; battlefield → battlefield with **Ganking**.
- Instant, no chain. A cleanup follows.

**Contested:** a unit becoming present at a battlefield its controller doesn't control applies Contested (if not already applied), recording who applied it.

**Non-combat showdown** (units arrived at an empty battlefield)
- The applier gets focus and priority.
- Players alternate, using Action/Reaction cards or passing. It ends when both pass in a row with an empty chain.
- Afterwards:
  - if only one player has units there and doesn't control it, they take control. That's a **Conquer** if they haven't scored it this turn.
  - If no units remain, the battlefield is left uncontested and uncontrolled.

**Recall** (to the unit's own Base) is not a move: no move rules apply, and damage and statuses stay.

### 7.8 Combat

1. **Combat showdown**
   - Roles: attacker = the player who applied Contested; defender = the other. Units present get the designations; later arrivals get theirs at the next cleanup.
   - The attacker gets focus (unless a showdown was already running, in which case focus stays where it was). Then it runs as a showdown.
2. **Damage**, only if both sides still have units; otherwise go straight to Resolution.
   - Each side's total = sum of current Might (negative counts as 0; stunned units count 0).
   - **The attacker assigns first, then the defender** (`AssignDamage`). The engine validates:
     - Tank units must receive lethal damage before non-Tank units;
     - Backline units only after all others;
     - each unit gets lethal damage before the next gets any;
     - no more than lethal per unit unless no other eligible units remain;
     - the full total is assigned.
   - All assigned damage is dealt simultaneously.
3. **Resolution**
   - **Combat cleanup:** kill lethal-damaged units; heal **all** units; recall attackers if defenders remain.
   - **Result:**
     - a player wins if they're the only one with units there, and loses if they're the only one without;
     - otherwise the result is "no result". If both sides still have units, showdown and combat are staged again.
   - **Control:** if nothing is staged there, the player with remaining units establishes control (a **Conquer** if not scored this turn). No units → uncontrolled. Contested is cleared. Facedown cards whose controller differs from the battlefield's controller go to their owner's trash.
   - **End of combat:** designations removed; "this combat" modifiers expire.

### 7.9 Scoring, winning, Burn Out

- **Hold:** in the turn player's Scoring step, they score every battlefield they control.
- **Conquer:** gaining control of a battlefield the player hasn't scored this turn.
- At most **one score per battlefield per player per turn**.
- Each score: gain 1 point, and record a `BattlefieldScored` event, so Conquer and Hold abilities can be added by hand.
- **Final Point:** a Conquer that would give the point while the player is at 7 or more gives it only if they've scored **every battlefield this turn, including the one being conquered**. Otherwise they draw a card instead. Points from Hold, manual actions and Burn Out are unrestricted.
- **Winning:** checked in cleanup step 1: 8 or more points and more than the opponent. A tie at 8 or more continues the game.
- **Concede:** legal at any time; the opponent wins the game.
- **Burn Out:** when a draw or burn needs more cards than the Main Deck holds:
  1. do as much as possible;
  2. shuffle the trash into the Main Deck;
  3. the opponent gains 1 point;
  4. finish the action.

  With deck and trash both empty it repeats. From the second consecutive burnout, a point that takes a player to 8 or more with the lead wins **immediately** (CR 431.3). The Rune Deck never burns out.
- **Game end in a match:**
  - the result (win or draw) goes to the match;
  - Bo1 ends;
  - Bo3 ends at 2 wins, otherwise the next game is set up (§9.3).

### 7.10 Keywords handled in 2a

A card's **own** keywords are read from its text: bracketed keywords that **start a text line**. Keywords it grants to others ("give a unit [Tank]"), has only conditionally ("while buffed, I have [Ganking]"), or that time one of its abilities ("[Reaction][>] …", "cost: [Reaction] — …") don't count; conditional cases are handled with manual actions. `cards.json`'s `keywords` lists every bracketed term and is for search only.

| Keyword | Engine behavior |
|---|---|
| Action / Reaction | Timing permissions (§7.2) |
| Hidden | `Hide`: pay `[A]`, card goes face down at a battlefield you control with an empty facedown slot (Neutral Open only). From the next turn it has Reaction and can be played ignoring its base cost (additional costs still apply), entering at that battlefield. Removed to the owner's trash if control of the battlefield is lost. |
| Ganking | Standard move battlefield → battlefield |
| Tank / Backline | Damage assignment order |
| Accelerate | Optional additional cost 1 energy + 1 `[C]`; the unit enters ready |
| Temporary | Killed at the start of its controller's Beginning step, before scoring |
| Unique | Deck validation only |

All other keywords (Assault, Shield, Deflect, Vision, Legion, Level, Empower, …) are handled by hand until 2b.

---

## 8. Manual actions and hand-resolved cards

**Manual actions**
- Either player may submit them at any time during a game, including during a `TurnPoint` or `ResolveManually` decision (not during pre-game decisions, nor while an undo request is pending), on any object.
- They're logged and highlighted; the opponent may request an undo.

| Action | Parameters | Typical use |
|---|---|---|
| `ManualMoveCard` | object, destination zone or location, top/bottom for decks | draw, discard, kill, banish, return to hand, recall, recycle |
| `ManualDamage` / `ManualHeal` | unit, amount | damage and healing effects |
| `ManualSetStatus` | object, status, on/off | ready, exhaust, stun, buff, empower |
| `ManualModifyMight` | unit, ±N, duration (this turn / this combat / permanent) | pump effects |
| `ManualAdjustPoints` / `ManualAdjustXp` | player, ±N | scoring and XP effects (points never below 0) |
| `ManualAdjustPool` | player, ± energy, ± power by domain | "Add" effects, refunds |
| `ManualCreateToken` | token id from `tokens.json`, location, controller | Recruit, Sprite, Gold, … |
| `ManualGainControl` | object, player | steal effects |
| `ManualShuffle` | player's Main or Rune Deck | search effects |
| `ManualLookAtTop` | deck, N | predict, look (cards visible only to that player) |
| `ManualReveal` | card in a hidden zone | reveal effects (visible to both) |
| `ManualCounter` | chain item | counter effects |
| `AddAbilityToChain` | source object, text line, kind (`Triggered` or `Activated`), optional cost | triggered and activated abilities |

**Rules still apply afterwards:**
- After a manual action the loop runs again: cleanup kills lethal-damaged units, control and scoring update, and so on.
- If the pending decision became invalid (e.g. a unit awaiting damage assignment was moved), it's rebuilt.

**Hand resolution**
- When a spell or a hand-added ability resolves, its controller gets `ResolveManually`, showing the card or ability text.
- Both players may use manual actions meanwhile, e.g. the opponent discarding a card. The controller then submits `ResolveDone`.
- Cleanup waits until `ResolveDone`.

**`AddAbilityToChain`**
- Puts a `Pending` ability item on the chain, controlled by the source's controller. The kind matters for focus: a chain started by a `Triggered` item doesn't pass focus in a showdown.
- If a cost was entered, it's paid with the normal `PayCost` step. Then normal priority follows, so the opponent can respond.

**What changes in 2b**
- A card with a **Full** effects file is run by the interpreter: no `ResolveManually`, and triggers are added automatically.
- **Partial** files run their mapped parts; the rest is done by hand.
- Unmapped cards and all manual actions stay as in 2a.

---

## 9. Decks, validation and the match layer

### 9.1 Deck format

`Deck` (architecture §6.3) gains an optional `sideboard`, a list of `{ printing, count }` like `main`.
- The engine receives **copies** of the decks at match creation; editing a saved deck mid-match changes nothing.
- Saved decks (id, owner, name, last edited) belong to the future server, not to `Deck`.
- `deck.schema.json` is regenerated.

### 9.2 DeckValidator (`CromoBound.Data`)

`DeckValidator.Validate(Deck deck, CardDatabase db) : DeckReport`
- `DeckReport { bool IsLegal; IReadOnlyList<DeckIssue> Issues }`
- `DeckIssue { DeckIssueCode Code; DeckIssueSeverity Severity; IReadOnlyList<string> CardIds; int? Actual; int? Expected; string Message }`
- `DeckIssueSeverity`:
  - `Incomplete`: not finished yet, e.g. 38/40. Normal while building.
  - `Illegal`: wrong regardless, e.g. too many copies.
- `IsLegal` is true only with no issues at all.

The deck builder calls it live while editing. The engine calls it at match creation and after each sideboarding.

| Code | Severity | Check |
|---|---|---|
| `UnknownPrinting` | Illegal | Every printing id exists |
| `WrongCardType` | Illegal | Legend is a Legend; runes are Runes; battlefields are Battlefields; main and sideboard hold only Main Deck cards (no tokens, runes, battlefields, legends) |
| `MainDeckSize` | Incomplete if fewer, Illegal if more | `main` + champion = exactly 40 |
| `RuneDeckSize` | Incomplete / Illegal | Exactly 12 runes |
| `BattlefieldCount` | Incomplete / Illegal | Exactly 3 battlefields |
| `DuplicateBattlefield` | Illegal | Battlefields have different names |
| `SideboardSize` | Illegal | At most 10 |
| `TooManyCopies` | Illegal | Per card id, across main + champion + sideboard: at most 3 |
| `UniqueCopies` | Illegal | Unique cards: at most 1 |
| `TooManySignatures` | Illegal | At most 3 Signature cards in total |
| `SignatureTag` | Illegal | Signature cards carry the legend's champion tag |
| `ChampionMismatch` | Illegal | The champion is a Champion unit whose tag matches the legend |
| `OutsideIdentity` | Illegal | Every domain of every card (runes included) is inside the legend's domains |

Copy limits count per **card id**, never per printing (architecture §6.4).

### 9.3 Match layer

**Match state**
- Format, game number, game wins per player.
- Each player's battlefields still available.
- The previous game's result and play order.
- Each player's **registered** deck (unchanged) and **current** deck (after sideboarding).

**Pre-game steps** (each game):

| Step | Bo1 | Bo3 game 1 | Bo3 game 2+ |
|---|---|---|---|
| 1 | Legends revealed | Legends revealed | Legends revealed |
| 2 | One battlefield per player chosen **at random** | Each player **picks** one unused battlefield (simultaneous, hidden until both have picked) | Same as game 1. After a **draw**, the previous battlefields are kept and there's no pick. |
| 3 | **d20 roll-off** (reroll ties); the winner chooses first or last | **d20 roll-off**; the winner chooses | The **previous game's loser** chooses; after a draw the previous order is kept |
| 4 | **Sideboarding** | — | **Sideboarding** (none after a draw) |
| 5 | Champion placed; decks shuffled; draw 4; mulligan in turn order | same | same |

- **Sideboarding:**
  - simultaneous;
  - 1-for-1 swaps between main (including the champion slot) and sideboard;
  - may switch the champion to a legal one;
  - runes, legend and battlefields can't change.
  - The resulting deck is validated; an illegal result is rejected and the player resubmits.
- **Mulligan:** set aside up to 2 cards, draw that many, then recycle the set-aside cards to the bottom of the Main Deck in random order.
- **Battlefields in Bo3:** after a game someone **won**, the battlefields used in it are removed for the rest of the match.
- **Between games:** record the result. Bo1 ends after one game; Bo3 ends at 2 wins; otherwise the next game is set up from the current decks.

---

## 10. Views

`PlayerView ViewFor(PlayerId)` is rebuilt on demand. It contains only what that player may see.

| Information | Viewer's own | Opponent's |
|---|---|---|
| Board (units, gear, runes, battlefields with controller and contested status), chain with choices | full | full |
| Trash, Banishment, Legend Zone, Champion Zone | full | full |
| Points, XP, rune pool | full | full |
| Hand | cards | **count** |
| Facedown cards | cards | **card back** in the battlefield's slot |
| Sideboard | cards | **count** |
| Main Deck, Rune Deck | **count** | **count** |

- Also public: turn player, phase and step, state, priority and focus holders, battlefields scored this turn, staged or active showdown or combat with roles, match score, and the pending decision's kind and decider.
- **Only the decider** sees the decision's options.
- **No identity leaks:** no `CardId`, `PrintingId` or `ObjectId` of any card in a secret zone, or in the opponent's private zones, ever appears in that player's view or events.
- **Events** are filtered by visibility. Cards seen through `ManualLookAtTop` appear only in the looker's events. Revealed cards appear in both players' events.

---

## 11. Errors

- **Rejected actions** return `Rejection { RejectionCode Code; string Message }` and change nothing.
  - Codes include `NotYourDecision`, `UnexpectedAction`, `WrongTiming`, `UnknownObject`, `IllegalLocation`, `InsufficientPayment`, `InvalidAssignment`, `InvalidSideboard`, `UndoNotAllowed`, `MatchOver`.
- **Illegal decks at creation** return `MatchCreateResult` with the reports and no match.
- **Engine invariant violations** (bugs) throw `InvalidOperationException`. They're never used for normal flow.
- **Loading a match record** with an action that's rejected during replay throws an exception naming the log entry index.
- **Loading a match record from another engine version or card data** throws `MatchVersionMismatchException`, naming the recorded and current values. It never attempts the replay.

---

## 12. Testing

`tests/CromoBound.Engine.Tests` (new):
- **State builder helper:** starts a test from an exact situation (cards in given zones and locations, phase, points, pools) without going through setup.
- **Rules tests by area:**
  - **Chain:** loop and priority, including stacking your own Reactions.
  - **Payment:** domain symbols, `Self`, `[A]`, universal power, rejecting insufficient payments.
  - **Cleanup:** each step, plus repetition until stable.
  - **Movement and showdowns:** standard move and Ganking; showdown start and end, including the no-units case.
  - **Combat:** roles, damage validation (lethal order, Tank, Backline), simultaneous damage, resolution and conquer.
  - **Scoring:** Hold, Conquer, one score per battlefield per turn, the Final Point, Burn Out (including the immediate win).
  - **Keywords:** Hidden, Temporary, Accelerate.
  - **Turn structure:** extra rune for the second player; rune pools emptying.
- **Manual actions:** each action; cleanup runs after them; hand resolution; `AddAbilityToChain`.
- **Match tests:**
  - Bo1 random battlefields; Bo3 picks and removal; battlefields kept after a draw;
  - d20 ties reroll; play-order choice by roll-off winner and by loser;
  - sideboarding, including a champion switch and a rejected illegal swap;
  - mulligan.
- **Determinism:** the same setup and log produce identical state, compared via serialized state. A `MatchRecord` round-trips; a record with a different engine version or data fingerprint is refused without replaying. Undo gives the same state as never having taken the action.
- **Views:** the opponent's view and events never contain hidden identities.
- **Full games:** two legal test decks built from real card data, played to a win through `Submit`, in Bo1 and Bo3.

`tests/CromoBound.Models.Tests` (existing):
- `DeckValidator` tests, one per issue code, plus a legal deck with no issues.
- `CardDatabase.Fingerprint`: stable across loads of the same files; changes when any data file changes.
- `Deck` sideboard round-trip.
- The schema-up-to-date test covers the regenerated `deck.schema.json`.

---

## 13. Rules interpretations

Where the rules are ambiguous or contradict each other, the engine follows these readings:

| # | Question | Reading |
|---|---|---|
| 1 | Win condition | CR 194.2: 8+ points **and** more than the opponent, checked in cleanup (not CR 485.6's "first to 8") |
| 2 | Priority after finalizing ("next" vs "newest" item) | Newest (top) item |
| 3 | Hide timing | Neutral Open only (Hide is discretionary, CR 410.1.a) |
| 4 | Non-combat showdown ending with no units | Battlefield uncontested and uncontrolled |
| 5 | Combat damage step with one side empty | Skip to Resolution |
| 6 | Final Point "every battlefield" | Includes the battlefield being conquered |
| 7 | Hidden play cost | Base cost ignored; additional costs apply (CR 811.1.b) |
| 8 | Bo3 battlefields after a draw | Kept (TR 406.1.b) |
| 9 | Public zones (CR 355.10.a.1 omits Banishment and Chain) | Banishment and Chain are public (CR 108.1.b, 108.6.e) |
| 10 | Main Deck size | Exactly 40 including the champion (TR 601.1.b) |

---

## 14. Decisions log

| Decision | Choice | Reason |
|---|---|---|
| Strictness | Rules-enforcing + manual actions | Correct games, but unmapped cards stay playable |
| Modes | 1v1, players as a list | The friends' use case; multiplayer not blocked |
| State and history | Explicit state machine + action log + replay | Maps 1:1 to the rules' task/chain/cleanup procedures; saving, reconnects and undo come for free |
| Undo | Back to before the requester's last action, opponent confirms | Casual play; misclicks happen |
| Manual actions | Immediate, logged and highlighted | No friction; disagreements go through undo |
| Hidden information | Per-player views, no identity leaks | No peeking even with dev tools |
| Randomness | Own xoshiro256** generator | Replays identical across .NET versions |
| Payments and damage | Suggested, never forced | Rune management is part of the game |
| Deck validation | In `CromoBound.Data`, structured report | Reused live by the future deck builder |
| Saved decks | Server, not the engine | Storage and users aren't game rules |
| Timing keywords in 2a | From `cards.json` keywords | Effects files take over in 2b |
| Sideboard | Up to 10, 1-for-1, champion switch allowed | Current tournament rules |
| Engine updates | Matches don't survive them: finish running matches before deploying; records from another version are refused | Two players and one server make this easy; no migration code or old engines to keep |

---

## Appendix A: Core Rules digest (1v1 duel)

**Sources:** `docs/Riftbound Core Rules RUP4.pdf` (CR, dated 2026-07-16) and `docs/Riftbound Tournament Rules RUP4.pdf` (TR). Citations are CR unless marked TR.

**Extracting text:** use `pdftotext -raw "<pdf>" out.txt`. The default and `-layout` modes misalign rule numbers with their text.

### A.1 Game objects and zones

**Board zones (107)**
- **Base**: one per player; a Location; public. Permanents and runes a player controls sit there. An attached permanent can be in the Base of whoever controls its top-most card (107.1.c.1).
- **Battlefield Zone**: holds the battlefields; each battlefield is a Location and also a game object there (107.2, 199.1).
- **Facedown Zone**: one per battlefield; **not** a location (107.3.a, e).
  - Max 1 card; that number can change, and excess goes to the trash (107.3.b).
  - A card can only be or stay there if its controller controls that battlefield; otherwise it's removed in the next cleanup (107.3.c–d).
  - Facedown cards are private to their controller (107.3.f, 128.4).
- **Legend Zone**: not a location. The Champion Legend can't be removed, moved or displaced. Later legends can be removed but only exist in the Legend Zone or Banishment (107.4).

**Non-board zones (108)**
- **Chain**: public; exists only while it holds an item; only one at a time (108.1, 330).
- **Trash**: per player, unordered, public.
- **Champion Zone**: per player, public. The champion can be played from it; it can't return there by normal means (108.3).
- **Main Deck** and **Rune Deck**: order is secret (108.4.d, 108.5.d).
- **Banishment**: per player, public; also used as temporary holding (108.6).
- **Hand**: private to the owner, unordered; the count is public; can be targeted as a zone (108.7).

**Privacy (128)**
- Secret: no one may look. Private: controller (on the board) or owner (elsewhere). Public: anyone.
- A player can't be forced to act on secret or private cards by type or quality (e.g. "play a unit from your hand"); they may ignore the instruction, which counts as impossible (128.6).

**Ownership of zones:** a card can never go to another player's non-board zone; it goes to its owner's instead (056).

**Zone change = new object (124):** moving to or from a non-board zone loses all temporary changes. A target that leaves and comes back is no longer a legal target (359.3.e.4).

**Tokens (183–186)**
- Not cards; created only on the board or the chain.
- Cease to exist in any non-board zone except the chain.
- Cost 0, no domain unless added by a layer effect.
- Owner = controller of the creating effect; controller = controller of the creating spell or ability.

**Card types**
- Permanents: Unit and Gear. Spells don't stay on the board.
- Runes stay on the board but aren't permanents.
- Battlefields and Legends aren't permanents; they start in play and are never played, killed or moved (133.4–133.5, 170–178).
- Supertypes: Champion (units only), Signature. Tags have no rules meaning.
- An object with several types has all their properties (178).
- In card text, "card" means a Main Deck card (052).

**Statuses (124.2):** Attached, Attacking, Buffed, Banished, Controlled, Defending, Empowered, Equipped, Exhausted, Facedown, Readied, Replaced, Revealed, Stunned.

**Control**
- Owner = who brought or created the card. Controller = who plays, hides or creates it; set when it enters the board (127, 191).
- An ability's controller = its source's controller (owner, if the source is off-board); it doesn't change if the source later changes controller (191.4).

### A.2 Deck construction and setup (1v1 duel)

**Deck construction (103)**
- A Champion Legend sets the Domain Identity. Multi-domain cards are allowed only if all their domains are inside it.
- Main Deck: CR says at least 40; **TR says exactly 40** (TR 601.1.b). **Our decision: exactly 40.**
  - At most 3 copies per name; the champion counts.
  - At most 3 Signature cards, with the legend's champion tag.
  - Unique: 1 copy (825.3.a).
- Chosen Champion: a champion unit whose tag matches the legend.
- Rune Deck: exactly 12 runes inside the identity.
- Battlefields (duel): 3, with different names.

**Setup (110–118)**
1. Legend and champion placed.
2. Battlefields set: duel = 1 random per player, so 2 in play (485.5); match mode = chosen (486.5).
3. Shuffle both decks.
4. Determine turn order (TR 407: a designated player chooses first or last; in later games the previous loser chooses).
5. Draw 4.
6. Mulligan in turn order: set aside up to 2, draw that many, then recycle the set-aside cards to the bottom (random order) (117). Replacements are drawn before recycling.

**First turn:** the second player channels 1 extra rune on their first turn (485.7). The first player does draw on turn 1.

**Winning**
- Victory Score 8. You win if, **during a cleanup**, you have at least 8 **and more points than every other player**. On a tie, play continues (194.2, 323.1).
- Points never go below 0.
- Other ways to end: an effect says so, "lose the game", concession (650–651), repeated Burn Out (431.3.c.1).

### A.3 Turn structure (314–317)

- Phases are fixed; actions within them can happen in any order. Actions happen one at a time; simultaneous things are ordered by turn order, Turn Player first (303).
- A phase ends when there are no chain items and the Turn Player takes no discretionary action (305).
- A cleanup follows every phase transition (319.2).

1. **Awaken:** the Turn Player readies everything they control (315.1).
2. **Beginning:**
   - Beginning step: "start of Beginning Phase" effects; Temporary units are killed here, before scoring (816.1.b).
   - Scoring step: the Turn Player **Holds** every battlefield they control.
3. **Channel:** 2 runes from the Rune Deck, or as many as remain; they enter ready (315.3, 430). Duel: +1 for the second player's first turn.
4. **Draw:** 1 card. Empty Main Deck → Burn Out first, then draw (315.4).
5. **Main:**
   - Tasks: every player's rune pool empties, then "start of Main Phase" effects happen (316.2–316.4).
   - Then Neutral Open, and only the Turn Player may play spells or activate abilities. Combats and showdowns arise from actions here.
   - The phase ends when the Turn Player declares end of turn (316.9).
6. **Ending:**
   - Ending step: end-of-turn effects.
   - Expiration step: Ending special cleanup with three inserted steps: 3c heal all units; 3d "this turn" effects expire (Stunned is removed); 3e rune pools empty.
   - Repeat the Expiration step while chain activity happens (317.2.f).
   - The next player becomes Turn Player.

**Trigger points to expose:** start of Beginning, Hold, start of Main, start of combat/showdown, end of combat, end of turn, delayed triggers.

### A.4 States and timing

**States**
- Showdown state while a showdown or combat is in progress, otherwise Neutral. Closed while a chain exists, otherwise Open (308–310).
- Default: cards can be played and abilities activated only by the priority holder, on their own turn, in Neutral Open (310.1.a).
- Unit and gear activated abilities: controller's Main Phase, Open, not during a showdown.

**Timing keywords (permission only)**
- **Action**: also playable in a Showdown state, on any player's turn.
- **Reaction**: everything Action allows, plus Closed states, on any turn.
- Neither changes what the card does; a unit still enters only at your Base or a battlefield you control.
- Add abilities with Reaction can be used any time costs must be paid, even mid-resolution; they finalize and resolve immediately (429.3).

**Priority (312)**
- At most one holder; it's the right to take discretionary actions.
- Received:
  - in Neutral Open during your Main Phase;
  - in a Showdown when you gain Focus;
  - in a Closed state when you control the newest chain item (once all pending items are finalized);
  - when you're next in turn order and the holder passes.
- **Limited actions** (choices you're told to make) need no priority.

**Focus (313):** at most one holder; allows acting in Showdown Open (still needs Action/Reaction). Gaining Focus gives Priority; passing Priority keeps Focus. No Focus in Neutral.

**Discretionary vs Limited (410)**
- Discretionary, on your turn in Neutral Open: Play, Standard Move, Hide (and activating abilities).
- Limited: only when instructed or at the prescribed time.
- Players only act on their own turn unless told otherwise (409).

**Tasks and HOT FEPR (332–336)**
- A task is a mandatory process that blocks everything else: cleanups, start-of-turn steps, combat steps, end-of-turn steps.
- Procedure: handle outstanding tasks, then Finalize, Execute, Pass, Resolve.
- When nothing is outstanding: in Main the Turn Player gets priority; in other phases play advances; in a showdown the Focus holder gets priority.

**Cleanup (318–324)**

A cleanup is triggered by any of:
- Open↔Closed change or phase change;
- a pending item added or finalized; a chain item removed;
- objects entering or leaving the board;
- any status change;
- a completed Move.

While a cleanup runs, nothing finalizes or resolves and priority/focus don't pass. No cleanup happens mid-resolution (it's queued instead). Cleanups repeat until one changes nothing.

Steps (323):
1. **Win check.**
2. **Combat designations:** units at the combat battlefield take their controller's role; units elsewhere lose any.
3. **Board state:**
   - 3a. Lethal-damaged units with death triggers trigger now.
   - 3b. Lethal-damaged units are killed and go to their owners' trash.
   - Special cleanups insert their steps here.
4. **Battlefield control:** players lose control of battlefields they don't occupy (Open state, no showdown or combat there).
5. **Recall:**
   - non-unit gear and runes at battlefields;
   - permanents in a Base not their controller's.
   - Also remove hidden cards at battlefields their controller doesn't control, to the **owner's** trash.
6. **Stage a showdown** where Contested was applied.
7. **Stage a combat** where opposing players both have units. 7a: un-stage if one side is gone before it opens.
8. **Remove Contested** where the applier has no units and nothing is ongoing. 8a: re-apply for remaining units on a battlefield their controller doesn't control.
9. **Neutral Open, showdowns staged (no combat):** the Turn Player picks one to begin.
10. **Neutral Open, combats staged:** the Turn Player picks one to begin. 10a: in Showdown Open, a combat staged at the current non-combat showdown's battlefield converts it into a combat showdown.

**Lethal damage:** non-zero and ≥ Might (142.4.b). Negative Might counts as 0 when referenced, but changes are calculated from the real value (143.2.b).

### A.5 The chain

**Structure:** items enter **Pending** and become **Finalized** after Check Legality. A play started while a chain exists joins it. The first item closes the state.

**FEPR loop (337–340)**
1. **Finalize:**
   - The oldest pending item's controller completes its play steps; pending items finalize in order added.
   - Units, gear and Add abilities **resolve immediately** on finalize. Spells that add resources wait on the chain.
   - Then the controller of the newest item gets priority.
2. **Execute:** the priority holder plays a legally timed card or ability (back to Finalize) or passes to the next player.
3. **Pass:** if all players passed in sequence without adding anything → Resolve.
4. **Resolve:**
   - The **newest finalized** item resolves completely (LIFO).
   - Empty chain → Open. In a showdown, Focus passes, unless the chain was started by a trigger or an Add ability.
   - Then Finalize again if anything is pending; otherwise the newest item's controller gets priority.

While an item resolves, nothing else finalizes or resolves; a spell finishes all its effects before anything it added (158.3).

**Playing a card (353–359)**
1. **To the chain** (Pending, Closed). Finish any resolution or outstanding tasks first.
2. **Choices (355):**
   - "As I am played" choices, including optional additional costs.
   - Unit location: your Base or a battlefield you control.
   - Modes, destinations, targets.
   - Triggers aren't chosen now. Choices are locked; you may not make choices that deterministically lead to an illegal result. Other choices are made on resolution.
3. **Total cost (356),** in order:
   - base-cost changes;
   - additional costs (Deflect is a mandatory one);
   - increases;
   - discounts (component-specific first, then whole-cost);
   - total-cost modifications.
   - Energy and power never go below 0. Effects that read a card's cost use the printed or copied cost.
4. **Pay (357):** energy and power together, Reaction Add abilities allowed; then non-standard costs in any order. A cost whose action is replaced still counts as paid.
5. **Check legality (358):** targets, costs, no illegal state, timing permission. Any failure → **undo everything** (358.5). "Prevent" effects don't make the play illegal; that instruction is just skipped on resolution.
6. **Finish finalizing (359):**
   - Permanent: leaves the chain as a game object. A unit enters **exhausted** at the chosen location; non-unit gear enters **ready** in Base.
   - Spell: stays on the chain, finalized; on resolution it executes top to bottom, then goes to its owner's trash.
   - "Played" for triggers = resolved; a countered card wasn't played. Checks like Legion look at finalization.

**Resolution edge rules (359.3)**
- A spell resolves even with illegal targets; those targets are unaffected.
- Do as much as possible.
- A missing object returns null.
- Linked instructions need the earlier one to have happened.
- Leaving the chain mid-resolution stops execution.
- "Here", "its" and similar are checked when executed.

**Activated abilities:** "Cost: Effect"; same steps; they resolve like spells without a card (377, 398–406).

**Triggered abilities (382–388)**
- Written "When …", "At …" or "the Nth time"; checked after the event.
- Go on the chain in Open or Closed states, on any turn.
- **Order:** each player orders their own triggers, Turn Player first (383.3.d). Since pending items finalize first-in-first-out and resolve last-in-first-out, the Turn Player's triggers resolve **after** the opponent's.
- "You may" at the start is decided at finalization; declining removes the trigger. A cost at the start is the trigger's base cost; declining removes it (not a counter).
- No legal choices → removed. If legal choices exist, they **must** be made (402.4).
- Death triggers go pending before the unit reaches the trash; a replaced death removes them.
- Conquer and Hold triggers fire even if the point is negated.
- Attack and Defend triggers fire the first time per combat.

### A.6 Resources

**Runes**
- Channeled, not played. Each basic rune has:
  - `[E]: [Reaction] — Add [1]` (exhaust for 1 energy);
  - `Recycle this: [Reaction] — Add [C]` (1 power of its domain; the rune goes to the bottom of the Rune Deck).
- Several recycled at once: the owner orders them.

**Energy and power**
- Energy has no domain and pays the number. Power has a domain and pays the symbols. Some power is universal.
- **Rune pool:** you must Add before you can spend; floating is allowed. The pool empties at the start of Main and at end of turn.

**Cost symbols**
- `[A]` (rainbow): payable with any domain; when added, it pays any domain.
- `[C]`: the card's own domain; on a multi-domain card, any of its domains; on a domainless card, treated as `[A]` (135.2.e.6).
- Deflect's tax can be paid with any domain.

**Paying**
- Pay = remove from your pool. Declining a card's cost undoes the play; declining another cost means skipping the linked effect.
- An impossible cost can't be paid; an already exhausted object can't pay `[E]`.

**Cost kinds (204):**
- Base, additional, and "costs within instructions" (`[do X] to [do Y]`): paid on resolution for spells; for triggers, a leading cost is paid at finalization.
- Applied costs ("must pay") are paid during the action itself, with no chain.

**XP:** a separate public player resource, unlimited (728–733).

### A.7 Units, movement and showdowns

**Locations:** the two Bases and the battlefields; no unit limit (170.6).

**Playing units:** to your Base or a battlefield you control (Ambush and similar widen this). Non-unit gear goes to Base only, except gear played from Hidden enters at its battlefield.

**Standard Move (144, 420.3)**
- Discretionary: your Main Phase, Neutral Open, no showdown or combat.
- Cost: **exhaust** the unit.
- Several units can move as one action (same destination; different origins allowed).
- Base → battlefield, battlefield → own Base. With Ganking, battlefield → battlefield.
- Moves are instant, don't use the chain and can't be responded to; a cleanup follows.
- A forced move that can't legally happen becomes a Recall.

**Recall (454–458):** to the unit's own Base; **not** a Move (no move triggers, ignores movement restrictions); keeps damage and statuses.

**Contested (190.3)**
- Applied when a unit becomes present at a battlefield its controller doesn't control, if not already contested.
- Stays while a showdown or combat is running there; removed in cleanup step 8.
- Card effects can't reference it.

**Showdowns**
- Staged by cleanup step 6; started by step 9 (Turn Player's choice).
- **Non-combat showdown:** you moved into an empty battlefield. If enemy units arrive during it, it becomes a combat showdown.
- **Combat showdown:** both sides present; it's step 1 of combat.

**Showdown process (341–348)**
- The player who applied Contested gets Focus and Priority.
- The Focus holder plays a legally timed card or ability. When that chain closes, Focus passes (not if started by a trigger or an Add ability).
- Or they pass. If all players pass once in sequence, the showdown ends.
- **When it ends:**
  - Combat showdown → continue combat.
  - Non-combat showdown → if only one player has units there and doesn't control it, they establish control. That's a **Conquer** if they haven't scored that battlefield this turn.

### A.8 Combat (459–466)

- Happens during a cleanup when no chain items exist, a combat is staged, and nothing else is ongoing.
- Staged when both players have units at a battlefield. The Turn Player picks which runs first. Always exactly 2 players.

**Step 1: Combat showdown**
- Start-of-combat/showdown effects.
- Roles: **Attacker** = whoever applied Contested; **Defender** = the other player. Units get the matching designations; units arriving later get them at the next cleanup.
- The Attacker gets Focus (or whoever had it keeps it, if a showdown was already running).
- Attack triggers go on the chain first, then the defender's Defend triggers.

**Step 2: Combat damage** (only if both sides still have units)
- Sum current Might per side; stunned units contribute 0; negative Might counts as 0.
- **Attacker assigns first, then the defender**, each across the other side's units:
  - each unit must get lethal damage before the next gets any;
  - no more than the minimum lethal per unit unless no other units remain;
  - replacement and prevention apply at assignment;
  - Tank and Backline ordering must be respected;
  - units that can't be dealt damage are exempt.
- All assigned damage is then dealt **simultaneously**. Sources: each side's units.
- Skip FEPR and cancel outstanding tasks; go to Resolution.

**Step 3: Resolution**
1. **Combat cleanup** (special cleanup):
   - 3a and 3b kill lethal-damaged units first;
   - 3c heals **all** units (not just this combat's);
   - 3d recalls attackers if defenders are still present.
2. **Result:**
   - Win: you're the only player with units left. Lose: you're the only player without.
   - No Result: attackers were recalled, both sides remain, or neither does. If No Result with both remaining, stage showdown and combat again.
3. **Control** (if nothing is staged there):
   - The player with remaining units establishes control; clear Contested.
   - No units → uncontrolled.
   - Remove hidden cards whose controller differs from the battlefield's.
   - Establishing control = **Conquer** if not scored this turn (it doesn't have to be the attacker).
4. **End of combat:** remove designations, end-of-combat effects, "this combat" effects expire.

Each step waits for its chain to finish. Survivors keep no damage. Recalled attackers stay exhausted.

### A.9 Control, conquer, hold, scoring

**Battlefield control (190)**
- Binary; established when a showdown or combat ends after Contested.
- Kept while you have units there.
- During a showdown or combat, control changes only through those procedures.
- No units in an Open state → lost at the next cleanup (unless something is ongoing).
- The controller controls the battlefield's abilities. For an uncontrolled battlefield, the Turn Player handles them, and "you" in them refers to no one.
- Losing control removes your facedown card there.

**Scoring (467–472)**
- **Conquer:** gain control of a battlefield you haven't scored this turn.
- **Hold:** keep control during **your** Beginning Phase scoring step.
- At most **one score per battlefield per player per turn**, so a held battlefield can't be conquered for points later that turn.
- Each score: gain up to 1 point, then trigger Conquer/Hold abilities (once per turn).

**Final Point (471.1)**
- A Conquer that would take you to 8 (i.e. you're at 7 or more) gives the point only if you've scored **every battlefield this turn**. Otherwise you **draw a card instead**.
- Points from other sources (Hold, effects, Burn Out) aren't restricted.
- In a duel: winning by conquest from 7 needs both battlefields scored that turn.

**Burn Out (431)**
- Happens when you must remove more cards from your Main Deck than it holds (draw, burn, put into trash; not look/reveal/Predict).
- Procedure: do as much as possible → shuffle your trash into your Main Deck → **an opponent gains 1 point** → finish the action.
- Repeated burnouts with an empty deck and trash: points from the second burnout onward can't be prevented. If they bring a player to the Victory Score with more points than the opponent, that player wins **immediately**, without waiting for a cleanup.
- The Rune Deck never burns out.

The win check runs at cleanup step 1.

### A.10 Game actions (407–458)

| Rule | Action | Meaning |
|---|---|---|
| 413 | Draw | Top of Main Deck to hand; Burn Out if short |
| 414 | Exhaust | Mark spent; already exhausted → nothing happens, and it can't pay `[E]` |
| 415 | Ready | Reverse of exhaust; everything you control in Awaken |
| 416 | Recycle | To the bottom of the owner's matching deck; several to Main Deck = random order, several runes = owner's order |
| 417 | Deal | Mark damage, positive integers only; has a source |
| 418 | Heal | Remove damage |
| 419 | Play | To the chain and finalize; discretionary from hand or Champion Zone |
| 420/445 | Move | Board location to board location; Standard Move is discretionary; Recall and attached cards following their host aren't Moves |
| 421 | Hide | Facedown at a battlefield you control (Hidden keyword) |
| 422 | Discard | Hand to trash, without executing |
| 423 | Stun | Binary; contributes no Might in combat; cleared at end of turn |
| 424 | Reveal | Show a hidden card; it stays where it is |
| 425 | Counter | Negate a chain item, to the trash; it doesn't count as played |
| 426/701 | Buff | Max 1 counter, +1 Might; "spend" removes it (own units only) |
| 427 | Banish | To Banishment |
| 428 | Kill | Permanent to trash; active (instruction or cost) or passive (lethal damage); death triggers first |
| 429 | Add | Resources to the pool; Add abilities resolve immediately |
| 430 | Channel | Top runes to the board, ready |
| 431 | Burn Out | See A.9 |
| 432 | Double | Increase a number by its current value |
| 433 | Swap | Exchange two numeric values |
| 434/435 | Attach/Detach | Link under a top-most card (its text and Might bonus pass to the host) |
| 436 | Predict | Look at the top X cards; recycle any number; put the rest back on top in any order; no Burn Out |
| 437 | Prevent | Reduce the next damage |
| 438 | Replace | A token takes the object's place |
| 439 | Create | Bring a new object into existence |
| 440 | Burn | Top of Main Deck to trash; Burn Out if short |
| 441/442 | Empower/Disempower | Binary Empowered status |
| 443 | Skip | Replacement that turns an event into nothing |
| 444 | Pay | Remove resources from your pool |
| 454 | Recall | To own Base; not a Move |

### A.11 Other things to model

- **Golden and Silver rules:** card text overrides rules (002); "can't" beats "can"; "only" is exclusive; do as much as you can (054–055).
- **Hidden (421, 811):**
  - Pay `[A]` to put a card facedown at a battlefield you control that has no facedown card.
  - From the next turn, it gains Reaction and can be played ignoring its base cost.
  - A permanent played from facedown enters at that battlefield; targets must be there where possible.
- **Replacement effects (367–375):**
  - Applied before the event, each at most once per event.
  - With several, the affected object's controller chooses the order.
  - Burn Out, Prevent and Skip are replacements.
- **Simultaneity:** only events from the same action are simultaneous. A trigger doesn't fire if its source leaves at that same moment.
- **Layers (473–480):**
  1. Trait (name, type, tags, controller, cost, domain, Might set-to, copy).
  2. Ability (keywords, appended text, attached effect text).
  3. Arithmetic (increases then decreases).
  - Repeat until stable; within a layer, order by dependency, then timestamp.
- **Inactive text:** Legion, Level and Empowered abilities are inactive until their condition holds.
- **Also:** Counters (741–749); Mighty = Might ≥ 5 (706–711); untargetability; additional turns (queued, turn order unchanged); concession at any time.
- **Infinite loops:** none in CR. TR 505: the players maintaining the loop name an iteration count; a loop nobody maintains and nobody breaks is a draw.
- **Missed triggers (TR 506):** a tabletop concept; an engine should resolve every mandatory trigger.
- **Public information to expose (TR 502.4):** Turn Player, phase and step, state, priority and focus holders, rune pools, scores, current stats, chain contents and choices.
- **No limits** on hand size, units per battlefield or runes on the board.
