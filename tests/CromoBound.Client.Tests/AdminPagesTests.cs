using Bunit;
using CromoBound.Client.Pages.Admin;
using CromoBound.Client.Services;
using CromoBound.Contracts;

namespace CromoBound.Client.Tests;

public class AdminPagesTests
{
    private static Ui Admin()
    {
        var ui = new Ui(new MeResponse("marco", true));
        ui.Api.Users.AddRange([new(1, "admin", true, false), new(2, "giulia", false, false), new(3, "marco", true, false), new(4, "tommaso", false, true)]);
        return ui;
    }

    private static string Row(IRenderedComponent<Users> cut, string user) =>
        string.Join("|", cut.FindAll($"[data-user='{user}'] td").Take(3).Select(td => td.TextContent.Trim()));

    [Fact]
    public async Task Users_are_listed_with_their_role_and_account()
    {
        await using var ui = Admin();

        var cut = ui.Ctx.Render<Users>();

        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr").Count));
        Assert.Equal("admin|Admin|Active", Row(cut, "admin"));
        Assert.Equal("giulia|Player|Active", Row(cut, "giulia"));
        Assert.Equal("marco (you)|Admin|Active", Row(cut, "marco"));
        Assert.Equal("tommaso|Player|Disabled", Row(cut, "tommaso"));
        Assert.Equal(new[] { "Set password", "Make admin", "Enable" },
            cut.FindAll("[data-user='tommaso'] button").Select(b => b.TextContent.Trim()));
    }

    [Fact]
    public async Task A_player_gets_a_plain_refusal()
    {
        await using var ui = new Ui();

        var cut = ui.Ctx.Render<Users>();

        Assert.Equal(ServerApi.Forbidden, cut.Find("[role=alert]").TextContent);
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public async Task Switching_the_role_and_the_account_calls_the_server_and_reloads()
    {
        await using var ui = Admin();
        var cut = ui.Ctx.Render<Users>();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr").Count));

        await cut.ClickAsync("[data-user='giulia'] .toggle-admin");
        await cut.ClickAsync("[data-user='giulia'] .toggle-disabled");

        cut.WaitForAssertion(() => Assert.Equal("giulia|Admin|Disabled", Row(cut, "giulia")));
        Assert.Equal(new[] { "admin 2 True", "disabled 2 True" }, ui.Api.Calls);
    }

    [Fact]
    public async Task A_refused_change_shows_the_servers_message()
    {
        await using var ui = Admin();
        ui.Api.NextError = "You can't do that to your own account.";
        var cut = ui.Ctx.Render<Users>();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr").Count));

        await cut.ClickAsync("[data-user='marco'] .toggle-disabled");

        cut.WaitForAssertion(() => Assert.Contains("You can't do that to your own account.", ui.Notices));
        Assert.Equal("marco (you)|Admin|Active", Row(cut, "marco"));
    }

    [Fact]
    public async Task Creating_a_user_keeps_the_dialog_open_on_a_refusal()
    {
        await using var ui = Admin();
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Users>();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr").Count));
        ui.Api.NextError = "A password has at least 12 characters.";

        await cut.ClickAsync("#create-user");
        dialogs.WaitForElement("#new-username").Input("chiara");
        dialogs.Find("#new-password").Input("short");
        await dialogs.InvokeAsync(() => dialogs.Find("#new-admin").Change(true));
        await dialogs.ClickAsync("#create");
        dialogs.WaitForAssertion(() => Assert.Equal("A password has at least 12 characters.", dialogs.Find("[role=alert]").TextContent));
        Assert.Equal("chiara", dialogs.Find("#new-username").GetAttribute("value"));

        dialogs.Find("#new-password").Input("a-long-enough-password");
        await dialogs.ClickAsync("#create");

        cut.WaitForAssertion(() => Assert.Equal("chiara|Admin|Active", Row(cut, "chiara")));
        Assert.Empty(dialogs.FindAll("#create"));
        Assert.Equal(new[] { "create chiara True", "create chiara True" }, ui.Api.Calls);
    }

    [Fact]
    public async Task Setting_a_password_confirms_it()
    {
        await using var ui = Admin();
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Users>();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr").Count));

        await cut.ClickAsync("[data-user='giulia'] .set-password");
        dialogs.WaitForElement("#password-input").Input("a-long-enough-password");
        await dialogs.ClickAsync("#save-password");

        cut.WaitForAssertion(() => Assert.Contains("giulia's password is set.", ui.Notices));
        Assert.Equal(new[] { "password 2" }, ui.Api.Calls);
    }

    [Fact]
    public async Task The_maintenance_page_switches_and_counts_running_matches_live()
    {
        await using var ui = Admin();
        ui.Api.Maintenance = new MaintenanceStatus(false, 2);
        var cut = ui.Ctx.Render<Maintenance>();
        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("#running").TextContent));
        Assert.False(cut.Find("#maintenance-switch").HasAttribute("checked"));

        await cut.InvokeAsync(() => cut.Find("#maintenance-switch").Change(true));
        cut.WaitForAssertion(() => Assert.True(cut.Find("#maintenance-switch").HasAttribute("checked")));
        Assert.Equal(new[] { "maintenance True" }, ui.Api.Calls);

        ui.Api.Maintenance = ui.Api.Maintenance with { RunningMatches = 0 };
        await cut.InvokeAsync(() => ui.Lobby.PlayerChanged(new PlayerPresence("giulia", true, false)));
        cut.WaitForAssertion(() => Assert.Equal("0", cut.Find("#running").TextContent));
    }

    [Fact]
    public async Task A_refused_switch_stays_as_it_was()
    {
        await using var ui = Admin();
        ui.Api.NextError = ServerApi.Unexpected;
        var cut = ui.Ctx.Render<Maintenance>();
        cut.WaitForAssertion(() => Assert.Equal("0", cut.Find("#running").TextContent));

        await cut.InvokeAsync(() => cut.Find("#maintenance-switch").Change(true));

        cut.WaitForAssertion(() => Assert.Contains(ServerApi.Unexpected, ui.Notices));
        Assert.False(cut.Find("#maintenance-switch").HasAttribute("checked"));
    }
}
