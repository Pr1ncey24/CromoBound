# Client Plan L: the Board (Phase 4b-1)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Second of two plans for Phase 4b-1 (K: images and catalog, done; L: the board).

**Goal:** The match page becomes the board: the player's view drawn as the approved Board mock, with a control for every 4b-1 decision (the pre-game steps, the normal turn, paying, moving, resolving text by hand, undo), the activity log, card zoom and concede. Two friends can play a game without combat to the end.

**Architecture:**
- **`Board/` (plain C#, no Blazor):**
  - `CardBook` looks cards and printings up in the catalog.
  - `BoardModel.From(view, book, names, interaction)` turns the player's `PlayerView` into an immutable snapshot: zones, cards with their marks, lanes, the chain, the big button, the hint and the current panel. It also works out, up front, what every click does: a `BoardStep`, meaning an action to send, a new local `Interaction`, or a menu.
  - `ActivityLog` turns the view's events into short sentences.
- **`Board/Components/`:** Razor components that only draw the model and report clicks as steps.
  - `BoardView` is the 1440x900 canvas, scaled to the window by a small script (`wwwroot/js/board.js`).
  - `BoardHost` holds the interaction state, sends actions through `IGameHub`, locks the board while one is in flight, and shows the engine's refusals.
- **The match page:** its placeholder is replaced by `BoardHost`. Its loading, its redirect for a match that isn't yours, and its result and abandoned dialogs stay. `MainLayout` hides its top bar on the match page, as the mock has none.

**Tech Stack:** .NET 10, Blazor WebAssembly, MudBlazor 9.11 (only for the existing notifications and dialogs), xUnit 2.9.3, bUnit 2.11.3.

**Spec:** `docs/client-4b.md`: sections 4 to 8, the client bullets of 10, and row L of 11.
- **Design:** the approved mocks in the "Screens" tab of https://claude.ai/artifact/YCkWxnsoXJAJcKY4z3z7QT:
  - the Board artboard with its scenes, Card zoom and Mulligan;
  - the Battlefield pick, Play order and Sideboarding dialogs.
- **Palette:** Crimson night, the one 4a ships. The Shadow isles tab is only a comparison.

## Global Constraints

- **Language:** `net10.0` with nullable enabled and implicit usings. New client types are `public` (Razor components and their models).
- **The look:**
  - The board keeps the mock's layout at 1440x900 and is scaled as one canvas to fit the window (spec §2).
  - Crimson night colors: ground `#090707`, your half `#170f0d`, the opponent's half `#120e15`, panels `#161112`, primary `#d8392a` with white text, accents `#ff7a6b` and `#ff8a7a`, danger rose `#ff9db0`.
  - Card domain colors: Fury `#e0574a` on `#4a2420`, Order `#e3c34a` on `#463c18`, Chaos `#a56bd6` on `#3a2450`.
  - Fredoka for titles and badges, Nunito for text, Barlow Condensed for card names and numbers.
- **Copy:** the mock's words where the mock has them. The engine's and the server's messages are shown word for word (spec §8).
- **Accessibility:**
  - A clickable card is a real `<button>` with an `aria-label` naming the card and its state; a card that can't be clicked is an image with an `aria-label`.
  - Esc cancels a move.
  - Panels are dialogs with `aria-labelledby`.
- **Privacy:** card images come only from the app's own `/cards/img/{printingId}`; nothing loads from a third party.
- **Packages:** none added.
- **Owner rules:**
  - 0 build warnings and 0 errors at all times.
  - Conventional, title-only commit messages: no body, no co-author trailer, no mention of Claude/AI.
  - No em dashes or en dashes (U+2014, U+2013) in code, comments, strings, markup or docs. The middle dot `·` (U+00B7) in the score line is intended.
  - LF line endings; UTF-8 without BOM.
- **dotnet:** in Git Bash on Windows, prefix with `export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && `. The build reports in Italian: `Avvisi` = warnings, `Errori` = errors, `Superato` = passed.
- **bUnit:**
  - Every component test makes its own context with `await using var ui = new Ui();`.
  - Clicks go through `ClickAsync(selector)`, never `WaitForElement` inside `InvokeAsync`.

## Deliberate deviations from the spec and the mocks (reviewers: these are intended)

1. **No "Lobby" link in the match panel.** The lobby page sends a player who is in a match straight back to the match, so the link would only reload the board. Concede is the way out, as in 4a.
2. **The battlefield pick lists only the battlefields you may still pick.** The decision carries only the unused ones (`BattlefieldChoice.Printings`), so the mock's greyed "Played in game 1" tile can't be drawn.
3. **Sideboarding doesn't offer a new Chosen Champion** (`SubmitSideboard.Champion`). Only one-for-one swaps, as the mock shows. Changing the champion can come with the deck builder (4c).
4. **The activity log leaves out events that only restate the board:**
   - phases, statuses, resources, chain bookkeeping, forced choices and targets;
   - control and attachment changes, looks, reveals and shuffles.

   The other event types have a sentence, and an event type it doesn't know shows its type name (spec §7).
5. **Pre-game steps, prompts and the undo answer are panels drawn by the board, not MudBlazor dialogs.** They come and go with the view's decision, so the board model owns them, and component tests can drive them.
6. **The mulligan text doesn't say who plays first.** The decision doesn't carry it.
7. **The undo question names the player but no pronoun:** "giulia asks to undo the last action", where the mock wrote "her last action". The app doesn't know anyone's pronouns.

## Review Focus

1. **A view arrives while the player is halfway through a move or a payment** (the opponent acts, a reconnect reloads the view). The half-made choice is dropped, never sent against the new decision. Pinned in Task 5 (`A_new_decision_drops_a_half_made_move`).
2. **A second click while an action is in flight** sends nothing. Pinned in Task 5 (`While_an_action_is_in_flight_the_board_takes_no_clicks`).
3. **A card whose image is missing or fails to load** shows the placeholder with its name, and the board still works. Pinned in Task 4 (`A_card_without_an_image_shows_its_name`).
4. **A move of several units only offers the destinations they all share**, and paying cycles each rune through exhaust, recycle and unused, with an exhausted rune skipping exhaust. Pinned in Task 2 (`Adding_a_unit_keeps_only_the_destinations_all_share`, `A_rune_cycles_exhaust_recycle_unused`).
5. **The opponent decides** (a pre-game step, a response, an undo answer): the board shows whom it waits for, and nothing of yours is clickable. Pinned in Task 3 (`While_giulia_picks_the_board_waits_for_her`).

---

## File Structure

```
src/CromoBound.Client/
  Board/CardBook.cs                        card and printing lookups over the catalog
  Board/BoardParts.cs                      the snapshot's records: cards, sides, lanes, buttons, panels, steps, interactions
  Board/BoardModel.cs                      BoardModel.From and the clicks
  Board/ActivityLog.cs                     events as sentences
  Board/Components/BoardView.razor         the scaled 1440x900 canvas
  Board/Components/SideView.razor          one side: points, pool, base, runes, piles, legend and champion, hand
  Board/Components/LaneView.razor          one battlefield lane
  Board/Components/CardFace.razor          a card: image or placeholder, overlays, button when clickable
  Board/Components/CardZoom.razor          the hovered card, large
  Board/Components/MatchPanel.razor        turn, badge, phases, chain, big button, undo, concede, log, settings
  Board/Components/BoardPanels.razor       the pre-game steps, prompts and the undo answer
  Board/Components/CardMenu.razor          a card's menu
  Board/Components/BoardHost.razor         interaction state, sending, locking, refusals, catalog, settings
  Pages/Match.razor                        (modify: BoardHost in place of the placeholder)
  Layout/MainLayout.razor                  (modify: no top bar on the match page)
  wwwroot/css/board.css, wwwroot/js/board.js, wwwroot/index.html (modify)
tests/CromoBound.Client.Tests/
  TestBoard.cs                             views built in code over a small catalog
  BoardModelTests.cs, BoardTurnTests.cs, BoardPanelTests.cs, ActivityLogTests.cs
  BoardViewTests.cs, BoardHostTests.cs
  MatchPageTests.cs                        (modify: the board replaces the placeholder)
tests/CromoBound.Server.Tests/BoardGameTests.cs   one game through the real hub, driven by the board model
docs/client-manual-checks.md               (modify: a 4b-1 section)
```

---

### Task 1: The board model's snapshot of the view

**Files:**
- Create: `src/CromoBound.Client/Board/CardBook.cs`, `Board/BoardParts.cs`, `Board/BoardModel.cs`, `tests/CromoBound.Client.Tests/TestBoard.cs`, `tests/CromoBound.Client.Tests/BoardModelTests.cs`
- Modify: `src/CromoBound.Client/_Imports.razor`

**Interfaces:**
- Consumes:
  - `CardCatalog`, `CatalogCard` and `CatalogPrinting` (Plan K, `CromoBound.Contracts`);
  - `PlayerView`, `PlayerSideView`, `CardView`, `BattlefieldView`, `ChainItemView` and `TurnView` (`CromoBound.Engine.Views`);
  - `Formats.Name` (4a).
- Produces:
  - `CardBook(CardCatalog)`, with `Card(string cardId)` returning `CatalogCard?`, `NameOf(string cardId)`, `PrintingOf(string printingId)` returning `CatalogPrinting?`, and `DefaultPrinting(string cardId)` returning `string?`.
  - The records in `BoardParts.cs`:
    - `BoardCard`, `BoardSide`, `BoardLane`, `ChainRow`;
    - `BoardButton`, `MenuItem`;
    - the steps: `BoardStep`, `SendStep`, `NextStep`, `MenuStep`, `NoStep`;
    - the interactions: `Interaction`, `Idle`, `Moving`, `Paying`;
    - the panels: `BoardPanel` and its subtypes, which Tasks 2 and 3 fill;
    - the enums `Ring` and `PayMark`.
  - `BoardModel` (a class), with:
    - `From(PlayerView view, CardBook book, string me, string opponent, Interaction interaction)`;
    - the properties `Me`, `Them`, `Lanes`, `Chain`, `Turn`, `Phase`, `Badge`, `ScoreLine`, `Button`, `Extras`, `Hint`, `Panel`, `UndoWaiting`, `CanRequestUndo`, `Log`, `MyDecision`;
    - the methods `Click(ObjectId)`, `ClickLane(int)`, `ClickBase()` and `Cancel()`, which Task 2 fills.
  - In the tests: `TestBoard`, with `Catalog`, `Book`, `Me`, `Them`, the zone lists, `Add`, `AddLane`, `AddToLane` and `View(...)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Client.Tests/TestBoard.cs`:

```csharp
using CromoBound.Client.Board;
using CromoBound.Contracts;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Tests;

/// <summary>A board built in code: a small catalog (printing <c>p-{cardId}</c> for each card) and the zones of a view, filled by the
/// test, then turned into the viewer's <see cref="PlayerView"/>. The viewer is seat 0 ("marco"), the opponent seat 1 ("giulia").</summary>
internal sealed class TestBoard
{
    public static readonly PlayerId Me = new(0);
    public static readonly PlayerId Them = new(1);

    private static CatalogCard C(string id, string name, CardType type, int? energy = null, int? might = null, Supertype? supertype = null) =>
        new(id, name, type, supertype, [], energy, [], might, supertype == Supertype.Token ? null : $"p-{id}");

    public static CardCatalog Catalog { get; } = new(
        [
            C("jinx-legend", "Jinx, Loose Cannon", CardType.Legend),
            C("jinx-champ", "Jinx, Rebel", CardType.Unit, 3, 3, Supertype.Champion),
            C("unit-a", "Blade Twirler", CardType.Unit, 2, 2),
            C("unit-b", "Daring Poro", CardType.Unit, 1, 1),
            C("gear-a", "Doran's Blade", CardType.Gear, 1),
            C("spell-a", "Angle Shot", CardType.Spell, 2),
            C("fury-rune", "Fury Rune", CardType.Rune, supertype: Supertype.Basic),
            C("order-rune", "Order Rune", CardType.Rune, supertype: Supertype.Basic),
            C("bf-a", "Back-Alley Bar", CardType.Battlefield),
            C("bf-b", "Altar to Unity", CardType.Battlefield),
            C("bf-c", "Dusk Rose Lab", CardType.Battlefield),
            C("token-bird", "Bird", CardType.Unit, might: 1, supertype: Supertype.Token),
        ],
        [.. new[] { "jinx-legend", "jinx-champ", "unit-a", "unit-b", "gear-a", "spell-a", "fury-rune", "order-rune", "bf-a", "bf-b", "bf-c" }
            .Select(id => new CatalogPrinting($"p-{id}", id, id.StartsWith("bf-", StringComparison.Ordinal) ? Orientation.Landscape : Orientation.Portrait))]);

    public CardBook Book { get; } = new(Catalog);

    private int _ids;

    public TestBoard()
    {
        MyLegend.Add(Card("jinx-legend", Me));
        TheirLegend.Add(Card("jinx-legend", Them));
        MyChampion.Add(Card("jinx-champ", Me, might: 3));
        TheirChampion.Add(Card("jinx-champ", Them, might: 3));
    }

    public List<CardView> MyLegend { get; } = [];
    public List<CardView> TheirLegend { get; } = [];
    public List<CardView> MyChampion { get; } = [];
    public List<CardView> TheirChampion { get; } = [];
    public List<CardView> Hand { get; } = [];
    public List<CardView> MyBase { get; } = [];
    public List<CardView> TheirBase { get; } = [];
    public List<CardView> MyTrash { get; } = [];
    public List<CardView> MyBanished { get; } = [];
    public List<ChainItemView> Chain { get; } = [];
    public List<GameEvent> Log { get; } = [];
    public List<(CardView Card, PlayerId? Controller, List<CardView> Units)> Lanes { get; } = [];

    public int TheirHandCount { get; set; } = 4;
    public int MyPoints { get; set; } = 5;
    public int TheirPoints { get; set; } = 6;
    public int MyXp { get; set; } = 1;
    public int MyWins { get; set; } = 1;
    public int TheirWins { get; set; }
    public PlayerId TurnPlayer { get; set; } = Me;
    public int TurnNumber { get; set; } = 5;
    public Phase Phase { get; set; } = Phase.Main;
    public MatchStage Stage { get; set; } = MatchStage.Playing;
    public MatchFormat Format { get; set; } = MatchFormat.Bo3;
    public int GameNumber { get; set; } = 2;

    public CardView Card(string cardId, PlayerId owner, bool exhausted = false, int damage = 0, int? might = null, ObjectId? attachedTo = null,
        bool stunned = false, bool buffed = false) =>
        new(new ObjectId(++_ids), cardId, Catalog.Cards.First(c => c.Id == cardId).DefaultPrintingId, owner, owner,
            exhausted, stunned, buffed, false, damage, might, null, MappingStatus.Full, [], attachedTo);

    /// <summary>Adds a card to a zone list and returns it, so a test can name it in decisions.</summary>
    public CardView Add(List<CardView> zone, string cardId, PlayerId? owner = null, bool exhausted = false, int damage = 0, int? might = null,
        ObjectId? attachedTo = null)
    {
        var card = Card(cardId, owner ?? (ReferenceEquals(zone, TheirBase) ? Them : Me), exhausted, damage, might, attachedTo);
        zone.Add(card);
        return card;
    }

    public CardView AddLane(string cardId, PlayerId? controller = null)
    {
        var card = Card(cardId, Me);
        Lanes.Add((card, controller, []));
        return card;
    }

    public CardView AddToLane(int lane, string cardId, PlayerId owner, bool exhausted = false, int? might = null)
    {
        var card = Card(cardId, owner, exhausted, might: might);
        Lanes[lane].Units.Add(card);
        return card;
    }

    public PlayerView View(PendingDecision? decision = null, IReadOnlyList<PlayerId>? deciding = null, string? kind = null)
    {
        PlayerSideView Side(PlayerId player) => player == Me
            ? new PlayerSideView(Me, MyPoints, MyXp, MyWins, new PoolView(2, new Dictionary<Domain, int> { [Domain.Fury] = 1 }, 0),
                MyLegend, MyChampion, MyBase, Hand, Hand.Count, 29, 7, MyTrash, MyBanished, null, 0)
            : new PlayerSideView(Them, TheirPoints, 2, TheirWins, new PoolView(0, new Dictionary<Domain, int>(), 0),
                TheirLegend, TheirChampion, TheirBase, null, TheirHandCount, 27, 8, [], [], null, 0);
        var turn = Stage == MatchStage.Playing
            ? new TurnView(TurnNumber, TurnPlayer, Phase, TurnStep.None, TurnPlayer, null, Chain.Count > 0, null, false, null, null, [[], []])
            : null;
        var lanes = Lanes.Select((l, i) => new BattlefieldView(i, l.Card, l.Controller, null, l.Units, false, null)).ToList();
        return new PlayerView(Me, Format, Stage, GameNumber, null, [Side(Me), Side(Them)], turn, lanes, Chain,
            deciding ?? (decision is null ? [] : decision.Players), kind ?? decision?.GetType().Name.Replace("Decision", ""), decision, Log);
    }

    public BoardModel Model(PendingDecision? decision = null, Interaction? interaction = null, IReadOnlyList<PlayerId>? deciding = null,
        string? kind = null) =>
        BoardModel.From(View(decision, deciding, kind), Book, "marco", "giulia", interaction ?? Idle.Instance);

    public static PriorityDecision Priority(
        IEnumerable<ObjectId>? playable = null, IEnumerable<RuneOption>? runes = null, IEnumerable<MoveOption>? moves = null,
        IEnumerable<HideOption>? hides = null, IEnumerable<ActivateOption>? activations = null, bool canPass = false, bool canEndTurn = true) =>
        new(Me, [.. playable ?? []], [.. runes ?? []], [.. moves ?? []], [.. hides ?? []], [.. activations ?? []], canPass, canEndTurn);
}
```

Create `tests/CromoBound.Client.Tests/BoardModelTests.cs`:

```csharp
using CromoBound.Client.Board;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Tests;

public class BoardModelTests
{
    [Fact]
    public void The_sides_show_points_xp_piles_and_the_legend_and_champion()
    {
        var board = new TestBoard();
        board.Add(board.MyTrash, "spell-a");
        board.Add(board.MyTrash, "unit-b");
        board.Add(board.MyBanished, "gear-a");

        var model = board.Model();

        Assert.Equal(("marco", true, 5, 1), (model.Me.Name, model.Me.IsMe, model.Me.Points, model.Me.Xp));
        Assert.Equal(("giulia", false, 6, 2), (model.Them.Name, model.Them.IsMe, model.Them.Points, model.Them.Xp));
        Assert.Equal(("Jinx, Loose Cannon", "Jinx, Rebel"), (model.Me.Legend?.Name, model.Me.Champion?.Name));
        Assert.Equal(("Daring Poro", 2), (model.Me.TrashTop?.Name, model.Me.TrashCount));
        Assert.Equal("Doran's Blade", Assert.Single(model.Me.Banished).Name);
        Assert.Equal((29, 7, 4), (model.Me.MainDeckCount, model.Me.RuneDeckCount, model.Them.HandCount));
        Assert.Null(model.Them.TrashTop);
    }

    [Fact]
    public void Runes_are_apart_from_the_units_and_gear_of_the_base()
    {
        var board = new TestBoard();
        board.Add(board.MyBase, "fury-rune");
        board.Add(board.MyBase, "unit-a", might: 2);
        board.Add(board.MyBase, "order-rune", exhausted: true);
        board.Add(board.MyBase, "gear-a");

        var model = board.Model();

        Assert.Equal(new[] { "Blade Twirler", "Doran's Blade" }, model.Me.Base.Select(c => c.Name));
        Assert.Equal(new[] { ("Fury Rune", false), ("Order Rune", true) }, model.Me.Runes.Select(c => (c.Name, c.Exhausted)));
    }

    [Fact]
    public void A_card_carries_its_state_its_printing_and_what_changed_from_the_printed_card()
    {
        var board = new TestBoard();
        var unit = board.Add(board.MyBase, "unit-a", damage: 1, might: 4);
        board.Add(board.MyBase, "gear-a", attachedTo: unit.Id);
        board.Add(board.MyBase, "unit-b", might: 1);

        var model = board.Model();

        var twirler = model.Me.Base.Single(c => c.Id == unit.Id);
        Assert.Equal(("p-unit-a", 1, 4, 2, true, 1), (twirler.PrintingId, twirler.Damage, twirler.Might, twirler.PrintedMight, twirler.MightChanged, twirler.Gear));
        Assert.False(model.Me.Base.Single(c => c.Name == "Daring Poro").MightChanged);
    }

    [Fact]
    public void Lanes_split_the_units_by_side_and_name_who_holds_them()
    {
        var board = new TestBoard();
        board.AddLane("bf-a", TestBoard.Me);
        board.AddLane("bf-b", TestBoard.Them);
        board.AddToLane(0, "unit-a", TestBoard.Me, might: 2);
        board.AddToLane(1, "unit-b", TestBoard.Them, might: 1);
        board.AddToLane(1, "unit-a", TestBoard.Me, exhausted: true, might: 2);

        var model = board.Model();

        Assert.Equal(new[] { "Back-Alley Bar", "Altar to Unity" }, model.Lanes.Select(l => l.Card.Name));
        Assert.Equal(((PlayerId?)TestBoard.Me, 1, 0), (model.Lanes[0].Controller, model.Lanes[0].Mine.Count, model.Lanes[0].Theirs.Count));
        Assert.Equal(("Daring Poro", "Blade Twirler", true), (model.Lanes[1].Theirs[0].Name, model.Lanes[1].Mine[0].Name, model.Lanes[1].Mine[0].Exhausted));
    }

    [Fact]
    public void The_chain_names_its_cards_and_their_controllers_newest_first()
    {
        var board = new TestBoard();
        board.Chain.Add(new ChainItemView(1, ChainItemKind.Card, TestBoard.Them, ChainItemStatus.Finalized, board.Card("spell-a", TestBoard.Them),
            null, null, null, false, []));
        board.Chain.Add(new ChainItemView(2, ChainItemKind.Ability, TestBoard.Me, ChainItemStatus.Pending, null, "unit-a", "text", null, false, []));

        var model = board.Model();

        Assert.Equal(new[] { new ChainRow(2, "Blade Twirler", "marco"), new ChainRow(1, "Angle Shot", "giulia") }, model.Chain);
    }

    [Theory]
    [InlineData(1, 0, "Best of three · game 2 · you lead 1 : 0")]
    [InlineData(0, 1, "Best of three · game 2 · giulia leads 1 : 0")]
    [InlineData(1, 1, "Best of three · game 2 · 1 : 1")]
    public void The_score_line_says_who_leads(int mine, int theirs, string line)
    {
        var board = new TestBoard { MyWins = mine, TheirWins = theirs };

        Assert.Equal(line, board.Model().ScoreLine);
    }

    [Fact]
    public void The_turn_badge_and_phase_follow_the_turn()
    {
        var board = new TestBoard();
        Assert.Equal(("YOUR TURN", 5, (Phase?)Phase.Main), (board.Model().Badge, board.Model().Turn, board.Model().Phase));

        board.TurnPlayer = TestBoard.Them;
        board.Phase = Phase.Channel;
        Assert.Equal(("GIULIA'S TURN", (Phase?)Phase.Channel), (board.Model().Badge, board.Model().Phase));
    }

    [Fact]
    public void Without_a_decision_of_mine_nothing_is_clickable_and_the_button_waits()
    {
        var board = new TestBoard();
        var card = board.Add(board.Hand, "unit-b");
        board.TurnPlayer = TestBoard.Them;

        var model = board.Model(deciding: [TestBoard.Them], kind: "Priority");

        Assert.False(model.MyDecision);
        Assert.False(model.Me.Hand.Single().Clickable);
        Assert.IsType<NoStep>(model.Click(card.Id));
        Assert.Equal(("END TURN", "Waiting for giulia", false), (model.Button.Label, model.Button.Detail, model.Button.Enabled));
    }

    [Fact]
    public void Before_the_game_has_a_turn_there_is_no_badge_or_phase()
    {
        var board = new TestBoard { Stage = MatchStage.Mulligan };

        var model = board.Model();

        Assert.Equal(("", 0, (Phase?)null), (model.Badge, model.Turn, model.Phase));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`CromoBound.Client.Board` and its types don't exist).

- [ ] **Step 3: Write the implementation**

Add to `src/CromoBound.Client/_Imports.razor`:

```razor
@using CromoBound.Client.Board
@using CromoBound.Client.Board.Components
```

Create `src/CromoBound.Client/Board/CardBook.cs`:

```csharp
using CromoBound.Contracts;

namespace CromoBound.Client.Board;

/// <summary>Cards and printings by id, over the server's catalog. A card the catalog doesn't know reads as "a card", so a board drawn
/// from a newer view than the catalog still works.</summary>
public sealed class CardBook(CardCatalog catalog)
{
    public const string Unknown = "a card";

    private readonly Dictionary<string, CatalogCard> _cards = catalog.Cards.ToDictionary(c => c.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogPrinting> _printings = catalog.Printings.ToDictionary(p => p.Id, StringComparer.Ordinal);

    public CatalogCard? Card(string cardId) => _cards.GetValueOrDefault(cardId);

    public string NameOf(string cardId) => Card(cardId)?.Name ?? Unknown;

    public CatalogPrinting? PrintingOf(string printingId) => _printings.GetValueOrDefault(printingId);

    public string? DefaultPrinting(string cardId) => Card(cardId)?.DefaultPrintingId;
}
```

Create `src/CromoBound.Client/Board/BoardParts.cs`:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Board;

/// <summary>How a card is marked: a legal choice, the engine's suggestion, or chosen by the player.</summary>
public enum Ring { None, Legal, Suggested, Selected }

/// <summary>What a rune does in the payment being made.</summary>
public enum PayMark { None, Exhaust, Recycle }

/// <summary>A card as the board draws it: the view's state, the catalog's name, cost and printed might, and the board's marks.</summary>
public sealed record BoardCard(
    ObjectId Id, string CardId, string? PrintingId, string Name, CardType? Type, int? Cost, int? PrintedMight,
    bool Exhausted, bool Stunned, bool Buffed, bool Empowered, int Damage, int? Might, int Gear,
    bool Clickable = false, Ring Ring = Ring.None, PayMark Mark = PayMark.None)
{
    public bool MightChanged => Might is not null && PrintedMight is not null && Might != PrintedMight;
}

/// <summary>One player's side. The opponent's hand is only a count.</summary>
public sealed record BoardSide(
    PlayerId Player, string Name, bool IsMe, int Points, int Xp, PoolView Pool,
    BoardCard? Legend, BoardCard? Champion, IReadOnlyList<BoardCard> Base, IReadOnlyList<BoardCard> Runes,
    IReadOnlyList<BoardCard> Hand, int HandCount, int MainDeckCount, int RuneDeckCount,
    BoardCard? TrashTop, int TrashCount, IReadOnlyList<BoardCard> Banished, bool BaseIsDestination);

/// <summary>A battlefield lane: its card, who holds it, the units on each side, and whether a move may end here.</summary>
public sealed record BoardLane(
    int Index, BoardCard Card, PlayerId? Controller, bool Contested, IReadOnlyList<BoardCard> Mine, IReadOnlyList<BoardCard> Theirs,
    bool HasHidden, BoardCard? Hidden, bool IsDestination);

public sealed record ChainRow(int Id, string Name, string Controller);

/// <summary>What a click does: send an action, change the local interaction, open a card's menu, or nothing.</summary>
public abstract record BoardStep;

public sealed record SendStep(PlayerAction Action) : BoardStep;

public sealed record NextStep(Interaction Next) : BoardStep;

public sealed record MenuStep(ObjectId Card, IReadOnlyList<MenuItem> Items) : BoardStep;

public sealed record NoStep : BoardStep
{
    public static NoStep Instance { get; } = new();
}

public sealed record MenuItem(string Label, BoardStep Step);

/// <summary>A button of the match panel; it is enabled when it does something.</summary>
public sealed record BoardButton(string Label, string? Detail, BoardStep Step)
{
    public bool Enabled => Step is not NoStep;
}

/// <summary>What the player is in the middle of, kept by the page between views.</summary>
public abstract record Interaction;

public sealed record Idle : Interaction
{
    public static Idle Instance { get; } = new();
}

/// <summary>Moving these units; they pick a destination next.</summary>
public sealed record Moving(IReadOnlyList<ObjectId> Units) : Interaction;

/// <summary>Paying, with the use the player chose for each rune (runes not listed are unused).</summary>
public sealed record Paying(IReadOnlyDictionary<ObjectId, RuneUse> Uses) : Interaction;

/// <summary>A panel over the board: a pre-game step, a prompt, or the undo answer. Tasks 2 and 3 add the kinds.</summary>
public abstract record BoardPanel;
```

Create `src/CromoBound.Client/Board/BoardModel.cs`:

```csharp
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Board;

/// <summary>Everything the board draws, from the player's view, the catalog and what the player is in the middle of (spec §4). What
/// every click does is worked out here, once, so the components only draw and report clicks.</summary>
public sealed partial class BoardModel
{
    private readonly Dictionary<ObjectId, BoardStep> _cardSteps = [];
    private readonly Dictionary<int, BoardStep> _laneSteps = [];
    private BoardStep _baseStep = NoStep.Instance;
    private BoardStep _cancel = NoStep.Instance;

    private BoardModel() { }

    public BoardSide Me { get; private set; } = default!;
    public BoardSide Them { get; private set; } = default!;
    public IReadOnlyList<BoardLane> Lanes { get; private set; } = [];
    public IReadOnlyList<ChainRow> Chain { get; private set; } = [];

    /// <summary>The turn number, 0 before the first turn.</summary>
    public int Turn { get; private set; }

    public Phase? Phase { get; private set; }

    /// <summary>"YOUR TURN" or "GIULIA'S TURN"; empty before the game has a turn.</summary>
    public string Badge { get; private set; } = "";

    public string ScoreLine { get; private set; } = "";
    public BoardButton Button { get; private set; } = default!;
    public IReadOnlyList<BoardButton> Extras { get; private set; } = [];
    public string? Hint { get; private set; }
    public BoardPanel? Panel { get; private set; }

    /// <summary>The player asked for an undo and the opponent hasn't answered yet.</summary>
    public bool UndoWaiting { get; private set; }

    public bool CanRequestUndo { get; private set; }
    public IReadOnlyList<string> Log { get; private set; } = [];

    /// <summary>The engine is waiting for this player.</summary>
    public bool MyDecision { get; private set; }

    public BoardStep Click(ObjectId card) => _cardSteps.GetValueOrDefault(card, NoStep.Instance);

    public BoardStep ClickLane(int battlefield) => _laneSteps.GetValueOrDefault(battlefield, NoStep.Instance);

    public BoardStep ClickBase() => _baseStep;

    /// <summary>Esc: drops a half-made move.</summary>
    public BoardStep Cancel() => _cancel;

    public static BoardModel From(PlayerView view, CardBook book, string me, string opponent, Interaction interaction)
    {
        var model = new BoardModel();
        var build = new Build(view, book, me, opponent);
        model.MyDecision = view.Decision is not null;
        model.Turn = view.Turn?.Number ?? 0;
        model.Phase = view.Turn?.Phase;
        model.Badge = view.Turn is null ? "" : view.Turn.TurnPlayer == view.Viewer ? "YOUR TURN" : $"{opponent.ToUpperInvariant()}'S TURN";
        model.ScoreLine = ScoreLine(view, opponent);
        model.Chain = [.. view.Chain.Reverse().Select(i => new ChainRow(
            i.Id, book.NameOf(i.Card?.CardId ?? i.SourceCardId ?? ""), i.Controller == view.Viewer ? me : opponent))];
        model.Button = new BoardButton("END TURN", view.Decision is null && view.Deciding.Count > 0 ? $"Waiting for {opponent}" : null, NoStep.Instance);
        model.Decide(view, build, interaction);
        model.Me = build.Side(view.Viewer, me, model);
        model.Them = build.Side(new PlayerId(1 - view.Viewer.Index), opponent, model);
        model.Lanes = [.. view.Battlefields.Select(b => build.Lane(b, model))];
        return model;
    }

    /// <summary>Fills the clicks, marks, button and panel for the player's decision. Tasks 2 and 3 fill this in; a decision without its
    /// own handling changes nothing.</summary>
    private partial void Decide(PlayerView view, Build build, Interaction interaction);

    private static string ScoreLine(PlayerView view, string opponent)
    {
        var mine = view.Players[view.Viewer.Index].GameWins;
        var theirs = view.Players[1 - view.Viewer.Index].GameWins;
        var score = mine > theirs ? $"you lead {mine} : {theirs}" : theirs > mine ? $"{opponent} leads {theirs} : {mine}" : $"{mine} : {theirs}";
        return $"{Formats.Name(view.Format)} · game {view.GameNumber} · {score}";
    }

    // The marks the decision handling sets, read when the cards are built.
    private readonly Dictionary<ObjectId, Ring> _rings = [];
    private readonly Dictionary<ObjectId, PayMark> _marks = [];
    private readonly HashSet<int> _destinations = [];
    private bool _baseIsDestination;

    /// <summary>Turns the view's cards into board cards, with the catalog's data and the decision's marks.</summary>
    private sealed class Build(PlayerView view, CardBook book, string me, string opponent)
    {
        private readonly Dictionary<ObjectId, int> _gear = AllCards(view).Where(c => c.AttachedTo is not null)
            .GroupBy(c => c.AttachedTo!.Value).ToDictionary(g => g.Key, g => g.Count());

        public PlayerView View => view;
        public CardBook Book => book;
        public string MeName => me;
        public string Opponent => opponent;

        public BoardCard Card(CardView card, BoardModel model)
        {
            var info = book.Card(card.CardId);
            return new BoardCard(
                card.Id, card.CardId, card.PrintingId ?? info?.DefaultPrintingId, info?.Name ?? CardBook.Unknown, info?.Type, info?.Energy,
                info?.Might, card.Exhausted, card.Stunned, card.Buffed, card.Empowered, card.Damage, card.Might, _gear.GetValueOrDefault(card.Id),
                model._cardSteps.ContainsKey(card.Id), model._rings.GetValueOrDefault(card.Id), model._marks.GetValueOrDefault(card.Id));
        }

        public BoardSide Side(PlayerId player, string name, BoardModel model)
        {
            var side = view.Players[player.Index];
            var onBase = side.Base.Select(c => Card(c, model)).ToList();
            return new BoardSide(
                player, name, player == view.Viewer, side.Points, side.Xp, side.Pool,
                side.Legend.Select(c => Card(c, model)).FirstOrDefault(), side.ChampionZone.Select(c => Card(c, model)).FirstOrDefault(),
                [.. onBase.Where(c => c.Type != CardType.Rune)], [.. onBase.Where(c => c.Type == CardType.Rune)],
                [.. (side.Hand ?? []).Select(c => Card(c, model))], side.HandCount, side.MainDeckCount, side.RuneDeckCount,
                side.Trash.Count > 0 ? Card(side.Trash[^1], model) : null, side.Trash.Count,
                [.. side.Banishment.Select(c => Card(c, model))], player == view.Viewer && model._baseIsDestination);
        }

        public BoardLane Lane(BattlefieldView lane, BoardModel model) => new(
            lane.Index, Card(lane.Card, model), lane.Controller, lane.ContestedBy is not null,
            [.. lane.Units.Where(u => u.Controller == view.Viewer).Select(u => Card(u, model))],
            [.. lane.Units.Where(u => u.Controller != view.Viewer).Select(u => Card(u, model))],
            lane.HasFacedown, lane.Facedown is { } hidden ? Card(hidden, model) : null, model._destinations.Contains(lane.Index));

        public CardView? Find(ObjectId id) => AllCards(view).FirstOrDefault(c => c.Id == id);

        private static IEnumerable<CardView> AllCards(PlayerView view) =>
            view.Players.SelectMany(p => p.Legend.Concat(p.ChampionZone).Concat(p.Base).Concat(p.Hand ?? []).Concat(p.Trash).Concat(p.Banishment))
                .Concat(view.Battlefields.SelectMany(b => b.Units.Append(b.Card).Concat(b.Facedown is { } f ? [f] : [])));
    }
}
```

`BoardModel` is a `partial` class, so Tasks 2 and 3 can add `Decide` and its helpers in their own files.

For this task, create `src/CromoBound.Client/Board/BoardModel.Decide.cs` with an empty implementation; Task 2 replaces it:

```csharp
using CromoBound.Engine.Views;

namespace CromoBound.Client.Board;

public sealed partial class BoardModel
{
    private partial void Decide(PlayerView view, Build build, Interaction interaction)
    {
    }
}
```

`ActivityLog` arrives in Task 3, so `Log` stays empty until then.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, the new `BoardModelTests` included).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Client tests/CromoBound.Client.Tests
git commit -m "feat(client): build the board model from the player's view"
```

---
### Task 2: The turn: playing, menus, runes, moving and paying

**Files:**
- Create: `src/CromoBound.Client/Board/BoardPanels.cs`, `Board/BoardModel.Panels.cs`, `tests/CromoBound.Client.Tests/BoardTurnTests.cs`
- Modify: `src/CromoBound.Client/Board/BoardModel.Decide.cs` (replace)

**Interfaces:**
- Consumes:
  - Task 1's `BoardModel` (partial), its `Build` helper and its mark fields;
  - from `CromoBound.Engine.Decisions`: `PriorityDecision`, `PlayChoicesDecision`, `PayCostDecision`, `TotalCost`, `RuneOption`, `MoveOption`, `HideOption`, `ActivateOption`;
  - from `CromoBound.Engine.Actions`: `PlayCard`, `UseRune`, `RuneUse`, `StandardMove`, `Hide`, `ActivateAbility`, `Pass`, `EndTurn`, `PayCost`, `CancelPlay`.
- Produces:
  - `PlayOptionsPanel(string CardName, IReadOnlyList<PlaceOption> Locations, bool AccelerateAvailable)` and `PlaceOption(Place Place, string Label)`;
  - `BoardModel.CostText(TotalCost)`;
  - a `BoardModel.Panels.cs` holding `Other(PlayerView, Build)`, empty here, which Task 3 replaces.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Client.Tests/BoardTurnTests.cs`:

```csharp
using CromoBound.Client.Board;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Tests;

public class BoardTurnTests
{
    [Fact]
    public void A_playable_card_glows_and_a_click_plays_it()
    {
        var board = new TestBoard();
        var poro = board.Add(board.Hand, "unit-b");
        var twirler = board.Add(board.Hand, "unit-a");

        var model = board.Model(TestBoard.Priority(playable: [poro.Id]));

        Assert.Equal((true, Ring.Legal), (model.Me.Hand[0].Clickable, model.Me.Hand[0].Ring));
        Assert.Equal((false, Ring.None), (model.Me.Hand[1].Clickable, model.Me.Hand[1].Ring));
        Assert.Equal(new SendStep(new PlayCard(poro.Id)), model.Click(poro.Id));
        Assert.IsType<NoStep>(model.Click(twirler.Id));
    }

    [Fact]
    public void A_card_with_several_options_opens_its_menu()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        var unit = board.Add(board.MyBase, "unit-a", might: 2);

        var model = board.Model(TestBoard.Priority(moves: [new MoveOption(unit.Id, [Place.Battlefield(0)])], activations: [new ActivateOption(unit.Id, 0)]));

        var menu = Assert.IsType<MenuStep>(model.Click(unit.Id));
        Assert.Equal(new[] { "Move", "Use ability 1" }, menu.Items.Select(i => i.Label));
        Assert.Equal(new[] { unit.Id }, Assert.IsType<Moving>(Assert.IsType<NextStep>(menu.Items[0].Step).Next).Units);
        Assert.Equal(new SendStep(new ActivateAbility(unit.Id, 0)), menu.Items[1].Step);
    }

    [Fact]
    public void A_hidden_card_offers_to_be_played_or_hidden_at_each_battlefield()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        board.AddLane("bf-b");
        var card = board.Add(board.Hand, "spell-a");

        var model = board.Model(TestBoard.Priority(playable: [card.Id], hides: [new HideOption(card.Id, [0, 1])]));

        var menu = Assert.IsType<MenuStep>(model.Click(card.Id));
        Assert.Equal(new[] { "Play", "Hide at Back-Alley Bar", "Hide at Altar to Unity" }, menu.Items.Select(i => i.Label));
        Assert.Equal(new SendStep(new Hide(card.Id, 1)), menu.Items[2].Step);
    }

    [Fact]
    public void A_rune_always_asks_how_to_use_it()
    {
        var board = new TestBoard();
        var ready = board.Add(board.MyBase, "fury-rune");
        var spent = board.Add(board.MyBase, "order-rune", exhausted: true);

        var model = board.Model(TestBoard.Priority(runes: [new RuneOption(ready.Id, true), new RuneOption(spent.Id, false)]));

        var readyMenu = Assert.IsType<MenuStep>(model.Click(ready.Id));
        Assert.Equal(new[] { "Exhaust for 1 energy", "Recycle for 1 power" }, readyMenu.Items.Select(i => i.Label));
        Assert.Equal(new SendStep(new UseRune(ready.Id, RuneUse.Exhaust)), readyMenu.Items[0].Step);
        var spentMenu = Assert.IsType<MenuStep>(model.Click(spent.Id));
        Assert.Equal(new SendStep(new UseRune(spent.Id, RuneUse.Recycle)), Assert.Single(spentMenu.Items).Step);
    }

    [Theory]
    [InlineData(true, true, "PASS")]
    [InlineData(false, true, "END TURN")]
    public void The_big_button_passes_or_ends_the_turn(bool canPass, bool canEndTurn, string label)
    {
        var model = new TestBoard().Model(TestBoard.Priority(canPass: canPass, canEndTurn: canEndTurn));

        Assert.Equal((label, true), (model.Button.Label, model.Button.Enabled));
        Assert.Equal(canPass ? new SendStep(new Pass()) : new SendStep(new EndTurn()), model.Button.Step);
    }

    [Fact]
    public void With_neither_the_big_button_is_off()
    {
        var model = new TestBoard().Model(TestBoard.Priority(canPass: false, canEndTurn: false));

        Assert.False(model.Button.Enabled);
    }

    [Fact]
    public void Moving_a_unit_highlights_where_it_can_go_and_a_click_there_moves_it()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        board.AddLane("bf-b");
        var unit = board.Add(board.MyBase, "unit-b", might: 1);
        var priority = TestBoard.Priority(moves: [new MoveOption(unit.Id, [Place.Battlefield(1)])]);

        var model = board.Model(priority, new Moving([unit.Id]));

        Assert.Equal(Ring.Selected, model.Me.Base.Single().Ring);
        Assert.Equal(new[] { false, true }, model.Lanes.Select(l => l.IsDestination));
        Assert.False(model.Me.BaseIsDestination);
        Assert.Equal("Moving Daring Poro: pick a destination", model.Hint);
        Assert.False(model.Button.Enabled);
        var move = Assert.IsType<StandardMove>(Assert.IsType<SendStep>(model.ClickLane(1)).Action);
        Assert.Equal(new[] { unit.Id }, move.Units);
        Assert.Equal(Place.Battlefield(1), move.Destination);
        Assert.IsType<NoStep>(model.ClickLane(0));
        Assert.Equal(new NextStep(Idle.Instance), model.Cancel());
        Assert.Equal(new NextStep(Idle.Instance), model.Click(unit.Id));
    }

    [Fact]
    public void Adding_a_unit_keeps_only_the_destinations_all_share()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        board.AddLane("bf-b");
        var first = board.Add(board.MyBase, "unit-a", might: 2);
        var second = board.Add(board.MyBase, "unit-b", might: 1);
        var third = board.Add(board.MyBase, "jinx-champ", might: 3);
        var priority = TestBoard.Priority(moves:
        [
            new MoveOption(first.Id, [Place.Battlefield(0), Place.Battlefield(1)]),
            new MoveOption(second.Id, [Place.Battlefield(1)]),
            new MoveOption(third.Id, [Place.Base(TestBoard.Me)]),
        ]);

        var one = board.Model(priority, new Moving([first.Id]));
        var added = Assert.IsType<Moving>(Assert.IsType<NextStep>(one.Click(second.Id)).Next);
        Assert.Equal(new[] { first.Id, second.Id }, added.Units);
        Assert.IsType<NoStep>(one.Click(third.Id));

        var two = board.Model(priority, added);
        Assert.Equal(new[] { false, true }, two.Lanes.Select(l => l.IsDestination));
        Assert.Equal("Moving 2 units: pick a destination", two.Hint);
        var removed = Assert.IsType<Moving>(Assert.IsType<NextStep>(two.Click(second.Id)).Next);
        Assert.Equal(new[] { first.Id }, removed.Units);
        var move = Assert.IsType<StandardMove>(Assert.IsType<SendStep>(two.ClickLane(1)).Action);
        Assert.Equal(new[] { first.Id, second.Id }, move.Units);
    }

    [Fact]
    public void A_unit_can_move_to_its_base()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        var unit = board.AddToLane(0, "unit-b", TestBoard.Me, might: 1);

        var model = board.Model(TestBoard.Priority(moves: [new MoveOption(unit.Id, [Place.Base(TestBoard.Me)])]), new Moving([unit.Id]));

        Assert.True(model.Me.BaseIsDestination);
        var move = Assert.IsType<StandardMove>(Assert.IsType<SendStep>(model.ClickBase()).Action);
        Assert.Equal(Place.Base(TestBoard.Me), move.Destination);
    }

    [Fact]
    public void A_move_whose_units_can_no_longer_move_is_dropped()
    {
        var board = new TestBoard();
        var unit = board.Add(board.MyBase, "unit-b", might: 1);

        var model = board.Model(TestBoard.Priority(), new Moving([unit.Id]));

        Assert.Null(model.Hint);
        Assert.Equal(Ring.None, model.Me.Base.Single().Ring);
        Assert.True(model.Button.Enabled);
    }

    [Fact]
    public void Play_options_ask_where_the_card_enters_and_about_accelerate()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        var card = board.Add(board.Hand, "unit-a");

        var model = board.Model(new PlayChoicesDecision(TestBoard.Me, card.Id, [Place.Base(TestBoard.Me), Place.Battlefield(0)], true));

        var panel = Assert.IsType<PlayOptionsPanel>(model.Panel);
        Assert.Equal("Blade Twirler", panel.CardName);
        Assert.Equal(new[] { new PlaceOption(Place.Base(TestBoard.Me), "Your base"), new PlaceOption(Place.Battlefield(0), "Back-Alley Bar") }, panel.Locations);
        Assert.True(panel.AccelerateAvailable);
        Assert.False(model.Button.Enabled);
    }

    [Fact]
    public void Paying_starts_from_the_suggestion_and_pay_sends_it()
    {
        var board = new TestBoard();
        var a = board.Add(board.MyBase, "fury-rune");
        var b = board.Add(board.MyBase, "fury-rune");
        var c = board.Add(board.MyBase, "order-rune");
        var pay = new PayCostDecision(TestBoard.Me, new TotalCost(2, [PowerSymbol.Order]), [], new PaymentSuggestion([a.Id, b.Id], [c.Id]));

        var model = board.Model(pay);

        Assert.Equal(new[] { PayMark.Exhaust, PayMark.Exhaust, PayMark.Recycle }, model.Me.Runes.Select(r => r.Mark));
        Assert.All(model.Me.Runes, r => Assert.Equal(Ring.Suggested, r.Ring));
        Assert.Equal(("PAY", "2 energy and 1 Order"), (model.Button.Label, model.Button.Detail));
        var paid = Assert.IsType<PayCost>(Assert.IsType<SendStep>(model.Button.Step).Action);
        Assert.Equal(new[] { a.Id, b.Id }, paid.Exhaust);
        Assert.Equal(new[] { c.Id }, paid.Recycle);
        Assert.Equal(new[] { "Cancel", "Suggest" }, model.Extras.Select(e => e.Label));
        Assert.Equal(new SendStep(new CancelPlay()), model.Extras[0].Step);
    }

    [Fact]
    public void A_rune_cycles_exhaust_recycle_unused()
    {
        var board = new TestBoard();
        var ready = board.Add(board.MyBase, "fury-rune");
        var spent = board.Add(board.MyBase, "fury-rune", exhausted: true);
        var pay = new PayCostDecision(TestBoard.Me, new TotalCost(1, []), [], null);
        Paying Next(BoardModel model, ObjectId rune) => Assert.IsType<Paying>(Assert.IsType<NextStep>(model.Click(rune)).Next);

        var start = board.Model(pay);
        Assert.Equal(new[] { PayMark.None, PayMark.None }, start.Me.Runes.Select(r => r.Mark));

        var exhaust = Next(start, ready);
        Assert.Equal(RuneUse.Exhaust, exhaust.Uses[ready.Id]);
        var recycle = Next(board.Model(pay, exhaust), ready);
        Assert.Equal(RuneUse.Recycle, recycle.Uses[ready.Id]);
        var unused = Next(board.Model(pay, recycle), ready);
        Assert.False(unused.Uses.ContainsKey(ready.Id));

        Assert.Equal(RuneUse.Recycle, Next(start, spent).Uses[spent.Id]);
        Assert.Equal(new[] { "Cancel" }, start.Extras.Select(e => e.Label));
    }

    [Fact]
    public void Suggest_puts_the_suggestion_back()
    {
        var board = new TestBoard();
        var a = board.Add(board.MyBase, "fury-rune");
        var pay = new PayCostDecision(TestBoard.Me, new TotalCost(1, []), [], new PaymentSuggestion([a.Id], []));

        var model = board.Model(pay, new Paying(new Dictionary<ObjectId, RuneUse>()));

        Assert.Equal(PayMark.None, model.Me.Runes.Single().Mark);
        var back = Assert.IsType<Paying>(Assert.IsType<NextStep>(model.Extras[1].Step).Next);
        Assert.Equal(RuneUse.Exhaust, back.Uses[a.Id]);
    }

    [Theory]
    [InlineData(0, new PowerSymbol[0], "Free")]
    [InlineData(3, new PowerSymbol[0], "3 energy")]
    [InlineData(4, new[] { PowerSymbol.Fury }, "4 energy and 1 Fury")]
    [InlineData(2, new[] { PowerSymbol.Fury, PowerSymbol.Fury, PowerSymbol.Any }, "2 energy, 2 Fury and 1 of any domain")]
    [InlineData(0, new[] { PowerSymbol.Self }, "1 of its domain")]
    public void Costs_read_as_words(int energy, PowerSymbol[] power, string text)
    {
        Assert.Equal(text, BoardModel.CostText(new TotalCost(energy, power)));
    }
}
```

Records compare their lists by reference, so the tests compare a step's lists (a move's units, a payment's runes) on their own rather than whole steps.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`PlayOptionsPanel`, `PlaceOption` and `BoardModel.CostText` don't exist).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Client/Board/BoardPanels.cs`:

```csharp
using CromoBound.Engine.State;

