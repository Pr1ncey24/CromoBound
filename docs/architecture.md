# CromoBound — Card Data & Effects DSL Design

- **Date:** 2026-10-05
- **Status:** Approved
- **Scope:** Phase 1 of the CromoBound Riftbound simulator: card data import, the normalized card JSON, the effects DSL, sample card mappings, C# models and validation. No engine, backend or UI.

---

## 1. Context and goals

CromoBound is a Riftbound simulator for two friends. It will eventually be a web app with a .NET backend that also hosts the game engine.

The chosen automation strategy is **hybrid**:
- The engine automates the rules core: turn structure, chain, combat, scoring and keywords.
- It also automates card effects that have a structured definition.
- Cards without a definition show their text, and players resolve them by hand.
- Automation grows card by card.

This phase defines the data foundations everything else depends on:

1. A one-time import of all card, set and index data from the Riftcodex API, stored locally.
2. A normalized card model, which separates *gameplay identity* (the card) from *physical version* (the printing), so each player can pick which rarity or art they use.
3. An **effects DSL**: declarative JSON built from ordered, composable effect primitives, with a C# script fallback for the few cards it can't express.
4. Twelve sample cards mapped end to end, to prove the DSL before any bulk mapping.
5. C# models as the single source of truth, a generated JSON Schema, and tests.

### Success criteria
- The importer has been run once. `data/raw/`, `cards.json`, `printings.json` and `sets.json` are populated.
- The 12 sample cards (§9) have effects files that pass strict deserialization and semantic validation.
- Vanilla and keyword-only cards have auto-scaffolded effects files.
- All tests pass, including the "schema is up to date" test.

### Out of scope
- Game engine, game state, rules execution.
- Backend (ASP.NET Core / SignalR) and web UI.
- Deck legality validation. The deck *format* is defined here; the copy-limit rules are written down in §6.4 for the future validator.
- Downloading card images (they are hotlinked).

---

## 2. Source data: Riftcodex API

Base URL: `https://api.riftcodex.com` (OpenAPI at `/openapi.json`).

| Endpoint | Use |
|---|---|
| `GET /cards?page=N&size=100` | All prints. `size` max 100. 1451 prints = **15 pages**. Pagination is in the query string, not the body. |
| `GET /sets?size=100` | Sets: OGN, OGS, SFD, UNL, VEN, OPP, PR, JDG. |
| `GET /index/{card-types, card-supertypes, domains, rarities, tags, keywords, energy, might, power, artists, card-names}` | Value lists. |

### 2.1 How each index is used

| Index | Use |
|---|---|
| `card-types` (6), `card-supertypes` (4), `domains` (7), `rarities` (6) | Clean. Mirrored as C# enums. The importer **fails** if the API returns a value missing from the enum. |
| `tags` (127) | Used to validate card tags and the "name a tag" choices (rule 764). |
| `keywords` (30) | **Dirty.** It contains `ADD`, `11`, `TEXT`, and misses `Level` and `Burn`. Our display-keyword list is curated (§5.2), and the index is only used to warn on unknown keywords. |
| `energy`, `might`, `power`, `artists`, `card-names` | Stored in `raw/` for future deck-builder filters. They can all be derived from `cards.json`. |

### 2.2 Known data quirks (the importer must handle them)

1. **Inconsistent names.** `Ahri - Inquisitive` vs `Ahri, Inquisitive`. Suffixes occur: `(Alternate Art)`, `(Overnumbered)`, `(Signature)`, `(Metal)`, `(Starter)`, `(Ultimate)`, `(Launch Exclusive)`, `(GG EZ)`, and numeric ones such as `Recruit (273)`. Token prints are named `X // Buff`, because a Buff counter is printed on the back.
   - Normalization: strip a trailing `(...)`, strip `// Buff`, and replace `" - "` with `", "`.
   - Result: **~938 distinct gameplay cards** out of 1451 prints.
2. **`riftbound_id` is not unique** (e.g. `ven-sp3-006` appears twice). The Riftcodex `id` (an ObjectId) is the printing identity.
3. **Text differs between printings of the same name** (87 names). It's mostly reminder text being present or absent, plus a few real wording changes.
   - Rule: same name = same card (Core Rules 103.2.b).
   - The **newest printing's** text is canonical (by set `publishedOn`).
   - Every conflict is listed in the import report for manual review.
4. **`attributes.power` is only a count.**
   - Single-domain card: the power list is derived (`count × domain`).
   - Multi-domain card: the domains can't be derived. `cost.power` stays `null` until an effects-file override supplies them. These cards are listed in the report.
5. **`text.plain` joins abilities with no separator** (`[Ganking]Recycle 1…`). `text.rich` keeps the `<br />` line breaks and is the source for line splitting and for the `line` references (§7.1).
6. **Inline icons in rich text:**
   - `:rb_might:`, `:rb_exhaust:`
   - `:rb_energy_N:`
   - `:rb_rune_<domain>:`, `:rb_rune_rainbow:` (any domain)

   The UI will render them later. The importer keeps them verbatim.
