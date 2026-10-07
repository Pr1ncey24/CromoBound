# CromoBound — Engine Design Notes (Phase 2a, work in progress)

- **Date:** 2026-10-07
- **Status:** Design in progress. Not yet a spec. Sections 1–2 approved, section 3 under review, sections 4–7 still to design.
- **Next documents:** once all sections are approved, this becomes `docs/engine-architecture.md` (spec), followed by `docs/engine-plan.md` (implementation plan).

---

## How to resume

1. Re-read **Decisions** (§2) and the **pre-game procedure** (§3). They're settled.
2. Section 3 (*How the engine is driven*, in §4.3) is waiting for review. Two points need an explicit answer:
   - **Undo granularity:** roll back to just before the requester's last action?
   - **Manual actions:** do they apply immediately, without the opponent confirming (the opponent can ask for an undo instead)?
3. Then design sections 4–7 (§5), one at a time, using the rules digest (Appendix A).
4. Resolve the rules ambiguities (§6), writing down the reading chosen for each.
5. Write the spec (`docs/engine-architecture.md`), self-review it, review it together, then write the plan.

---

## 1. Context

Phase 1 delivered card data, the effects DSL, `CardRepository` and `EffectsValidator` (see `docs/architecture.md`). Phase 2 builds the game engine. It's split into two sub-projects, each with its own spec → plan → tasks cycle:

- **2a: Rules core** (this document).
  - Setup and mulligan; turn phases; runes, energy and power.
  - Playing units, gear and spells; the chain and priority.
  - Movement, showdowns, combat, conquer and hold, scoring and winning.
  - Manual actions, the action log and undo.
  - Bo1/Bo3 matches and deck validation.
  - **All card effects are resolved by hand in 2a**, as if every card were unmapped. The game structure itself is fully rules-enforced.
- **2b: Effects interpreter** (later).
  - Runs effects files: steps, targeting, choices, triggered, activated and passive abilities, modifiers, mechanical keywords.
  - Done when the 12 Phase 1 sample cards run automatically.

---

## 2. Decisions

| # | Topic | Decision |
|---|---|---|
| 1 | Strictness | **Rules-enforcing with manual escape hatches.** The engine validates every action and rejects illegal ones. For unmapped cards and to fix mistakes, players have explicit manual actions (move a card, add or remove damage, adjust points, …). These are logged and visible to both players. |
| 2 | Game modes | **1v1 only, without blocking expansion.** Players are a list, not "me/you"; DSL terms like `EachOpponent`/`Ally` stay meaningful. Multiplayer can be added later by extending the engine, not rewriting it. |
| 3 | Undo and history | **Action log plus undo by agreement.** Every action is logged. Either player can request an undo; the opponent confirms. State is rebuilt by replaying the log (this also gives saved games and reconnects). Shuffles and rolls use a seeded random generator, so replay is exact. |
| 4 | Scope split | **2a rules core, then 2b effects interpreter** (see §1). |
| 5 | Hidden information | **Strict per-player views.** The engine produces a separate view for each player: own hand and own facedown cards visible; opponent's hidden cards are only backs and counts; deck order hidden from both. The server will only send each player their own view. |
| 6 | Deck legality | **Validated when a game is created.** Illegal decks are rejected with a list of problems. **Main Deck must be exactly 40** (not "at least 40"). |
| 7 | Formats | **Bo1 and Bo3.** **Sideboard of up to 10 cards** (the format raised it from 8). See §3. |
| 8 | Who goes first | **d20 roll-off** (reroll ties). The rolls are logged as events with each player's value, so a UI animation can replay them later. |
| 9 | Engine architecture | **Explicit state machine plus action log.** See §4.3. |

---

## 3. Match and pre-game procedure

| Step | Bo1 | Bo3, game 1 | Bo3, game 2+ |
|---|---|---|---|
| 1 | Legends revealed | Legends revealed | Legends revealed |
| 2 | Each player's battlefield chosen **at random** from their 3 | Each player **picks** one of their unused battlefields (simultaneous, hidden until both have picked) | Same as game 1 |
| 3 | **d20 roll-off**; winner chooses to play first or last | **d20 roll-off**; winner chooses | **Loser of the previous game** chooses; after a draw, the previous order is kept |
| 4 | **Sideboarding** | — (no sideboarding in game 1) | **Sideboarding** (none after a draw) |
| 5 | Champion placed, decks shuffled, draw 4, mulligan | same | same |

