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

        twirler.QuerySelector("img")!.TriggerEvent("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());

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

    [Fact]
    public async Task A_battlefields_ability_menu_is_titled_with_the_battlefield()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        var lane = board.AddLane("bf-a", TestBoard.Me);

        var cut = Render(ui, board.Model(TestBoard.Priority(activations: [new ActivateOption(lane.Id, 0)])), []);
        await cut.ClickAsync("button[aria-label^='Back-Alley Bar']");

        Assert.Equal("Back-Alley Bar", cut.Find(".cb-menu .cb-lbl").TextContent);
    }

    [Fact]
    public async Task A_zoom_is_dropped_when_a_new_model_no_longer_has_the_card()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        var unit = board.Add(board.MyBase, "unit-a", might: 2);
        board.Add(board.MyBase, "unit-b", might: 1);
        var cut = Render(ui, board.Model(), []);
        await cut.InvokeAsync(() => cut.Find("[aria-label^='Blade Twirler']").MouseEnter());
        Assert.Single(cut.FindAll(".cb-zoom"));

        board.MyBase.Remove(unit);
        cut.Render(ps => ps.Add(p => p.Model, board.Model()));

        Assert.Empty(cut.FindAll(".cb-zoom"));
    }

    [Fact]
    public async Task A_zoom_follows_its_card_into_a_new_model()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        var unit = board.Add(board.MyBase, "unit-a", might: 2);
        var cut = Render(ui, board.Model(), []);
        await cut.InvokeAsync(() => cut.Find("[aria-label^='Blade Twirler']").MouseEnter());

        board.MyBase[0] = unit with { Damage = 1 };
        cut.Render(ps => ps.Add(p => p.Model, board.Model()));

        Assert.Contains("1 damage", cut.Find(".cb-zoom").TextContent);
    }
}
