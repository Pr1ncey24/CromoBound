# CromoBound: The Fiora Deck Runs Itself (Phase 2c)

- **Status:** Approved (2026-10-10), with the owner's rules readings in §4.
- **Builds on:** `effects-engine.md` (Phase 2b). Same rules: the engine runs what mapped cards need and nothing more, and a file that uses anything the engine can't run plays by hand (`EffectsSupport`).

## 1. Goal and scope

- **Goal:** every card of the Fiora sample deck's starting 56 resolves automatically, so a game with it can be checked card by card.
- **Cards:** 19 to map. Rengar, Trophy Hunter was already mapped, and every basic rune (Body and Order included) was mapped on 2026-10-10.
  - Legend: Fiora, Grand Duelist. Champion: Fiora, Victorious.
  - Main deck: Dazzling Aurora, Divining Shells, Doran's Blade, Elder Dragon, Grim Resolve, Harnessed Dragon, Kayle Justified, Punch First, Rampage, Repulse, Rift Herald, Riposte, Sacrifice, Shepherd's Heirloom.
  - Battlefields: Amateur Recital, Risen Altar, Sunken Temple.
- **Out of scope:** the sideboard (Akshan, Challenge, Decree of Strength, Repulse's second copy is in the main deck, Sabotage, Salvage); any other card; the 4b-2 combat UI.
- **Success:**
  1. Every one of the 19 files is `Full` and passes `EffectsDataTests`.
  2. Each card has at least one engine test that plays it with the real card data and checks the outcome its text describes.
  3. Every existing test still passes; 0 warnings.

## 2. What each card needs

| Card | Text, in short | Mapping | New in the engine |
|---|---|---|---|
| Harnessed Dragon | when played, kill an enemy unit | Played trigger, Kill | none |
| Doran's Blade | Equip [Body]; +2 might to its unit | Equip keyword; passive ModifyMight on the host | equipment might (§3.4) |
| Shepherd's Heirloom | when played gain 1 XP; Equip: spend 1 XP; +2 might | Played trigger GainXp; Equip with a SpendXp cost | XP as a cost; equipment might |
| Punch First | +5 might to a unit this turn | ModifyMight step | ModifyMight step |
| Divining Shells | Vision; Kill this, exhaust: +2 might to a unit this turn | Vision; activated ability with a kill-self cost | kill as a cost |
| Grim Resolve | +3 to a friendly unit this turn; when it wins a combat this turn, gain 2 XP | ModifyMight; CreateDelayed with a WonCombat trigger | delayed abilities, WonCombat |
| Rampage | optional extra [Body]: +2 first; the two units deal their might to each other | additional cost; If paid; two Deal steps sourced by each unit | values from might, `paid` |
| Riposte | counter a spell; a friendly unit gets +might equal to its energy cost | targets a chain spell; Counter; ModifyMight by that spell's cost | Counter step, chain targets, `prop` values |
| Repulse | counter an enemy spell or ability that chooses your unit and no other friendly unit | targets a friendly unit at a battlefield, then a matching chain item | Counter, the "chooses only" filter |
| Sacrifice | extra cost: kill a friendly Mighty unit; draw 2, channel 1 exhausted | additional cost with a Kill; Draw; Channel | Mighty filter, kill as a cost |
| Kayle, Justified | Empower 3, up to three times; +2 might each; at three: Deflect 3 and Ganking | Empower keyword with a limit; passives | Empower count (§3.5), passive might and keywords |
| Fiora, Victorious | while Mighty: Deflect, Ganking, Shield | passive GrantKeyword while Mighty | passive keywords, `mighty` condition |
| Fiora, Grand Duelist | when one of your units becomes Mighty, you may exhaust me: channel 1 exhausted | BecameMighty trigger on friendly units | BecameMighty (§3.6) |
| Elder Dragon | any amount of your damage kills enemy units; when played, up to one enemy unit at each location takes 1 | passive Lethal; ForEach location, choose up to one, Deal 1 | Lethal (§3.7), ForEach |
| Rift Herald | when it moves to a battlefield: look at the top 3, you may reveal a unit and draw it, recycle the rest; Deathknell: play a unit from hand to base ignoring its energy cost | Moved trigger; LookAt, ChooseCard, Draw, Recycle; Deathknell Play | Moved trigger, look-and-pick |
| Dazzling Aurora | at the end of your turn, reveal until a unit, play it ignoring its cost, recycle the rest | TurnEnd trigger; Reveal until; Play; Recycle | TurnEnd trigger, reveal until |
| Amateur Recital | when you hold here, you may move a unit at a battlefield to its base | Hold trigger; Optional Move | Move step |
| Risen Altar | Empower costs of your units here cost 1 energy or 1 [A] less, the player's pick | passive KeywordCostReduction | Empower cost reduction with a choice |
| Sunken Temple | when you conquer here with a Mighty unit, you may pay 1 to draw 1 | Conquer trigger with an `exists` condition; Optional with a cost; Draw | `exists`, optional costs |

## 3. Engine additions

Each one is small and self-contained, and goes in only because a card above needs it.

### 3.1 Values, conditions, filters, selectors
- **Values:** `prop` + `of` (Might, EnergyCost) and `var`, besides literals.
- **Conditions:** `paid` (an additional cost was paid), `exists` (a selector finds something), `turnOf`.
- **Filters:** `mighty`, `location` (Here, or a battlefield), `controller`, `zone`, `not`.
- **Selectors:** `ChainItem`/`Spell` (items on the chain) for Counter's targets; `Battlefield`-and-base locations for Elder Dragon's ForEach.
- **References:** `Host` (the unit a gear is attached to), for equipment bonuses.

### 3.2 Steps
`ModifyMight` (this turn, using 2a's might modifiers), `Counter`, `Move`, `LookAt`, `Reveal` (with an `until` filter), `Recycle` of looked-at or revealed cards, `ForEach`, `If`, `CreateDelayed`, `SpendXp` and `Pay` (in costs), and `Play` from hand with `IgnoreEnergy`.

### 3.3 Triggers
`Moved` (to a battlefield), `TurnEnd` (the controller's own turn), `BecameMighty` (§3.6), `WonCombat` (when a combat ends: the unit survived and its controller is the only player with units at that battlefield, §4.2), and triggers on friendly units other than the source (Fiora, Grand Duelist).

### 3.4 Passives that apply live
- `ModifyMight` passives join `Modifiers.MightOf`, with a value (Kayle: 2 times her Empower count) and an `appliesTo` (Self, or Host for equipment).
- `GrantKeyword` passives join a single `KeywordsOf(object)` read. Everything that asks whether an object has Deflect, Ganking, Shield or Tank goes through it, so a granted keyword behaves exactly like a printed one.
- `KeywordCostReduction` lowers an Empower cost (Risen Altar).

### 3.5 Empower more than once
- `CardInstance.Empowered` (true/false) becomes an Empower count. "Empowered" means a count of 1 or more, so every 2b card behaves the same.
- An Empower keyword entry gets an optional `limit` (Kayle: 3; default 1, today's "only if not Empowered").
- The board's chip reads "Empowered" for 1 and "Empowered x2", "x3" above that.

### 3.6 Becoming Mighty
- Mighty means 5 or more might (CR 706-711).
- The game keeps the set of Mighty units in its state. A unit entering the board joins the set silently if it is already Mighty (§4.1). After each step, cleanup and might change, the game compares, and emits `BecameMighty` for each unit already on the board that crossed to 5 or more. Because the set is in the state, replay and undo need nothing new.

### 3.7 Elder Dragon's lethal damage
- Each unit remembers which players' sources damaged it this turn (cleared with the damage at end of turn).
- Cleanup also kills an enemy unit that has any damage from a player who controls an active Lethal passive.
- Combat damage: for that player's assignments, every enemy unit's lethal amount is 1 (`CombatDamage.Lethal`).

## 4. Rules readings

The owner's rulings (2026-10-10) are marked **owner**; the rest are this design's readings.

1. **Becomes Mighty (owner):** a unit already on the board whose might goes from below 5 to 5 or more. A unit that enters the board with 5 or more is not "becoming" Mighty.
2. **Wins a combat (owner):** when the combat (its showdown) is over, the unit survived and its controller is the only player with units at that battlefield. The enemy units can be gone by any means (killed, returned to hand, moved to base).
3. **Risen Altar (owner):** the player chooses whether the Empower cost loses 1 energy or one [A] power. When only one of the two is in the cost, that one is reduced without asking.
4. **Elder Dragon (owner):** any damage the Elder Dragon's controller does counts: combat damage from their units, and damage from their spells and abilities (Flurry of Blades dealing 1 to every unit at battlefields kills every enemy unit there).
5. **Dazzling Aurora:** the unit is played to your base. If no unit is revealed, every revealed card is recycled. Revealed cards are seen by both players.
6. **Rift Herald:** "moves to a battlefield" counts a move from base or from another battlefield, not being played there. The recycled cards go to the bottom in random order.
7. **Amateur Recital:** any unit at any battlefield, yours or the opponent's. It moves to its controller's base; this is a move, not a recall.
8. **Riposte:** "a spell" is any spell on the chain, yours or the opponent's. The might bonus is the spell's printed energy cost.
9. **Repulse:** it counters an enemy spell or ability on the chain whose chosen targets include your chosen unit and no other unit you control.
10. **Sacrifice:** killing the Mighty unit is a kill, so its Deathknell triggers (Rift Herald).
11. **Equipment:** Doran's Blade and Shepherd's Heirloom print +2 might, which the card data lacks. Their files give it to the unit they're attached to.

## 5. Testing

- **Data:** `EffectsDataTests` already loads and runs every file in `data/effects/`.
- **Per card:** an engine test plays the card from a set-up state with the real card data, answers its decisions, and checks the result.
  - For example: Punch First makes a 3-might unit 8 this turn and 3 next turn; Riposte counters a 2-cost spell and gives +2; Kayle empowered three times has 9 might, Deflect 3 and Ganking.
- **Interaction tests:** Fiora, Grand Duelist triggering when Punch First makes a unit Mighty; Sacrifice killing Rift Herald and its Deathknell playing a unit; Elder Dragon's 1 damage killing a 6-might enemy at cleanup.
- **A replay test:** a game using these cards is saved, replayed and undone, and ends in the same state.

## 6. Plans

- **Plan O:** §3.1, §3.2 and §3.4 to §3.5, with the cards they cover: Harnessed Dragon, Punch First, Divining Shells, Doran's Blade, Shepherd's Heirloom, Rampage, Sacrifice, Kayle, Fiora Victorious, Risen Altar, Sunken Temple, Amateur Recital.
- **Plan P:** §3.3, §3.6 and §3.7, the chain and the deck: Riposte, Repulse, Grim Resolve, Fiora Grand Duelist, Elder Dragon, Rift Herald, Dazzling Aurora; then the interaction and replay tests.