namespace CromoBound.Client.Board;

/// <summary>Where a card being played enters, and whether Accelerate is on offer.</summary>
public sealed record PlayOptionsPanel(string CardName, IReadOnlyList<PlaceOption> Locations, bool AccelerateAvailable) : BoardPanel;

public sealed record PlaceOption(Place Place, string Label);
```

Create `src/CromoBound.Client/Board/BoardModel.Panels.cs` (Task 3 replaces it):

```csharp
using CromoBound.Engine.Views;

namespace CromoBound.Client.Board;

public sealed partial class BoardModel
{
    /// <summary>The decisions the turn doesn't handle: the pre-game steps, the prompts and waiting (Task 3).</summary>
    private void Other(PlayerView view, Build build)
    {
    }
}
```

Replace `src/CromoBound.Client/Board/BoardModel.Decide.cs` with:

```csharp
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Board;

public sealed partial class BoardModel
{
    private partial void Decide(PlayerView view, Build build, Interaction interaction)
    {
        switch (view.Decision)
        {
            case PriorityDecision priority:
                if (interaction is not Moving moving || !Move(priority, build, moving)) Priority(priority, build);
                break;
            case PlayChoicesDecision choices:
                Panel = new PlayOptionsPanel(build.Book.NameOf(build.Find(choices.Card)?.CardId ?? ""),
                    [.. choices.Locations.Select(l => new PlaceOption(l, PlaceName(l, build)))], choices.AccelerateAvailable);
                break;
            case PayCostDecision pay:
                Pay(pay, build, interaction as Paying);
                break;
            default:
                Other(view, build);
                break;
        }
    }