7. **Basic runes have the text `[NO TEXT]`.** Their two abilities are defined by the rules and must be written in their effects files (§9.2).
8. **Most rule-defined tokens are missing from the API.** Only Gold, Sprite and Recruit exist as prints. Tokens are therefore **hand-authored** (§6.5), and API token prints are kept only as printings (art) of those tokens.
9. **"Signature" means two different things:** the *supertype* (a deck restriction, `classification.supertype`) and the *print variant* (`metadata.signature`, an autographed print). They're stored in different fields.

---

## 3. Solution layout

Target framework **`net10.0`** (SDK 10.0.401 installed).

```
CromoBound.sln
src/
  CromoBound.Models/        C# records for all JSON shapes. No dependencies.
  CromoBound.Data/          CardRepository: load + merge + semantic validation. Depends on Models.
tools/
  CromoBound.Importer/      Console app: fetch / normalize / scaffold / report. Depends on Models.
tests/
  CromoBound.Models.Tests/  xUnit. Depends on Models, Data.
schema/                     Generated JSON Schemas (never hand-edited).
data/                       See §4.
docs/
```

Future phases add `CromoBound.Engine`, `CromoBound.Server` (ASP.NET Core + SignalR) and a web frontend.

**Dependencies:**
- Runtime projects use only the .NET base library (`HttpClient`, `System.Text.Json`, `System.Text.Json.Schema`).
- Test project: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` (approved).

---

## 4. Data layering

```
data/
  raw/                         Verbatim API responses. Never edited by hand.
    cards.json                 All 1451 prints (concatenated page items)
    sets.json
    index/<name>.json          One file per index endpoint
  cards.json                   GENERATED: one entry per gameplay card
  printings.json               GENERATED: one entry per print → cardId
  sets.json                    GENERATED
  tokens.json                  HAND-AUTHORED: token card definitions (§6.5)
  effects/                     HAND-AUTHORED (+ scaffolded): one file per card, effects only
    <cardId>.json
  import-report.md             GENERATED by the importer
