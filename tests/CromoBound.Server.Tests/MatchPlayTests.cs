using CromoBound.Engine;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Server.Hubs;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

public class MatchPlayTests
{
    private static readonly PlayerId[] Seats = [new(0), new(1)];

    [Fact]
    public async Task Scripted_players_finish_a_bo1_through_the_hub_and_each_only_ever_sees_their_own_view()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);

        await players.PlayAsync();

        var ended = await players.First.WaitForAsync<MatchEndedNotice>();
        var other = await players.Second.WaitForAsync<MatchEndedNotice>();
        Assert.Equal((players.MatchId, MatchEndReason.Finished), (ended.MatchId, ended.Reason));
        Assert.Contains(ended.Winner, new[] { "alice", "bob" });
        Assert.Equal(1, ended.GameWins.Max());
        Assert.Equal((ended.MatchId, ended.Reason, ended.Winner), (other.MatchId, other.Reason, other.Winner));
        Assert.Equal(ended.GameWins, other.GameWins);
        Assert.Equal(0, factory.Services.GetRequiredService<MatchRegistry>().Count);
        Assert.Equal(MatchReply.None, await players.First.GetMatchAsync());
        await factory.WithDbAsync(async db => Assert.Equal(MatchStatus.Finished, (await db.Matches.SingleAsync()).Status));
        foreach (var seat in Seats)
        {
            var opponent = 1 - seat.Index;
            var views = players[seat].All<MatchViewNotice>();
            Assert.True(views.Count > 20, $"{seat} received {views.Count} views.");
            Assert.All(views, notice =>
            {
                var view = notice.View;
                Assert.Equal(seat, view.Viewer);
                Assert.Null(view.Players[opponent].Hand);
                Assert.Null(view.Players[opponent].Sideboard);
                Assert.True(view.Decision is null || view.Decision.Players.Contains(seat));
                Assert.All(view.Battlefields ?? [], b => Assert.True(b.Facedown is null || b.Facedown.Controller == seat));
            });
        }
    }

    [Fact]
    public async Task A_rejected_action_returns_the_engines_reason_and_saves_nothing()
    {
        using var factory = new ServerFactory(services: TestMatchStore.Register);
        await using var players = await TwoPlayers.StartAsync(factory);
        var store = factory.Services.GetRequiredService<TestMatchStore>();
        var decider = players.Host.Match.Pending!.Players[0];
        var other = new PlayerId(1 - decider.Index);

        var refused = await players[other].SubmitAsync(players.MatchId, new ChoosePlayOrder(true));

        Assert.False(refused.Accepted);
        Assert.Equal(RejectionCode.NotYourDecision, refused.Rejection!.Code);
        Assert.Equal(0, store.Saves);
        Assert.True((await players[decider].SubmitAsync(players.MatchId, new ChoosePlayOrder(true))).Accepted);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task A_match_the_player_isnt_in_reads_as_not_found_and_an_empty_action_is_refused()
    {
        using var factory = new ServerFactory();
        await using var first = await TwoPlayers.StartAsync(factory);
        await using var second = await TwoPlayers.StartAsync(factory, "carol", "dave");
        var action = new ChoosePlayOrder(true);

        Assert.Equal(MatchRegistry.NotFound, (await first.First.SubmitAsync(second.MatchId, action)).Error);
        Assert.Equal(MatchRegistry.NotFound, (await first.First.SubmitAsync(Guid.NewGuid(), action)).Error);
        Assert.Equal(MatchRegistry.UnreadableAction, (await first.First.SubmitRawAsync(first.MatchId, null)).Error);

        Assert.Equal(MatchStage.PlayOrder, second.Host.Match.Stage);
        Assert.Equal(MatchStage.PlayOrder, first.Host.Match.Stage);
    }

    [Fact]
    public async Task An_action_sent_without_its_type_is_a_plain_error_and_the_connection_stays_open()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        var decider = players[players.Host.Match.Pending!.Players[0]];

        await Assert.ThrowsAsync<HubException>(() => decider.SubmitRawAsync(players.MatchId, new { first = true }));

        Assert.Equal(HubConnectionState.Connected, decider.Connection.State);
        Assert.True((await decider.SubmitAsync(players.MatchId, new ChoosePlayOrder(true))).Accepted);
    }

    [Fact]
    public async Task Actions_sent_by_both_players_at_once_are_both_applied()
    {
        using var factory = new ServerFactory(services: TestMatchStore.Register);
        await using var players = await TwoPlayers.StartAsync(factory);
        var decider = players.Host.Match.Pending!.Players[0];
        Assert.True((await players[decider].SubmitAsync(players.MatchId, new ChoosePlayOrder(true))).Accepted);
        Assert.IsType<SideboardDecision>(players.Host.Match.Pending);

        var replies = await Task.WhenAll(Seats.Select(seat => players[seat].SubmitAsync(players.MatchId, new SubmitSideboard())));

        Assert.All(replies, reply => Assert.True(reply.Accepted, reply.Rejection?.Message ?? reply.Error));
        Assert.IsType<MulliganDecision>(players.Host.Match.Pending);
        Assert.Equal(3, factory.Services.GetRequiredService<TestMatchStore>().Saves);
    }

    [Fact]
    public async Task Conceding_ends_the_match_for_both_and_frees_the_players()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);

        Assert.True((await players.First.SubmitAsync(players.MatchId, new Concede())).Accepted);

        foreach (var seat in Seats)
        {
            var ended = await players[seat].WaitForAsync<MatchEndedNotice>();
            Assert.Equal((MatchEndReason.Finished, "bob"), (ended.Reason, ended.Winner));
            Assert.Equal(new[] { 0, 1 }, ended.GameWins);
        }
        Assert.Null((await players.Second.ChallengeAsync("alice", Decks.Second)).Error);
    }

    [Fact]
    public async Task A_failed_save_takes_the_match_back_to_its_last_saved_state()
    {
        using var factory = new ServerFactory(services: TestMatchStore.Register);
        await using var players = await TwoPlayers.StartAsync(factory);
        var store = factory.Services.GetRequiredService<TestMatchStore>();
        var decider = players.Host.Match.Pending!.Players[0];
        var before = TwoPlayers.AsReceived(players.Host.Match.ViewFor(decider));
        var viewsBefore = players[decider].All<MatchViewNotice>().Count;
        store.FailSaves = true;

        var failed = await players[decider].SubmitAsync(players.MatchId, new ChoosePlayOrder(true));

        Assert.Equal((false, MatchHost.SaveFailed), (failed.Accepted, failed.Error));
        Assert.Equal(before, TwoPlayers.AsReceived(players.Host.Match.ViewFor(decider)));
        Assert.Equal(viewsBefore, players[decider].All<MatchViewNotice>().Count);
        store.FailSaves = false;
        Assert.True((await players[decider].SubmitAsync(players.MatchId, new ChoosePlayOrder(true))).Accepted);
        Assert.IsType<SideboardDecision>(players.Host.Match.Pending);
    }

    [Fact]
    public async Task Every_connection_of_a_player_gets_the_views()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        var decider = players.Host.Match.Pending!.Players[0];
        await using var secondTab = await GameClient.ConnectAsync(factory, decider.Index == 0 ? "alice" : "bob");

        Assert.True((await players[decider].SubmitAsync(players.MatchId, new ChoosePlayOrder(true))).Accepted);

        var view = await secondTab.WaitForAsync<MatchViewNotice>();
        Assert.Equal((players.MatchId, decider, MatchStage.Sideboarding), (view.MatchId, view.View.Viewer, view.View.Stage));
    }
}