    /// <summary>Each card's options: one option is a click, several open a menu. A rune always opens its menu, so a stray click never
    /// spends it.</summary>
    private void Priority(PriorityDecision priority, Build build)
    {
        var items = new Dictionary<ObjectId, List<MenuItem>>();
        void Add(ObjectId card, string label, BoardStep step)
        {
            if (!items.TryGetValue(card, out var list)) items[card] = list = [];
            list.Add(new MenuItem(label, step));
        }

        foreach (var card in priority.Playable)
        {
            Add(card, "Play", new SendStep(new PlayCard(card)));
            _rings[card] = Ring.Legal;
        }
        foreach (var move in priority.Moves) Add(move.Unit, "Move", new NextStep(new Moving([move.Unit])));
        foreach (var hide in priority.Hides)
            foreach (var battlefield in hide.Battlefields)
                Add(hide.Card, $"Hide at {LaneName(battlefield, build)}", new SendStep(new Hide(hide.Card, battlefield)));
        foreach (var activation in priority.Activations)
            Add(activation.Source, $"Use ability {activation.Ability + 1}", new SendStep(new ActivateAbility(activation.Source, activation.Ability)));
        var runes = priority.Runes.Select(r => r.Rune).ToHashSet();
        foreach (var rune in priority.Runes)
        {
            if (rune.CanExhaust) Add(rune.Rune, "Exhaust for 1 energy", new SendStep(new UseRune(rune.Rune, RuneUse.Exhaust)));
            Add(rune.Rune, "Recycle for 1 power", new SendStep(new UseRune(rune.Rune, RuneUse.Recycle)));
        }
        foreach (var (card, list) in items)
            _cardSteps[card] = list.Count == 1 && !runes.Contains(card) ? list[0].Step : new MenuStep(card, list);

        Button = priority.CanPass ? new BoardButton("PASS", null, new SendStep(new Pass()))
            : priority.CanEndTurn ? new BoardButton("END TURN", null, new SendStep(new EndTurn()))
            : new BoardButton("END TURN", null, NoStep.Instance);
    }

    /// <summary>A move under way: only destinations every chosen unit shares; another unit that shares one may join; clicking a
    /// chosen unit drops it (the first one drops the whole move). False when none of the units can move any more.</summary>
    private bool Move(PriorityDecision priority, Build build, Moving moving)
    {
        var options = priority.Moves.ToDictionary(m => m.Unit);
        var units = moving.Units.Where(options.ContainsKey).ToList();
        if (units.Count == 0) return false;
        var shared = units.Skip(1).Aggregate((IEnumerable<Place>)options[units[0]].Destinations, (d, u) => d.Intersect(options[u].Destinations)).ToList();
        if (shared.Count == 0) return false;

        foreach (var unit in units)
        {
            _rings[unit] = Ring.Selected;
            _cardSteps[unit] = new NextStep(unit == units[0] ? Idle.Instance : new Moving([.. units.Where(u => u != unit)]));
        }
        foreach (var (unit, option) in options)
        {
            if (units.Contains(unit) || !option.Destinations.Intersect(shared).Any()) continue;
            _cardSteps[unit] = new NextStep(new Moving([.. units, unit]));
            _rings[unit] = Ring.Legal;
        }
        foreach (var destination in shared)
        {
            var step = new SendStep(new StandardMove { Units = units, Destination = destination });
            if (destination.Kind == PlaceKind.Battlefield && destination.Index is { } index)
            {
                _laneSteps[index] = step;
                _destinations.Add(index);
            }
            else if (destination.Kind == PlaceKind.Base)
            {
                _baseStep = step;
                _baseIsDestination = true;
            }
        }
        _cancel = new NextStep(Idle.Instance);
        Hint = units.Count == 1
            ? $"Moving {build.Book.NameOf(build.Find(units[0])?.CardId ?? "")}: pick a destination"
            : $"Moving {units.Count} units: pick a destination";
        Button = new BoardButton("END TURN", null, NoStep.Instance);
        return true;
    }

    /// <summary>Paying: each of the player's runes cycles exhaust, recycle, unused (an exhausted rune skips exhaust); the engine's
    /// suggestion is the starting point and what Suggest puts back.</summary>
    private void Pay(PayCostDecision pay, Build build, Paying? paying)
    {
        var suggested = Suggestion(pay);
        var uses = paying?.Uses ?? suggested;
        var runes = build.View.Players[build.View.Viewer.Index].Base.Where(c => build.Book.Card(c.CardId)?.Type == CardType.Rune).ToList();
        foreach (var rune in runes)
        {
            RuneUse? use = uses.TryGetValue(rune.Id, out var chosen) ? chosen : null;
            _marks[rune.Id] = use switch { RuneUse.Exhaust => PayMark.Exhaust, RuneUse.Recycle => PayMark.Recycle, _ => PayMark.None };
            if (suggested.ContainsKey(rune.Id)) _rings[rune.Id] = Ring.Suggested;
            _cardSteps[rune.Id] = new NextStep(new Paying(Cycle(uses, rune.Id, use, rune.Exhausted)));
        }
        List<ObjectId> With(RuneUse wanted) => [.. runes.Where(r => uses.TryGetValue(r.Id, out var u) && u == wanted).Select(r => r.Id)];

        Button = new BoardButton("PAY", CostText(pay.Cost), new SendStep(new PayCost { Exhaust = With(RuneUse.Exhaust), Recycle = With(RuneUse.Recycle) }));
        var cancel = new BoardButton("Cancel", null, new SendStep(new CancelPlay()));
        Extras = pay.Suggested is null ? [cancel] : [cancel, new BoardButton("Suggest", null, new NextStep(new Paying(suggested)))];
    }

    public static string CostText(TotalCost cost)
    {
        var parts = new List<string>();
        if (cost.Energy > 0) parts.Add($"{cost.Energy} energy");
        foreach (var symbol in cost.Power.GroupBy(p => p))
            parts.Add(symbol.Key switch
            {
                PowerSymbol.Any => $"{symbol.Count()} of any domain",
                PowerSymbol.Self => $"{symbol.Count()} of its domain",
                _ => $"{symbol.Count()} {symbol.Key}",
            });
        return parts.Count switch
        {
            0 => "Free",
            1 => parts[0],
            _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
        };
    }

    private static Dictionary<ObjectId, RuneUse> Suggestion(PayCostDecision pay)
    {
        var uses = new Dictionary<ObjectId, RuneUse>();
        if (pay.Suggested is not { } suggested) return uses;
        foreach (var rune in suggested.Exhaust) uses[rune] = RuneUse.Exhaust;
        foreach (var rune in suggested.Recycle) uses[rune] = RuneUse.Recycle;
        return uses;
    }

    private static Dictionary<ObjectId, RuneUse> Cycle(IReadOnlyDictionary<ObjectId, RuneUse> uses, ObjectId rune, RuneUse? current, bool exhausted)
    {
        var next = new Dictionary<ObjectId, RuneUse>(uses);
        RuneUse? after = current switch
        {
            null => exhausted ? RuneUse.Recycle : RuneUse.Exhaust,
            RuneUse.Exhaust => RuneUse.Recycle,
            _ => null,
        };
        if (after is { } use) next[rune] = use;
        else next.Remove(rune);
        return next;
    }

    private static string LaneName(int index, Build build) =>
        build.View.Battlefields.FirstOrDefault(b => b.Index == index) is { } lane ? build.Book.NameOf(lane.Card.CardId) : $"battlefield {index + 1}";