```

**Why it's split this way:**
- `raw/` lets normalization be re-run without calling the API again. The API is hit 15 + 1 + 11 times, once, and again only when a new set is released.
- `cards.json` vs `printings.json`: gameplay never cares about the art. Decks reference printings, and the rules reference cards.
- `effects/` is separate from generated data: **re-importing never overwrites hand-mapped effects**. The repository merges `cards.json` + `tokens.json` + `effects/<id>.json` at load time.
- One file per card gives small git diffs, easy review, and lets two people map cards in parallel without conflicts.

---

## 5. Card model (`cards.json`)

### 5.1 Shape

```json
{
  "id": "falling-star",
  "name": "Falling Star",
  "type": "Spell",
  "supertype": null,
  "domains": ["Fury"],
  "cost": { "energy": 2, "power": ["Fury", "Fury"] },
  "might": null,
  "tags": [],
  "keywords": [],
  "text": {
    "rich": "<p>Deal 3 to a unit.<br />Deal 3 to a unit.</p>",
    "plain": "Deal 3 to a unit. Deal 3 to a unit."
  },
  "defaultPrintingId": "69bc5bc8d308c64675ca86d4"
}
```

| Field | Type | Notes |
|---|---|---|
| `id` | string | Kebab-case slug of the normalized name (`ahri-inquisitive`). Stable; also the effects file name. |
| `name` | string | Normalized `Short Name, Subtitle` form. |
| `type` | enum `CardType` | `Unit`, `Spell`, `Gear`, `Rune`, `Battlefield`, `Legend` |
| `supertype` | enum `Supertype?` | `Champion`, `Signature`, `Basic`, `Token`, or null |
| `domains` | `Domain[]` | `Fury`, `Calm`, `Mind`, `Body`, `Chaos`, `Order`, `Colorless` |
| `cost` | `Cost?` | `energy` (int?) + `power` (`Domain[]?`). Null for cards with no cost (runes, battlefields, legends). `power: null` means "unknown, override required". |
| `might` | int? | Units only. |
| `tags` | string[] | Validated against the tags index. |
| `keywords` | `DisplayKeyword[]` | **Display/filter keywords** (§5.2). Not used by the rules. |
| `text.rich` / `text.plain` | string | Canonical text (newest printing). |
| `defaultPrintingId` | string | The printing used when a deck doesn't say otherwise. The newest standard printing is preferred. |

### 5.2 Display keywords vs mechanical keywords

There are two separate concepts:

- **Display keywords** (`cards.json` → `keywords`): every bracketed term on the card, the way card websites filter them. It includes action and condition terms such as `Buff`, `Stun`, `Mighty`, `Burn`, `Level`.
  - Extracted by the importer from the `[...]` in the rich text.
  - The enum is the keyword index minus noise (`ADD`, `11`, `TEXT`) plus missing ones (`Level`, `Burn`).
  - Search and filter only.
- **Mechanical keywords** (effects file → `keywords`, §7.2): only the keywords that are *abilities an object has*, per the Core Rules (800–829).
  - In the DSL, `Buff` and `Stun` are **actions** (rules 426, 423).
  - `Mighty` is a **condition** (rule 702).
  - `Legion` / `Level N` / `Empowered` are **dependent-ability conditions** (§7.3).

---

## 6. Printings, sets, decks, tokens

### 6.1 Printing (`printings.json`)

```json
{
  "id": "69c4407c9288b1e85d94de8a",
  "cardId": "vi-piltover-enforcer",
  "riftboundId": "unl-229*-219",
  "tcgplayerId": "685522",
  "set": "UNL",
  "collectorNumber": 229,
  "rarity": "Rare",
  "variant": "Signature",
  "imageUrl": "https://cmsassets.rgpub.io/…",
  "orientation": "portrait",
  "artist": "Jonathan Santoro",
  "flavour": null
}
```

`variant` is one of `Standard`, `AlternateArt`, `Overnumbered`, `Signature`, `Metal`, `Starter`, `Ultimate`, `LaunchExclusive`, `Other`. It comes from the name suffix and the metadata flags. `rarity` is one of `Common`, `Uncommon`, `Rare`, `Epic`, `Showcase`, `Promo`.

### 6.2 Set (`sets.json`)

```json
{ "id": "UNL", "name": "Unleashed", "publishedOn": "2026-05-08", "cardCount": 280 }
```

### 6.3 Deck

Decks reference **printings**, so each player chooses which rarity or art of a card they use. Mixed printings of the same card are allowed.

```json
{
  "name": "Kharox Burn",
  "legend": "<printingId>",
  "champion": "<printingId>",
  "main":        [ { "printing": "<printingId>", "count": 2 }, { "printing": "<printingId>", "count": 1 } ],
  "runes":       [ { "printing": "<printingId>", "count": 6 }, { "printing": "<printingId>", "count": 6 } ],
  "battlefields": ["<printingId>", "<printingId>", "<printingId>"]
}
```

In a game, every card instance carries both `cardId` (used by the rules) and `printingId` (the art shown to both players).

### 6.4 Deck rules to enforce later (documented, not implemented)
- **Copy limits** are counted per **`cardId`**, never per printing:
  - max 3 per name, including the chosen champion;
  - `Unique` → max 1;
  - max 3 Signature-supertype cards in total.
- Main deck is 40 cards including the champion (tournament) or at least 40 (core). Rune deck is exactly 12. 3 battlefields with unique names.
- Domain identity comes from the legend's domains. Every domain of a multi-domain card must be inside the identity.

### 6.5 Tokens (`tokens.json`, hand-authored)

Token definitions use the card shape (§5.1) with `supertype: "Token"`, no cost, and ids prefixed `token-`. They come from Core Rules 187:

| id | type | might | tags | rules text / effects |
|---|---|---|---|---|
| `token-recruit` | Unit | 1 | Recruit | — |
| `token-sprite` | Unit | 3 | Fae | `Temporary` |
| `token-sand-soldier` | Unit | 2 | Shurima | — |
| `token-mech` | Unit | 3 | Mech | — |
| `token-reflection` | Unit | 0 | — | — |
| `token-bird` | Unit | 1 | Bird | `Deflect` |
| `token-tentacle` | Unit | 1 | Bilgewater | — |
| `token-shadow-clone` | Unit | 0 | — | "When I attack, you may banish a unit from your trash. If you do, give me [Assault 4] this turn." |
| `token-gold` | Gear | — | — | `[Reaction] Kill this, [E]: [Add] [A].` |
| `token-brush` | Battlefield | — | — | 2 abilities (rule 187) |
| `token-baron-pit` | Battlefield | — | — | "Units can move here from anywhere." |

- Tokens have effects files in `data/effects/` like any other card.
- API token prints (`Gold // Buff`, `Sprite (274) // Buff`, `Recruit (271–273) // Buff`) become **printings** of the matching token card, with `cardId` set to `token-gold` and so on. They are not separate cards.

---

## 7. Effects DSL

### 7.1 Effects file (`data/effects/<cardId>.json`)

```json
{
  "$schema": "../../schema/effects.schema.json",
  "cardId": "garbage-grabber",
  "status": "Full",
  "overrides": { },
  "additionalCosts": [ ],
  "asYouPlay": [ ],
  "keywords": [ ],
  "abilities": [ ]
}
```

| Field | Meaning |
|---|---|
| `cardId` | Must exist in `cards.json` or `tokens.json`. |
| `status` | `Full` (fully automated), `Partial` (some lines automated), `Unmapped` (manual). **A missing file means `Unmapped`.** |
| `overrides` | Corrections to static data. Currently only `cost` (e.g. the power domains of multi-domain cards). |
| `additionalCosts` | Additional costs to play the card (§7.5). |
| `asYouPlay` | Choices made while playing (step 2), e.g. naming a tag. Steps whose results are saved with `store`. |
| `keywords` | Mechanical keywords with their parameters (§7.2). |
| `abilities` | All other abilities, in printed order (§7.3). |

Every ability can have **`line`** (1-based): the line of `text.rich` (split on `<br />`) that it implements. It can be a number or an array of numbers, for an ability spanning several lines. For `Partial` cards, the UI uses it to show which lines players must resolve by hand.

### 7.2 Mechanical keywords

```json
{ "keyword": "Shield", "value": 2 }
{ "keyword": "Empower", "cost": { "energy": 6, "power": ["Chaos", "Chaos"] } }
{ "keyword": "Deathknell", "steps": [ { "action": "Channel", "count": 1, "exhausted": true } ] }
```

The engine expands each keyword into its rules-defined ability:

