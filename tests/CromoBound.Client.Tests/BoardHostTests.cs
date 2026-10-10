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
        cut.Render(ps => ps.Add(p => p.View, board.View(TestBoard.Priority())));
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

        cut.Render(ps => ps.Add(p => p.View, board.View(priority)));

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

    [Fact]
    public async Task A_page_render_with_the_same_view_keeps_the_panel_choices()
    {
        await using var ui = Connected();
        var board = new TestBoard { Stage = Engine.Matches.MatchStage.Mulligan };
        var hand = new[] { "unit-a", "unit-b" }.Select(id => board.Add(board.Hand, id)).ToList();
        var view = board.View(new MulliganDecision(TestBoard.Me, [.. hand.Select(c => c.Id)]));

        var cut = Render(ui, view);
        cut.WaitForElement("#aside-0");
        await cut.InvokeAsync(() => cut.Find("#aside-0").Change(true));
        Assert.Equal("Set aside 1 and draw", cut.Find("#set-aside").TextContent.Trim());
        cut.Render(ps => ps.Add(p => p.View, view).Add(p => p.Me, "marco").Add(p => p.Opponent, "giulia").Add(p => p.CanConcede, true));

        Assert.Equal("Set aside 1 and draw", cut.Find("#set-aside").TextContent.Trim());
    }

    [Fact]
    public async Task A_page_render_with_the_same_view_keeps_a_half_made_move()
    {
        await using var ui = Connected();
        var board = new TestBoard();
        board.AddLane("bf-a");
        var unit = board.Add(board.MyBase, "unit-b", might: 1);
        var view = board.View(TestBoard.Priority(moves: [new MoveOption(unit.Id, [Place.Battlefield(0)])]));

        var cut = Render(ui, view);
        await cut.ClickAsync("button[aria-label^='Daring Poro']");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".cb-hint")));
        cut.Render(ps => ps.Add(p => p.View, view).Add(p => p.Me, "marco").Add(p => p.Opponent, "giulia").Add(p => p.CanConcede, true));

        Assert.NotEmpty(cut.FindAll(".cb-hint"));
    }
}
