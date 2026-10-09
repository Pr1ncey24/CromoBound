using System.Net.Http.Json;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>Presence notices (spec §6.2): every other connected player hears when someone comes, goes, starts or ends a match, or is
/// created, disabled or enabled. No one hears about themselves.</summary>
public class PresenceTests
{
    private static IReadOnlyList<PlayerPresence> About(GameClient client, string userName) =>
        [.. client.All<PlayerPresence>().Where(p => p.UserName == userName)];

    [Fact]
    public async Task Presence_changes_only_with_the_first_and_the_last_tab()
    {
        using var factory = new ServerFactory();
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await factory.AddUserAsync("alice");

        var first = await GameClient.ConnectAsync(factory, "alice");
        await bob.WaitForAsync<PlayerPresence>(p => p is { UserName: "alice", Online: true });
        var second = await GameClient.ConnectAsync(factory, "alice");
        await first.DisposeAsync();
        await second.DisposeAsync();
        await bob.WaitForAsync<PlayerPresence>(p => p is { UserName: "alice", Online: false });

        Assert.Equal(new[] { new PlayerPresence("alice", true, false), new PlayerPresence("alice", false, false) }, About(bob, "alice"));
        Assert.Empty(About(first, "alice"));
        Assert.Empty(About(second, "alice"));
    }

    [Fact]
    public async Task Starting_and_ending_a_match_is_announced_to_the_other_players()
    {
        using var factory = new ServerFactory();
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        await using var players = await TwoPlayers.StartAsync(factory);

        await carol.WaitForAsync<PlayerPresence>(p => p is { UserName: "alice", InMatch: true });
        await carol.WaitForAsync<PlayerPresence>(p => p is { UserName: "bob", InMatch: true });
        var seen = carol.All<PlayerPresence>().Count;

        Assert.True((await players.First.SubmitAsync(players.MatchId, new Concede())).Accepted);

        foreach (var name in new[] { "alice", "bob" })
            Assert.Equal(new PlayerPresence(name, true, false), await carol.WaitForAsync<PlayerPresence>(p => p.UserName == name, after: seen));
    }

    [Fact]
    public async Task Admins_creating_disabling_and_enabling_a_user_is_announced()
    {
        using var factory = new ServerFactory();
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        var admin = await factory.SignInAdminAsync();

        var created = await admin.PostAsJsonAsync("/api/admin/users", new CreateUserRequest("erin", ServerFactory.PlayerPassword, false));
        var erin = (await created.Content.ReadFromJsonAsync<UserSummary>())!;

        Assert.Equal(new PlayerPresence("erin", false, false), await bob.WaitForAsync<PlayerPresence>(p => p.UserName == "erin"));
        await admin.PutAsJsonAsync($"/api/admin/users/{erin.Id}/disabled", new DisabledRequest(true));
        Assert.Equal(new PlayerLeftNotice("erin"), await bob.WaitForAsync<PlayerLeftNotice>());
        var seen = bob.All<PlayerPresence>().Count;
        await admin.PutAsJsonAsync($"/api/admin/users/{erin.Id}/disabled", new DisabledRequest(false));
        Assert.Equal(new PlayerPresence("erin", false, false), await bob.WaitForAsync<PlayerPresence>(p => p.UserName == "erin", after: seen));
    }

    [Fact]
    public async Task Disabling_a_connected_player_removes_them_and_their_disconnect_doesnt_bring_them_back()
    {
        using var factory = new ServerFactory();
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        await bob.WaitForAsync<PlayerPresence>(p => p is { UserName: "carol", Online: true });
        var seen = bob.All<PlayerPresence>().Count;
        var admin = await factory.SignInAdminAsync();
        var carolId = (await admin.GetFromJsonAsync<List<UserSummary>>("/api/admin/users"))!.Single(u => u.UserName == "carol").Id;

        await admin.PutAsJsonAsync($"/api/admin/users/{carolId}/disabled", new DisabledRequest(true));

        await carol.Closed.WaitAsync(TimeSpan.FromSeconds(10));
        await bob.WaitForAsync<PlayerLeftNotice>(n => n.UserName == "carol");
        await bob.WaitForAsync<PlayerLeftNotice>(n => n.UserName == "carol", after: 1);
        Assert.DoesNotContain(bob.All<PlayerPresence>().Skip(seen), p => p.UserName == "carol");
        Assert.DoesNotContain((await bob.GetLobbyAsync()).Players, p => p.UserName == "carol");
    }

    [Fact]
    public async Task A_failing_announcement_breaks_neither_a_connection_nor_an_admin_creating_a_user()
    {
        using var factory = new ServerFactory(services: services => services.AddSingleton<IHubContext<GameHub, IGameClient>, ThrowingHubContext>());
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        var admin = await factory.SignInAdminAsync();

        var created = await admin.PostAsJsonAsync("/api/admin/users", new CreateUserRequest("erin", ServerFactory.PlayerPassword, false));

        Assert.Equal(System.Net.HttpStatusCode.Created, created.StatusCode);
        Assert.False(bob.Closed.IsCompleted);
        Assert.Contains((await bob.GetLobbyAsync()).Players, p => p.UserName == "erin");
    }

    /// <summary>A hub context whose broadcast to everyone but some connections always fails, as a broken backplane would.</summary>
    private sealed class ThrowingHubContext : IHubContext<GameHub, IGameClient>
    {
        public IHubClients<IGameClient> Clients { get; } = new ThrowingClients();

        public IGroupManager Groups => throw new NotSupportedException();

        private sealed class ThrowingClients : IHubClients<IGameClient>
        {
            public IGameClient All => throw new NotSupportedException();

            public IGameClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new InvalidOperationException("The broadcast failed.");

            public IGameClient Client(string connectionId) => throw new NotSupportedException();

            public IGameClient Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

            public IGameClient Group(string groupName) => throw new NotSupportedException();

            public IGameClient Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

            public IGameClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

            public IGameClient User(string userId) => throw new NotSupportedException();

            public IGameClient Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
        }
    }
}