| Keyword | Parameter | Expansion (Core Rules) |
|---|---|---|
| `Accelerate` | — | Optional additional cost `[1][C]` → enters ready (805) |
| `Action`, `Reaction` | — | Timing permissions (806, 813) |
| `Assault`, `Shield` | `value` (default 1, stacks) | +X Might while attacker / defender (807, 814) |
| `Deathknell` | `steps` | Triggered on own death (808) |
| `Deflect` | `value` (default 1, stacks) | Opponents pay +X `[A]` per choice (809) |
| `Ganking` | — | Standard Move from battlefield to battlefield (810) |
| `Hidden` | — | Hide for `[A]`; later play with Reaction, ignoring base cost (811) |
| `Tank`, `Backline` | — | Combat damage assignment order (815, 826) |
| `Temporary` | — | Killed at start of Beginning Phase, before scoring (816) |
| `Vision` | — | When played, predict (817) |
| `Equip` | `cost` | `cost`: attach to a unit you control (818) |
| `Quick-Draw` | — | Reaction + attach on play (819) |
| `Repeat` | `cost` | Optional additional cost → instructions run one more time (820) |
| `Weaponmaster` | — | On play, attach an Equipment for its Equip cost minus `[A]` (821) |
| `Ambush` | — | Play to a battlefield where you control units, with Reaction (822) |
| `Hunt` | `value` (default 1, stacks) | Conquer/hold → gain X XP (823) |
| `Unique` | — | Deck restriction only (825) |
| `Empower` | `cost` | `cost`: Empower this; only if not Empowered (827) |
| `Flow` | `cost` | Alternate cost to play from trash, then banish (829) |

### 7.3 Abilities

| `kind` | Fields |
|---|---|
| `Spell` | `steps`. Runs on resolution, top to bottom. |
| `Triggered` | `trigger`, `if` (intervening condition, rule 382), `optional` (a leading "you may"), `cost` (a leading cost, paid at finalization), `limit`, `steps` |
| `Activated` | `cost`, `timing` (`Action`/`Reaction`, default: own turn, Open state), `useOnlyIf`, `limit`, `steps` |
| `Passive` | `while` (condition), `modifiers[]` |
| `Replacement` | `replaces` (event pattern like `trigger`), `with` (steps or a modifier), `optional`, `limit` |

**Shared fields:**
- `line`
- `condition`: a dependent keyword. `{ "legion": true }`, `{ "level": 6 }`, `{ "empowered": true }`. The ability is inactive while the condition is false.
- `activeIn`: the zone where the ability works. Default `Board`; e.g. `Trash`.
- `script`: C# fallback for the whole ability (§7.10).

**`limit`:** `{ "per": "Turn", "times": 1 }` ("once each turn").

**Passive `modifiers[]` types (first version):**

| type | Fields | Example |
|---|---|---|
| `ModifyMight` | `amount` (Value), `appliesTo` (selector) | "Other friendly units have +1 [M]" |
| `GrantKeyword` | `keyword` entry, `appliesTo` | "Your Mechs have [Vision]" |
| `CostReduction` / `CostIncrease` | `energy` (Value), `power`, `appliesTo`, `minimum`, `fromZone` | Noxus Hopeful, Rhasa, Void Drone |
| `KeywordCostReduction` | `keyword`, `energy`, `appliesTo` | "Friendly [Repeat] costs cost [1] less" |
| `Permission` | `permission` (enum), `appliesTo` | `PlayToBattlefieldWithEnemyUnits` (Rengar) |
| `EnterReady` | `appliesTo` | "I enter ready" (also expressible as a Replacement) |
| `Untargetable` | `by` (`EnemySpellsAndAbilities`), `appliesTo` | Master Yi Level 16 |

### 7.4 Costs

```json
"cost": {
  "energy": 1,
  "power": [],
  "exhaustSelf": true,
  "actions": [ { "action": "Recycle", "from": { "zone": "Trash", "owner": "You" }, "count": 3 } ]
}
```

