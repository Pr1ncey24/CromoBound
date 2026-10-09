using CromoBound.Data;
using CromoBound.Engine.State;
using CromoBound.Models.Json;
using CromoBound.Server.Hubs;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

public class MatchStartTests
{
    [Fact]
    public async Task Accepting_starts_a_running_match_with_the_challenger_in_seat_0()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);

        Assert.Equal(new MatchStartedNotice(players.MatchId, "bob", new PlayerId(0)), await players.First.WaitForAsync<MatchStartedNotice>());
        Assert.Equal(new MatchStartedNotice(players.MatchId, "alice", new PlayerId(1)), await players.Second.WaitForAsync<MatchStartedNotice>());
        foreach (var client in new[] { players.First, players.Second })
            Assert.Equal(ChallengeEnd.Accepted, (await client.WaitForAsync<ChallengeClosedNotice>()).Reason);
        Assert.Equal(1, factory.Services.GetRequiredService<MatchRegistry>().Count);
        await factory.WithDbAsync(async db =>
        {
            var row = await db.Matches.SingleAsync();
            var ids = await db.Users.ToDictionaryAsync(u => u.UserName, u => u.Id);
            Assert.Equal((players.MatchId, MatchStatus.Running, ids["alice"], ids["bob"]), (row.Id, row.Status, row.Seat0UserId, row.Seat1UserId));
        });
    }

    [Fact]
    public async Task Each_player_is_sent_their_own_view_and_gets_it_back_from_GetMatch()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");

        foreach (var seat in new[] { new PlayerId(0), new PlayerId(1) })
        {
            var expected = TwoPlayers.AsReceived(players.Host.Match.ViewFor(seat));
            var pushed = await players[seat].WaitForAsync<MatchViewNotice>();
            var current = await players[seat].GetMatchAsync();

            Assert.Equal(players.MatchId, pushed.MatchId);
            Assert.Equal(expected, CromoJson.Serialize(pushed.View));
            Assert.Equal(players.MatchId, current.MatchId);
            Assert.Equal(expected, CromoJson.Serialize(current.View));
        }
        Assert.Equal(MatchReply.None, await carol.GetMatchAsync());
    }

    [Fact]
    public async Task An_illegal_deck_doesnt_accept_and_the_challenge_stays_open()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        var id = (await alice.ChallengeAsync("bob", Decks.First)).Id!.Value;

        var refused = await bob.AcceptAsync(id, Decks.Illegal);

        Assert.Equal(Lobby.IllegalDeck, refused.Error);
        Assert.Contains(refused.DeckIssues!, issue => issue.Code == DeckIssueCode.BattlefieldCount);
        Assert.Equal(Lobby.NoDeck, (await bob.AcceptAsync(id, null)).Error);
        Assert.Equal(0, factory.Services.GetRequiredService<MatchRegistry>().Count);
        Assert.Null((await bob.AcceptAsync(id, Decks.Second)).Error);
    }

    [Fact]
    public async Task Only_the_challenged_player_can_accept()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        var id = (await alice.ChallengeAsync("bob", Decks.First)).Id!.Value;

        Assert.Equal(Lobby.NoSuchChallenge, (await alice.AcceptAsync(id, Decks.Second)).Error);
        Assert.Equal(Lobby.NoSuchChallenge, (await carol.AcceptAsync(id, Decks.Second)).Error);
        Assert.Equal(Lobby.NoSuchChallenge, (await bob.AcceptAsync(Guid.NewGuid(), Decks.Second)).Error);

        Assert.Equal(0, factory.Services.GetRequiredService<MatchRegistry>().Count);
    }

    [Fact]
    public async Task A_challenge_from_a_player_disabled_since_is_withdrawn_on_accept()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        var id = (await alice.ChallengeAsync("bob", Decks.First)).Id!.Value;
        await factory.WithStoreAsync(async users => await users.SetDisabledAsync((await users.FindAsync("alice"))!, true));

        var reply = await bob.AcceptAsync(id, Decks.Second);

        Assert.Equal(Lobby.NoSuchChallenge, reply.Error);
        Assert.Equal(new ChallengeClosedNotice(id, ChallengeEnd.Withdrawn), await bob.WaitForAsync<ChallengeClosedNotice>());
        Assert.Equal(0, factory.Services.GetRequiredService<MatchRegistry>().Count);
    }

    [Fact]
    public async Task Players_in_a_match_cant_challenge_or_be_challenged()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");

        Assert.Equal(Lobby.YouArePlaying, (await players.First.ChallengeAsync("carol", Decks.First)).Error);
        Assert.Equal(Lobby.TheyArePlaying, (await carol.ChallengeAsync("bob", Decks.First)).Error);

        Assert.Empty(carol.All<ChallengeNotice>());
    }

    [Fact]
    public async Task Starting_a_match_withdraws_the_players_other_challenges()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        await using var dave = await GameClient.NewPlayerAsync(factory, "dave");
        var aliceToBob = (await alice.ChallengeAsync("bob", Decks.First)).Id!.Value;
        var carolToAlice = (await carol.ChallengeAsync("alice", Decks.First)).Id!.Value;
        var bobToDave = (await bob.ChallengeAsync("dave", Decks.Second)).Id!.Value;
        var daveToCarol = (await dave.ChallengeAsync("carol", Decks.First)).Id!.Value;

        Assert.Null((await bob.AcceptAsync(aliceToBob, Decks.Second)).Error);

        Assert.Equal(ChallengeEnd.Withdrawn, (await carol.WaitForAsync<ChallengeClosedNotice>(c => c.ChallengeId == carolToAlice)).Reason);
        Assert.Equal(ChallengeEnd.Withdrawn, (await dave.WaitForAsync<ChallengeClosedNotice>(c => c.ChallengeId == bobToDave)).Reason);
        Assert.Equal(Lobby.NoSuchChallenge, (await dave.AcceptAsync(bobToDave, Decks.First)).Error);
        Assert.Null((await carol.AcceptAsync(daveToCarol, Decks.Second)).Error);
    }
}
