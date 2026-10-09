using CromoBound.Contracts;
using CromoBound.Data;
using CromoBound.Engine.Matches;
using CromoBound.Server.Matches;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace CromoBound.Server.Tests;

public class ChallengeTests
{
    [Fact]
    public async Task A_challenge_reaches_the_opponent_whatever_the_case_of_their_name()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");

        var reply = await alice.ChallengeAsync("BOB", Decks.First, MatchFormat.Bo3);

        Assert.Null(reply.Error);
        var notice = await bob.WaitForAsync<ChallengeNotice>();
        Assert.Equal(new ChallengeNotice(reply.Id!.Value, "alice", MatchFormat.Bo3), notice);
        Assert.Empty(alice.All<ChallengeNotice>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Declining_or_cancelling_closes_the_challenge_for_both(bool decline)
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        var id = (await alice.ChallengeAsync("bob", Decks.First)).Id!.Value;

        var reply = decline ? await bob.DeclineAsync(id) : await alice.CancelAsync(id);

        Assert.Null(reply.Error);
        var closed = new ChallengeClosedNotice(id, decline ? ChallengeEnd.Declined : ChallengeEnd.Cancelled);
        Assert.Equal(closed, await alice.WaitForAsync<ChallengeClosedNotice>());
        Assert.Equal(closed, await bob.WaitForAsync<ChallengeClosedNotice>());
        Assert.Equal(Lobby.NoSuchChallenge, (await bob.DeclineAsync(id)).Error);
    }

    [Fact]
    public async Task Only_the_challenged_player_declines_and_only_the_challenger_cancels()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        var id = (await alice.ChallengeAsync("bob", Decks.First)).Id!.Value;

        Assert.Equal(Lobby.NoSuchChallenge, (await alice.DeclineAsync(id)).Error);
        Assert.Equal(Lobby.NoSuchChallenge, (await bob.CancelAsync(id)).Error);
        Assert.Equal(Lobby.NoSuchChallenge, (await carol.DeclineAsync(id)).Error);
        Assert.Equal(Lobby.NoSuchChallenge, (await bob.DeclineAsync(Guid.NewGuid())).Error);
        Assert.Empty(bob.All<ChallengeClosedNotice>());

        Assert.Null((await bob.DeclineAsync(id)).Error);
    }

    [Theory]
    [InlineData("nobody", Lobby.NoSuchPlayer)]
    [InlineData("carol", Lobby.NoSuchPlayer)]
    [InlineData("not a name", Lobby.NoSuchPlayer)]
    [InlineData("", Lobby.NoSuchPlayer)]
    [InlineData("ALICE", Lobby.NotYourself)]
    public async Task A_challenge_needs_another_enabled_player(string opponent, string error)
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await factory.AddUserAsync("carol");
        await factory.WithStoreAsync(async users => await users.SetDisabledAsync((await users.FindAsync("carol"))!, true));

        var reply = await alice.ChallengeAsync(opponent, Decks.First);

        Assert.Null(reply.Id);
        Assert.Equal(error, reply.Error);
        Assert.Empty(alice.All<ChallengeNotice>());
    }

    [Fact]
    public async Task A_challenger_has_one_open_challenge_at_a_time()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        var first = await alice.ChallengeAsync("bob", Decks.First);

        Assert.Equal(Lobby.AlreadyChallenging, (await alice.ChallengeAsync("carol", Decks.First)).Error);
        Assert.Empty(carol.All<ChallengeNotice>());
        Assert.Null((await bob.ChallengeAsync("alice", Decks.Second)).Error);

        await alice.CancelAsync(first.Id!.Value);
        Assert.Null((await alice.ChallengeAsync("carol", Decks.First)).Error);
    }

    [Fact]
    public async Task An_illegal_deck_is_refused_with_its_issues()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");

        var reply = await alice.ChallengeAsync("bob", Decks.Illegal);

        Assert.Equal(Lobby.IllegalDeck, reply.Error);
        Assert.Contains(reply.DeckIssues!, issue => issue.Code == DeckIssueCode.BattlefieldCount);
        Assert.Empty(bob.All<ChallengeNotice>());
    }

    [Fact]
    public async Task Unreadable_input_is_a_plain_error_and_the_connection_stays_open()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");

        Assert.Equal(Lobby.NoDeck, (await alice.ChallengeAsync("bob", null)).Error);
        await Assert.ThrowsAsync<HubException>(() => alice.Connection.InvokeAsync<HubReply>("Challenge", "bob", "Bo5", Decks.First));
        await Assert.ThrowsAsync<HubException>(() => alice.Connection.InvokeAsync<HubReply>("CancelChallenge", "not-a-guid"));

        Assert.Equal(HubConnectionState.Connected, alice.Connection.State);
        Assert.Null((await alice.ChallengeAsync("bob", Decks.First)).Error);
    }
}
