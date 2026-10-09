using System.Net;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Tests;

/// <summary>The hub's door: signed-out callers get nothing, and a changed account loses its live connections at once.</summary>
public class HubTests
{
    [Fact]
    public async Task Signed_out_the_hub_is_a_bare_401()
    {
        using var factory = new ServerFactory();
        var client = factory.NewClient();
        using var page = new HttpRequestMessage(HttpMethod.Get, "/hub");
        page.Headers.Accept.ParseAdd("text/html");

        var responses = new[] { await client.PostAsync("/hub/negotiate?negotiateVersion=1", null), await client.SendAsync(page) };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task A_forged_session_cant_open_a_connection()
    {
        using var factory = new ServerFactory();

        await Assert.ThrowsAnyAsync<Exception>(() => GameClient.ConnectWithCookieAsync(factory, "__Host-session=forged"));
    }

    [Fact]
    public async Task A_signed_in_player_connects()
    {
        using var factory = new ServerFactory();

        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");

        Assert.Equal(HubConnectionState.Connected, alice.Connection.State);
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("password")]
    [InlineData("promote")]
    public async Task A_changed_account_closes_its_live_connections_at_once(string change)
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("alice");
        var cookie = await factory.SessionCookieAsync("alice", ServerFactory.PlayerPassword);
        await using var alice = await GameClient.ConnectWithCookieAsync(factory, cookie);
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");

        await factory.WithStoreAsync(async users =>
        {
            var user = (await users.FindAsync("alice"))!;
            await (change switch
            {
                "disable" => users.SetDisabledAsync(user, true),
                "password" => users.SetPasswordAsync(user, "another-password-1"),
                _ => users.SetAdminAsync(user, true),
            });
        });

        await alice.Closed.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HubConnectionState.Connected, bob.Connection.State);
        await Assert.ThrowsAnyAsync<Exception>(() => GameClient.ConnectWithCookieAsync(factory, cookie));
    }

    [Fact]
    public async Task A_call_after_the_session_changed_elsewhere_is_refused_and_closes_the_connection()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await factory.WithDbAsync(db => db.Users.Where(u => u.UserName == "alice")
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.SecurityStamp, "changed-elsewhere")));

        await Assert.ThrowsAnyAsync<Exception>(() => alice.ChallengeAsync("bob", Decks.First));

        await alice.Closed.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(bob.All<ChallengeNotice>());
    }
}