- `power` entries are a `Domain` or `"Any"` (`[A]`, `:rb_rune_rainbow:`) or `"Self"` (`[C]`, the card's own domain).
- **Non-standard costs** (recycle, discard, kill, spend buff, pay XP, banish/kill this) go in `actions`. They use the step vocabulary of §7.6.
- The engine runs them in **cost mode**: they must be fully possible, otherwise the play or activation is illegal (rules 416, 422).

### 7.5 Additional costs and the forms of cost on cards

| Form | Where it lives | Example |
|---|---|---|
| Mandatory additional cost to play | `additionalCosts[]` with `optional: false` | Cruel Patron: "As an additional cost to play me, kill a friendly unit." |
| Optional additional cost with an effect | `additionalCosts[]` with `optional: true` + `onPaid` steps | Clockwork Keeper: "you may pay [Calm]… If you do, draw 1." |
| Optional additional cost that changes the cost | `additionalCosts[]` + `modifiesCost` | Brazen Buccaneer (discard 1 → cost [2] less), Wallop (spend a buff → `IgnoreCost`) |
| Keyword additional costs | Mechanical keyword | `Accelerate`, `Repeat` (cost), `Deflect` (value) |
| Leading cost on a trigger | `Triggered.optional` + `Triggered.cost` | Hall of Legends: "you may pay [1] to ready your legend" |
| Cost inside instructions | `Optional` step with `cost` | "…then you may discard 1 to draw 2." Paid on resolution. |
| "As you play" choices | `asYouPlay` steps | The List: "As you play this, name a tag." |
| Dynamic cost changes | `Passive` + `CostReduction` with a computed `energy` | Rhasa: "[1] less for each card in your trash" |

```json
"additionalCosts": [
  { "id": "keeper", "optional": true,
    "cost": { "power": ["Calm"] },
    "onPaid": [ { "action": "Draw", "player": "You", "amount": 1 } ] },
  { "id": "discount", "optional": true,
    "cost": { "actions": [ { "action": "Discard", "player": "You", "count": 1 } ] },
    "modifiesCost": { "type": "CostReduction", "energy": 2 } }
]
```

- Abilities can check `{ "paid": "<id>" }`.
- The **order in which costs are combined is fixed by the engine** (rule 356): base-cost changes → additional costs → increases → discounts → total-cost changes, with a minimum of 0. It is never written in card JSON.

### 7.6 Steps

A step is `{ "action": "<Name>", ...params }`, plus the common fields `store`, `script`, `player`/`chooser` (default: the ability's controller).

| Group | Steps and main params |
|---|---|
| Cards & zones | `Draw` (player, amount), `Discard` (player, count), `Burn` (player, amount), `Recycle` (from/target, count), `Banish` (target), `ReturnToHand` (target), `Reveal`, `LookAt`, `Predict` (amount), `Counter` (target) |
| Playing | `Play` (card, `from`, `cost`: `IgnoreAll`/`IgnoreEnergy`/`IgnorePower`/`{ "for": Cost }`, `location`, `exhausted`), `PlayToken` (`token`: token cardId, `count`, `location`, `exhausted`) |
| Units & permanents | `Deal` (amount, target, `split`, `bonus`, `source`), `Heal`, `Kill`, `Stun`, `Buff`, `SpendBuff`, `Ready`, `Exhaust`, `Move` (target, `to`), `Recall`, `Attach`, `Detach`, `Empower`, `GainControl` |
| Changes over time | `ModifyMight` (target, amount, duration), `GrantKeyword` (target, keyword entry, duration), `GrantTag` (target, tag, duration) |
| Resources & players | `Add` (`energy`, `power`), `Channel` (count, `exhausted`), `Score` (player, amount), `GainXp`, `SpendXp`, `Pay` (cost), `ExtraTurn` |
| Choices without a target | `ChoosePlayer` (filter), `ChooseCard` (`from` zone, filter, count), `NameTag`, `NameCard` |
| Control flow | `Optional` (`cost?`, `reflexive?`, steps), `If` (condition, `then`, `else`), `ForEach` (selector, `as`, steps), `Repeat` (times, steps), `ChooseOne` / `ChooseN` (`modes[]`) |
| Delayed | `CreateDelayed` (ability: Triggered/Replacement/Passive, `duration`) |
| Fallback | `Script` (`script`, `args`) |

**`duration`:** `ThisTurn` | `ThisCombat` | `UntilLeavesBoard` | `WhileInZone` (default, rule 801) | `Permanent`.

**`reflexive: true`** marks "do this:" blocks, which create a new chain item (Core Rules reflexive abilities).

### 7.7 Selectors and references

```json
{ "select": "Unit", "count": 1,
  "filter": { "relation": "Enemy", "location": { "ref": "Here" }, "might": { "lte": 3 }, "tags": ["Mech"] } }
```

- **`select`:** `Unit`, `Gear`, `Permanent`, `Spell`, `Card`, `Rune`, `Battlefield`, `Legend`, `ChainItem`, `Player`.
- **Quantity:** `count: N`, `upTo: N`, or `all: true`.
- **`filter`:**
  - `relation`: `Friendly`/`Enemy` for objects, `Self`/`Opponent`/`Ally` for players;
  - `controller`, `owner`;
  - `location` (ref, var or a nested selector), `zone`;
  - `type`, `supertype`, `tags`, `domains`, `name`;
  - `might` and `energyCost` (`eq`/`lte`/`gte`/`lt`/`gt`);
  - `status` (`Stunned`, `Exhausted`, `Buffed`, `Damaged`, `Empowered`);
  - `mighty`, `token`, `keyword`, `other` (excludes self), `not`.
- **References:**
  - `{ "ref": "Self" | "Here" | "Controller" | "Owner" | "TriggerSubject" | "TriggerSource" }`
  - `{ "var": "<name>" }`
- **Player references:**
  - the strings `"You"`, `"Opponent"`, `"EachPlayer"`, `"EachOpponent"`;
  - `{ "var": "<name>" }`;
  - `{ "controllerOf": <ref> }`.
- **Zone references:** `{ "zone": "Hand" | "MainDeck" | "RuneDeck" | "Trash" | "Banishment" | "ChampionZone" | "Base" | "Facedown", "owner": <player>, "position": "Top" | "Bottom" }`.

**Targeting is decided by the engine, not the JSON** (rule 355):
- A selector with `count`/`upTo` over public objects is a target, chosen at finalization when the card is played.
- `all`, hidden-zone choices, and `ChooseCard` are not targets. They resolve on resolution.
- Targets are chosen in JSON order.

### 7.8 Values and conditions

**Values:**

| Form | Meaning |
|---|---|
| `3` | Literal number |
| `{ "count": <selector> }` | Number of matching objects |
| `{ "prop": "Might" \| "EnergyCost" \| "PowerCost" \| "Damage", "of": <ref> }` | A property of an object |
| `{ "var": "x" }` | A stored value |
| `{ "sum": [...] }`, `{ "mul": [...] }`, `{ "min": [...] }`, `{ "max": [...] }` | Arithmetic |

**Conditions:**

| Form | Meaning |
|---|---|
| `{ "all": [...] }`, `{ "any": [...] }`, `{ "not": ... }` | Combinators |
| `{ "exists": <selector> }` | At least one object matches |
| `{ "compare": [<value>, "gte", <value>] }` | Comparison (`eq`, `lte`, `gte`, `lt`, `gt`) |
| `{ "paid": "<additionalCostId>" }` | An additional cost was paid |
| `{ "did": "<storedStepVar>" }` | "If you do": the stored step actually happened. A replaced action does not count (rule 359.3.e). |
| `{ "legion": true }`, `{ "level": N }`, `{ "empowered": true }` | Dependent keywords |
| `{ "phase": "Beginning" }` | Current phase |
| `{ "turnOf": "You" }` | Whose turn it is |
| `{ "playedThisTurn": <selector> }` | Turn history |

### 7.9 Triggers

```json
"trigger": { "event": "Conquer", "subject": { "ref": "Self" }, "by": "You", "where": { "ref": "Here" }, "filter": { } }
```

Events in the first version, grouped:
- **Play & cards:** `Played`, `Discarded`, `Drawn`, `Recycled`, `Chosen`.
- **Combat & scoring:** `Attack`, `Defend`, `Conquer`, `Hold`, `Scored`.
- **Movement & death:** `Moved`, `Dies`, `Killed`.
- **Status changes:** `Stunned`, `Buffed`, `Readied`, `Equipped`, `BecameEmpowered`, `BecameMighty`, `Damaged`.
- **Timing:** `PhaseStart` (with `phase`), `TurnEnd`, `CombatStart`, `CombatEnd`.

The list grows as cards need it.

### 7.10 C# script fallback

- A `Script` step, or an ability-level `script`, names a C# handler registered with `[CardScript("<Name>")]`.
- Scripts call the same engine primitives as JSON steps, so the rules are never duplicated in scripts.
- Scripts receive previously stored variables through `args`.

**Rules:**
- **When in doubt, use data.** Scripts are only for one-off mechanics that would need a primitive used by a single card (copy effects, odd layer interactions, rule-changing cards).
- **3+ cards needing the same script** → promote it to a DSL step.
- Script names in JSON are checked against the registry **when the engine exists** (next phase). In this phase, `Script` steps are only syntax-validated.

Interface contract (implemented in the Engine phase; documented here so the DSL stays compatible):

```csharp
public interface IStepScript
{
    void Execute(StepContext ctx, ScriptArgs args);
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class CardScriptAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
```

---

## 8. C# models and JSON handling

- `CromoBound.Models` holds immutable `record` types for every shape above.
- **The C# models are the single source of truth.** JSON Schemas are **generated** from them.

**Serialization** (`System.Text.Json`, shared `JsonSerializerOptions` in Models):
- camelCase names; enums as strings (`JsonStringEnumConverter`).
- `UnmappedMemberHandling = Disallow`, so typos such as `"ammount"` fail.
- `AllowOutOfOrderMetadataProperties = true`, so the discriminator doesn't have to be the first property.
- Polymorphism via `[JsonPolymorphic]` / `[JsonDerivedType]`:
  - `Step` → discriminator `action`;
  - `Ability` → `kind`;
  - `Modifier` → `type`.
- **Union-shaped values** need custom converters:
  - `Value`: number or object;
  - `ObjectRef`: `{ref}` / `{var}` / selector;
  - `PlayerRef`: string or object;
  - `Condition`: a single-key object.

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "action")]
[JsonDerivedType(typeof(DrawStep), "Draw")]
[JsonDerivedType(typeof(DealStep), "Deal")]
// …
public abstract record Step
{
    public string? Store { get; init; }
    public string? Script { get; init; }
}

