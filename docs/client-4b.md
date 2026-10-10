# CromoBound: Client Design (Phase 4b, the play screen)

Status: approved design, before implementation. Builds on the 4a client (`docs/client.md`), the server (`docs/server.md`) and the engine (`docs/engine-architecture.md`).

## 1. Goal and scope

4b replaces the 4a match placeholder with the board: everything a player sees and does during a match, from the player's own `PlayerView`. It is built in three parts:

| Part | Content |
|---|---|
| **4b-1 (plans K and L)** | Card images and the card catalog; the board; the pre-game steps; the normal turn (play, pay, runes, move, hide, activate, pass, end turn); the chain; resolving text by hand (Done and Continue only); the activity log; card zoom; undo and concede |
| 4b-2 (plan M) | Combat and choices: showdowns, damage assignment, targets, choosing a player or cards, yes or no, trigger order |
| 4b-3 (plan N) | The by-hand menu for every manual action, and polish |

### In scope (4b-1)
- Every card image downloaded once by the importer and served by the server to signed-in players.
- A compact card catalog for the client.
- The board drawn from the player's view, scaled to fit the window.
- A control for each decision plan 1 covers (§6), and a clear panel for each decision it doesn't.
- The activity log, card zoom, undo with the other player's consent, concede.
- Server tests, board model tests, component tests, and a manual check list.

### Out of scope (4b-1)
- The decisions of 4b-2 (they show a "comes in the next update" panel).
- The by-hand menu (4b-3). Text resolved by hand is agreed between the players and confirmed with Done or Continue.
- Drag and drop, automatic passing, animations, sound, spectators, a turn timer.
- Phone and tablet layouts.

### Success criteria
- Two friends play a Bo1 and a Bo3 of cards without combat to the end, from the battlefield picks to the result, in two browsers, without talking through what the screen can't do.
- Every card on the board shows its own image; a missing image never breaks the board.
- A refused action shows the engine's reason and leaves the board as it was.
- A dropped connection locks the board and restores it on reconnect.

## 2. Decisions summary

| Topic | Decision |
|---|---|
| How 4b is split | Three parts: the board and normal turn (4b-1), combat and choices (4b-2), the by-hand menu and polish (4b-3) |
| Card images | Downloaded up front by the importer into a folder outside git, served by the server to signed-in players |
| Card data on the client | A compact catalog from `GET /api/cards`, loaded once per tab |
| Interaction | Click first: click a card to play it, click a unit then a destination to move, a menu per card for the rest; hover for the card zoom |
| Screen size | The board keeps the mock's 1440x900 layout and is scaled to fit the window |
| Reaction windows | Always ask: every priority shows PASS and waits; no automatic passing |
| Client structure | A plain C# board model built from the view, the decision and a local interaction state; components only draw it |
| Look | The approved Board mock (rounded, Crimson night), with the owner's six comments applied |

## 3. Card images

### 3.1 The importer
- A new command, `images`, downloads the image of every printing in `data/printings.json` (`imageUrl`) to `<folder>/{printingId}.png`. The folder is given on the command line.
- It skips files already there, so it resumes and only fetches what is new.
- It downloads a few files at a time (at most 4 at once).
- It checks that each answer is an image (`image/*` content type, non-empty) before saving it, through a temporary file, so a broken download never leaves a half file.
- It ends with a report: downloaded, skipped, failed (with the printing ids).

### 3.2 The server
- A new setting, `CardImagesPath` (default `card-images`, next to the database), names the folder.
- `GET /cards/img/{printingId}`:
  - only for signed-in players (the `Seat` policy, like the app);
  - the id must be a printing the card database knows, so nothing outside the folder can be asked for;
  - answers the PNG with `Cache-Control: private, max-age=604800`;
  - 404 for an unknown printing or a missing file.
- `docs/server-deploy.md` gains the step: run the importer's `images` command into the server's `CardImagesPath` once, and again after each new set.

### 3.3 The card catalog
- `GET /api/cards`, signed-in players only, answers a `CardCatalog` (in `CromoBound.Contracts`):
  - `Cards`: per card its id, name, type, domains, energy cost, power cost and might;
  - `Printings`: per printing its id and card id;
  - `Tokens`: per token its id, name and might.
- No rules text: the image carries it.
- `Cache-Control: private, max-age=3600`.
- The client's `CardCatalog` service loads it once when the match page first needs it and keeps it for the tab.

## 4. The board model

The match page builds a `BoardModel` from:
- the player's latest view (`LobbyState.CurrentView`),
- the catalog,
- a local `Interaction` (what the player is in the middle of).

