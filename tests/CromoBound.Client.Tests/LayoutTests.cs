using Bunit;
using CromoBound.Client.Layout;
using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;

namespace CromoBound.Client.Tests;

public class LayoutTests
{
    [Fact]
    public async Task The_top_bar_shows_the_player_and_no_admin_link_to_a_player()
    {
        await using var ui = new Ui();

        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#page")));
        Assert.Contains("marco", cut.Find(".cb-bar").TextContent);
        Assert.Equal(new[] { "Play", "Decks" }, cut.FindAll(".cb-nav a").Select(a => a.TextContent.Trim()));
    }

    [Fact]
    public async Task An_admin_also_gets_the_admin_link()
    {
        await using var ui = new Ui(new MeResponse("admin", true));

        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));

        cut.WaitForAssertion(() => Assert.Equal(new[] { "Play", "Decks", "Admin" }, cut.FindAll(".cb-nav a").Select(a => a.TextContent.Trim())));
    }

    [Fact]
    public async Task A_401_ends_the_session_once_and_the_layout_goes_to_login()
    {
        await using var ui = new Ui(signedIn: false);
        ui.Api.SessionOver = true;

        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));

        cut.WaitForAssertion(() => Assert.Equal("http://localhost/login", ui.Nav.Uri));
        Assert.Empty(cut.FindAll("#page"));
        Assert.Equal(1, ui.Api.MeCalls);
    }

    [Fact]
    public async Task Signing_out_posts_logout_and_goes_to_login()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#page")));

        await cut.ClickAsync("#sign-out");

        Assert.Contains("logout", ui.Api.Calls);
        Assert.Equal("http://localhost/login", ui.Nav.Uri);
    }

    [Fact]
    public async Task The_layout_starts_the_hub_and_shows_the_banners()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));
        cut.WaitForAssertion(() => Assert.Equal(1, ui.Hub.Starts));

        await cut.InvokeAsync(() => ui.Hub.SetStateAsync(HubState.Reconnecting));
        cut.WaitForAssertion(() => Assert.Contains("Reconnecting...", cut.Find(".cb-banner.warn").TextContent));

        await cut.InvokeAsync(() => ui.Hub.SetStateAsync(HubState.Connected));
        await cut.InvokeAsync(() => ui.Hub.Push(c => c.MaintenanceChanged(new MaintenanceNotice(true))));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".cb-banner.warn")));
        Assert.Contains("The server is in maintenance.", cut.Find(".cb-banner.info").TextContent);
    }

    [Fact]
    public async Task A_notice_pops_up_as_a_notification()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));
        cut.WaitForAssertion(() => Assert.Equal(1, ui.Hub.Starts));
        await cut.InvokeAsync(() => ui.Hub.SetStateAsync(HubState.Connected));

        await cut.InvokeAsync(() => ui.Hub.Push(c => c.ChallengeReceived(new ChallengeNotice(Guid.NewGuid(), "giulia", MatchFormat.Bo3))));

        cut.WaitForAssertion(() => Assert.Contains("giulia challenges you to a best of three.", ui.Notices));
    }

    [Fact]
    public async Task A_started_match_opens_its_page_from_anywhere()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));
        cut.WaitForAssertion(() => Assert.Equal(1, ui.Hub.Starts));
        await cut.InvokeAsync(() => ui.Hub.SetStateAsync(HubState.Connected));
        var match = Guid.NewGuid();

        await cut.InvokeAsync(() => ui.Hub.Push(c => c.MatchStarted(new MatchStartedNotice(match, "giulia", new PlayerId(0)))));

        cut.WaitForAssertion(() => Assert.EndsWith($"/match/{match}", ui.Nav.Uri));
    }

    [Fact]
    public async Task A_started_match_whose_page_is_open_adds_no_history_entry()
    {
        await using var ui = new Ui();
        var match = Guid.NewGuid();
        ui.Nav.NavigateTo($"match/{match}");
        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));
        cut.WaitForAssertion(() => Assert.Equal(1, ui.Hub.Starts));
        var before = ui.Nav.History.Count;

        await cut.InvokeAsync(() => ui.Hub.Push(c => c.MatchStarted(new MatchStartedNotice(match, "giulia", new PlayerId(0)))));

        Assert.Equal(before, ui.Nav.History.Count);
    }

    [Fact]
    public async Task A_match_abandoned_at_startup_is_explained_once()
    {
        await using var ui = new Ui();
        ui.Hub.Lobby = new LobbyReply([], [], null, new MatchEndedNotice(Guid.NewGuid(), MatchEndReason.Abandoned, [0, 0], null), false);
        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));
        cut.WaitForAssertion(() => Assert.Equal(1, ui.Hub.Starts));

        await cut.InvokeAsync(() => ui.Hub.SetStateAsync(HubState.Connected));

        cut.WaitForAssertion(() => Assert.Contains("The server was updated while your match was running", cut.Markup));
        await cut.ClickAsync("#abandoned-ok");
        await cut.InvokeAsync(() => ui.Lobby.SetConnection(HubState.Connected));
        cut.WaitForAssertion(() => Assert.DoesNotContain("The server was updated while your match was running", cut.Markup));
    }
}
