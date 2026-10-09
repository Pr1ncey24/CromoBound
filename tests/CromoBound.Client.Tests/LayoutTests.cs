using Bunit;
using CromoBound.Client.Layout;
using CromoBound.Contracts;

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
}