`BoardModel.From(view, catalog, interaction)` returns an immutable snapshot:
- **Zones.** For each side: legend, champion, base units and gear, runes, hand (the opponent's as backs and a count), main deck and rune deck counts, the top card of the trash and the trash count, banished cards, points, XP. For each battlefield: its card, who holds it, whether it is contested, the units on each side, a hidden card if any. Then the chain, the turn number and the phase.
- **Per card:** clickable, highlighted, ringed (suggested or legal), dimmed, exhausted, and the overlays of §5.2.
- **The side panel:** the turn badge, the big button (label, enabled, the action it sends), the buttons under it, the chain row.

`model.Click(target)` returns one of:
- an action to send (`PlayCard`, `UseRune`, `StandardMove`, `EndTurn`, and so on);
- a new interaction (for example "moving Blade Twirler: pick a destination", with the legal destinations taken from the decision's `MoveOption`);
- a card menu to open.

The page sends actions with `IGameHub.SubmitAsync`. While an action is in flight, the board takes no clicks. When the decision changes (a new view with a different decision), the interaction resets.

## 5. The board

### 5.1 Layout
- The approved Board mock at 1440x900, scaled as one canvas to fit the window (`transform: scale`), with plain bars where the window's shape differs.
- Your half at the bottom, the opponent's mirrored at the top, the two battlefield lanes across the middle, the match panel on the right.
- The owner's comments on the mock apply:
  - no player plates in the panel;
  - XP as a badge on the legend, behind a "Show XP on legends" switch in a board-settings menu (kept in the browser);
  - lanes where units overlap the battlefield card from both sides, its name band left visible;
  - one points badge per side;
  - trash beside the deck and banished above the legend and champion, as piles;
  - the rune deck left of the hand;
  - a small turn badge instead of a decision panel; the big button changes with the situation.

### 5.2 A card on the board
- The printing's image (`/cards/img/{printingId}`), scaled to the card's size.
- Overlays only for what the image can't show:
  - damage;
  - the current might when it differs from the printed one;
  - stunned, buffed and empowered marks;
  - exhausted (the card turned sideways);
  - attached gear;
  - a ring for a suggested or legal choice.
- A card without an image (none downloaded, or the load failed) shows a placeholder in the mock's style: name, cost and might from the catalog.
- Hover shows the card zoom: the image at full size, plus the overlays in words.

## 6. Decisions in 4b-1

### 6.1 Before the game
Dialogs over the board. While the other player chooses, a "Waiting for giulia..." note shows instead.
- **Battlefield pick** (`PickBattlefieldDecision`, Bo3): your unused battlefields; click one, then Confirm. Sends `PickBattlefield`.
- **Play order** (`ChoosePlayOrderDecision`): "Go first" or "Go second". Sends `ChoosePlayOrder`.
- **Sideboarding** (`SideboardDecision`, between Bo3 games): the main deck and the sideboard side by side. Picking one card on each side makes a swap; the swaps are listed and can be removed; Submit sends `SubmitSideboard`.
- **Mulligan** (`MulliganDecision`): as in the mock; tick up to 2 cards, then "Keep all" or "Set aside N and draw". Sends `Mulligan`.

### 6.2 Priority (`PriorityDecision`)
- Cards in `Playable` glow; clicking one sends `PlayCard`.
- **Play options** (`PlayChoicesDecision`): a small panel asks where the card enters (base or a battlefield) and, when offered, whether to accelerate. Sends `ChoosePlayOptions`.
- **Paying** (`PayCostDecision`):
  - the runes are ringed with the engine's suggestion and the big button reads PAY;
  - clicking a rune cycles it through exhaust, recycle and unused, each with its own mark;
  - Suggest restores the suggestion, Cancel sends `CancelPlay`, PAY sends `PayCost` with the chosen runes.
- **Moving:** clicking a unit with a `MoveOption` starts a move and highlights its destinations. Clicking more of your units with the same destination available adds them. Clicking a destination sends `StandardMove`. Esc, or clicking the first unit again, cancels.
- **Hide** and **activate an ability:** items in the card's menu, from `HideOption` and `ActivateOption`. Hide asks for the battlefield.
- **Runes outside a payment:** a small menu, Exhaust (energy) or Recycle (power), from `RuneOption`. Sends `UseRune`.
- **The big button:** PASS when `CanPass` (answering the chain), END TURN when `CanEndTurn`, disabled otherwise.

### 6.3 Text resolved by hand
- **Resolve by hand** (`ResolveManuallyDecision`): a panel shows the card and the text the controller carries out themselves, with Done (`ResolveDone`).
- **Turn point** (`TurnPointDecision`): the panel lists the cards whose start- or end-of-turn text is applied by hand, with Continue (`ContinueTurn`).
- Until 4b-3's by-hand menu, the players agree what the text does and press the button.

### 6.4 Undo and concede
- **Request undo** (in the panel) sends `RequestUndo`. The other player gets `ConfirmUndoDecision` as a dialog: "giulia asks to undo her last action", with Allow and Refuse (`AnswerUndo`).
- **Concede** keeps 4a's confirmation and sends `Concede`.

### 6.5 Waiting and decisions of later parts
- When it isn't your decision, the turn badge says whose it is, the board takes no clicks, and hover still zooms.
- Any decision 4b-2 covers (`ChooseShowdownDecision`, `AssignDamageDecision`, `ChooseTargetsDecision`, `ChoosePlayerDecision`, `ChooseCardsDecision`, `OptionalDecision`, `OrderTriggersDecision`) shows a panel: "This choice comes in the next update", with the decision's kind. The match can still be conceded.

## 7. The match panel
- **The header:**
  - the turn number and the turn badge ("YOUR TURN" or "giulia's turn");
  - the format, the game number and the score;
  - the board-settings menu;
  - the link to the lobby.
- The phases, with the current one marked.
- **The chain row,** when the chain isn't empty: the items, newest first, with their controller.
- The big button and the buttons under it (§6.2).
- Request undo and Concede.
- **The activity log:**
  - the view's `Log`, one short sentence per event type, newest at the bottom;
  - an event type without a sentence yet shows its type name;
  - the log scrolls and keeps to the bottom unless the player has scrolled up.

## 8. Errors
- A refused action: the engine's message (`Rejection.Message`), or the server's error, shown word for word in a notification. The board stays as it was.
- A lost connection: 4a's banner, and the board takes no clicks until the connection is back and the view is reloaded.
- The catalog fails to load: "The cards couldn't be loaded." with a Retry button, in place of the board.
- An image fails to load: the placeholder (§5.2).

## 9. Projects and files
```
tools/CromoBound.Importer/        the images command
src/CromoBound.Contracts/         CardCatalog records
src/CromoBound.Server/            CardImagesPath, /cards/img/{printingId}, /api/cards
src/CromoBound.Client/
  Services/CardCatalog.cs         loads and keeps the catalog
  Board/                          BoardModel, Interaction, BoardClick (plain C#, no Blazor)
  Board/Components/               BoardView (the scaled canvas), sides, lanes, hand, runes, piles, points,
                                  CardFace, CardZoom, MatchPanel, ActivityLog
  Board/Dialogs/                  battlefield pick, play order, sideboard, mulligan, play options,
                                  resolve by hand, turn point, undo request
  Pages/Match.razor               the 4a page, with the placeholder replaced by BoardView
```

## 10. Testing
- **Importer:** downloading against a fake HTTP handler: saves images, skips existing files, refuses non-images, never leaves a half file, reports failures.
- **Server:**
  - `/cards/img` and `/api/cards` refuse signed-out requests;
  - an unknown printing, a traversal attempt and a missing file each give a 404;
  - the catalog's contents and cache headers;
  - `DefaultDenyTests` covers the new routes.
- **Board model (xUnit, views built in code):** for every 4b-1 decision kind:
  - what is clickable and highlighted;
  - the big button's label and state;
  - the exact action each click sends;
  - how interactions start, grow (moving several units) and reset.
- **Components (bUnit):** the board renders the model (zones, overlays, placeholder images), clicks reach the model, the dialogs send their actions, a refused action shows its message.
- **End to end:** one server test plays a short game through the real hub with the client's `GameConnection`: picks, mulligans, a card played and paid, end of turn.
- **Manual check list:** a "4b" section in `docs/client-manual-checks.md`: a Bo1 and a Bo3 without combat to the end, undo asked and refused and allowed, a reconnect mid-turn, images present and missing.

## 11. Plans
| Plan | Content |
|---|---|
| K: images and catalog | The importer's `images` command, `CardImagesPath`, `/cards/img/{printingId}`, `/api/cards`, server tests, the deploy step |
| L: the board | The client of 4b-1: card catalog service, board model, board components and dialogs, the match page, tests, manual checks |
| M: combat and choices (4b-2) | Its own spec section or spec, after its mocks |
| N: the by-hand menu and polish (4b-3) | Its own spec section or spec, after its mocks |

Plan K needs no mocks. Before plan L is written, these are mocked as artifacts and reviewed by the owner:
- battlefield pick, play order and sideboarding;
- the play-options panel;
- paying (runes cycling through exhaust and recycle);
- moving (destinations highlighted);
- resolve by hand and turn point;
- the undo request from both sides;
- the waiting state;
- the "comes in the next update" panel.

## 12. Decisions log
| Decision | Choice | Reason |
|---|---|---|
| Split | Three parts, the first playable | Feedback on the board early; each plan stays reviewable |
| Images | Downloaded up front, served by the server | Robust against changes on Riot's side; friends' browsers never contact a third party for card art |
| Catalog | A compact `GET /api/cards` | The client needs names, costs and might for placeholders and menus, not rules text |
| Interaction | Click first | Precise, the same for every action, testable without a browser, keyboard reachable |
| Screen size | One scaled 1440x900 canvas | The board always looks like the approved mock |
| Reaction windows | Always ask | Never passes a chance the player wanted |
| Structure | A board model drawn by components | All client game logic in one testable class; later parts add modes instead of spreading logic |
| Text resolved by hand in 4b-1 | Done and Continue panels | Games with cards the engine doesn't run don't get stuck before 4b-3 |