**Battlefield rules (Core Rules 486.5)**
- In Bo3, after a game that someone **won**, the battlefields used in that game are removed for the rest of the match. Each battlefield is played at most once, unless a player wins 2-0 first.
- After a **draw**, the same battlefields may be used again (CR 486.5.a). Tournament Rules 406.1.b says they *must* be. Pick one reading in the spec.

**Sideboarding rules (Tournament Rules 403, 601.1.c)**
- The sideboard holds up to 10 cards, and only valid Main Deck cards.
- Copy limits apply to Main Deck plus sideboard combined.
- Cards are swapped 1-for-1, so the Main Deck stays exactly 40 and must still be legal.
- The Chosen Champion **may be changed** during sideboarding, to a legal one from the sideboard or Main Deck. This is why the champion is placed at step 5, after sideboarding.
- Runes, Legend and battlefields can't be changed after the deck is set.
- The match ends at 2 game wins.

**Deck format change:** the Phase 1 `Deck` record (architecture §6.3) gains an optional `sideboard` list.

---

## 4. Design sections

### 4.1 Section 1: Projects and components (approved)

- **New project `src/CromoBound.Engine`**
  - Depends only on `CromoBound.Models` and `CromoBound.Data`; no NuGet packages (same rule as Phase 1).
  - A pure library; the future `CromoBound.Server` wraps it.
- **New test project `tests/CromoBound.Engine.Tests`**, using the same xUnit versions as Phase 1.
- **`DeckValidator` lives in `CromoBound.Data`**, so the future deck builder and the server can reuse it without the engine.
- **Engine folders:**

| Folder | Purpose |
|---|---|
| `State/` | Game state: players, zones, card instances (each with an object id), battlefields, chain items, scores. Mutable, owned only by the engine. |
| `Actions/` | What players submit: play card, standard move, hide, pass, end turn, answer a decision, manual actions, undo request/confirm, concede. |
| `Decisions/` | What the engine asks: who must decide, which kind (targets, mulligan, damage assignment, …), the legal options. |
| `Events/` | What happened: card moved, damage dealt, point scored, d20 rolled, … Feeds the log, animations and the UI. |
| `Rules/` | Turn structure, task queue, cleanup, chain loop, showdowns, combat, scoring, resources. |
| `Match/` | Bo1/Bo3, pre-game steps, battlefield picks, play-first, sideboarding, results. |
| `Views/` | Per-player view with hidden information removed. |
| `Random/` | Own small seeded random generator, so replays are identical on any .NET version (`System.Random`'s seeded algorithm isn't guaranteed to stay the same). |

- **Public entry point (sketch):**

```csharp
var match = Match.Create(format, deckA, deckB, cardDatabase, seed);
SubmitResult r = match.Submit(playerId, action);   // accepted → events; rejected → reason
PendingDecision? next = match.Pending;             // who must act, legal options
PlayerView view = match.ViewFor(playerId);         // hidden info stripped
IReadOnlyList<LoggedAction> log = match.Log;       // replay / undo / save
```

### 4.2 Section 2: Game state (approved)

**Card instances**
- Every physical card in a game is a `CardInstance` holding:
  - an `ObjectId`, its card id and printing, owner and controller;
  - statuses: exhausted, stunned, buffed, empowered, facedown, attacking/defending;
  - damage, attachments, and temporary modifiers with a duration (e.g. "+2 Might this turn").
- **New object on zone change** (CR 124): moving into or out of a non-board zone gives a new `ObjectId` and clears damage, statuses and modifiers. Anything pointing at the old id (e.g. a pending target) then correctly stops matching.
- **Tokens** are instances with no printing. They stop existing when they leave the board (except onto the chain).
- **Runes** are instances in the Base.

**Players:** points, XP, a rune pool (energy, power by domain, any-domain power) and their zones:
- Main Deck and Rune Deck: ordered, secret.
- Hand: private.
- Trash, Banishment, Champion Zone, Legend Zone: public.
- Base: on the board.

