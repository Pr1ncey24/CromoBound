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

    [Fact]
    public async Task Once_two_cards_are_set_aside_the_other_boxes_are_disabled()
    {
        await using var ui = new Ui();
        var board = new TestBoard();
        var hand = new[] { "unit-a", "unit-b", "spell-a" }.Select(id => board.Add(board.Hand, id)).ToList();
        var model = board.Model(new Engine.Decisions.MulliganDecision(TestBoard.Me, [.. hand.Select(c => c.Id)]));
        var (cut, _) = Render(ui, model.Panel!);

        await cut.InvokeAsync(() => cut.Find("#aside-0").Change(true));
        await cut.InvokeAsync(() => cut.Find("#aside-1").Change(true));

        Assert.False(cut.Find("#aside-0").HasAttribute("disabled"));
        Assert.False(cut.Find("#aside-1").HasAttribute("disabled"));
        Assert.True(cut.Find("#aside-2").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("#aside-1").Change(false));
        Assert.False(cut.Find("#aside-2").HasAttribute("disabled"));
    }
}
