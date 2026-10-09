using System.Net.Http.Json;
using CromoBound.Contracts;
using CromoBound.Engine.Matches;

namespace CromoBound.Server.Tests;

/// <summary>GetLobby (spec §6.1): one call that restores everything the lobby shows.</summary>
public class LobbyTests
{
    [Fact]
    public async Task The_lobby_lists_every_other_enabled_player_by_name_with_their_presence()
    {
        using var factory = new ServerFactory();
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await factory.AddUserAsync("dave");
        await factory.AddUserAsync("erin");
        await factory.WithStoreAsync(async users => await users.SetDisabledAsync((await users.FindAsync("erin"))!, true));

        var lobby = await carol.GetLobbyAsync();

        Assert.Equal(
            new[] { new PlayerPresence(ServerFactory.AdminName, false, false), new PlayerPresence("bob", true, false), new PlayerPresence("dave", false, false) },
            lobby.Players);
        Assert.Empty(lobby.Challenges);
        Assert.Null(lobby.MatchId);
        Assert.Null(lobby.Ended);
        Assert.False(lobby.Maintenance);
    }

    [Fact]
    public async Task Players_in_a_match_show_as_in_a_match_and_their_own_lobby_names_it()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");

        var seen = await carol.GetLobbyAsync();

        Assert.Contains(new PlayerPresence("alice", true, true), seen.Players);
        Assert.Contains(new PlayerPresence("bob", true, true), seen.Players);
        Assert.Null(seen.MatchId);
        Assert.Equal(players.MatchId, (await players.First.GetLobbyAsync()).MatchId);
        Assert.Equal(players.MatchId, (await players.Second.GetLobbyAsync()).MatchId);
    }

    [Fact]
    public async Task Your_open_challenges_come_back_after_a_reconnect()
    {
        using var factory = new ServerFactory();
        var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        var id = (await alice.ChallengeAsync("bob", Decks.First, MatchFormat.Bo3)).Id!.Value;
        await alice.DisposeAsync();

        await using var again = await GameClient.ConnectAsync(factory, "alice");
        var expected = new ChallengeInfo(id, "alice", "bob", MatchFormat.Bo3);

        Assert.Equal(new[] { expected }, (await again.GetLobbyAsync()).Challenges);
        Assert.Equal(new[] { expected }, (await bob.GetLobbyAsync()).Challenges);
        Assert.Empty((await carol.GetLobbyAsync()).Challenges);
    }

    [Fact]
    public async Task Maintenance_shows_in_the_lobby_and_its_switch_is_announced_to_everyone()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        var admin = await factory.SignInAdminAsync();

        await admin.PostAsJsonAsync("/api/admin/maintenance", new MaintenanceRequest(true));

        Assert.Equal(new MaintenanceNotice(true), await alice.WaitForAsync<MaintenanceNotice>());
        Assert.True((await alice.GetLobbyAsync()).Maintenance);

        await admin.PostAsJsonAsync("/api/admin/maintenance", new MaintenanceRequest(false));

        Assert.Equal(new MaintenanceNotice(false), await alice.WaitForAsync<MaintenanceNotice>(n => !n.On));
        Assert.False((await alice.GetLobbyAsync()).Maintenance);
    }
}