**Board locations**
- A board object stores its `Location`: the Base of a given player, or Battlefield 0 or 1.
- Each **battlefield** holds:
  - the battlefield card;
  - its controller (or none);
  - its contested status, with who applied it;
  - its facedown slot (capacity 1).

**Turn state**
- Turn player, phase and step, priority holder, focus holder.
- Battlefields each player has scored this turn, for "one score per battlefield per turn" and the Final Point.
- The task queue, and staged or active showdowns and combats, with attacker and defender.

**Chain:** ordered items, each a card or an ability, with a controller, a status (pending or finalized) and the choices made (targets, location, …).

**Derived values are computed, not stored**
- Current Might = printed + buff + active modifiers.
- Open/Closed comes from whether the chain is empty; Neutral/Showdown from whether a showdown is active.
- In 2b, effects plug into this same calculation (the rules' three layers, CR 473–480).

**Random generator state** is part of the game state.

### 4.3 Section 3: How the engine is driven (under review)

**Submitting an action:** `Submit(player, action)`
1. **Validate** against the current pending decision. If illegal, reject with a reason; nothing changes.
2. **Accept:** append to the log, apply, then run the rules (tasks, cleanups, chain loop) until a player must decide (the new pending decision) or the game ends.
3. **Return the events** produced.

**Pending decisions:** while the game runs there's always exactly one, naming who must answer and the legal options.

| Decision kind | Examples |
|---|---|
| Priority | "Your move": legal plays, moves and hides, plus pass or end turn |
| Choices while playing | targets, location, optional additional costs, cancel |
| Pay cost | which runes to exhaust or recycle; runes' Add abilities used mid-payment |
| Rules choices | mulligan, which staged showdown to start, combat damage assignment |
| Match | first or last, battlefield pick, sideboard swaps |
| Simultaneous | battlefield pick and sideboarding: both players answer, in any order; nothing revealed until both have answered |
| Undo | confirm or decline the opponent's undo request |

Cancelling during a play's choice or payment steps undoes the play (CR 358.5).

**Events**
- Immutable records with a sequence number.
- Each event says who may see it. "Drew a card": the drawer sees which card; the opponent sees only that a card was drawn.

**Log, replay and saving**
- A game is fully defined by its setup (decks, format, seed) plus its action log.
- Saving means serializing exactly that with the Phase 1 JSON settings (`CromoJson`); loading means replaying it.
- The engine is deterministic: no clock, no GUIDs, sequential ids.

**Undo (to confirm)**
- `RequestUndo` rolls back to just before the requester's last action.
- The opponent must confirm. Then the engine replays the log minus the undone entries; the random generator replays too, so shuffles and rolls are identical.

**Manual actions (to confirm)**
- Move a card, add or remove damage, set points, ready or exhaust, add resources, …
- They apply immediately, without confirmation, and are highlighted in the log. If the opponent disagrees, they ask for an undo.

---

## 5. Sections still to design

### Section 4: Rules machinery

This is the core of 2a. Inputs are in Appendix A §3–§9.
- **Turn structure:** Awaken → Beginning (Beginning step, Scoring step: Hold) → Channel (2 runes; +1 for the second player on their first turn) → Draw (1; Burn Out if empty) → Main → Ending (Ending step; Expiration step = Ending special cleanup, looping while chain activity happens).
- **Task queue ("Handle Outstanding Tasks, then Finalize, Execute, Pass, Resolve"):** tasks block everything else; chain items added during tasks wait.
- **The four states:** Neutral or Showdown, each Open or Closed. Plus priority, focus, and which actions are legal in each state (Action/Reaction are timing permissions only).
- **Chain loop:** finalize oldest pending first; units, gear and Add abilities resolve immediately on finalize; priority passing; the newest finalized item resolves first (LIFO).
- **Playing a card:** move to chain → choices → total cost (base changes, additional costs, increases, discounts, total modifications) → pay → check legality (undo all if illegal) → finish finalizing.
- **Cleanup:** the 10 steps of CR 323, repeated until nothing changes; plus the Ending and Combat special cleanups.
- **Showdowns:** staging, focus, passing, how they end; non-combat showdown → conquer.
- **Combat:** combat showdown → damage (sum Might, attacker assigns first, lethal-before-next, Tank/Backline ordering, dealt simultaneously) → resolution (combat cleanup with heal-all and recall, result, control, conquer, end of combat).
- **Scoring:** conquer, hold, one score per battlefield per turn, the **Final Point** (a conquer at 7 points needs every battlefield scored this turn, otherwise draw a card instead), Burn Out, win check in cleanup (8 points **and** more than the opponent).
- **Resources:** runes' two abilities (exhaust → 1 energy; recycle → 1 power of the rune's domain), rune pool emptying (start of Main, end of turn), paying `[A]` (any domain) and `Self`/`[C]` (any of the card's domains — Phase 1 decision for multi-domain cards).
- **Keywords the rules core needs even in 2a:** Action, Reaction, Hidden (hide/play facedown), Ganking (battlefield-to-battlefield move), Tank/Backline (damage order), Accelerate (enter ready, extra cost), Deflect (tax), Temporary (killed at Beginning). Decide which belong to 2a versus 2b.

### Section 5: Match layer and deck validation

- **`DeckValidator` (in `CromoBound.Data`):**
  - Main Deck exactly 40 including the Chosen Champion.
  - At most 3 copies per name; the champion counts.
  - At most 3 Signature cards in total, all with the legend's champion tag.
  - Unique cards: 1 copy.
  - Rune Deck exactly 12, inside the Domain Identity.
  - 3 battlefields with different names.
  - Every multi-domain card's domains inside the legend's identity.
  - Chosen Champion matches the legend's champion tag.
  - Sideboard: up to 10 valid Main Deck cards; copy limits counted across Main Deck plus sideboard.
- **Match state:** format, game number, game wins, battlefields still available per player, previous game result (for play-first and sideboarding permissions), current decks after sideboarding.
- The pre-game steps of §3 as decisions.

### Section 6: Manual actions and hybrid card handling

- The full list of manual actions and their validation (e.g. can't move a card into a zone it can't legally be in?).
- How a spell's effect is resolved by hand in 2a. Proposal: when a spell resolves, the engine pauses with a "resolve manually" decision for its controller. They perform manual actions, then confirm "done", and the spell goes to the trash.
- Same for triggered abilities (2a has no automatic triggers; a manual "add trigger to chain" action?).
- What 2b will replace: the manual pause, when an effects file with status `Full` exists.

### Section 7: Views, error handling, testing

- `PlayerView` contents (public information list: Tournament Rules 502.4) and event filtering.
- Rejection reasons: typed codes plus a human-readable message.
- Testing strategy: scripted games driven through `Submit`; rules-section unit tests (cleanup, chain, combat damage, Final Point, Burn Out); replay determinism tests (same seed + log = same state); undo tests.

---

## 6. Rules ambiguities to resolve in the spec

1. **Win condition wording:** CR 485.6 says "first player to reach the Victory Score wins", but CR 194.2/323.1 add "more points than every other player" and "checked in cleanup". Proposal: follow 194.2.
2. **Priority after finalizing:** "next item" (312.2.c, 337.4) vs "newest item" (340.4). Proposal: newest (top) item.
3. **Hide timing:** 811.1.b ("on your turn during an Open State") vs Hide being Discretionary (Neutral Open only, 410.1.a). Pick one.
4. **Non-combat showdown ending with no units left** (348.2 is silent). Proposal: the battlefield ends uncontested and uncontrolled via cleanup steps 4 and 8.
5. **Combat damage step when one side has no units left** (465 is silent). Proposal: skip straight to Resolution.
6. **Final Point "every Battlefield":** does it include the battlefield being conquered right now? Proposal: yes.
7. **Hidden play cost:** 421.3 "played for [0]" vs 811.1.b "ignoring its base cost" (additional costs still apply). Proposal: 811.1.b.
8. **Bo3 battlefields after a draw:** CR 486.5.a "may be reused" vs TR 406.1.b "must use the same". Pick one.
9. **Public zones for targeting:** 355.10.a.1 omits Banishment and Chain, which 108.6.e and 108.1.b call public. Proposal: treat both as public.

---

## 7. Phase 1 leftovers (not blocking)

- `ADD` still appears in the import report as an unknown keyword (uppercase form of the ignored `[Add]`). One-line fix in `KeywordText`.
- The Kennen legend's id is `yordle-kennen-heart-of-the-tempest`, because the API names it "Yordle, Kennen - Heart of the Tempest". It probably needs a manual name override.

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