    private static string PlaceName(Place place, Build build) => place.Kind switch
    {
        PlaceKind.Base => "Your base",
        PlaceKind.Battlefield when place.Index is { } index => LaneName(index, build),
        _ => place.Kind.ToString(),
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Client tests/CromoBound.Client.Tests
git commit -m "feat(client): play, move, use runes and pay from the board model"
```

---
### Task 3: The pre-game steps, the prompts, undo, waiting and the activity log

**Files:**
- Create: `src/CromoBound.Client/Board/ActivityLog.cs`, `tests/CromoBound.Client.Tests/BoardPanelTests.cs`, `tests/CromoBound.Client.Tests/ActivityLogTests.cs`
- Modify: `src/CromoBound.Client/Board/BoardPanels.cs` (append), `Board/BoardModel.Panels.cs` (replace), `Board/BoardModel.cs` (two lines in `From`)

**Interfaces:**
- Consumes:
  - Task 1's `BoardModel` and `Build`;
  - Task 2's `BoardPanels.cs`;
  - from `CromoBound.Engine.Decisions`: `ResolveManuallyDecision`, `TurnPointDecision`, `TurnPoint`, `ConfirmUndoDecision`, `PickBattlefieldDecision`, `ChoosePlayOrderDecision`, `SideboardDecision`, `MulliganDecision`;
  - the events of `CromoBound.Engine.Events`.
- Produces:
  - the panels:
    - `ResolvePanel(string CardName, string? PrintingId, string Text)`;
    - `TurnPointPanel(string Title, IReadOnlyList<BoardCard> Cards)`;
    - `UndoPanel(string Requester, string? LastAction)`;
    - `LaterPanel(string Kind, string What)`;
    - `PickBattlefieldPanel(int GameNumber, int Games, IReadOnlyList<PrintingOption> Options, bool Waiting, string Opponent)`, with `PrintingOption(string Printing, string Name)`;
    - `PlayOrderPanel(int GameNumber, bool Waiting, string Opponent)`;
    - `SideboardPanel(int GameNumber, IReadOnlyList<DeckRow> Main, IReadOnlyList<DeckRow> Sideboard, bool Waiting, string Opponent)`, with `DeckRow(string Printing, string Name, int Count)`;
    - `MulliganPanel(IReadOnlyList<BoardCard> Hand, bool Waiting, string Opponent)`;
  - `ActivityLog.Lines(PlayerView, CardBook, string me, string opponent)` and `ActivityLog.MaxLines`;
  - `BoardModel.Log`, `CanRequestUndo` and `UndoWaiting`, now filled.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Client.Tests/BoardPanelTests.cs`:

```csharp
using CromoBound.Client.Board;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Tests;

public class BoardPanelTests
{
    [Fact]
    public void Resolve_by_hand_shows_the_card_and_its_text()
    {
        var model = new TestBoard().Model(new ResolveManuallyDecision(TestBoard.Me, 3, "unit-a", "The first time I move each turn, choose a player."));

        Assert.Equal(new ResolvePanel("Blade Twirler", "p-unit-a", "The first time I move each turn, choose a player."), model.Panel);
        Assert.False(model.Button.Enabled);
    }

    [Theory]
    [InlineData(TurnPoint.StartOfBeginning, "Start of your Beginning Phase")]
    [InlineData(TurnPoint.StartOfMain, "Start of your Main Phase")]
    [InlineData(TurnPoint.EndOfTurn, "End of your turn")]
    public void A_turn_point_lists_the_cards_to_apply_by_hand(TurnPoint point, string title)
    {
        var board = new TestBoard();
        var unit = board.Add(board.MyBase, "unit-b", might: 1);

        var panel = Assert.IsType<TurnPointPanel>(board.Model(new TurnPointDecision(TestBoard.Me, point, [unit.Id])).Panel);

        Assert.Equal(title, panel.Title);
        Assert.Equal("Daring Poro", Assert.Single(panel.Cards).Name);
    }

    [Fact]
    public void An_undo_request_names_who_asks_and_their_last_action()
    {
        var board = new TestBoard();
        var spell = board.Card("spell-a", TestBoard.Them);
        board.Log.Add(new CardPlayed(spell.Id, "spell-a", TestBoard.Them));
        board.Log.Add(new UndoRequested(TestBoard.Them));

        var model = board.Model(new ConfirmUndoDecision(TestBoard.Me, TestBoard.Them));

        Assert.Equal(new UndoPanel("giulia", "giulia played Angle Shot."), model.Panel);
    }

    [Fact]
    public void While_giulia_answers_an_undo_the_request_waits()
    {
        var board = new TestBoard();
        board.Log.Add(new UndoRequested(TestBoard.Me));

        var model = board.Model(deciding: [TestBoard.Them], kind: "ConfirmUndo");

        Assert.True(model.UndoWaiting);
        Assert.False(model.CanRequestUndo);
        Assert.Null(model.Panel);
    }

    [Fact]
    public void Undo_can_be_asked_during_a_game_once_something_happened()
    {
        var board = new TestBoard();
        Assert.False(board.Model(TestBoard.Priority()).CanRequestUndo);

        board.Log.Add(new TurnStarted(TestBoard.Me, 1));
        Assert.True(board.Model(TestBoard.Priority()).CanRequestUndo);

        board.Stage = MatchStage.Mulligan;
        Assert.False(board.Model().CanRequestUndo);
    }

    [Fact]
    public void A_choice_of_a_later_update_says_what_it_is()
    {
        var model = new TestBoard().Model(new ChooseShowdownDecision(TestBoard.Me, [0], false));

        Assert.Equal(new LaterPanel("ChooseShowdown", "choose where the showdown happens"), model.Panel);
        Assert.False(model.Button.Enabled);
    }

    [Fact]
    public void The_battlefield_pick_offers_the_unused_battlefields()
    {
        var board = new TestBoard { Stage = MatchStage.PickBattlefields };

        var model = board.Model(new PickBattlefieldDecision([TestBoard.Me, TestBoard.Them],
            [new BattlefieldChoice(TestBoard.Me, ["p-bf-b", "p-bf-c"])]));

        var panel = Assert.IsType<PickBattlefieldPanel>(model.Panel);
        Assert.Equal((2, 3, false, "giulia"), (panel.GameNumber, panel.Games, panel.Waiting, panel.Opponent));
        Assert.Equal(new[] { new PrintingOption("p-bf-b", "Altar to Unity"), new PrintingOption("p-bf-c", "Dusk Rose Lab") }, panel.Options);
    }

    [Fact]
    public void While_giulia_picks_the_board_waits_for_her()
    {
        var board = new TestBoard { Stage = MatchStage.PickBattlefields };
        var card = board.Add(board.Hand, "unit-b");

        var model = board.Model(deciding: [TestBoard.Them], kind: "PickBattlefield");

        var panel = Assert.IsType<PickBattlefieldPanel>(model.Panel);
        Assert.Equal((true, "giulia"), (panel.Waiting, panel.Opponent));
        Assert.Empty(panel.Options);
        Assert.IsType<NoStep>(model.Click(card.Id));
        Assert.False(model.Button.Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_play_order_is_asked_or_waited_for(bool waiting)
    {
        var board = new TestBoard { Stage = MatchStage.PlayOrder, GameNumber = 1 };

        var model = waiting ? board.Model(deciding: [TestBoard.Them], kind: "ChoosePlayOrder") : board.Model(new ChoosePlayOrderDecision(TestBoard.Me));

        Assert.Equal(new PlayOrderPanel(1, waiting, "giulia"), model.Panel);
    }

    [Fact]
    public void Sideboarding_lists_the_main_deck_and_the_sideboard_by_name()
    {
        var board = new TestBoard { Stage = MatchStage.Sideboarding };
        var choice = new SideboardChoice(TestBoard.Me,
            [new DeckEntry { Printing = "p-unit-a", Count = 3 }, new DeckEntry { Printing = "p-unit-b", Count = 2 }],
            [new DeckEntry { Printing = "p-spell-a", Count = 2 }], "p-jinx-champ", []);

        var panel = Assert.IsType<SideboardPanel>(board.Model(new SideboardDecision([TestBoard.Me], [choice])).Panel);

        Assert.Equal(new[] { new DeckRow("p-unit-a", "Blade Twirler", 3), new DeckRow("p-unit-b", "Daring Poro", 2) }, panel.Main);
        Assert.Equal(new[] { new DeckRow("p-spell-a", "Angle Shot", 2) }, panel.Sideboard);
        Assert.False(panel.Waiting);
    }

    [Fact]
    public void The_mulligan_shows_the_opening_hand()
    {
        var board = new TestBoard { Stage = MatchStage.Mulligan };
        var a = board.Add(board.Hand, "unit-a");
        var b = board.Add(board.Hand, "spell-a");

        var panel = Assert.IsType<MulliganPanel>(board.Model(new MulliganDecision(TestBoard.Me, [a.Id, b.Id])).Panel);

        Assert.Equal(new[] { "Blade Twirler", "Angle Shot" }, panel.Hand.Select(c => c.Name));
        Assert.False(panel.Waiting);
    }

    [Fact]
    public void The_log_is_filled()
    {
        var board = new TestBoard();
        board.Log.Add(new TurnStarted(TestBoard.Me, 5));

        Assert.Equal(new[] { "Turn 5: your turn." }, board.Model().Log);
    }
}
```

Panels holding lists are compared part by part: records compare lists by reference.

Create `tests/CromoBound.Client.Tests/ActivityLogTests.cs`:

```csharp
using CromoBound.Client.Board;
using CromoBound.Engine;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Tests;

public class ActivityLogTests
{
    private sealed record Mystery : GameEvent;

    private static IReadOnlyList<string> Lines(TestBoard board) => ActivityLog.Lines(board.View(), board.Book, "marco", "giulia");

    [Fact]
    public void Events_read_as_short_sentences_for_you_and_for_the_opponent()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        var unit = board.Add(board.MyBase, "unit-a", might: 2);
        board.Log.AddRange(
        [
            new GameStarted(1),
            new D20Rolled(TestBoard.Them, 17),
            new PlayOrderChosen(TestBoard.Them, TestBoard.Them),
            new MulliganTaken(TestBoard.Me, 0),
            new MulliganTaken(TestBoard.Them, 2),
            new TurnStarted(TestBoard.Them, 1),
            new TurnStarted(TestBoard.Me, 2),
            new CardPlayed(unit.Id, "unit-a", TestBoard.Me),
            new CardMoved("unit-a", unit.Id, unit.Id, Place.Base(TestBoard.Me), Place.Battlefield(0)),
            new BattlefieldScored(TestBoard.Me, 0, ScoreKind.Hold, true),
            new PointsChanged(TestBoard.Me, 6),
            new PointsChanged(TestBoard.Them, 1),
            new DamageDealt(unit.Id, 2),
            new UnitDied(unit.Id, "unit-a", TestBoard.Me),
            new UndoRequested(TestBoard.Them),
            new GameEnded(TestBoard.Them, GameEndReason.Concede),
        ]);

        Assert.Equal(new[]
        {
            "Game 1 begins.",
            "giulia rolled 17.",
            "giulia goes first.",
            "You kept the opening hand.",
            "giulia set aside 2 cards and drew 2.",
            "Turn 1: giulia's turn.",
            "Turn 2: your turn.",
            "You played Blade Twirler.",
            "Blade Twirler moved to Back-Alley Bar.",
            "You held Back-Alley Bar: +1 point.",
            "You now have 6 points.",
            "giulia now has 1 point.",
            "Blade Twirler took 2 damage.",
            "Blade Twirler died.",
            "giulia asked to undo the last action.",
            "giulia won the game by concession.",
        }, Lines(board));
    }

    [Fact]
    public void Draws_trash_and_banishment_are_told()
    {
        var board = new TestBoard();
        board.Log.AddRange(
        [
            new CardMoved(null, null, null, Place.MainDeck(TestBoard.Them), Place.Hand(TestBoard.Them)),
            new CardMoved("spell-a", new ObjectId(90), new ObjectId(91), Place.Chain, Place.Trash(TestBoard.Me)),
            new CardMoved("unit-b", new ObjectId(92), new ObjectId(93), Place.Base(TestBoard.Me), Place.Banishment(TestBoard.Me)),
        ]);

        Assert.Equal(new[] { "giulia drew a card.", "Angle Shot went to the trash.", "Daring Poro was banished." }, Lines(board));
    }

    [Fact]
    public void Events_that_restate_the_board_are_left_out_and_unknown_ones_show_their_type()
    {
        var board = new TestBoard();
        board.Log.AddRange(
        [
            new PhaseStarted(Phase.Main, TurnStep.None),
            new StatusChanged(new ObjectId(1), StatusKind.Exhausted, true),
            new ResourcesAdded(TestBoard.Me, 1, null),
            new ChainItemAdded(1, TestBoard.Me),
            new Mystery(),
        ]);

        Assert.Equal(new[] { "Mystery" }, Lines(board));
    }

    [Fact]
    public void Only_the_last_lines_are_kept()
    {
        var board = new TestBoard();
        for (var i = 1; i <= ActivityLog.MaxLines + 5; i++) board.Log.Add(new TurnStarted(TestBoard.Me, i));

        var lines = Lines(board);

        Assert.Equal(ActivityLog.MaxLines, lines.Count);
        Assert.Equal("Turn 6: your turn.", lines[0]);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (the panels and `ActivityLog` don't exist).

- [ ] **Step 3: Write the implementation**

Append to `src/CromoBound.Client/Board/BoardPanels.cs`:

```csharp
/// <summary>A chain item's text the engine doesn't run: the controller carries it out by hand, then presses Done.</summary>
public sealed record ResolvePanel(string CardName, string? PrintingId, string Text) : BoardPanel;

/// <summary>Cards whose start- or end-of-turn text is applied by hand, then Continue.</summary>
public sealed record TurnPointPanel(string Title, IReadOnlyList<BoardCard> Cards) : BoardPanel;

/// <summary>The opponent asks to undo their last action.</summary>
public sealed record UndoPanel(string Requester, string? LastAction) : BoardPanel;

/// <summary>A decision a later part of 4b handles (spec §6.5).</summary>
public sealed record LaterPanel(string Kind, string What) : BoardPanel;

public sealed record PickBattlefieldPanel(int GameNumber, int Games, IReadOnlyList<PrintingOption> Options, bool Waiting, string Opponent) : BoardPanel;

public sealed record PrintingOption(string Printing, string Name);

public sealed record PlayOrderPanel(int GameNumber, bool Waiting, string Opponent) : BoardPanel;

public sealed record SideboardPanel(int GameNumber, IReadOnlyList<DeckRow> Main, IReadOnlyList<DeckRow> Sideboard, bool Waiting, string Opponent) : BoardPanel;

public sealed record DeckRow(string Printing, string Name, int Count);

public sealed record MulliganPanel(IReadOnlyList<BoardCard> Hand, bool Waiting, string Opponent) : BoardPanel;
```

Replace `src/CromoBound.Client/Board/BoardModel.Panels.cs` with:

```csharp
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Board;

public sealed partial class BoardModel
{
    /// <summary>The decisions the turn doesn't handle: the pre-game steps, the prompts, the undo answer, a later part's choices, and
    /// waiting for the opponent (who decides is in <see cref="PlayerView.Deciding"/>; the decision itself only reaches its player).</summary>
    private void Other(PlayerView view, Build build)
    {
        var opponent = build.Opponent;
        var game = view.GameNumber;
        switch (view.Decision)
        {
            case ResolveManuallyDecision resolve:
                Panel = new ResolvePanel(build.Book.NameOf(resolve.CardId), build.Book.DefaultPrinting(resolve.CardId), resolve.Text);
                break;
            case TurnPointDecision point:
                Panel = new TurnPointPanel(PointTitle(point.Point), [.. point.Cards.Select(build.Find).OfType<CardView>().Select(c => build.Card(c, this))]);
                break;
            case ConfirmUndoDecision:
                Panel = new UndoPanel(opponent, ActivityLog.Lines(view, build.Book, build.MeName, opponent).LastOrDefault(l => !l.EndsWith("asked to undo the last action.", StringComparison.Ordinal)));
                break;
            case PickBattlefieldDecision pick:
                Panel = new PickBattlefieldPanel(game, Games(view), [.. pick.Choices.Where(c => c.Player == view.Viewer).SelectMany(c => c.Printings)
                    .Select(p => new PrintingOption(p, PrintingName(p, build)))], false, opponent);
                break;
            case ChoosePlayOrderDecision:
                Panel = new PlayOrderPanel(game, false, opponent);
                break;
            case SideboardDecision sideboard:
                var choice = sideboard.Choices.FirstOrDefault(c => c.Player == view.Viewer);
                Panel = new SideboardPanel(game, Rows(choice?.Main, build), Rows(choice?.Sideboard, build), false, opponent);
                break;
            case MulliganDecision mulligan:
                Panel = new MulliganPanel([.. mulligan.Hand.Select(build.Find).OfType<CardView>().Select(c => build.Card(c, this))], false, opponent);
                break;
            case null when view.Deciding.Count > 0:
                Panel = view.Stage switch
                {
                    MatchStage.PickBattlefields => new PickBattlefieldPanel(game, Games(view), [], true, opponent),
                    MatchStage.PlayOrder => new PlayOrderPanel(game, true, opponent),
                    MatchStage.Sideboarding => new SideboardPanel(game, [], [], true, opponent),
                    MatchStage.Mulligan => new MulliganPanel([], true, opponent),
                    _ => null,
                };
                UndoWaiting = view.DecisionKind == "ConfirmUndo";
                break;
            case { } later:
                Panel = new LaterPanel(view.DecisionKind ?? later.GetType().Name, What(later));
                break;
        }
    }

    private static int Games(PlayerView view) => view.Format == MatchFormat.Bo3 ? 3 : 1;

    private static string PointTitle(TurnPoint point) => point switch
    {
        TurnPoint.StartOfBeginning => "Start of your Beginning Phase",
        TurnPoint.StartOfMain => "Start of your Main Phase",
        _ => "End of your turn",
    };

    private static string What(PendingDecision decision) => decision switch
    {
        ChooseShowdownDecision => "choose where the showdown happens",
        AssignDamageDecision => "assign combat damage",
        ChooseTargetsDecision => "choose targets",
        ChoosePlayerDecision => "choose a player",
        ChooseCardsDecision => "choose cards",
        OptionalDecision => "answer a yes or no question",
        OrderTriggersDecision => "order triggered abilities",
        _ => "make a choice",
    };

    private static string PrintingName(string printing, Build build) =>
        build.Book.PrintingOf(printing) is { } known ? build.Book.NameOf(known.CardId) : CardBook.Unknown;

    private static List<DeckRow> Rows(IReadOnlyList<DeckEntry>? entries, Build build) =>
        [.. (entries ?? []).Select(e => new DeckRow(e.Printing, PrintingName(e.Printing, build), e.Count))];
}
```

The undo panel shows the last log line that isn't the undo request itself: the action being undone.

Create `src/CromoBound.Client/Board/ActivityLog.cs`:

```csharp
using CromoBound.Engine;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;

namespace CromoBound.Client.Board;

/// <summary>The view's events as short sentences, oldest first (spec §7). Events that only restate what the board shows (phases,
/// statuses, resources, chain bookkeeping, forced choices and targets, control and attachments, looks, reveals and shuffles) are left
/// out; an event type without a sentence shows its type name.</summary>
public static class ActivityLog
{
    public const int MaxLines = 200;

    public static IReadOnlyList<string> Lines(PlayerView view, CardBook book, string me, string opponent)
    {
        var cards = CardIds(view);
        var lines = new List<string>();
        foreach (var e in view.Log)
            if (Sentence(e, view, book, cards, opponent) is { } line) lines.Add(line);
        return lines.Count > MaxLines ? lines.GetRange(lines.Count - MaxLines, MaxLines) : lines;
    }

    private static string? Sentence(GameEvent e, PlayerView view, CardBook book, Dictionary<ObjectId, string> cards, string opponent)
    {
        string Who(PlayerId player) => player == view.Viewer ? "You" : opponent;
        string Verb(PlayerId player, string you, string they) => player == view.Viewer ? you : they;
        string Card(ObjectId? id) => id is { } known && cards.TryGetValue(known, out var cardId) ? book.NameOf(cardId) : CardBook.Unknown;
        string Lane(int index) => view.Battlefields.FirstOrDefault(b => b.Index == index) is { } lane ? book.NameOf(lane.Card.CardId) : $"battlefield {index + 1}";
        static string Plural(int count, string one, string many) => count == 1 ? one : many;

        return e switch
        {
            GameStarted started => $"Game {started.GameNumber} begins.",
            BattlefieldsChosen chosen => $"Battlefields: {string.Join(", ", chosen.Printings.Select(p => book.PrintingOf(p) is { } printing ? book.NameOf(printing.CardId) : CardBook.Unknown))}.",
            D20Rolled rolled => $"{Who(rolled.Player)} rolled {rolled.Value}.",
            PlayOrderChosen order => $"{Who(order.First)} {Verb(order.First, "go", "goes")} first.",
            MulliganTaken taken => taken.Count == 0
                ? $"{Who(taken.Player)} kept the opening hand."
                : $"{Who(taken.Player)} set aside {taken.Count} {Plural(taken.Count, "card", "cards")} and drew {taken.Count}.",
            SideboardChanged changed => $"{Who(changed.Player)} swapped {changed.Swaps} {Plural(changed.Swaps, "card", "cards")} with the sideboard.",
            TurnStarted turn => turn.Player == view.Viewer ? $"Turn {turn.Number}: your turn." : $"Turn {turn.Number}: {opponent}'s turn.",
            CardPlayed played => $"{Who(played.Controller)} played {book.NameOf(played.CardId)}.",
            AbilityActivated activated => $"{Who(activated.Controller)} used {Card(activated.Source)}'s ability.",
            CardMoved moved => Moved(moved, Who, Card, Lane, book),
            DamageDealt damage => $"{Card(damage.Unit)} took {damage.Amount} damage.",
            UnitDied died => $"{book.NameOf(died.CardId)} died.",
            PointsChanged points => $"{Who(points.Player)} now {Verb(points.Player, "have", "has")} {points.Points} {Plural(points.Points, "point", "points")}.",
            XpChanged xp => $"{Who(xp.Player)} now {Verb(xp.Player, "have", "has")} {xp.Xp} XP.",
            BattlefieldScored scored => scored.GainedPoint
                ? $"{Who(scored.Player)} {(scored.Kind == ScoreKind.Conquer ? "conquered" : "held")} {Lane(scored.Battlefield)}: +1 point."
                : null,
            ShowdownStarted showdown => $"A showdown started at {Lane(showdown.Battlefield)}.",
            CombatStarted combat => $"{Who(combat.Attacker)} attacked at {Lane(combat.Battlefield)}.",
            PlayCancelled => "A play was cancelled.",
            BurnedOut burned => $"{Who(burned.Player)} burned out.",
            TokenCreated token => $"A {book.NameOf(token.CardId)} token was made.",
            UndoRequested undo => $"{Who(undo.Player)} asked to undo the last action.",
            ManualActionTaken manual => $"{Who(manual.Player)} did something by hand ({manual.Kind}).",
            GameEnded ended => ended.Winner is { } winner ? $"{Who(winner)} won the game{Reason(ended.Reason)}." : "The game ended without a winner.",
            MatchEnded ended => $"{Who(ended.Winner)} won the match.",
            PhaseStarted or StatusChanged or ResourcesAdded or CostAdjusted or ChainItemAdded or ChainItemResolved or ChoiceMade or TargetsChosen
                or TriggerAdded or LegendsRevealed or GameRecorded or UnitsHealed or UnitHealed or MightModified or PoolAdjusted or ControlChanged
                or ControlGained or ShowdownEnded or CombatEnded or DeckShuffled or CardsLookedAt or CardRevealed or ChainItemCountered
                or PlayerChosen or Predicted or Attached or Detached => null,
            _ => e.GetType().Name,
        };
    }

    /// <summary>Draws, trashings, banishments and standard moves are told; other moves restate the board.</summary>
    private static string? Moved(CardMoved moved, Func<PlayerId, string> who, Func<ObjectId?, string> card, Func<int, string> lane, CardBook book)
    {
        var name = moved.CardId is { } cardId ? book.NameOf(cardId) : card(moved.From);
        return (moved.FromPlace.Kind, moved.ToPlace.Kind) switch
        {
            (PlaceKind.MainDeck, PlaceKind.Hand) when moved.ToPlace.Player is { } player => $"{who(player)} drew a card.",
            (_, PlaceKind.Trash) => $"{name} went to the trash.",
            (_, PlaceKind.Banishment) => $"{name} was banished.",
            (PlaceKind.Base or PlaceKind.Battlefield, PlaceKind.Battlefield) when moved.ToPlace.Index is { } index => $"{name} moved to {lane(index)}.",
            (PlaceKind.Battlefield, PlaceKind.Base) => $"{name} went back to its base.",
            _ => null,
        };
    }

    private static string Reason(GameEndReason reason) => reason switch
    {
        GameEndReason.Concede => " by concession",
        GameEndReason.BurnOut => " by burn out",
        _ => "",
    };

    /// <summary>The card behind each object id the viewer may know: the cards on view, and the ids the log names.</summary>
    private static Dictionary<ObjectId, string> CardIds(PlayerView view)
    {
        var cards = new Dictionary<ObjectId, string>();
        foreach (var card in view.Players.SelectMany(p => p.Legend.Concat(p.ChampionZone).Concat(p.Base).Concat(p.Hand ?? []).Concat(p.Trash).Concat(p.Banishment))
                     .Concat(view.Battlefields.SelectMany(b => b.Units.Append(b.Card))))
            cards[card.Id] = card.CardId;
        foreach (var e in view.Log)
        {
            switch (e)
            {
                case CardMoved { CardId: { } cardId } moved:
                    if (moved.From is { } from) cards.TryAdd(from, cardId);
                    if (moved.To is { } to) cards.TryAdd(to, cardId);
                    break;
                case CardPlayed played:
                    cards.TryAdd(played.Card, played.CardId);
                    break;
                case UnitDied died:
                    cards.TryAdd(died.Unit, died.CardId);
                    break;
                case TokenCreated token:
                    cards.TryAdd(token.Token, token.CardId);
                    break;
            }
        }
        return cards;
    }
}
```

In `src/CromoBound.Client/Board/BoardModel.cs`, in `From`, add after the line `model.Decide(view, build, interaction);`:

```csharp
        model.Log = ActivityLog.Lines(view, book, me, opponent);
        model.CanRequestUndo = view.Stage == MatchStage.Playing && !model.UndoWaiting && view.Log.Count > 0;
```

and add `using CromoBound.Engine.Matches;` at the top of that file.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Client tests/CromoBound.Client.Tests
git commit -m "feat(client): add the pre-game steps, prompts, undo and the activity log to the board model"
```

---
### Task 4: The board's components

**Files:**
- Create:
  - `src/CromoBound.Client/Board/Components/CardFace.razor`, `CardZoom.razor`, `CardMenu.razor`, `SideView.razor`, `LaneView.razor`, `MatchPanel.razor`, `BoardView.razor`
  - `src/CromoBound.Client/wwwroot/css/board.css`, `wwwroot/js/board.js`
  - `tests/CromoBound.Client.Tests/BoardViewTests.cs`
- Modify: `src/CromoBound.Client/wwwroot/index.html`

**Interfaces:**
- Consumes: Tasks 1 to 3's `BoardModel`, `BoardCard`, `BoardSide`, `BoardLane`, `BoardStep` and its kinds, `MenuStep` and `MenuItem`.
- Produces:
  - `BoardView`, with the parameters:
    - `Model` (`BoardModel`), `OnStep` (`EventCallback<BoardStep>`), `Locked` (`bool`);
    - `ShowXp` (`bool`), `ShowXpChanged` (`EventCallback<bool>`);
    - `CanConcede` (`bool`), `OnConcede` (`EventCallback`);
    - `ChildContent` (`RenderFragment?`), drawn over the field, for Task 5's panels.
  - `CardFace`, with `Card`, `Size`, `Style`, `Locked`, `Upright`, `OnClick` and `OnHover`.
  - The element ids tests and Task 5 rely on:
    - `#big-button`, `#request-undo`, `#concede`, `#score-line`, `#show-xp`, `#cancel-move`;
    - `.extra` buttons, with ids `extra-cancel` and `extra-suggest`;
    - `.move-here` and `.move-base`, `.cb-hand`, `.cb-zoom`, `.cb-menu`, `.cb-log li`, `.cb-chain li`.
  - `window.cromoBoard.fit(element)` and `release(element)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Client.Tests/BoardViewTests.cs`:

```csharp
using Bunit;
using CromoBound.Client.Board;
using CromoBound.Client.Board.Components;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using Microsoft.AspNetCore.Components.Web;

namespace CromoBound.Client.Tests;

public class BoardViewTests
{
    private static IRenderedComponent<BoardView> Render(Ui ui, BoardModel model, List<BoardStep> steps, bool locked = false, bool showXp = true) =>
        ui.Ctx.Render<BoardView>(ps => ps
            .Add(p => p.Model, model)
            .Add(p => p.OnStep, s => steps.Add(s))
            .Add(p => p.Locked, locked)
            .Add(p => p.ShowXp, showXp)
            .Add(p => p.CanConcede, true));

    [Fact]
    public async Task The_board_draws_both_sides_the_lanes_and_the_panel()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        board.AddLane("bf-a", TestBoard.Me);
        board.AddLane("bf-b");
        board.Add(board.Hand, "unit-a");
        board.Add(board.Hand, "spell-a");
        board.Log.Add(new TurnStarted(TestBoard.Me, 5));
        board.Chain.Add(new ChainItemView(1, ChainItemKind.Card, TestBoard.Them, ChainItemStatus.Finalized, board.Card("spell-a", TestBoard.Them),
            null, null, null, false, []));

        var cut = Render(ui, board.Model(TestBoard.Priority()), []);

        Assert.Equal("marco vs giulia", cut.Find("h1").TextContent);
        Assert.Equal(new[] { "giulia6", "You5" }, cut.FindAll(".cb-points").Select(p => string.Concat(p.TextContent.Where(c => !char.IsWhiteSpace(c)))));
        Assert.Equal(2, cut.FindAll(".cb-lane").Count);
        Assert.Equal(2, cut.FindAll(".cb-side.me .cb-hand .cb-card").Count);
        Assert.Equal(4, cut.FindAll(".cb-side.them .cb-hand .cb-back").Count);
        Assert.Equal("Best of three · game 2 · you lead 1 : 0", cut.Find("#score-line").TextContent);
        Assert.Equal("Turn 5: your turn.", cut.Find(".cb-log li").TextContent);
        Assert.Contains("Angle Shot", cut.Find(".cb-chain li").TextContent);
        Assert.Contains("YOUR TURN", cut.Find(".cb-badge").TextContent);
    }

    [Fact]
    public async Task A_card_without_an_image_shows_its_name()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        board.Add(board.MyBase, "token-bird", might: 1);
        board.Add(board.MyBase, "unit-a", might: 2);

        var cut = Render(ui, board.Model(), []);

        var bird = cut.Find("[aria-label^='Bird']");
        Assert.Empty(bird.QuerySelectorAll("img"));
        Assert.Equal("Bird", bird.QuerySelector(".nm")!.TextContent);
        var twirler = cut.Find("[aria-label^='Blade Twirler']");
        Assert.Equal("cards/img/p-unit-a", twirler.QuerySelector("img")!.GetAttribute("src"));

        twirler.QuerySelector("img")!.TriggerEvent("onerror", new ErrorEventArgs());

        cut.WaitForAssertion(() => Assert.Equal("Blade Twirler", cut.Find("[aria-label^='Blade Twirler'] .nm").TextContent));
    }

    [Fact]
    public async Task Clicking_a_playable_card_sends_its_step()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        var poro = board.Add(board.Hand, "unit-b");
        var steps = new List<BoardStep>();

        var cut = Render(ui, board.Model(TestBoard.Priority(playable: [poro.Id])), steps);
        await cut.ClickAsync("button[aria-label^='Daring Poro']");

        Assert.Equal(new BoardStep[] { new SendStep(new PlayCard(poro.Id)) }, steps);
    }

    [Fact]
    public async Task A_cards_menu_opens_and_its_item_sends()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        var rune = board.Add(board.MyBase, "fury-rune");
        var steps = new List<BoardStep>();

        var cut = Render(ui, board.Model(TestBoard.Priority(runes: [new RuneOption(rune.Id, true)])), steps);
        await cut.ClickAsync("button[aria-label^='Fury Rune']");

        Assert.Empty(steps);
        Assert.Contains("Fury Rune", cut.Find(".cb-menu").TextContent);
        await cut.ClickAsync(".cb-menu button.item");
        Assert.Equal(new BoardStep[] { new SendStep(new UseRune(rune.Id, RuneUse.Exhaust)) }, steps);
        Assert.Empty(cut.FindAll(".cb-menu"));
    }

    [Fact]
    public async Task A_destination_lane_moves_and_escape_cancels()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        board.AddLane("bf-a");
        var unit = board.Add(board.MyBase, "unit-b", might: 1);
        var steps = new List<BoardStep>();
        var model = board.Model(TestBoard.Priority(moves: [new MoveOption(unit.Id, [Place.Battlefield(0)])]), new Moving([unit.Id]));

        var cut = Render(ui, model, steps);
        Assert.Equal("Moving Daring Poro: pick a destination", cut.Find(".cb-hint span").TextContent);
        await cut.ClickAsync(".cb-lane .move-here");
        await cut.InvokeAsync(() => cut.Find(".cb-board").KeyDown(new KeyboardEventArgs { Key = "Escape" }));

        Assert.IsType<StandardMove>(Assert.IsType<SendStep>(steps[0]).Action);
        Assert.Equal(new NextStep(Idle.Instance), steps[1]);
    }

    [Fact]
    public async Task A_locked_board_sends_nothing()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        var poro = board.Add(board.Hand, "unit-b");
        var steps = new List<BoardStep>();

        var cut = Render(ui, board.Model(TestBoard.Priority(playable: [poro.Id])), steps, locked: true);

        Assert.Empty(cut.FindAll("button[aria-label^='Daring Poro']"));
        Assert.True(cut.Find("#big-button").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Hovering_a_card_zooms_it_with_its_state()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        board.Add(board.MyBase, "unit-a", damage: 2, might: 4);

        var cut = Render(ui, board.Model(), []);
        await cut.InvokeAsync(() => cut.Find("[aria-label^='Blade Twirler']").MouseEnter());

        var zoom = cut.Find(".cb-zoom");
        Assert.Contains("2 damage", zoom.TextContent);
        Assert.Contains("Might 4 (printed 2)", zoom.TextContent);
        await cut.InvokeAsync(() => cut.Find(".cb-field [aria-label^='Blade Twirler']").MouseLeave());
        Assert.Empty(cut.FindAll(".cb-zoom"));
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 0)]
    public async Task The_xp_badge_shows_only_when_on(bool showXp, int badges)
    {
        await using var ui = new Ui();

        var cut = Render(ui, new TestBoard().Model(), [], showXp: showXp);

        Assert.Equal(badges, cut.FindAll(".cb-xpbadge").Count);
    }

    [Fact]
    public async Task The_big_button_and_the_extras_send_their_steps()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        var rune = board.Add(board.MyBase, "fury-rune");
        var steps = new List<BoardStep>();
        var pay = new PayCostDecision(TestBoard.Me, new TotalCost(1, []), [], new PaymentSuggestion([rune.Id], []));

        var cut = Render(ui, board.Model(pay), steps);
        Assert.Contains("1 energy", cut.Find("#big-button").TextContent);
        await cut.ClickAsync("#big-button");
        await cut.ClickAsync("#extra-cancel");

        Assert.IsType<PayCost>(Assert.IsType<SendStep>(steps[0]).Action);
        Assert.Equal(new SendStep(new CancelPlay()), steps[1]);
    }

    [Fact]
    public async Task Request_undo_sends_it_and_waits_while_the_opponent_answers()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        board.Log.Add(new TurnStarted(TestBoard.Me, 5));
        var steps = new List<BoardStep>();

        var cut = Render(ui, board.Model(TestBoard.Priority()), steps);
        await cut.ClickAsync("#request-undo");
        Assert.Equal(new BoardStep[] { new SendStep(new RequestUndo()) }, steps);

        board.Log.Add(new UndoRequested(TestBoard.Me));
        var waiting = Render(ui, board.Model(deciding: [TestBoard.Them], kind: "ConfirmUndo"), steps);
        Assert.Equal("Waiting for giulia...", waiting.Find("#request-undo").TextContent.Trim());
        Assert.True(waiting.Find("#request-undo").HasAttribute("disabled"));
    }
}
```

`ErrorEventArgs` is `Microsoft.AspNetCore.Components.Web.ErrorEventArgs`. If bUnit 2.11's `TriggerEvent` name or signature differs, use bUnit 2's equivalent for dispatching an `onerror` event and name it in the report.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`BoardView` doesn't exist).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Client/wwwroot/js/board.js`:

```js
// Scales the 1440x900 board to fit the window (spec 5.1): the frame gets --board-scale, and the board inside is scaled by it.
window.cromoBoard = {
  fit(frame) {
    if (!frame || frame._cromoFit) return;
    const apply = () => {
      const top = frame.getBoundingClientRect().top;
      const scale = Math.min(window.innerWidth / 1440, (window.innerHeight - top) / 900);
      frame.style.setProperty('--board-scale', String(Math.max(0.3, scale)));
    };
    frame._cromoFit = apply;
    window.addEventListener('resize', apply);
    apply();
  },
  release(frame) {
    if (!frame || !frame._cromoFit) return;
    window.removeEventListener('resize', frame._cromoFit);
    frame._cromoFit = null;
  },
};
```

In `src/CromoBound.Client/wwwroot/index.html`:
- add `<link rel="stylesheet" href="css/board.css" />` after the `css/app.css` link;
- add `<script src="js/board.js"></script>` after the MudBlazor script;
- add `Barlow+Condensed:wght@500;600;700` to the Google Fonts link. Its `family=` list then reads `family=Fredoka:wght@500;600;700&amp;family=Nunito:wght@400;600;700&amp;family=Barlow+Condensed:wght@500;600;700&amp;display=swap`.

Create `src/CromoBound.Client/wwwroot/css/board.css`:

```css
/* The board (spec 5): the approved Board mock at 1440x900, scaled as one canvas by js/board.js. */
.cb-board-frame { position: relative; margin: 0 auto; width: calc(1440px * var(--board-scale, 1)); height: calc(900px * var(--board-scale, 1)); overflow: hidden; }
.cb-board { position: absolute; left: 0; top: 0; width: 1440px; height: 900px; transform: scale(var(--board-scale, 1)); transform-origin: top left; overflow: hidden; background: #090707; color: #f2e9e6; font-family: Nunito, system-ui, sans-serif; outline: none; }
.cb-field { position: absolute; left: 0; top: 0; width: 1140px; height: 900px; overflow: hidden; }
.cb-half { position: absolute; left: 0; width: 1140px; height: 450px; }
.cb-half.them { top: 0; background: #120e15; }
.cb-half.me { top: 450px; background: #170f0d; }
.cb-centerline { position: absolute; left: 0; top: 449px; width: 1140px; height: 1px; background: #4a1f1b; }
.cb-lbl { font-family: 'Barlow Condensed', Nunito, sans-serif; font-size: 12px; font-weight: 600; letter-spacing: .14em; color: #a8918d; text-transform: uppercase; }
.cb-board .cb-btn { display: inline-flex; align-items: center; justify-content: center; gap: 8px; min-height: 44px; padding: 0 16px; border-radius: 999px; font: inherit; font-weight: 600; font-size: 14px; cursor: pointer; box-sizing: border-box; }
.cb-board .cb-btn:disabled { opacity: .5; cursor: default; }
.cb-board .cb-gold { background: #d8392a; color: #ffffff; border: 1px solid #ff7a6b; }
.cb-board .cb-ghost { background: transparent; color: #ff8a7a; border: 1px solid #5a2420; }
.cb-board .cb-text { background: transparent; color: #ff8a7a; border: 1px solid transparent; }

/* Cards */
.cb-card { position: relative; flex: none; width: 76px; height: 106px; padding: 0; border-radius: 6px; background: #1d1516; border: 1px solid #3a2a2b; box-sizing: border-box; display: flex; flex-direction: column; overflow: hidden; color: #f2e9e6; font-family: 'Barlow Condensed', Nunito, sans-serif; text-align: left; box-shadow: 0 4px 10px rgba(0,0,0,.45); }
button.cb-card { cursor: pointer; font: inherit; }
.cb-card img { width: 100%; height: 100%; object-fit: cover; display: block; }
.cb-card.hand { width: 104px; height: 146px; }
.cb-card.rune { width: 44px; height: 62px; border-radius: 5px; }
.cb-card.pile { width: 64px; height: 88px; }
.cb-card.field { width: 170px; height: 110px; border-radius: 16px; }
.cb-card.zoom { width: 300px; height: 418px; border-radius: 14px; }
.cb-card.exhausted { rotate: 90deg; }
.cb-card.rune.exhausted { opacity: .55; }
.cb-card.ring-legal { border-color: #ff7a6b; box-shadow: 0 0 0 1px #ff7a6b, 0 0 16px rgba(255,122,107,.5); }
.cb-card.ring-suggested { outline: 2px solid #ff7a6b; outline-offset: 3px; }
.cb-card.ring-selected { outline: 2px solid #ff7a6b; outline-offset: 3px; translate: 0 -10px; box-shadow: 0 0 0 1px #ff7a6b, 0 0 16px rgba(255,122,107,.5); }
.cb-card .ph { flex: 1; display: flex; flex-direction: column; justify-content: flex-end; background: #241a1b; }
.cb-card .nm { padding: 3px 5px 4px; font-size: 11px; font-weight: 600; line-height: 1.1; background: #140f10; min-height: 26px; box-sizing: border-box; }
.cb-card.hand .nm { font-size: 12px; }
.cb-card.field .nm { font-family: Fredoka, sans-serif; font-weight: 700; font-size: 13px; text-align: center; }
.cb-card .cost { position: absolute; top: 4px; left: 4px; width: 20px; height: 20px; border-radius: 50%; background: #0b0909; border: 1px solid #d8392a; color: #ff7a6b; font-size: 12px; font-weight: 700; display: flex; align-items: center; justify-content: center; }
.cb-card .might { position: absolute; right: 4px; bottom: 30px; min-width: 20px; height: 20px; padding: 0 4px; border-radius: 5px; background: #0b0909; border: 1px solid #a8918d; font-size: 12px; font-weight: 700; display: flex; align-items: center; justify-content: center; box-sizing: border-box; }
.cb-card .might.changed { border-color: #ff7a6b; color: #ff7a6b; }
.cb-card .dmg { position: absolute; left: 4px; bottom: 30px; min-width: 20px; height: 20px; border-radius: 50%; background: #b8284f; border: 1px solid #e0507a; color: #ffffff; font-size: 12px; font-weight: 700; display: flex; align-items: center; justify-content: center; }
.cb-card .chips { position: absolute; left: 4px; right: 4px; top: 28px; display: flex; flex-wrap: wrap; gap: 2px; }
.cb-card .chip { padding: 0 4px; border-radius: 4px; background: rgba(14,9,9,.85); font-size: 10px; font-weight: 600; }
.cb-card .mark { position: absolute; right: -2px; top: -2px; min-width: 20px; height: 20px; padding: 0 4px; box-sizing: border-box; border-radius: 999px; border: 1px solid; color: #ffffff; font-family: Nunito, sans-serif; font-size: 11px; font-weight: 700; display: flex; align-items: center; justify-content: center; }
.cb-card .mark.exhaust { background: #d8392a; border-color: #ff7a6b; }
.cb-card .mark.recycle { background: #6a4a8e; border-color: #a56bd6; }
.cb-back { position: absolute; width: 70px; height: 98px; border-radius: 6px; background: #1a1213; border: 1px solid #5a2420; box-sizing: border-box; box-shadow: 0 4px 10px rgba(0,0,0,.45); }

/* Sides */
.cb-side > * { position: absolute; }
.cb-points { width: 96px; height: 84px; box-sizing: border-box; border-radius: 24px; border: 2px solid; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 1px; }
.cb-points .who { font-family: 'Barlow Condensed', sans-serif; font-size: 12px; font-weight: 600; letter-spacing: .12em; text-transform: uppercase; color: #d7c7c3; }
.cb-points .n { font-family: Fredoka, sans-serif; font-weight: 700; font-size: 32px; line-height: 1; color: #fff4f1; }
.cb-points .pips { display: flex; gap: 3px; margin-top: 3px; }
.cb-points .pip { width: 6px; height: 6px; border-radius: 50%; box-sizing: border-box; border: 1px solid #5a2420; }
.me .cb-points { left: 24px; top: 466px; border-color: #d8392a; background: radial-gradient(circle at 30% 20%, #3a1714, #140c0c); box-shadow: 0 0 0 4px rgba(216,57,42,.14), 0 8px 18px rgba(0,0,0,.5); }
.me .cb-points .pip.on { background: #ff7a6b; border-color: #ff7a6b; }
.them .cb-points { left: 1020px; top: 350px; border-color: #a56bd6; background: radial-gradient(circle at 30% 20%, #3a2450, #140d19); box-shadow: 0 0 0 4px rgba(165,107,214,.12), 0 8px 18px rgba(0,0,0,.5); }
.them .cb-points .pip.on { background: #a56bd6; border-color: #a56bd6; }
.cb-pool { display: flex; flex-direction: column; gap: 4px; padding: 8px 12px; border-radius: 16px; background: rgba(14,9,9,.6); border: 1px solid #3a2222; font-size: 14px; color: #d7c7c3; }
.me .cb-pool { left: 24px; top: 572px; }
.them .cb-pool { left: 950px; top: 236px; }
.cb-base { width: 730px; height: 130px; }
.me .cb-base { left: 200px; top: 600px; }
.them .cb-base { left: 200px; top: 170px; }
.cb-base .row { position: absolute; left: 0; top: 22px; display: flex; align-items: flex-start; gap: 40px; }
.cb-base .runes, .cb-base .units { display: flex; align-items: flex-start; gap: 8px; }
.cb-base .units { gap: 14px; }
.cb-base .move-base { position: absolute; right: 0; top: 0; min-height: 36px; font-size: 13px; }
.me .cb-legend { left: 958px; top: 636px; }
.me .cb-champion { left: 1042px; top: 636px; }
.them .cb-legend { left: 106px; top: 158px; }
.them .cb-champion { left: 22px; top: 158px; }
.cb-xpbadge { position: absolute; top: 4px; left: 50%; translate: -50% 0; z-index: 1; padding: 1px 8px; border-radius: 999px; background: #0b0909; border: 1px solid #e3c34a; color: #f2e19b; font-size: 11px; font-weight: 700; letter-spacing: .06em; white-space: nowrap; }
.cb-pilebox { width: 64px; height: 88px; }
.cb-pilebox .empty { position: absolute; inset: 0; border-radius: 6px; border: 1px dashed #3a2a2b; background: rgba(14,9,9,.4); }
.cb-pilebox.deck { border-radius: 6px; background: #1a1213; border: 1px solid #5a2420; box-sizing: border-box; box-shadow: 2px 2px 0 #241a1b, 4px 4px 0 #1a1314, 0 6px 12px rgba(0,0,0,.5); }
.cb-pilebox.deck.runes { border-color: #4a3466; }
.cb-pilebox .cap { position: absolute; left: 0; right: 0; bottom: 0; padding: 3px 0; text-align: center; font-family: 'Barlow Condensed', sans-serif; font-size: 11px; font-weight: 600; letter-spacing: .12em; text-transform: uppercase; color: #d7c7c3; background: rgba(14,9,9,.82); border-radius: 0 0 5px 5px; }
.cb-pilebox .count { position: absolute; right: -8px; bottom: -8px; z-index: 1; min-width: 28px; height: 24px; padding: 0 6px; border-radius: 6px; background: #0b0909; border: 1px solid #b8463a; color: #fff4f1; font-family: 'Barlow Condensed', sans-serif; font-weight: 700; font-size: 15px; display: flex; align-items: center; justify-content: center; box-sizing: border-box; }
.me .cb-banished { left: 1000px; top: 520px; }
.me .cb-trash { left: 958px; top: 790px; }
.me .cb-deck { left: 1046px; top: 790px; }
.me .cb-runedeck { left: 240px; top: 800px; }
.them .cb-banished { left: 76px; top: 292px; }
.them .cb-trash { left: 118px; top: 22px; }
.them .cb-deck { left: 30px; top: 22px; }
.them .cb-runedeck { left: 836px; top: 12px; }
.me .cb-hand { left: 330px; top: 772px; width: 600px; height: 160px; }
.them .cb-hand { left: 430px; top: -34px; width: 300px; height: 110px; }
.cb-hand > .cb-card { position: absolute; }

/* Lanes */
.cb-lane { position: absolute; top: 312px; width: 360px; height: 276px; border-radius: 18px; background: rgba(14,9,9,.35); }
.cb-lane.dest { outline: 2px dashed #ff7a6b; outline-offset: -2px; background: rgba(224,74,58,.08); }
.cb-lane .field-card { position: absolute; left: 95px; top: 83px; }
.cb-lane .theirs, .cb-lane .mine { position: absolute; left: 0; right: 0; display: flex; justify-content: center; gap: 10px; }
.cb-lane .theirs { top: 11px; }
.cb-lane .mine { top: 159px; }
.cb-lane .hidden { position: absolute; left: 278px; top: 18px; }
.cb-lane .owner, .cb-lane .contested { position: absolute; top: 124px; padding: 3px 10px; border-radius: 24px; font-size: 11px; font-weight: 600; }
.cb-lane .owner { left: 8px; background: #26160f; color: #f5b5ab; }
.cb-lane .owner.theirs-owner { background: #1c1626; color: #d9bdf2; }
.cb-lane .contested { right: 8px; background: #3a2414; color: #f6c08a; letter-spacing: .06em; }
.cb-lane .move-here { position: absolute; left: 50%; bottom: 10px; translate: -50% 0; min-height: 36px; padding: 0 14px; font-size: 13px; }

/* The hint, the card menu and the zoom */
.cb-hint { position: absolute; left: 570px; top: 432px; translate: -50% 0; z-index: 4; display: flex; align-items: center; gap: 10px; padding: 6px 8px 6px 16px; border-radius: 999px; background: #2a1312; border: 1px solid #b8463a; box-shadow: 0 6px 18px rgba(0,0,0,.55); font-size: 14px; white-space: nowrap; }
.cb-hint .cb-btn { min-height: 28px; padding: 0 10px; font-size: 13px; }
.cb-menu { position: absolute; left: 460px; top: 330px; z-index: 5; width: 220px; padding: 6px; background: #221819; border: 1px solid #b8463a; border-radius: 16px; box-shadow: 0 10px 28px rgba(0,0,0,.6); display: flex; flex-direction: column; }
.cb-menu .cb-lbl { padding: 6px 10px; }
.cb-menu .item { text-align: left; min-height: 40px; padding: 0 10px; border: 0; border-radius: 8px; background: transparent; color: #f2e9e6; font: inherit; cursor: pointer; }
.cb-menu .item:hover, .cb-menu .item:focus-visible { background: rgba(224,74,58,.14); }
.cb-menu .item.close { color: #ff8a7a; }
.cb-zoom { position: absolute; left: 820px; top: 30px; z-index: 6; display: flex; flex-direction: column; gap: 8px; pointer-events: none; }
.cb-zoom ul { margin: 0; padding: 8px 12px; list-style: none; border-radius: 14px; background: rgba(14,9,9,.9); border: 1px solid #3a2a2b; font-size: 14px; }

/* The match panel */
.cb-side-panel { position: absolute; right: 0; top: 0; width: 300px; height: 900px; box-sizing: border-box; padding: 14px; background: #140e0f; border-left: 1px solid #1f1718; display: flex; flex-direction: column; gap: 12px; }
.cb-side-panel .head { position: relative; display: flex; align-items: flex-start; justify-content: space-between; gap: 8px; }
.cb-side-panel .turn { font-family: Fredoka, sans-serif; font-weight: 700; font-size: 18px; letter-spacing: .06em; }
.cb-side-panel .score { display: block; font-size: 12px; color: #a8918d; }
.cb-badge { margin-left: 8px; padding: 2px 10px; border-radius: 999px; font-size: 11px; font-weight: 700; letter-spacing: .08em; background: rgba(224,74,58,.18); color: #ff7a6b; border: 1px solid #b8463a; }
.cb-badge.theirs { background: #1c1626; color: #d9bdf2; border-color: #4a3466; }
.cb-side-panel .gear { width: 36px; height: 36px; flex: none; border-radius: 50%; border: 1px solid #5a2420; background: transparent; color: #ff8a7a; cursor: pointer; display: flex; align-items: center; justify-content: center; }
.cb-settings { position: absolute; right: 0; top: 44px; z-index: 5; width: 230px; padding: 8px; background: #221819; border: 1px solid #b8463a; border-radius: 16px; box-shadow: 0 10px 28px rgba(0,0,0,.6); }
.cb-settings label { display: flex; align-items: center; justify-content: space-between; gap: 10px; min-height: 40px; padding: 0 8px; font-size: 14px; cursor: pointer; }
.cb-settings input { width: 18px; height: 18px; accent-color: #d8392a; }
.cb-phases { margin: 0; padding: 0; list-style: none; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 4px; }
.cb-phases li { padding: 5px 0; text-align: center; border-radius: 24px; font-family: 'Barlow Condensed', sans-serif; font-size: 13px; font-weight: 600; letter-spacing: .04em; color: #a8918d; border: 1px solid #2a2021; }
.cb-phases li.current { background: rgba(224,74,58,.18); color: #ff7a6b; border-color: #b8463a; }
.cb-chain { margin: 0; padding: 8px; list-style: none; border-radius: 16px; background: #1c1626; border: 1px solid #6a4a8e; display: flex; flex-direction: column; gap: 4px; font-size: 14px; }
.cb-chain .who { display: block; font-size: 12px; color: #b7a3cf; }
#big-button { min-height: 56px; border-radius: 16px; border: 1px solid #ff7a6b; background: #d8392a; color: #ffffff; font-family: Fredoka, sans-serif; font-weight: 700; font-size: 18px; letter-spacing: .1em; cursor: pointer; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 2px; }
#big-button:disabled { background: #161112; color: #5e4f4d; border-color: #2a2021; cursor: default; }
#big-button small { font-family: Nunito, sans-serif; font-weight: 600; font-size: 12px; letter-spacing: .02em; }
.cb-side-panel .pair { display: flex; gap: 8px; }
.cb-side-panel .pair .cb-btn { flex: 1; }
.cb-side-panel #concede { color: #ff9db0; }
.cb-log { flex: 1; min-height: 0; padding: 10px; border-radius: 20px; background: #171112; border: 1px solid #2a2021; display: flex; flex-direction: column; gap: 8px; }
.cb-log h2 { margin: 0; }
.cb-log ol { margin: 0; padding: 0; list-style: none; flex: 1; min-height: 0; overflow: auto; display: flex; flex-direction: column-reverse; gap: 6px; font-size: 13px; color: #d7c7c3; }
.cb-log li { border-left: 2px solid #b8463a; padding-left: 8px; }
.cb-board.locked .cb-field { cursor: progress; }
```

Create `src/CromoBound.Client/Board/Components/CardFace.razor`:

```razor
@if (Card.Clickable && !Locked && OnClick.HasDelegate)
{
    <button type="button" class="@Classes" style="@Style" aria-label="@Label" @onclick="() => OnClick.InvokeAsync(Card)"
            @onmouseenter="() => OnHover.InvokeAsync(Card)" @onmouseleave="() => OnHover.InvokeAsync(null)">@Face</button>
}
else
{
    <div class="@Classes" style="@Style" role="img" aria-label="@Label"
         @onmouseenter="() => OnHover.InvokeAsync(Card)" @onmouseleave="() => OnHover.InvokeAsync(null)">@Face</div>
}

@code {
    [Parameter, EditorRequired] public BoardCard Card { get; set; } = default!;

    /// <summary>"", "hand", "rune", "pile", "field" or "zoom".</summary>
    [Parameter] public string Size { get; set; } = "";

    [Parameter] public string? Style { get; set; }
    [Parameter] public bool Locked { get; set; }

    /// <summary>Drawn upright even when exhausted (the zoom).</summary>
    [Parameter] public bool Upright { get; set; }

    [Parameter] public EventCallback<BoardCard> OnClick { get; set; }
    [Parameter] public EventCallback<BoardCard?> OnHover { get; set; }

    private bool imageFailed;
    private string? shownPrinting;

    protected override void OnParametersSet()
    {
        if (shownPrinting == Card.PrintingId) return;
        shownPrinting = Card.PrintingId;
        imageFailed = false;
    }

    private bool ShowsImage => Card.PrintingId is not null && !imageFailed;

    private string Classes => string.Join(' ', new[]
    {
        "cb-card", Size, !Upright && Card.Exhausted ? "exhausted" : "",
        Card.Ring switch { Ring.Legal => "ring-legal", Ring.Suggested => "ring-suggested", Ring.Selected => "ring-selected", _ => "" },
    }.Where(c => c.Length > 0));

    private string Label
    {
        get
        {
            var parts = new List<string> { Card.Name };
            if (Card.Exhausted) parts.Add("exhausted");
            if (Card.Damage > 0) parts.Add($"{Card.Damage} damage");
            if (Card.Might is { } might) parts.Add($"might {might}");
            if (Card.Stunned) parts.Add("stunned");
            if (Card.Buffed) parts.Add("buffed");
            if (Card.Empowered) parts.Add("empowered");
            if (Card.Gear > 0) parts.Add($"{Card.Gear} gear attached");
            if (Card.Mark == PayMark.Exhaust) parts.Add("exhaust to pay");
            if (Card.Mark == PayMark.Recycle) parts.Add("recycle to pay");
            return string.Join(", ", parts);
        }
    }

    private RenderFragment Face => @<text>
        @if (ShowsImage)
        {
            <img src="cards/img/@Card.PrintingId" alt="" loading="lazy" @onerror="() => imageFailed = true" />
        }
        else
        {
            <span class="ph"><span class="nm">@Card.Name</span></span>
            @if (Card.Cost is { } cost)
            {
                <span class="cost">@cost</span>
            }
        }
        @if (Card.Might is { } might && (!ShowsImage || Card.MightChanged))
        {
            <span class="might @(Card.MightChanged ? "changed" : "")">@might</span>
        }
        @if (Card.Damage > 0)
        {
            <span class="dmg">@Card.Damage</span>
        }
        @if (Card.Stunned || Card.Buffed || Card.Empowered || Card.Gear > 0)
        {
            <span class="chips">
                @if (Card.Stunned) { <span class="chip">Stunned</span> }
                @if (Card.Buffed) { <span class="chip">Buffed</span> }
                @if (Card.Empowered) { <span class="chip">Empowered</span> }
                @if (Card.Gear > 0) { <span class="chip">+@Card.Gear gear</span> }
            </span>
        }
        @if (Card.Mark != PayMark.None)
        {
            <span class="mark @(Card.Mark == PayMark.Exhaust ? "exhaust" : "recycle")" aria-hidden="true">@(Card.Mark == PayMark.Exhaust ? "E" : "R")</span>
        }
    </text>;
}
```

Create `src/CromoBound.Client/Board/Components/CardZoom.razor`:

```razor
<div class="cb-zoom" aria-hidden="true">
    <CardFace Card="Card" Size="zoom" Upright="true" />
    @if (Lines.Count > 0)
    {
        <ul>
            @foreach (var line in Lines)
            {
                <li>@line</li>
            }
        </ul>
    }
</div>

@code {
    [Parameter, EditorRequired] public BoardCard Card { get; set; } = default!;

    private List<string> Lines
    {
        get
        {
            var lines = new List<string>();
            if (Card.Exhausted) lines.Add("Exhausted");
            if (Card.Damage > 0) lines.Add($"{Card.Damage} damage");
            if (Card.MightChanged) lines.Add($"Might {Card.Might} (printed {Card.PrintedMight})");
            if (Card.Stunned) lines.Add("Stunned");
            if (Card.Buffed) lines.Add("Buffed");
            if (Card.Empowered) lines.Add("Empowered");
            if (Card.Gear > 0) lines.Add($"{Card.Gear} gear attached");
            return lines;
        }
    }
}
```

Create `src/CromoBound.Client/Board/Components/CardMenu.razor`:

```razor
<div class="cb-menu" role="menu" aria-label="@Title">
    <span class="cb-lbl">@Title</span>
    @foreach (var item in Menu.Items)
    {
        <button type="button" role="menuitem" class="item" @onclick="() => OnPick.InvokeAsync(item)">@item.Label</button>
    }
    <button type="button" role="menuitem" class="item close" @onclick="OnClose">Close</button>
</div>

@code {
    [Parameter, EditorRequired] public MenuStep Menu { get; set; } = default!;
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public EventCallback<MenuItem> OnPick { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
}
```

Create `src/CromoBound.Client/Board/Components/SideView.razor`:

```razor
<div class="cb-side @(Side.IsMe ? "me" : "them")">
    <div class="cb-points" role="img" aria-label="@($"{Whose} points: {Side.Points} of 8")">
        <span class="who">@(Side.IsMe ? "You" : Side.Name)</span>
        <span class="n">@Side.Points</span>
        <span class="pips" aria-hidden="true">
            @for (var i = 0; i < 8; i++)
            {
                <span class="pip @(i < Side.Points ? "on" : "")"></span>
            }
        </span>
    </div>
    <div class="cb-pool" role="group" aria-label="@($"{Whose} floating energy and power")">
        <span class="cb-lbl">Floating</span>
        <span>@PoolText</span>
    </div>
    <div class="cb-base" role="group" aria-label="@($"{Whose} base")">
        <span class="cb-lbl">@(Side.IsMe ? "Base · Runes" : "Base")</span>
        <div class="row">
            <div class="runes">
                @foreach (var rune in Side.Runes)
                {
                    <CardFace @key="rune.Id" Card="rune" Size="rune" Locked="Locked" OnClick="OnCard" OnHover="OnHover" />
                }
            </div>
            <div class="units">
                @foreach (var card in Side.Base)
                {
                    <CardFace @key="card.Id" Card="card" Locked="Locked" OnClick="OnCard" OnHover="OnHover" />
                }
            </div>
        </div>
        @if (Side.BaseIsDestination && !Locked)
        {
            <button type="button" class="cb-btn cb-gold move-base" @onclick="OnBase">Move here</button>
        }
    </div>
    @if (Side.Legend is { } legend)
    {
        <div class="cb-legend">
            <CardFace Card="legend" Locked="Locked" OnClick="OnCard" OnHover="OnHover" />
            @if (ShowXp)
            {
                <span class="cb-xpbadge">XP @Side.Xp</span>
            }
        </div>
    }
    @if (Side.Champion is { } champion)
    {
        <div class="cb-champion"><CardFace Card="champion" Locked="Locked" OnClick="OnCard" OnHover="OnHover" /></div>
    }
    <div class="cb-banished cb-pilebox">
        @if (Side.Banished.Count > 0)
        {
            <CardFace Card="Side.Banished[^1]" Size="pile" OnHover="OnHover" />
        }
        else
        {
            <span class="empty" role="img" aria-label="@($"{Whose} banished cards, none")"></span>
        }
        <span class="cap">Banished</span>
        <span class="count">@Side.Banished.Count</span>
    </div>
    <div class="cb-trash cb-pilebox">
        @if (Side.TrashTop is { } top)
        {
            <CardFace Card="top" Size="pile" OnHover="OnHover" />
        }
        else
        {
            <span class="empty" role="img" aria-label="@($"{Whose} trash, empty")"></span>
        }
        <span class="cap">Trash</span>
        <span class="count">@Side.TrashCount</span>
    </div>
    <div class="cb-deck cb-pilebox deck" role="img" aria-label="@($"{Whose} main deck, {Side.MainDeckCount} cards")">
        <span class="count">@Side.MainDeckCount</span>
    </div>
    <div class="cb-runedeck cb-pilebox deck runes" role="img" aria-label="@($"{Whose} rune deck, {Side.RuneDeckCount} runes")">
        <span class="count">@Side.RuneDeckCount</span>
    </div>
    <div class="cb-hand" role="group" aria-label="@($"{Whose} hand, {Side.HandCount} cards")">
        @if (Side.IsMe)
        {
            @for (var i = 0; i < Side.Hand.Count; i++)
            {
                var card = Side.Hand[i];
                <CardFace @key="card.Id" Card="card" Size="hand" Upright="true" Style="@HandStyle(i, Side.Hand.Count, card)" Locked="Locked" OnClick="OnCard" OnHover="OnHover" />
            }
        }
        else
        {
            @for (var i = 0; i < Side.HandCount; i++)
            {
                <span class="cb-back" aria-hidden="true" style="@BackStyle(i, Side.HandCount)"></span>
            }
        }
    </div>
</div>

@code {
    [Parameter, EditorRequired] public BoardSide Side { get; set; } = default!;
    [Parameter] public bool Locked { get; set; }
    [Parameter] public bool ShowXp { get; set; }
    [Parameter] public EventCallback<BoardCard> OnCard { get; set; }
    [Parameter] public EventCallback<BoardCard?> OnHover { get; set; }
    [Parameter] public EventCallback OnBase { get; set; }

    private string Whose => Side.IsMe ? "Your" : $"{Side.Name}'s";

    private string PoolText
    {
        get
        {
            var parts = new List<string> { $"Energy {Side.Pool.Energy}" };
            parts.AddRange(Side.Pool.Power.Where(p => p.Value > 0).Select(p => $"{p.Key} {p.Value}"));
            if (Side.Pool.UniversalPower > 0) parts.Add($"Any {Side.Pool.UniversalPower}");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>The hand fans out; it closes up when it holds many cards. A chosen card rises.</summary>
    private static string HandStyle(int index, int count, BoardCard card)
    {
        var step = count <= 5 ? 96 : Math.Max(40, 480 / (count - 1));
        var middle = (count - 1) / 2.0;
        var left = 40 + index * step;
        var top = card.Ring == Ring.Selected ? -24 : 10 + Math.Abs(index - middle) * 6;
        var turn = (index - middle) * 5;
        return FormattableString.Invariant($"left: {left}px; top: {top}px; rotate: {turn}deg");
    }

    private static string BackStyle(int index, int count)
    {
        var middle = (count - 1) / 2.0;
        return FormattableString.Invariant($"left: {60 + index * 44}px; top: {Math.Abs(index - middle) * 6}px; rotate: {(middle - index) * 5}deg");
    }
}
```

Create `src/CromoBound.Client/Board/Components/LaneView.razor`:

```razor
@using CromoBound.Engine.State

<div class="cb-lane @(Lane.IsDestination ? "dest" : "")" style="left: @(200 + Lane.Index * 380)px" role="group" aria-label="@Lane.Card.Name">
    <div class="field-card"><CardFace Card="Lane.Card" Size="field" Locked="Locked" OnClick="OnCard" OnHover="OnHover" /></div>
    <div class="theirs">
        @foreach (var unit in Lane.Theirs)
        {
            <CardFace @key="unit.Id" Card="unit" Locked="Locked" OnClick="OnCard" OnHover="OnHover" />
        }
    </div>
    @if (Lane.HasHidden)
    {
        <div class="hidden">
            @if (Lane.Hidden is { } hidden)
            {
                <CardFace Card="hidden" Size="pile" Locked="Locked" OnClick="OnCard" OnHover="OnHover" />
            }
            else
            {
                <span class="cb-back" role="img" aria-label="A hidden card" style="position: relative; display: block; width: 64px; height: 88px"></span>
            }
        </div>
    }
    <div class="mine">
        @foreach (var unit in Lane.Mine)
        {
            <CardFace @key="unit.Id" Card="unit" Locked="Locked" OnClick="OnCard" OnHover="OnHover" />
        }
    </div>
    @if (Lane.Controller is { } controller)
    {
        <span class="owner @(controller == Me ? "" : "theirs-owner")">@(controller == Me ? "Yours" : $"{Opponent}'s")</span>
    }
    @if (Lane.Contested)
    {
        <span class="contested">CONTESTED</span>
    }
    @if (Lane.IsDestination && !Locked)
    {
        <button type="button" class="cb-btn cb-gold move-here" @onclick="() => OnLane.InvokeAsync(Lane.Index)">Move here</button>
    }
</div>

@code {
    [Parameter, EditorRequired] public BoardLane Lane { get; set; } = default!;
    [Parameter] public PlayerId Me { get; set; }
    [Parameter] public string Opponent { get; set; } = "";
    [Parameter] public bool Locked { get; set; }
    [Parameter] public EventCallback<BoardCard> OnCard { get; set; }
    [Parameter] public EventCallback<BoardCard?> OnHover { get; set; }
    [Parameter] public EventCallback<int> OnLane { get; set; }
}
```

Create `src/CromoBound.Client/Board/Components/MatchPanel.razor`:

```razor
@using CromoBound.Engine.Actions
@using CromoBound.Models.Effects

<aside class="cb-side-panel" aria-label="Match panel">
    <div class="head">
        <div>
            <span class="turn">Turn @Model.Turn</span>
            @if (Model.Badge.Length > 0)
            {
                <span class="cb-badge @(Model.Badge == "YOUR TURN" ? "" : "theirs")" role="status">@Model.Badge</span>
            }
            <span id="score-line" class="score">@Model.ScoreLine</span>
        </div>
        <button type="button" class="gear" aria-label="Board settings" aria-expanded="@(settingsOpen ? "true" : "false")" @onclick="() => settingsOpen = !settingsOpen">
            <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="12" r="3"></circle><path d="M12 2v3M12 19v3M2 12h3M19 12h3M4.9 4.9l2.1 2.1M17 17l2.1 2.1M4.9 19.1L7 17M17 7l2.1-2.1"></path></svg>
        </button>
        @if (settingsOpen)
        {
            <div class="cb-settings" role="group" aria-label="Board settings">
                <span class="cb-lbl">Board</span>
                <label>Show XP on legends <input id="show-xp" type="checkbox" checked="@ShowXp" @onchange="e => ShowXpChanged.InvokeAsync(e.Value is true)" /></label>
            </div>
        }
    </div>
    <ol class="cb-phases" aria-label="Phases">
        @foreach (var phase in Enum.GetValues<Phase>())
        {
            <li class="@(phase == Model.Phase ? "current" : "")" aria-current="@(phase == Model.Phase ? "step" : null)">@phase</li>
        }
    </ol>
    @if (Model.Chain.Count > 0)
    {
        <ol class="cb-chain" aria-label="The chain">
            @foreach (var item in Model.Chain)
            {
                <li><strong>@item.Name</strong><span class="who">On the chain · @item.Controller</span></li>
            }
        </ol>
    }
    <button type="button" id="big-button" disabled="@(Locked || !Model.Button.Enabled)" @onclick="() => OnStep.InvokeAsync(Model.Button.Step)">
        @Model.Button.Label
        @if (Model.Button.Detail is { } detail)
        {
            <small>@detail</small>
        }
    </button>
    @if (Model.Extras.Count > 0)
    {
        <div class="pair">
            @foreach (var extra in Model.Extras)
            {
                <button type="button" id="@($"extra-{extra.Label.ToLowerInvariant()}")" class="cb-btn cb-ghost extra" disabled="@(Locked || !extra.Enabled)"
                        @onclick="() => OnStep.InvokeAsync(extra.Step)">@extra.Label</button>
            }
        </div>
    }
    <div class="pair">
        <button type="button" id="request-undo" class="cb-btn cb-ghost" disabled="@(Locked || !Model.CanRequestUndo)"
                @onclick="() => OnStep.InvokeAsync(new SendStep(new RequestUndo()))">@(Model.UndoWaiting ? $"Waiting for {Opponent}..." : "Request undo")</button>
        <button type="button" id="concede" class="cb-btn cb-text" disabled="@(!CanConcede)" @onclick="OnConcede">Concede</button>
    </div>
    <section class="cb-log" aria-labelledby="log-title">
        <h2 id="log-title" class="cb-lbl">Activity</h2>
        <ol>
            @foreach (var line in Enumerable.Reverse(Model.Log))
            {
                <li>@line</li>
            }
        </ol>
    </section>
</aside>

@code {
    [Parameter, EditorRequired] public BoardModel Model { get; set; } = default!;
    [Parameter] public string Opponent { get; set; } = "";
    [Parameter] public bool Locked { get; set; }
    [Parameter] public bool ShowXp { get; set; }
    [Parameter] public EventCallback<bool> ShowXpChanged { get; set; }
    [Parameter] public bool CanConcede { get; set; }
    [Parameter] public EventCallback OnConcede { get; set; }
    [Parameter] public EventCallback<BoardStep> OnStep { get; set; }

    private bool settingsOpen;
}
```

The log is drawn newest first inside a `column-reverse` list, so the newest line shows at the bottom and the list stays scrolled there.

Create `src/CromoBound.Client/Board/Components/BoardView.razor`:

```razor
@using CromoBound.Engine.State
@using Microsoft.JSInterop
@implements IAsyncDisposable
@inject IJSRuntime JS

<div class="cb-board-frame" @ref="frame">
    <div class="cb-board @(Locked ? "locked" : "")" tabindex="0" @onkeydown="KeyAsync">
        <h1 class="cb-visually-hidden">@Model.Me.Name vs @Model.Them.Name</h1>
        <div class="cb-field">
            <div class="cb-half them" aria-hidden="true"></div>
            <div class="cb-half me" aria-hidden="true"></div>
            <div class="cb-centerline" aria-hidden="true"></div>
            <SideView Side="Model.Them" Locked="Locked" ShowXp="ShowXp" OnCard="ClickCardAsync" OnHover="Hover" />
            @foreach (var lane in Model.Lanes)
            {
                <LaneView @key="lane.Index" Lane="lane" Me="Model.Me.Player" Opponent="@Model.Them.Name" Locked="Locked"
                          OnCard="ClickCardAsync" OnHover="Hover" OnLane="ClickLaneAsync" />
            }
            <SideView Side="Model.Me" Locked="Locked" ShowXp="ShowXp" OnCard="ClickCardAsync" OnHover="Hover" OnBase="ClickBaseAsync" />
            @if (Model.Hint is { } hint)
            {
                <div class="cb-hint" role="status">
                    <span>@hint</span>
                    <button type="button" id="cancel-move" class="cb-btn cb-text" @onclick="CancelAsync">Cancel</button>
                </div>
            }
            @if (menu is { } open)
            {
                <CardMenu Menu="open" Title="@NameOf(open.Card)" OnPick="PickAsync" OnClose="() => menu = null" />
            }
            @ChildContent
        </div>
        <MatchPanel Model="Model" Opponent="@Model.Them.Name" Locked="Locked" ShowXp="ShowXp" ShowXpChanged="ShowXpChanged"
                    CanConcede="CanConcede" OnConcede="OnConcede" OnStep="StepAsync" />
        @if (zoomed is { } card)
        {
            <CardZoom Card="card" />
        }
    </div>
</div>

@code {
    [Parameter, EditorRequired] public BoardModel Model { get; set; } = default!;
    [Parameter] public EventCallback<BoardStep> OnStep { get; set; }
    [Parameter] public bool Locked { get; set; }
    [Parameter] public bool ShowXp { get; set; }
    [Parameter] public EventCallback<bool> ShowXpChanged { get; set; }
    [Parameter] public bool CanConcede { get; set; }
    [Parameter] public EventCallback OnConcede { get; set; }

    /// <summary>Drawn over the field: Task 5's panels.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private ElementReference frame;
    private MenuStep? menu;
    private BoardCard? zoomed;
    private BoardModel? shown;

    /// <summary>A new model (a new view or interaction) closes a menu that belonged to the old one.</summary>
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(shown, Model)) return;
        shown = Model;
        menu = null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await JS.InvokeVoidAsync("cromoBoard.fit", frame);
    }

    private Task ClickCardAsync(BoardCard card) => StepAsync(Model.Click(card.Id));

    private Task ClickLaneAsync(int lane) => StepAsync(Model.ClickLane(lane));

    private Task ClickBaseAsync() => StepAsync(Model.ClickBase());

    private Task CancelAsync() => StepAsync(Model.Cancel());

    private void Hover(BoardCard? card) => zoomed = card;

    private Task PickAsync(MenuItem item)
    {
        menu = null;
        return StepAsync(item.Step);
    }

    /// <summary>A menu opens here; everything else goes to the host. A locked board takes nothing.</summary>
    private async Task StepAsync(BoardStep step)
    {
        if (Locked || step is NoStep) return;
        if (step is MenuStep open)
        {
            menu = open;
            return;
        }
        await OnStep.InvokeAsync(step);
    }

    private async Task KeyAsync(KeyboardEventArgs e)
    {
        if (e.Key != "Escape") return;
        if (menu is not null)
        {
            menu = null;
            return;
        }
        await StepAsync(Model.Cancel());
    }

    private string NameOf(ObjectId id) =>
        Model.Me.Base.Concat(Model.Me.Runes).Concat(Model.Me.Hand).Concat(Model.Lanes.SelectMany(l => l.Mine))
            .Concat(new[] { Model.Me.Legend, Model.Me.Champion }.OfType<BoardCard>())
            .FirstOrDefault(c => c.Id == id)?.Name ?? CardBook.Unknown;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("cromoBoard.release", frame);
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
```

`MatchPanel`'s Request undo button builds `new SendStep(new RequestUndo())` itself. The model says whether undo can be asked; the button is the only place that asks.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Client tests/CromoBound.Client.Tests
git commit -m "feat(client): draw the board"
```

---
### Task 5: The panels, the board host and the match page

**Files:**
- Create: `src/CromoBound.Client/Board/Components/BoardPanels.razor`, `Board/Components/BoardHost.razor`, `tests/CromoBound.Client.Tests/BoardPanelsViewTests.cs`, `tests/CromoBound.Client.Tests/BoardHostTests.cs`
- Modify: `src/CromoBound.Client/Pages/Match.razor`, `Layout/MainLayout.razor`, `wwwroot/css/board.css` (append), `tests/CromoBound.Client.Tests/Fakes/FakeGameHub.cs`, `MatchPageTests.cs`, `LayoutTests.cs`

**Interfaces:**
- Consumes:
  - Tasks 1 to 4: `BoardModel`, the panels, `BoardView` and `CardFace`;
  - 4a's `IGameHub.SubmitAsync`, `LobbyState`, `IBrowserStorage`, `ISnackbar` and `GameConnection.Unexpected`;
  - the deck-text work's `CatalogClient.GetAsync()`, which returns `ApiResult<CardCatalog>`.
- Produces:
  - `BoardPanels`, with `Panel`, `Locked` and `OnStep`, and its ids:
    - `#where-{i}`, `#accelerate`, `#prompt-cancel`, `#prompt-continue`;
    - `#resolve-done`, `#turn-point-continue`, `#undo-refuse`, `#undo-allow`;
    - `#pick-{i}`, `#pick-confirm`, `#go-first`, `#go-second`;
    - `.out-row`, `.in-row`, `#swap`, `.swap .remove`, `#sideboard-submit`;
    - `#aside-{i}`, `#keep-all`, `#set-aside`;
    - `.waiting`.
  - `BoardHost`, with `MatchId`, `View`, `Me`, `Opponent`, `CanConcede` and `OnConcede`, the constant `ShowXpKey`, and `#retry-cards`.
  - `FakeGameHub.SubmitHold`.

- [ ] **Step 1: Write the failing tests**

In `tests/CromoBound.Client.Tests/Fakes/FakeGameHub.cs`, add after the `Hold` property:

```csharp
    /// <summary>When set, a submit waits for it before answering, so a test can look at the board while the action is in flight.</summary>
    public TaskCompletionSource? SubmitHold { get; set; }
```

and replace `SubmitAsync` with:

```csharp
    public async Task<SubmitReply> SubmitAsync(Guid matchId, PlayerAction action)
    {
        Calls.Add($"Submit {matchId} {action.GetType().Name}");
        Actions.Add(action);
        if (SubmitHold is { } hold) await hold.Task;
        return Submitted;
    }
```

Create `tests/CromoBound.Client.Tests/BoardPanelsViewTests.cs`:

```csharp
using Bunit;
using CromoBound.Client.Board;
using CromoBound.Client.Board.Components;
using CromoBound.Engine.Actions;
using CromoBound.Engine.State;

namespace CromoBound.Client.Tests;

public class BoardPanelsViewTests
{
    private static (IRenderedComponent<BoardPanels> Cut, List<PlayerAction> Sent) Render(Ui ui, BoardPanel panel)
    {
        var sent = new List<PlayerAction>();
        var cut = ui.Ctx.Render<BoardPanels>(ps => ps.Add(p => p.Panel, panel).Add(p => p.OnStep, s => sent.Add(((SendStep)s).Action)));
        return (cut, sent);
    }

    [Fact]
    public async Task Play_options_send_the_chosen_place_and_accelerate()
    {
        await using var ui = new Ui();
        var (cut, sent) = Render(ui, new PlayOptionsPanel("Blade Twirler",
            [new PlaceOption(Place.Base(TestBoard.Me), "Your base"), new PlaceOption(Place.Battlefield(0), "Back-Alley Bar")], true));

        Assert.Equal("Where does Blade Twirler enter?", cut.Find("h2").TextContent);
        await cut.InvokeAsync(() => cut.Find("#where-1").Change(true));
        await cut.InvokeAsync(() => cut.Find("#accelerate").Change(true));
        await cut.ClickAsync("#prompt-continue");
        await cut.ClickAsync("#prompt-cancel");

        Assert.Equal(new PlayerAction[] { new ChoosePlayOptions(Place.Battlefield(0), true), new CancelPlay() }, sent);
    }

    [Fact]
    public async Task A_spell_without_places_only_asks_about_accelerate()
    {
        await using var ui = new Ui();
        var (cut, sent) = Render(ui, new PlayOptionsPanel("Angle Shot", [], true));

        Assert.Empty(cut.FindAll("input[type=radio]"));
        await cut.ClickAsync("#prompt-continue");

        Assert.Equal(new PlayerAction[] { new ChoosePlayOptions(null, false) }, sent);
    }

    [Fact]
    public async Task The_prompts_send_done_continue_and_the_undo_answers()
    {
        await using var ui = new Ui();
        var (resolve, fromResolve) = Render(ui, new ResolvePanel("Blade Twirler", "p-unit-a", "The first time I move each turn, choose a player."));
        Assert.Contains("The first time I move each turn, choose a player.", resolve.Find("blockquote").TextContent);
        await resolve.ClickAsync("#resolve-done");
        var (point, fromPoint) = Render(ui, new TurnPointPanel("Start of your Beginning Phase", []));
        await point.ClickAsync("#turn-point-continue");
        var (undo, fromUndo) = Render(ui, new UndoPanel("giulia", "giulia played Angle Shot."));
        Assert.Equal("giulia asks to undo the last action", undo.Find("h2").TextContent);
        await undo.ClickAsync("#undo-allow");
        await undo.ClickAsync("#undo-refuse");

        Assert.Equal(new PlayerAction[] { new ResolveDone() }, fromResolve);
        Assert.Equal(new PlayerAction[] { new ContinueTurn() }, fromPoint);
        Assert.Equal(new PlayerAction[] { new AnswerUndo(true), new AnswerUndo(false) }, fromUndo);
    }

    [Fact]
    public async Task A_later_choice_explains_itself()
    {
        await using var ui = new Ui();
        var (cut, _) = Render(ui, new LaterPanel("ChooseShowdown", "choose where the showdown happens"));

        Assert.Equal("This choice comes in the next update", cut.Find("h2").TextContent);
        Assert.Contains("choose where the showdown happens", cut.Markup);
    }

    [Fact]
    public async Task The_battlefield_pick_sends_the_chosen_printing_or_waits()
    {
        await using var ui = new Ui();
        var (cut, sent) = Render(ui, new PickBattlefieldPanel(2, 3, [new PrintingOption("p-bf-b", "Altar to Unity"), new PrintingOption("p-bf-c", "Dusk Rose Lab")], false, "giulia"));

        await cut.InvokeAsync(() => cut.Find("#pick-1").Change(true));
        await cut.ClickAsync("#pick-confirm");
        var (waiting, _) = Render(ui, new PickBattlefieldPanel(2, 3, [], true, "giulia"));

        Assert.Equal(new PlayerAction[] { new PickBattlefield("p-bf-c") }, sent);
        Assert.Equal("Waiting for giulia to pick...", waiting.Find(".waiting").TextContent.Trim());
        Assert.Empty(waiting.FindAll("#pick-confirm"));
    }

    [Fact]
    public async Task The_play_order_sends_first_or_second()
    {
        await using var ui = new Ui();
        var (cut, sent) = Render(ui, new PlayOrderPanel(1, false, "giulia"));

        await cut.ClickAsync("#go-second");
        await cut.ClickAsync("#go-first");

        Assert.Equal(new PlayerAction[] { new ChoosePlayOrder(false), new ChoosePlayOrder(true) }, sent);
    }

    [Fact]
    public async Task Sideboarding_swaps_one_for_one_and_submits()
    {
        await using var ui = new Ui();
        var (cut, sent) = Render(ui, new SideboardPanel(2,
            [new DeckRow("p-unit-a", "Blade Twirler", 3), new DeckRow("p-unit-b", "Daring Poro", 2)],
            [new DeckRow("p-spell-a", "Angle Shot", 1)], false, "giulia"));
        Assert.Equal("Keep my deck", cut.Find("#sideboard-submit").TextContent.Trim());

        await cut.ClickAsync(".out-row");
        await cut.ClickAsync(".in-row");
        Assert.Equal("Swap Blade Twirler for Angle Shot", cut.Find("#swap").TextContent.Trim());
        await cut.ClickAsync("#swap");
        Assert.Equal("Submit 1 swap", cut.Find("#sideboard-submit").TextContent.Trim());
        Assert.True(cut.Find(".in-row").HasAttribute("disabled"));
        await cut.ClickAsync("#sideboard-submit");

        var submitted = Assert.IsType<SubmitSideboard>(Assert.Single(sent));
        Assert.Equal(new[] { new SideboardSwap("p-unit-a", "p-spell-a") }, submitted.Swaps);
    }

    [Fact]
    public async Task A_swap_can_be_removed()
    {
        await using var ui = new Ui();
        var (cut, sent) = Render(ui, new SideboardPanel(2, [new DeckRow("p-unit-a", "Blade Twirler", 3)], [new DeckRow("p-spell-a", "Angle Shot", 1)], false, "giulia"));

        await cut.ClickAsync(".out-row");
        await cut.ClickAsync(".in-row");
        await cut.ClickAsync("#swap");
        await cut.ClickAsync(".swap .remove");
        await cut.ClickAsync("#sideboard-submit");

        Assert.Empty(Assert.IsType<SubmitSideboard>(Assert.Single(sent)).Swaps);
    }

    [Fact]
    public async Task The_mulligan_sets_aside_up_to_two_or_keeps_all()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        var hand = new[] { "unit-a", "unit-b", "spell-a", "gear-a" }.Select(id => board.Add(board.Hand, id)).ToList();
        var model = board.Model(new Engine.Decisions.MulliganDecision(TestBoard.Me, [.. hand.Select(c => c.Id)]));
        var (cut, sent) = Render(ui, model.Panel!);

        Assert.Equal("Keep all 4", cut.Find("#keep-all").TextContent.Trim());
        await cut.InvokeAsync(() => cut.Find("#aside-0").Change(true));
        await cut.InvokeAsync(() => cut.Find("#aside-2").Change(true));
        await cut.InvokeAsync(() => cut.Find("#aside-3").Change(true));
        Assert.Equal("Set aside 2 and draw", cut.Find("#set-aside").TextContent.Trim());
        await cut.ClickAsync("#set-aside");

        var mulligan = Assert.IsType<Mulligan>(Assert.Single(sent));
        Assert.Equal(new[] { hand[0].Id, hand[2].Id }, mulligan.SetAside);
    }
}
```

Create `tests/CromoBound.Client.Tests/BoardHostTests.cs`:

```csharp
using Bunit;
using CromoBound.Client.Board;
using CromoBound.Client.Board.Components;
using CromoBound.Contracts;
using CromoBound.Engine;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;

namespace CromoBound.Client.Tests;

public class BoardHostTests
{
    private static readonly Guid M = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    private static Ui Connected()
    {
        var ui = new Ui();
        ui.Api.Catalog = TestBoard.Catalog;
        ui.Lobby.SetConnection(Services.HubState.Connected);
        return ui;
    }

    private static IRenderedComponent<BoardHost> Render(Ui ui, PlayerView view) =>
        ui.Ctx.Render<BoardHost>(ps => ps.Add(p => p.MatchId, M).Add(p => p.View, view).Add(p => p.Me, "marco").Add(p => p.Opponent, "giulia")
            .Add(p => p.CanConcede, true));

    [Fact]
    public async Task The_host_loads_the_cards_once_and_draws_the_board()
    {
        await using var ui = Connected();
        var board = new TestBoard();
        board.Add(board.Hand, "unit-b");

        var cut = Render(ui, board.View(TestBoard.Priority()));

        cut.WaitForAssertion(() => Assert.Equal("marco vs giulia", cut.Find("h1").TextContent));
        Assert.NotEmpty(cut.FindAll("[aria-label^='Daring Poro']"));
        cut.SetParametersAndRender(ps => ps.Add(p => p.View, board.View(TestBoard.Priority())));
        Assert.Equal(1, ui.Api.CardsCalls);
    }

    [Fact]
    public async Task Without_the_cards_it_says_so_and_retries()
    {
        await using var ui = Connected();
        ui.Api.NextQueryError = Services.ServerApi.NotReachable;

        var cut = Render(ui, new TestBoard().View());

        cut.WaitForAssertion(() => Assert.Equal("The cards couldn't be loaded.", cut.Find("[role=alert]").TextContent));
        await cut.ClickAsync("#retry-cards");
        cut.WaitForAssertion(() => Assert.Equal("marco vs giulia", cut.Find("h1").TextContent));
    }

    [Fact]
    public async Task Clicking_a_playable_card_submits_it()
    {
        await using var ui = Connected();
        var board = new TestBoard();
        var poro = board.Add(board.Hand, "unit-b");

        var cut = Render(ui, board.View(TestBoard.Priority(playable: [poro.Id])));
        await cut.ClickAsync("button[aria-label^='Daring Poro']");

        cut.WaitForAssertion(() => Assert.Equal(new[] { $"Submit {M} PlayCard" }, ui.Hub.Calls));
        Assert.Equal(new PlayCard(poro.Id), ui.Hub.Actions.Single());
    }

    [Fact]
    public async Task A_refused_action_shows_the_engines_reason_and_keeps_the_choices()
    {
        await using var ui = Connected();
        ui.Hub.Submitted = new SubmitReply(false, new Rejection(RejectionCode.InsufficientPayment, "That isn't enough to pay the cost."), null);
        var board = new TestBoard();
        var rune = board.Add(board.MyBase, "fury-rune");
        var pay = new PayCostDecision(TestBoard.Me, new TotalCost(2, []), [], null);

        var cut = Render(ui, board.View(pay));
        await cut.ClickAsync("button[aria-label^='Fury Rune']");
        cut.WaitForAssertion(() => Assert.Contains("exhaust to pay", cut.Find("button[aria-label^='Fury Rune']").GetAttribute("aria-label")));
        await cut.ClickAsync("#big-button");

        cut.WaitForAssertion(() => Assert.Contains("That isn't enough to pay the cost.", ui.Notices));
        Assert.Contains("exhaust to pay", cut.Find("button[aria-label^='Fury Rune']").GetAttribute("aria-label"));
        Assert.Equal(new[] { rune.Id }, Assert.IsType<PayCost>(ui.Hub.Actions.Single()).Exhaust);
    }

    [Fact]
    public async Task While_an_action_is_in_flight_the_board_takes_no_clicks()
    {
        await using var ui = Connected();
        ui.Hub.SubmitHold = new TaskCompletionSource();
        var board = new TestBoard();
        var poro = board.Add(board.Hand, "unit-b");

        var cut = Render(ui, board.View(TestBoard.Priority(playable: [poro.Id], canEndTurn: true)));
        await cut.ClickAsync("button[aria-label^='Daring Poro']");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("button[aria-label^='Daring Poro']")));
        Assert.True(cut.Find("#big-button").HasAttribute("disabled"));
        Assert.Single(ui.Hub.Calls);
        ui.Hub.SubmitHold.SetResult();
        cut.WaitForAssertion(() => Assert.False(cut.Find("#big-button").HasAttribute("disabled")));
    }

    [Fact]
    public async Task A_new_decision_drops_a_half_made_move()
    {
        await using var ui = Connected();
        var board = new TestBoard();
        board.AddLane("bf-a");
        var unit = board.Add(board.MyBase, "unit-b", might: 1);
        var priority = TestBoard.Priority(moves: [new MoveOption(unit.Id, [Place.Battlefield(0)])]);

        var cut = Render(ui, board.View(priority));
        await cut.ClickAsync("button[aria-label^='Daring Poro']");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".cb-hint")));

        cut.SetParametersAndRender(ps => ps.Add(p => p.View, board.View(priority)));

        Assert.Empty(cut.FindAll(".cb-hint"));
        Assert.Empty(ui.Hub.Calls);
    }

    [Fact]
    public async Task Disconnected_the_board_is_locked()
    {
        await using var ui = Connected();
        ui.Lobby.SetConnection(Services.HubState.Reconnecting);
        var board = new TestBoard();
        var poro = board.Add(board.Hand, "unit-b");

        var cut = Render(ui, board.View(TestBoard.Priority(playable: [poro.Id])));

        cut.WaitForAssertion(() => Assert.True(cut.Find("#big-button").HasAttribute("disabled")));
        Assert.Empty(cut.FindAll("button[aria-label^='Daring Poro']"));
    }

    [Fact]
    public async Task The_xp_setting_is_kept_in_the_browser()
    {
        await using var ui = Connected();
        var board = new TestBoard();

        var cut = Render(ui, board.View());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".cb-xpbadge").Count));
        await cut.ClickAsync("button[aria-label='Board settings']");
        await cut.InvokeAsync(() => cut.Find("#show-xp").Change(false));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".cb-xpbadge")));
        Assert.Equal("false", ui.Storage.Items[BoardHost.ShowXpKey]);
        var again = Render(ui, board.View());
        again.WaitForAssertion(() => Assert.NotEmpty(again.FindAll("h1")));
        Assert.Empty(again.FindAll(".cb-xpbadge"));
    }

    [Fact]
    public async Task A_panel_action_is_submitted()
    {
        await using var ui = Connected();
        var board = new TestBoard { Stage = Engine.Matches.MatchStage.PlayOrder, GameNumber = 1 };

        var cut = Render(ui, board.View(new ChoosePlayOrderDecision(TestBoard.Me)));
        await cut.ClickAsync("#go-first");

        cut.WaitForAssertion(() => Assert.Equal(new ChoosePlayOrder(true), ui.Hub.Actions.Single()));
    }
}
```

In `tests/CromoBound.Client.Tests/MatchPageTests.cs`:

1. In `The_page_shows_the_players_format_stage_and_score`, replace the lines from `Assert.Equal("Best of three · game 2", cut.Find("#match-format").TextContent);` through `Assert.Single(cut.FindAll(".cb-pip.on"));` with:

```csharp
        Assert.Equal("Best of three · game 2 · you lead 1 : 0", cut.Find("#score-line").TextContent);
```

2. In `A_view_notice_updates_the_score`, replace the three assertions after the `View` notice with:

```csharp
        cut.WaitForAssertion(() => Assert.Equal("Best of three · game 3 · 1 : 1", cut.Find("#score-line").TextContent));
```

The board now shows the format, the game and the score in its score line; the stage is shown by the board's panels.

Add to `tests/CromoBound.Client.Tests/LayoutTests.cs`:

```csharp
    [Fact]
    public async Task The_match_page_has_no_top_bar()
    {
        await using var ui = new Ui();
        ui.Nav.NavigateTo($"match/{Guid.NewGuid()}");

        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#page")));
        Assert.Empty(cut.FindAll(".cb-bar"));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`BoardPanels` and `BoardHost` don't exist).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Client/Board/Components/BoardPanels.razor`:

```razor
@using CromoBound.Engine.Actions
@using CromoBound.Engine.State

@switch (Panel)
{
    case PlayOptionsPanel where:
        <div class="cb-prompt" role="dialog" aria-modal="false" aria-labelledby="prompt-title">
            <h2 id="prompt-title">Where does @where.CardName enter?</h2>
            @if (where.Locations.Count > 0)
            {
                <div class="choices" role="radiogroup" aria-label="Where it enters">
                    @for (var i = 0; i < where.Locations.Count; i++)
                    {
                        var index = i;
                        <label class="choice @(chosen == index ? "on" : "")">
                            <input id="@($"where-{index}")" type="radio" name="where" checked="@(chosen == index)" @onchange="() => chosen = index" />
                            @where.Locations[index].Label
                        </label>
                    }
                </div>
            }
            @if (where.AccelerateAvailable)
            {
                <label class="check"><input id="accelerate" type="checkbox" checked="@accelerate" @onchange="e => accelerate = e.Value is true" /> Accelerate: pay its extra cost so it enters ready</label>
            }
            <div class="actions">
                <button type="button" id="prompt-cancel" class="cb-btn cb-text" disabled="@Locked" @onclick="() => SendAsync(new CancelPlay())">Cancel</button>
                <button type="button" id="prompt-continue" class="cb-btn cb-gold" disabled="@Locked"
                        @onclick="() => SendAsync(new ChoosePlayOptions(where.Locations.Count > 0 ? where.Locations[chosen].Place : null, where.AccelerateAvailable && accelerate))">Continue</button>
            </div>
        </div>
        break;
    case ResolvePanel resolve:
        <div class="cb-prompt" role="dialog" aria-modal="false" aria-labelledby="prompt-title">
            <div class="top">
                <span class="art" aria-hidden="true" style="@ArtStyle(resolve.PrintingId)"></span>
                <div>
                    <span class="cb-lbl accent">Resolve by hand</span>
                    <h2 id="prompt-title">@resolve.CardName</h2>
                    <p>The engine can't carry out this text. Do it together with your opponent, then press Done.</p>
                </div>
            </div>
            <blockquote>@resolve.Text</blockquote>
            <div class="actions"><button type="button" id="resolve-done" class="cb-btn cb-gold" disabled="@Locked" @onclick="() => SendAsync(new ResolveDone())">Done</button></div>
        </div>
        break;
    case TurnPointPanel point:
        <div class="cb-prompt" role="dialog" aria-modal="false" aria-labelledby="prompt-title">
            <span class="cb-lbl accent">@point.Title</span>
            <h2 id="prompt-title">Apply these by hand</h2>
            <ul class="cards">
                @foreach (var card in point.Cards)
                {
                    <li><span class="art small" aria-hidden="true" style="@ArtStyle(card.PrintingId)"></span><strong>@card.Name</strong></li>
                }
            </ul>
            <div class="actions"><button type="button" id="turn-point-continue" class="cb-btn cb-gold" disabled="@Locked" @onclick="() => SendAsync(new ContinueTurn())">Continue</button></div>
        </div>
        break;
    case UndoPanel undo:
        <div class="cb-prompt" role="dialog" aria-modal="false" aria-labelledby="prompt-title">
            <h2 id="prompt-title">@undo.Requester asks to undo the last action</h2>
            @if (undo.LastAction is { } last)
            {
                <p>The last action was: <strong>@last</strong> If you allow it, the game goes back to just before it.</p>
            }
            <div class="actions">
                <button type="button" id="undo-refuse" class="cb-btn cb-ghost" disabled="@Locked" @onclick="() => SendAsync(new AnswerUndo(false))">Refuse</button>
                <button type="button" id="undo-allow" class="cb-btn cb-gold" disabled="@Locked" @onclick="() => SendAsync(new AnswerUndo(true))">Allow</button>
            </div>
        </div>
        break;
    case LaterPanel later:
        <div class="cb-prompt" role="dialog" aria-modal="false" aria-labelledby="prompt-title">
            <span class="cb-lbl accent">Not in this update yet</span>
            <h2 id="prompt-title">This choice comes in the next update</h2>
            <p>The game is asking you to <strong>@later.What</strong>. The board can't do that yet, so this game can't go on here. You can still concede it from the panel.</p>
        </div>
        break;
    case PickBattlefieldPanel pick:
        <div class="cb-overlay">
            <div class="cb-dialog wide" role="dialog" aria-modal="true" aria-labelledby="dialog-title">
                <span class="cb-lbl accent">GAME @pick.GameNumber OF @pick.Games</span>
                <h2 id="dialog-title">@(pick.Waiting ? $"{pick.Opponent} is picking a battlefield" : "Pick a battlefield")</h2>
                @if (pick.Waiting)
                {
                    <p class="waiting" role="status"><span class="spin" aria-hidden="true"></span> Waiting for @pick.Opponent to pick...</p>
                }
                else
                {
                    <p>Each game you play one of your battlefields you haven't played yet in this match.</p>
                    <div class="tiles" role="radiogroup" aria-label="Your battlefields">
                        @for (var i = 0; i < pick.Options.Count; i++)
                        {
                            var index = i;
                            var option = pick.Options[index];
                            <label class="tile @(chosen == index ? "on" : "")">
                                <span class="art wide" aria-hidden="true" style="@ArtStyle(option.Printing)"></span>
                                <span class="name">@option.Name</span>
                                <input id="@($"pick-{index}")" type="radio" name="pick" checked="@(chosen == index)" @onchange="() => chosen = index" />
                            </label>
                        }
                    </div>
                    <div class="actions">
                        <button type="button" id="pick-confirm" class="cb-btn cb-gold" disabled="@(Locked || pick.Options.Count == 0)"
                                @onclick="() => SendAsync(new PickBattlefield(pick.Options[chosen].Printing))">Confirm</button>
                    </div>
                }
            </div>
        </div>
        break;
    case PlayOrderPanel order:
        <div class="cb-overlay">
            <div class="cb-dialog" role="dialog" aria-modal="true" aria-labelledby="dialog-title">
                <span class="cb-lbl accent">GAME @order.GameNumber</span>
                <h2 id="dialog-title">@(order.Waiting ? $"{order.Opponent} chooses who goes first" : "You choose who goes first")</h2>
                @if (order.Waiting)
                {
                    <p class="waiting" role="status"><span class="spin" aria-hidden="true"></span> Waiting for @order.Opponent...</p>
                }
                else
                {
                    <p>Pick whether you play the first turn or the second.</p>
                    <div class="actions">
                        <button type="button" id="go-second" class="cb-btn cb-ghost" disabled="@Locked" @onclick="() => SendAsync(new ChoosePlayOrder(false))">Go second</button>
                        <button type="button" id="go-first" class="cb-btn cb-gold" disabled="@Locked" @onclick="() => SendAsync(new ChoosePlayOrder(true))">Go first</button>
                    </div>
                }
            </div>
        </div>
        break;
    case SideboardPanel sideboard:
        <div class="cb-overlay">
            <div class="cb-dialog wide" role="dialog" aria-modal="true" aria-labelledby="dialog-title">
                <span class="cb-lbl accent">BEFORE GAME @sideboard.GameNumber</span>
                <h2 id="dialog-title">Sideboarding</h2>
                @if (sideboard.Waiting)
                {
                    <p class="waiting" role="status"><span class="spin" aria-hidden="true"></span> Waiting for @sideboard.Opponent...</p>
                }
                else
                {
                    <p>Pick a card in your main deck and one in your sideboard to swap them, one copy for one copy. Your main deck stays at 40 cards.</p>
                    <div class="lists">
                        <section aria-label="Main deck">
                            <h3 class="cb-lbl">Main deck</h3>
                            @foreach (var row in sideboard.Main)
                            {
                                var left = row.Count - swaps.Count(s => s.Out == row.Printing);
                                <button type="button" class="row out-row @(outPick == row.Printing ? "on" : "")" aria-pressed="@(outPick == row.Printing ? "true" : "false")"
                                        disabled="@(left <= 0)" @onclick="() => outPick = row.Printing"><span class="n">@left</span>@row.Name</button>
                            }
                        </section>
                        <section aria-label="Sideboard">
                            <h3 class="cb-lbl">Sideboard</h3>
                            @foreach (var row in sideboard.Sideboard)
                            {
                                var left = row.Count - swaps.Count(s => s.In == row.Printing);
                                <button type="button" class="row in-row @(inPick == row.Printing ? "on" : "")" aria-pressed="@(inPick == row.Printing ? "true" : "false")"
                                        disabled="@(left <= 0)" @onclick="() => inPick = row.Printing"><span class="n">@left</span>@row.Name</button>
                            }
                            @if (outPick is { } o && inPick is { } n)
                            {
                                <button type="button" id="swap" class="cb-btn cb-ghost" @onclick="AddSwap">Swap @NameIn(sideboard.Main, o) for @NameIn(sideboard.Sideboard, n)</button>
                            }
                        </section>
                        <section aria-label="Your swaps" class="swaps">
                            <h3 class="cb-lbl">Your swaps (@swaps.Count)</h3>
                            <ul>
                                @foreach (var swap in swaps.ToList())
                                {
                                    <li class="swap">
                                        <span><span class="out">Out</span> @NameIn(sideboard.Main, swap.Out)</span>
                                        <span><span class="in">In</span> @NameIn(sideboard.Sideboard, swap.In)</span>
                                        <button type="button" class="cb-btn cb-text remove" @onclick="() => swaps.Remove(swap)">Remove</button>
                                    </li>
                                }
                            </ul>
                        </section>
                    </div>
                    <div class="actions">
                        <button type="button" id="sideboard-submit" class="cb-btn cb-gold" disabled="@Locked" @onclick="() => SendAsync(new SubmitSideboard { Swaps = [.. swaps] })">
                            @(swaps.Count == 0 ? "Keep my deck" : swaps.Count == 1 ? "Submit 1 swap" : $"Submit {swaps.Count} swaps")
                        </button>
                    </div>
                }
            </div>
        </div>
        break;
    case MulliganPanel mulligan:
        <div class="cb-overlay">
            <div class="cb-dialog wide" role="dialog" aria-modal="true" aria-labelledby="dialog-title">
                <h2 id="dialog-title">@(mulligan.Waiting ? $"{mulligan.Opponent} is choosing" : "Your opening hand")</h2>
                @if (mulligan.Waiting)
                {
                    <p class="waiting" role="status"><span class="spin" aria-hidden="true"></span> Waiting for @mulligan.Opponent...</p>
                }
                else
                {
                    <p>Pick up to 2 cards to put at the bottom of your deck; you draw the same number.</p>
                    <div class="hand" role="group" aria-label="Cards to set aside">
                        @for (var i = 0; i < mulligan.Hand.Count; i++)
                        {
                            var card = mulligan.Hand[i];
                            var index = i;
                            <label class="pick @(aside.Contains(card.Id) ? "on" : "")">
                                <CardFace Card="card" Size="hand" Upright="true" />
                                <span><input id="@($"aside-{index}")" type="checkbox" checked="@aside.Contains(card.Id)" @onchange="e => Toggle(card.Id, e.Value is true)" /> Set aside</span>
                            </label>
                        }
                    </div>
                    <div class="actions">
                        <button type="button" id="keep-all" class="cb-btn cb-ghost" disabled="@Locked" @onclick="() => SendAsync(new Mulligan())">Keep all @mulligan.Hand.Count</button>
                        <button type="button" id="set-aside" class="cb-btn cb-gold" disabled="@(Locked || aside.Count == 0)"
                                @onclick="() => SendAsync(new Mulligan { SetAside = [.. mulligan.Hand.Select(c => c.Id).Where(aside.Contains)] })">Set aside @aside.Count and draw</button>
                    </div>
                }
            </div>
        </div>
        break;
}

@code {
    [Parameter] public BoardPanel? Panel { get; set; }
    [Parameter] public bool Locked { get; set; }
    [Parameter] public EventCallback<BoardStep> OnStep { get; set; }

    private BoardPanel? shown;
    private int chosen;
    private bool accelerate;
    private string? outPick;
    private string? inPick;
    private readonly List<SideboardSwap> swaps = [];
    private readonly HashSet<ObjectId> aside = [];

    /// <summary>A new panel starts fresh.</summary>
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(shown, Panel)) return;
        shown = Panel;
        chosen = 0;
        accelerate = false;
        outPick = inPick = null;
        swaps.Clear();
        aside.Clear();
    }

    private Task SendAsync(PlayerAction action) => Locked ? Task.CompletedTask : OnStep.InvokeAsync(new SendStep(action));

    private void AddSwap()
    {
        if (outPick is null || inPick is null) return;
        swaps.Add(new SideboardSwap(outPick, inPick));
        outPick = inPick = null;
    }

    /// <summary>At most two cards are set aside (CR 117).</summary>
    private void Toggle(ObjectId card, bool on)
    {
        if (on && aside.Count < 2) aside.Add(card);
        if (!on) aside.Remove(card);
    }

    private static string NameIn(IReadOnlyList<DeckRow> rows, string printing) => rows.FirstOrDefault(r => r.Printing == printing)?.Name ?? CardBook.Unknown;

    private static string ArtStyle(string? printing) => printing is null ? "" : $"background-image: url('cards/img/{printing}')";
}
```

The mulligan test ticks three boxes and expects the third to be refused: at most two cards are set aside.

Append to `src/CromoBound.Client/wwwroot/css/board.css`:

```css
/* Panels over the board: prompts float over the field, pre-game steps sit on a dimmed field. */
.cb-prompt { position: absolute; left: 340px; top: 236px; z-index: 6; width: 460px; box-sizing: border-box; padding: 18px 20px; background: #161112; border: 1px solid #5a2420; border-radius: 24px; box-shadow: 0 18px 50px rgba(0,0,0,.7); display: flex; flex-direction: column; gap: 14px; }
.cb-prompt h2, .cb-dialog h2 { margin: 0; font-family: Fredoka, sans-serif; font-size: 20px; letter-spacing: .03em; }
.cb-prompt p, .cb-dialog p { margin: 0; font-size: 15px; color: #b39d99; }
.cb-prompt p strong { color: #f2e9e6; }
.cb-prompt .top { display: flex; gap: 14px; }
.cb-prompt blockquote { margin: 0; padding: 12px 14px; border-radius: 14px; background: #110c0d; border: 1px solid #3a2a2b; font-size: 15px; line-height: 1.45; }
.cb-prompt .cards { margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; gap: 8px; }
.cb-prompt .cards li { display: flex; gap: 12px; align-items: center; padding: 10px 12px; border-radius: 14px; background: #110c0d; border: 1px solid #3a2a2b; }
.cb-board .actions { display: flex; justify-content: flex-end; gap: 8px; }
.cb-board .cb-lbl.accent { color: #ff7a6b; }
.cb-board .art { flex: none; width: 64px; height: 90px; border-radius: 6px; background: #241a1b center / cover no-repeat; border: 1px solid #3a2a2b; }
.cb-board .art.small { width: 34px; height: 48px; border-radius: 4px; }
.cb-board .art.wide { width: 100%; height: 128px; border-radius: 16px; }
.cb-board .choices { display: flex; flex-direction: column; gap: 8px; }
.cb-board .choice { display: flex; align-items: center; gap: 10px; min-height: 44px; padding: 0 14px; border-radius: 14px; border: 1px solid #3a2a2b; cursor: pointer; }
.cb-board .choice.on { border-color: #d8392a; background: rgba(224,74,58,.1); }
.cb-board .check { display: flex; align-items: center; gap: 10px; min-height: 44px; font-size: 15px; cursor: pointer; }
.cb-board input[type=radio], .cb-board input[type=checkbox] { width: 18px; height: 18px; accent-color: #d8392a; }
.cb-overlay { position: absolute; inset: 0; z-index: 7; background: rgba(8,4,4,.72); display: flex; align-items: center; justify-content: center; }
.cb-dialog { width: 520px; max-height: 820px; overflow: auto; box-sizing: border-box; padding: 22px 24px; background: #161112; border: 1px solid #5a2420; border-radius: 28px; box-shadow: 0 18px 50px rgba(0,0,0,.6); display: flex; flex-direction: column; gap: 14px; }
.cb-dialog.wide { width: 900px; }
.cb-dialog .waiting { display: flex; align-items: center; gap: 10px; color: #d7c7c3; }
.cb-dialog .spin { width: 16px; height: 16px; border-radius: 50%; border: 2px solid #5a2420; border-top-color: #ff7a6b; animation: cb-spin 1s linear infinite; }
@keyframes cb-spin { to { transform: rotate(360deg); } }
.cb-dialog .tiles { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 14px; }
.cb-dialog .tile { position: relative; display: flex; flex-direction: column; gap: 8px; cursor: pointer; }
.cb-dialog .tile.on .art { border: 2px solid #ff7a6b; box-shadow: 0 0 0 1px #ff7a6b, 0 0 16px rgba(255,122,107,.4); }
.cb-dialog .tile .name { font-family: Fredoka, sans-serif; font-weight: 700; font-size: 14px; }
.cb-dialog .tile input { position: absolute; top: 10px; right: 10px; }
.cb-dialog .lists { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(0, .9fr); gap: 18px; }
.cb-dialog .lists section { display: flex; flex-direction: column; gap: 4px; }
.cb-dialog .lists h3 { margin: 0 0 4px; }
.cb-dialog .row { display: flex; align-items: center; gap: 10px; min-height: 40px; padding: 0 10px; border-radius: 12px; border: 1px solid transparent; background: transparent; color: #f2e9e6; font: inherit; font-size: 15px; text-align: left; cursor: pointer; }
.cb-dialog .row.on { background: rgba(224,74,58,.12); border-color: #d8392a; }
.cb-dialog .row:disabled { opacity: .45; cursor: default; }
.cb-dialog .row .n { min-width: 26px; font-family: 'Barlow Condensed', sans-serif; font-weight: 700; font-size: 16px; color: #d7c7c3; }
.cb-dialog .swaps { padding: 12px; border-radius: 20px; background: #110c0d; border: 1px solid #2a2021; }
.cb-dialog .swaps ul { margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; gap: 8px; }
.cb-dialog .swap { display: flex; flex-direction: column; gap: 4px; padding: 10px; border-radius: 14px; background: #1d1516; font-size: 14px; }
.cb-dialog .swap .out { color: #ff9db0; font-weight: 600; }
.cb-dialog .swap .in { color: #7fd9bd; font-weight: 600; }
.cb-dialog .swap .remove { align-self: flex-end; min-height: 32px; padding: 0 10px; font-size: 13px; }
.cb-dialog .hand { display: flex; justify-content: center; gap: 18px; flex-wrap: wrap; }
.cb-dialog .pick { display: flex; flex-direction: column; align-items: center; gap: 10px; cursor: pointer; }
.cb-dialog .pick.on .cb-card { opacity: .6; translate: 0 -10px; border-color: #ff7a6b; }
.cb-dialog .pick span { display: flex; align-items: center; gap: 8px; min-height: 44px; font-size: 14px; }
.cb-board-page { padding-top: 8px; }
```

Create `src/CromoBound.Client/Board/Components/BoardHost.razor`:

```razor
@using CromoBound.Engine.Views
@inject IGameHub Hub
@inject CatalogClient Catalog
@inject LobbyState Lobby
@inject IBrowserStorage Storage
@inject ISnackbar Snackbar

@if (catalogFailed)
{
    <div class="cb-main">
        <p class="cb-error" role="alert">The cards couldn't be loaded.</p>
        <div><button type="button" id="retry-cards" class="cb-btn cb-ghost" @onclick="LoadAsync">Retry</button></div>
    </div>
}
else if (model is null)
{
    <p class="cb-main cb-sub">Loading the cards...</p>
}
else
{
    <BoardView Model="model" OnStep="StepAsync" Locked="Locked" ShowXp="showXp" ShowXpChanged="SetShowXpAsync" CanConcede="CanConcede" OnConcede="OnConcede">
        <BoardPanels Panel="model.Panel" Locked="Locked" OnStep="StepAsync" />
    </BoardView>
}

@code {
    public const string ShowXpKey = "cromobound.board.showXp";

    [Parameter, EditorRequired] public Guid MatchId { get; set; }
    [Parameter, EditorRequired] public PlayerView View { get; set; } = default!;
    [Parameter] public string Me { get; set; } = "";
    [Parameter] public string Opponent { get; set; } = "";
    [Parameter] public bool CanConcede { get; set; }
    [Parameter] public EventCallback OnConcede { get; set; }

    private CardBook? book;
    private bool catalogFailed;
    private Interaction interaction = Idle.Instance;
    private PlayerView? builtFrom;
    private BoardModel? model;
    private bool busy;
    private bool showXp = true;

    /// <summary>Nothing is sent while an action is in flight or the connection is down.</summary>
    private bool Locked => busy || !Lobby.IsConnected;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            showXp = await Storage.GetAsync(ShowXpKey) != "false";
        }
        catch (Exception)
        {
            // A browser that refuses its storage keeps the default.
        }
        await LoadAsync();
    }

    /// <summary>A new view is a new decision: whatever the player was in the middle of is dropped (Review Focus 1).</summary>
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(builtFrom, View)) interaction = Idle.Instance;
        Build();
    }

    private async Task LoadAsync()
    {
        catalogFailed = false;
        var loaded = await Catalog.GetAsync();
        if (loaded.Value is { } catalog) book = new CardBook(catalog);
        else catalogFailed = true;
        Build();
    }

    private void Build()
    {
        if (book is null) return;
        builtFrom = View;
        model = BoardModel.From(View, book, Me, Opponent, interaction);
    }

    /// <summary>A local step changes what the player is in the middle of; an action is sent once, with the board locked until the
    /// answer. A refusal shows the engine's words and leaves the board as it was; an accepted action clears the local choices.</summary>
    private async Task StepAsync(BoardStep step)
    {
        switch (step)
        {
            case NextStep next:
                interaction = next.Next;
                Build();
                break;
            case SendStep send when !Locked:
                busy = true;
                StateHasChanged();
                try
                {
                    var reply = await Hub.SubmitAsync(MatchId, send.Action);
                    if (reply.Accepted)
                    {
                        interaction = Idle.Instance;
                        Build();
                    }
                    else
                    {
                        Snackbar.Add(reply.Rejection?.Message ?? reply.Error ?? GameConnection.Unexpected, Severity.Error);
                    }
                }
                finally
                {
                    busy = false;
                }
                break;
        }
    }

    private async Task SetShowXpAsync(bool on)
    {
        showXp = on;
        try
        {
            await Storage.SetAsync(ShowXpKey, on ? "true" : "false");
        }
        catch (Exception)
        {
            // Kept for this page only.
        }
    }
}
```

In `src/CromoBound.Client/Pages/Match.razor`:

1. Replace everything between `<main class="cb-main narrow">` and its closing `</main>` (the opening and closing tags included) with:

```razor
<main class="@(Lobby.CurrentView is null ? "cb-main narrow" : "cb-board-page")">
    @if (!Lobby.Loaded || (Lobby.MatchId != Id && Lobby.Ended?.MatchId != Id))
    {
        <p class="cb-sub">Loading the match...</p>
    }
    else if (Lobby.CurrentView is { } view)
    {
        <BoardHost MatchId="Id" View="view" Me="@Me" Opponent="@Opponent" CanConcede="CanConcede" OnConcede="ConcedeAsync" />
    }
    else
    {
        <p class="cb-sub">Waiting for the match to begin...</p>
    }
</main>
```

2. In `@code`, delete `FormatLine`, `Games` and `Wins`, which nothing uses now. Delete the `@using CromoBound.Engine.Views` line if nothing else in the file needs it; the build stays at 0 warnings either way.

In `src/CromoBound.Client/Layout/MainLayout.razor`:

1. Wrap the whole `<header class="cb-bar">...</header>` element in:

```razor
@if (!OnBoard)
{
    <header class="cb-bar">...</header>
}
```

keeping the header's contents exactly as they are.

2. Add to `@code`:

```csharp
    /// <summary>The board has no top bar (the approved mock): the match page fills the window.</summary>
    private bool OnBoard => Nav.ToBaseRelativePath(Nav.Uri).StartsWith("match/", StringComparison.OrdinalIgnoreCase);

    private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e) => _ = InvokeAsync(StateHasChanged);
```

3. In `OnInitializedAsync`, add `Nav.LocationChanged += OnLocationChanged;` as its first line; in `Dispose`, add `Nav.LocationChanged -= OnLocationChanged;`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, the updated match page and layout tests included).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Client tests/CromoBound.Client.Tests
git commit -m "feat(client): play on the board from the match page"
```

---

### Task 6: One game through the real hub, and the manual checks

**Files:**
- Create: `tests/CromoBound.Server.Tests/BoardGameTests.cs`
- Modify: `tests/CromoBound.Server.Tests/ClientConnectionTests.cs` (share its connection helper), `docs/client-manual-checks.md`

**Interfaces:**
- Consumes:
  - the client's `GameConnection` (internal constructor with `Uri` and `Action<HttpConnectionOptions>`) and `BoardModel`, `CardBook`, the steps and the panels;
  - the server's `CardEndpoints.CatalogOf(CardDatabase)`;
  - `ServerFactory.TestCards`, `Decks.First` and `Decks.Second`.
- Produces: `ClientConnectionTests.ConnectAsync` and `ClientConnectionTests.Notices`, made `internal` for reuse.

- [ ] **Step 1: Share the connection helper**

In `tests/CromoBound.Server.Tests/ClientConnectionTests.cs`, change `private static async Task<(GameConnection Connection, Notices Notices)> ConnectAsync(` to `internal static async Task<(GameConnection Connection, Notices Notices)> ConnectAsync(`, and `private sealed class Notices` to `internal sealed class Notices`.

- [ ] **Step 2: Write the test**

Create `tests/CromoBound.Server.Tests/BoardGameTests.cs`:

```csharp
using CromoBound.Client.Board;
using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Server.Cards;

namespace CromoBound.Server.Tests;

/// <summary>A first turn played end to end: the real hub, the client's GameConnection, and the board model choosing every action the
/// way a player's clicks would (spec 10).</summary>
public class BoardGameTests
{
    [Fact]
    public async Task A_first_turn_is_played_through_the_board_model()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("alice");
        await factory.AddUserAsync("bob");
        var (alice, aliceNotices) = await ClientConnectionTests.ConnectAsync(factory, "alice");
        var (bob, _) = await ClientConnectionTests.ConnectAsync(factory, "bob");
        await using var aliceConnection = alice;
        await using var bobConnection = bob;
        var challenge = await alice.ChallengeAsync("bob", MatchFormat.Bo1, Decks.First);
        var matchId = (await bob.AcceptAsync(challenge.Id!.Value, Decks.Second)).Id!.Value;
        await aliceNotices.WaitForAsync<MatchStartedNotice>();
        var book = new CardBook(CardEndpoints.CatalogOf(ServerFactory.TestCards));
        var players = new[] { (Connection: alice, Me: "alice", Opponent: "bob"), (Connection: bob, Me: "bob", Opponent: "alice") };
        var played = false;
        PlayerView? last = null;

        for (var step = 0; step < 80 && last?.Turn is not { Number: >= 2 }; step++)
        {
            var acted = false;
            foreach (var player in players)
            {
                var view = (await player.Connection.GetMatchAsync())!.View!;
                last = view;
                if (view.Turn is { Number: >= 2 }) break;
                if (view.Decision is null) continue;
                var model = BoardModel.From(view, book, player.Me, player.Opponent, Idle.Instance);
                var action = Choose(model, view, book, ref played);
                var reply = await player.Connection.SubmitAsync(matchId, action);
                Assert.True(reply.Accepted, $"{action.GetType().Name} was refused: {reply.Rejection?.Message ?? reply.Error}");
                acted = true;
                break;
            }
            Assert.True(acted || last?.Turn is { Number: >= 2 }, "Nobody had a decision.");
        }

        Assert.True(played, "No card was played on the first turn.");
        Assert.Equal(2, last?.Turn?.Number);
        var firstPlayer = last!.Players.Single(p => p.Player != last.Turn!.TurnPlayer);
        Assert.Contains(firstPlayer.Base, c => c.CardId.StartsWith("filler-", StringComparison.Ordinal));
    }

    /// <summary>What a player would click: go first, keep the hand, play one 1-energy card on the first turn and pay it as suggested,
    /// otherwise the big button (pass or end the turn).</summary>
    private static PlayerAction Choose(BoardModel model, PlayerView view, CardBook book, ref bool played)
    {
        switch (model.Panel)
        {
            case PlayOrderPanel:
                return new ChoosePlayOrder(true);
            case MulliganPanel:
                return new Mulligan();
            case PlayOptionsPanel options:
                return new ChoosePlayOptions(options.Locations.FirstOrDefault()?.Place, false);
            case ResolvePanel:
                return new ResolveDone();
            case TurnPointPanel:
                return new ContinueTurn();
        }
        if (!played && view.Turn?.TurnPlayer == view.Viewer && model.Button.Label != "PAY")
        {
            foreach (var card in model.Me.Hand)
                if (book.Card(card.CardId)?.Energy == 1 && model.Click(card.Id) is SendStep { Action: PlayCard play })
                {
                    played = true;
                    return play;
                }
        }
        return Assert.IsType<SendStep>(model.Button.Step).Action;
    }
}
```

`CardEndpoints.CatalogOf` is internal to the server; the server's project already makes its internals visible to `CromoBound.Server.Tests`, and the client's to it too.

- [ ] **Step 3: Run the test**

Run: `dotnet test tests/CromoBound.Server.Tests --filter BoardGameTests`
Expected: PASS.
- If it fails because the engine asks for something `Choose` doesn't answer (another decision kind on the first turn), add that case to `Choose`, the smallest answer a player would give, and name it in the report.
- Don't change the engine.

- [ ] **Step 4: Add the manual checks**

In `docs/client-manual-checks.md`, add before the "Release build" section:

```markdown
## The board (4b-1)
- [ ] With the card images filled, every card on the board shows its art; with the `card-images` folder empty, cards show their name, cost and might instead, and the board still works.
- [ ] The board fills the window and keeps its shape when the window is resized; there is no top bar on the match page.
- [ ] A Bo1 without combat: play order, both mulligans (keep, and set aside 2), a unit played and paid with the suggestion, a unit moved to a battlefield, holding it scores, end turn, to the end of the game.
- [ ] Paying: clicking a rune cycles exhaust, recycle and unused; Suggest restores the suggestion; Cancel withdraws the play; a payment that isn't enough shows the engine's message and keeps the choice.
- [ ] Moving two units together offers only the battlefields both can reach; Esc and Cancel drop the move.
- [ ] A card the engine resolves by hand shows its text with Done; a start-of-turn card shows the turn point with Continue.
- [ ] Request undo shows "Waiting for ..." on your side and the question on the other; Refuse and Allow both work.
- [ ] A Bo3: the battlefield pick and the sideboarding dialogs, with the waiting state on the side that finished first.
- [ ] Hovering a card shows the card zoom with its damage and might; the XP switch in the board settings hides the legend XP and stays off after a reload.
- [ ] Stopping the server mid-turn locks the board under the reconnecting banner; the board comes back as it was.
```

- [ ] **Step 5: Run everything and commit**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

```bash
git add tests/CromoBound.Server.Tests docs/client-manual-checks.md
git commit -m "test(server): play a first turn through the board model"
```

---

## Done criteria

- `dotnet build CromoBound.slnx --no-incremental` reports 0 warnings and 0 errors, and `dotnet test CromoBound.slnx` passes.
- The match page shows the board from the player's view, scaled to the window, with no top bar.
- Every 4b-1 decision has its control:
  - battlefield pick, play order, sideboarding, mulligan;
  - priority: play, the card menus, runes, moving (several units included), hide and activate;
  - play options and paying;
  - resolve by hand and turn point;
  - the undo request and its answer.
- Any other decision shows the "comes in the next update" panel.
- A refused action shows the engine's message and leaves the board as it was. Nothing is sent twice while an action is in flight, or while the connection is down.
- The activity log, the card zoom and the XP setting work. Concede and the result and abandoned dialogs work as in 4a.
- One server test plays a first turn end to end through the real hub, with the board model choosing the actions.
- `docs/client-manual-checks.md` has the 4b-1 checks.
