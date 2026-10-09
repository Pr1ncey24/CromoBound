using System.Net;
using System.Net.Http.Json;
using CromoBound.Server.Accounts;
using CromoBound.Server.Matches;

namespace CromoBound.Server.Tests;

public class MaintenanceTests
{
    private const string Route = "/api/admin/maintenance";

    private static Task<HttpResponseMessage> SwitchAsync(HttpClient client, bool? on) => client.PostAsJsonAsync(Route, new MaintenanceRequest(on));

    [Fact]
    public async Task Maintenance_stops_new_challenges_and_matches_but_not_running_ones()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        await using var dave = await GameClient.NewPlayerAsync(factory, "dave");
        var open = (await dave.ChallengeAsync("carol", Decks.First)).Id!.Value;
        var admin = await factory.SignInAdminAsync();

        var on = await SwitchAsync(admin, true);

        Assert.Equal(new MaintenanceStatus(true, 1), await on.Content.ReadFromJsonAsync<MaintenanceStatus>());
        Assert.Equal(Lobby.InMaintenance, (await carol.AcceptAsync(open, Decks.Second)).Error);
        Assert.Equal(Lobby.InMaintenance, (await carol.ChallengeAsync("dave", Decks.Second)).Error);
        Assert.Equal(players.MatchId, (await players.First.GetMatchAsync()).MatchId);
        Assert.Equal(new MaintenanceStatus(true, 1), await admin.GetFromJsonAsync<MaintenanceStatus>(Route));

        await SwitchAsync(admin, false);

        Assert.Null((await carol.AcceptAsync(open, Decks.Second)).Error);
        Assert.Equal(new MaintenanceStatus(false, 2), await admin.GetFromJsonAsync<MaintenanceStatus>(Route));
    }

    [Fact]
    public async Task Only_admins_see_or_switch_maintenance_and_a_switch_must_say_which()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");
        var player = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
        var admin = await factory.SignInAdminAsync();

        foreach (var response in new[] { await player.GetAsync(Route), await SwitchAsync(player, true) })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await SwitchAsync(factory.NewClient(), true)).StatusCode);
        var vague = await SwitchAsync(admin, null);
        Assert.Equal(HttpStatusCode.BadRequest, vague.StatusCode);
        Assert.Equal(MaintenanceEndpoints.SayOn, (await vague.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
        Assert.Equal(new MaintenanceStatus(false, 0), await admin.GetFromJsonAsync<MaintenanceStatus>(Route));
    }
}
