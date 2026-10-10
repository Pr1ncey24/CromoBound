using Bunit;
using CromoBound.Client.Pages;
using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Engine;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using Match = CromoBound.Client.Pages.Match;

namespace CromoBound.Client.Tests;

public class MatchPageTests
{
    private static readonly Guid M = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    private static Ui Playing(int game = 2, int myWins = 1, int theirWins = 0, MatchFormat format = MatchFormat.Bo3, int seat = 0)
    {
        var ui = new Ui();
        ui.Lobby.Load(new LobbyReply([], [], M, null, false),
            new MatchReply(M, Views.Of(format, MatchStage.Playing, game, myWins, theirWins, seat), "giulia"));
        ui.Lobby.SetConnection(HubState.Connected);
        return ui;
    }

    private static IRenderedComponent<Match> Render(Ui ui, Guid? id = null) => ui.Ctx.Render<Match>(ps => ps.Add(p => p.Id, id ?? M));

    [Fact]
    public async Task The_page_shows_the_players_format_stage_and_score()
    {
        await using var ui = Playing(seat: 1);

        var cut = Render(ui);

        cut.WaitForAssertion(() => Assert.Equal("marco vs giulia", cut.Find("h1").TextContent));
        Assert.Equal("Best of three · game 2", cut.Find("#match-format").TextContent);
        Assert.Equal("Playing", cut.Find("#match-stage").TextContent);
        Assert.Equal(("1", "0"), (cut.Find("#my-wins").TextContent, cut.Find("#their-wins").TextContent));
        Assert.Equal(3, cut.FindAll(".cb-pip").Count);
        Assert.Single(cut.FindAll(".cb-pip.on"));
        Assert.Contains("The board comes in the next update.", cut.Markup);
    }

    [Fact]
    public async Task A_match_that_isnt_yours_goes_back_to_the_lobby()
    {
        await using var ui = Playing();
        var other = Guid.NewGuid();
        ui.Nav.NavigateTo($"match/{other}");

        Render(ui, other);

        Assert.Equal("http://localhost/", ui.Nav.Uri);
    }

    [Fact]
    public async Task A_page_opened_before_the_lobby_loads_waits_for_it()
    {
        await using var ui = new Ui();
        ui.Nav.NavigateTo($"match/{M}");
        var cut = Render(ui);
        Assert.Contains("Loading the match...", cut.Markup);
        Assert.Equal($"http://localhost/match/{M}", ui.Nav.Uri);

        await cut.InvokeAsync(() => ui.Lobby.Load(new LobbyReply([], [], null, null, false), null));

        cut.WaitForAssertion(() => Assert.Equal("http://localhost/", ui.Nav.Uri));
    }

    [Fact]
    public async Task A_view_notice_updates_the_score()
    {
        await using var ui = Playing();
        var cut = Render(ui);

        await cut.InvokeAsync(() => ui.Lobby.View(new MatchViewNotice(M, Views.Of(game: 3, myWins: 1, theirWins: 1, stage: MatchStage.Mulligan))));

        cut.WaitForAssertion(() => Assert.Equal("1", cut.Find("#their-wins").TextContent));
        Assert.Equal("Best of three · game 3", cut.Find("#match-format").TextContent);
        Assert.Equal("Mulligan", cut.Find("#match-stage").TextContent);
    }

    [Fact]
    public async Task Conceding_asks_first_then_submits()
    {
        await using var ui = Playing();
        var dialogs = ui.RenderDialogs();
        var cut = Render(ui);

        await cut.ClickAsync("#concede");
        dialogs.WaitForAssertion(() => Assert.Contains(
            "giulia wins game 2. In a best of three, the match goes on to the next game unless this one decides it.", dialogs.Markup));
        await dialogs.ClickAsync("#keep-playing");
        Assert.Empty(ui.Hub.Calls);

        await cut.ClickAsync("#concede");
        await dialogs.ClickAsync("#confirm-concede");

        cut.WaitForAssertion(() => Assert.Equal(new[] { $"Submit {M} Concede" }, ui.Hub.Calls));
        Assert.IsType<Concede>(Assert.Single(ui.Hub.Actions));
    }

    [Fact]
    public async Task A_double_click_on_concede_asks_and_submits_once()
    {
        await using var ui = Playing();
        var dialogs = ui.RenderDialogs();
        var cut = Render(ui);
        var button = cut.WaitForElement("#concede");

        await cut.InvokeAsync(() =>
        {
            button.Click();
            button.Click();
        });

        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindAll("#confirm-concede")));
        await dialogs.ClickAsync("#confirm-concede");

        cut.WaitForAssertion(() => Assert.Equal(new[] { $"Submit {M} Concede" }, ui.Hub.Calls));
        Assert.Single(ui.Hub.Actions);
        cut.WaitForAssertion(() => Assert.False(cut.Find("#concede").HasAttribute("disabled")));
    }

    [Fact]
    public async Task In_a_best_of_one_conceding_gives_the_match()
    {
        await using var ui = Playing(game: 1, myWins: 0, format: MatchFormat.Bo1);
        var dialogs = ui.RenderDialogs();
        var cut = Render(ui);

        await cut.ClickAsync("#concede");

        dialogs.WaitForAssertion(() => Assert.Contains("giulia wins the match.", dialogs.Markup));
    }

    [Fact]
    public async Task A_refused_concede_shows_the_engines_reason()
    {
        await using var ui = Playing();
        ui.Hub.Submitted = new SubmitReply(false, new Rejection(RejectionCode.MatchOver, "The match is over."), null);
        var dialogs = ui.RenderDialogs();
        var cut = Render(ui);

        await cut.ClickAsync("#concede");
        await dialogs.ClickAsync("#confirm-concede");

        cut.WaitForAssertion(() => Assert.Contains("The match is over.", ui.Notices));
    }

    [Theory]
    [InlineData("giulia", 0, "giulia wins the match", 1, 2)]
    [InlineData("marco", 1, "You win the match", 2, 1)]
    public async Task The_end_of_the_match_names_the_winner_and_leads_back(string winner, int seat, string title, int mine, int theirs)
    {
        await using var ui = Playing(seat: seat);
        ui.Nav.NavigateTo($"match/{M}");
        var dialogs = ui.RenderDialogs();
        var cut = Render(ui);
        var wins = new int[2];
        wins[seat] = mine;
        wins[1 - seat] = theirs;

        await cut.InvokeAsync(() => ui.Lobby.MatchEnded(new MatchEndedNotice(M, MatchEndReason.Finished, wins, winner)));

        dialogs.WaitForAssertion(() => Assert.Equal(title, dialogs.Find(".mud-dialog-title").TextContent.Trim()));
        Assert.Equal(($"{mine}", $"{theirs}"), (dialogs.Find("#result-mine").TextContent, dialogs.Find("#result-theirs").TextContent));
        Assert.Equal($"http://localhost/match/{M}", ui.Nav.Uri);
        await dialogs.ClickAsync("#back-to-lobby");
        cut.WaitForAssertion(() => Assert.Equal("http://localhost/", ui.Nav.Uri));
    }

    [Fact]
    public async Task The_page_is_disabled_while_reconnecting()
    {
        await using var ui = Playing();
        ui.Lobby.SetConnection(HubState.Reconnecting);

        var cut = Render(ui);

        cut.WaitForAssertion(() => Assert.True(cut.Find("#concede").HasAttribute("disabled")));
    }
}