public sealed record DrawStep : Step
{
    public PlayerRef Player { get; init; } = PlayerRef.You;
    public Value Amount { get; init; } = Value.Of(1);
}
```

**Schema generation:**
- `JsonSchemaExporter.GetJsonSchemaAsNode` writes `schema/effects.schema.json`, `schema/card.schema.json` and `schema/deck.schema.json`.
- Custom converters are described through `TransformSchemaNode`, using `oneOf` for each union.
- Effects files reference the schema through `$schema`, which gives VS Code autocomplete and validation.

---

## 9. Sample cards (proof of the DSL)

| # | Card | cardId | What it tests |
|---|---|---|---|
| 1 | Vanguard Sergeant | `vanguard-sergeant` | No text: play, cost, enter exhausted |
| 2 | Fury Rune | `fury-rune` | Rule-defined rune abilities, Add, Reaction timing |
| 3 | Progress Day | `progress-day` | Single-step spell |
| 4 | Falling Star | `falling-star` | Two independent targets |
| 5 | Vengeance | `vengeance` | Kill |
| 6 | Mystic Poro | `mystic-poro` | Keyword expanding into a trigger |
| 7 | Rengar, Trophy Hunter | `rengar-trophy-hunter` | Keyword permission + passive permission |
| 8 | Soaring Scout | `soaring-scout` | Deathknell, Channel exhausted |
| 9 | Shadow Temple | `shadow-temple` | Battlefield trigger, Burn |
| 10 | Garbage Grabber | `garbage-grabber` | Activated ability, combined cost, Recycle |
| 11 | Noxus Hopeful | `noxus-hopeful` | Dependent keyword + cost reduction |
| 12 | Kharox | `kharox` | Empower, status trigger, player choice, variables, optional reflexive block, play from opponent's trash ignoring cost |

### 9.1 Vanguard Sergeant
Unit, 4 Energy, Might 4, Order, no text. **Scaffolded automatically.**
```json
{ "cardId": "vanguard-sergeant", "status": "Full", "keywords": [], "abilities": [] }
```

### 9.2 Fury Rune
Basic rune, text `[NO TEXT]`. Its abilities are rule-defined (Core Rules, basic runes) and written explicitly here.
```json
{
  "cardId": "fury-rune",
  "status": "Full",
  "abilities": [
    { "kind": "Activated", "timing": "Reaction",
      "cost": { "exhaustSelf": true },
      "steps": [ { "action": "Add", "energy": 1 } ] },
    { "kind": "Activated", "timing": "Reaction",
      "cost": { "actions": [ { "action": "Recycle", "target": { "ref": "Self" } } ] },
      "steps": [ { "action": "Add", "power": ["Fury"] } ] }
  ]
}
```

### 9.3 Progress Day
"Draw 4."
```json
{
  "cardId": "progress-day",
  "status": "Full",
  "abilities": [
    { "kind": "Spell", "line": 1,
      "steps": [ { "action": "Draw", "player": "You", "amount": 4 } ] }
  ]
}
```

### 9.4 Falling Star
"Deal 3 to a unit. / Deal 3 to a unit."
- This is one spell (one chain item) with two instructions.
- Each instruction has its own target, and both targets are chosen at finalization, so the same unit can be chosen twice.
```json
{
  "cardId": "falling-star",
  "status": "Full",
  "abilities": [
    { "kind": "Spell", "line": [1, 2],
      "steps": [
        { "action": "Deal", "amount": 3, "target": { "select": "Unit", "count": 1 } },
        { "action": "Deal", "amount": 3, "target": { "select": "Unit", "count": 1 } }
      ] }
  ]
}
```

### 9.5 Vengeance
"Kill a unit."
```json
{
  "cardId": "vengeance",
  "status": "Full",
  "abilities": [
    { "kind": "Spell", "line": 1,
      "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1 } } ] }
  ]
}
```

### 9.6 Mystic Poro
"[Vision]". **Scaffolded automatically** (keyword-only).
```json
{ "cardId": "mystic-poro", "status": "Full", "keywords": [ { "keyword": "Vision" } ], "abilities": [] }
```

### 9.7 Rengar, Trophy Hunter
"[Ambush] / I can be played to a battlefield where there are enemy units."
```json
{
  "cardId": "rengar-trophy-hunter",
  "status": "Full",
  "keywords": [ { "keyword": "Ambush" } ],
  "abilities": [
    { "kind": "Passive", "line": 2,
      "modifiers": [ { "type": "Permission", "permission": "PlayToBattlefieldWithEnemyUnits",
                       "appliesTo": { "ref": "Self" } } ] }
  ]
}
```

### 9.8 Soaring Scout
"[Deathknell] — Channel 1 rune exhausted."
```json
{
  "cardId": "soaring-scout",
  "status": "Full",
  "keywords": [
    { "keyword": "Deathknell", "steps": [ { "action": "Channel", "count": 1, "exhausted": true } ] }
  ]
}
```

### 9.9 Shadow Temple
Battlefield. "When you hold here, [Burn 3]."
```json
{
  "cardId": "shadow-temple",
  "status": "Full",
  "abilities": [
    { "kind": "Triggered", "line": 1,
      "trigger": { "event": "Hold", "by": "You", "where": { "ref": "Here" } },
      "steps": [ { "action": "Burn", "player": "You", "amount": 3 } ] }
  ]
}
```

### 9.10 Garbage Grabber
Gear. "Recycle 3 from your trash, [1], [E]: Draw 1."
```json
{
  "cardId": "garbage-grabber",
  "status": "Full",
  "abilities": [
    { "kind": "Activated", "line": 1,
      "cost": {
        "energy": 1,
        "exhaustSelf": true,
        "actions": [ { "action": "Recycle", "from": { "zone": "Trash", "owner": "You" }, "count": 3 } ]
      },
      "steps": [ { "action": "Draw", "player": "You", "amount": 1 } ] }
  ]
}
```

### 9.11 Noxus Hopeful
"[Legion] — I cost [2] less."
```json
{
  "cardId": "noxus-hopeful",
  "status": "Full",
  "abilities": [
    { "kind": "Passive", "line": 1, "condition": { "legion": true },
      "modifiers": [ { "type": "CostReduction", "energy": 2, "appliesTo": { "ref": "Self" } } ] }
  ]
}
```

### 9.12 Kharox (complex)
Unit, 6 Energy, Might 5, Chaos.
> [Empower] [6][Chaos][Chaos]
> When I become [Empowered], choose an opponent. They [Burn 3]. Then you may do this: Choose a unit in their trash and play it, ignoring its cost.

```json
{
  "cardId": "kharox",
  "status": "Full",
  "keywords": [
    { "keyword": "Empower", "cost": { "energy": 6, "power": ["Chaos", "Chaos"] } }
  ],
  "abilities": [
    {
      "kind": "Triggered", "line": 2,
      "trigger": { "event": "BecameEmpowered", "subject": { "ref": "Self" } },
      "steps": [
        { "action": "ChoosePlayer", "filter": { "relation": "Opponent" }, "store": "victim" },
        { "action": "Burn", "player": { "var": "victim" }, "amount": 3 },
        { "action": "Optional", "reflexive": true, "steps": [
          { "action": "ChooseCard",
            "from": { "zone": "Trash", "owner": { "var": "victim" } },
            "filter": { "type": "Unit" }, "count": 1, "store": "picked" },
          { "action": "Play", "card": { "var": "picked" }, "cost": "IgnoreAll" }
        ] }
      ]
    }
  ]
}
```

Handled by the engine rather than the JSON:
- Burn Out on Burn 3 (rule 431).
- The played unit goes back to its **owner's** trash later (rule 056).
- The reflexive block opens a new chain item, so opponents can respond.

---

## 10. Importer (`tools/CromoBound.Importer`)

Commands: `dotnet run --project tools/CromoBound.Importer -- <fetch|normalize|scaffold|all>`

1. **`fetch`**
   - Downloads 15 card pages (`size=100`), `/sets?size=100` and the 11 index endpoints into `data/raw/`, with a 1-second pause between requests.
   - Fails on any non-200 response or a `total` mismatch. It never writes partial `raw/` files.
   - Run only on demand.
2. **`normalize`**: `raw/` → `cards.json`, `printings.json`, `sets.json`.
   - Name normalization and grouping (§2.2.1); canonical text = newest printing.
   - Display-keyword extraction (§5.2).
   - `power` derived for single-domain cards.
   - Token prints mapped to `token-*` cards (§6.5).
   - Enum check: unknown values fail the run.
   - Output sorted by `id`, so diffs stay stable.
3. **`scaffold`**: creates `data/effects/<id>.json` **only when the file doesn't exist**.
   - Vanilla cards (empty text) → `Full`, empty abilities.
   - Keyword-only cards (text made only of mechanical keywords with optional parameters and reminder text) → `Full`, with keywords filled in.
   - Everything else: no file (counts as `Unmapped`).
4. **Report** (`data/import-report.md`), written by `normalize` and `scaffold`:
   - text conflicts between printings;
   - multi-domain cards missing `power`;
   - unknown keywords (from the index or from the text);
   - card counts per type and per mapping status.

---

## 11. Validation and tests (`tests/CromoBound.Models.Tests`)

1. **Strict deserialization:**
   - every file in `data/effects/` deserializes;
   - `cards.json`, `printings.json`, `sets.json` and `tokens.json` deserialize.
2. **Semantic validation** (`CromoBound.Data`, `EffectsValidator`):
   - `cardId` exists;
   - every `var` is `store`d earlier in the same ability or in `asYouPlay`;
   - `line` is within the card's rich-text line count;
   - `PlayToken.token` is a `token-*` id;
   - `paid` refers to an existing `additionalCosts.id`;
   - a multi-domain card with `power: null` has a cost override;
   - every printing's `cardId` exists.
3. **Sample-card assertions:** one test per §9 card checking its structure (e.g. Kharox has one `BecameEmpowered` trigger with 3 steps, the last being a reflexive `Optional`).
4. **Round-trip:** deserialize then serialize each sample file; the result must be semantically equal.
5. **Schema up to date:** regenerate the schemas in memory and compare them with `schema/*.json`.

---

## 12. Decisions log

| Decision | Choice | Reason |
|---|---|---|
| Automation strategy | Hybrid (C) | Playable early; automation grows card by card |
| Effect representation | Declarative JSON + C# script fallback (B) | Common effects stay data; odd cards don't warp the DSL |
| Card identity | Normalized name → `cardId` slug | Rules define identity by name (103.2.b) |
| Printing identity | Riftcodex `id` | `riftbound_id` is not unique |
| Decks reference | Printings | Players choose rarity/art; limits counted per card |
| Effects storage | One hand-authored file per card, separate from generated data | Re-import safety, small diffs, parallel mapping |
| Keywords | Display keywords (cards.json) separate from mechanical keywords (effects) | Website-style filters plus rules-accurate DSL |
| Tokens | Hand-authored `tokens.json` | API lacks most rule-defined tokens |
| Source of truth | C# models; schema generated | No drift between schema and code |
| Target framework | net10.0 | Installed LTS SDK |
| Test framework | xUnit | Approved |
