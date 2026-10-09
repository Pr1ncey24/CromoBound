using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Models.Json;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>A server restart (spec §6.6): running matches come back exactly; one that can't is abandoned, and its players are told
/// once.</summary>
public class RestartTests
{
    /// <summary>Runs a first server over the file, starts a match between alice and bob, lets <paramref name="before"/> act, then stops
    /// the server.</summary>
    private static async Task<Guid> FirstServerAsync(string path, Func<ServerFactory, TwoPlayers, Task> before)
    {
        using var first = new ServerFactory(databasePath: path);
        await using var players = await TwoPlayers.StartAsync(first);
        await before(first, players);
        return players.MatchId;
    }

    private static Task EditRecordAsync(ServerFactory factory, Func<string, string> edit) =>
        factory.WithDbAsync(async db =>
        {
            var row = await db.Matches.SingleAsync();
            row.RecordJson = edit(row.RecordJson);
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task A_restart_in_the_middle_of_a_match_loses_nothing()
    {
        var path = ServerFactory.NewDatabasePath();
        try
        {
            string firstView = "", secondView = "";
            var matchId = await FirstServerAsync(path, async (_, players) =>
            {
                await players.PlayAsync(actions: 12);
                firstView = CromoJson.Serialize((await players.First.GetMatchAsync()).View);
                secondView = CromoJson.Serialize((await players.Second.GetMatchAsync()).View);
            });

            using var second = new ServerFactory(databasePath: path);
            await using var again = await TwoPlayers.ReconnectAsync(second, matchId);
            var current = await again.First.GetMatchAsync();

            Assert.Equal(matchId, current.MatchId);
            Assert.Equal(firstView, CromoJson.Serialize(current.View));
            Assert.Equal(secondView, CromoJson.Serialize((await again.Second.GetMatchAsync()).View));
            await again.PlayAsync();
            Assert.Equal(MatchEndReason.Finished, (await again.First.WaitForAsync<MatchEndedNotice>()).Reason);
        }
        finally
        {
            ServerFactory.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task A_match_restarted_before_its_first_action_resumes()
    {
        var path = ServerFactory.NewDatabasePath();
        try
        {
            var matchId = await FirstServerAsync(path, (_, _) => Task.CompletedTask);

            using var second = new ServerFactory(databasePath: path);
            await using var again = await TwoPlayers.ReconnectAsync(second, matchId);

            Assert.Equal(matchId, (await again.First.GetMatchAsync()).MatchId);
            await again.PlayAsync();
            Assert.Equal(MatchEndReason.Finished, (await again.First.WaitForAsync<MatchEndedNotice>()).Reason);
        }
        finally
        {
            ServerFactory.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task A_match_saved_by_another_engine_build_is_abandoned_and_its_players_are_told_once()
    {
        var path = ServerFactory.NewDatabasePath();
        try
        {
            var matchId = await FirstServerAsync(path, (first, _) =>
                EditRecordAsync(first, json => MatchStore.Json(MatchStore.Read(json) with { EngineVersion = "0.0.0-old" })));

            using var second = new ServerFactory(databasePath: path);
            await using var alice = await GameClient.ConnectAsync(second, "alice");
            await using var bob = await GameClient.ConnectAsync(second, "bob");
            var told = await alice.GetMatchAsync();

            Assert.Equal(0, second.Services.GetRequiredService<MatchRegistry>().Count);
            await second.WithDbAsync(async db => Assert.Equal(MatchStatus.Abandoned, (await db.Matches.SingleAsync()).Status));
            Assert.Null(told.MatchId);
            Assert.Equal((matchId, MatchEndReason.Abandoned, (string?)null), (told.Ended!.MatchId, told.Ended.Reason, told.Ended.Winner));
            Assert.Equal(MatchReply.None, await alice.GetMatchAsync());
            Assert.Equal(MatchEndReason.Abandoned, (await bob.GetMatchAsync()).Ended!.Reason);
            Assert.Null((await alice.ChallengeAsync("bob", Decks.First)).Error);
        }
        finally
        {
            ServerFactory.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task An_unreadable_match_record_is_abandoned_and_the_server_still_starts()
    {
        var path = ServerFactory.NewDatabasePath();
        try
        {
            var matchId = await FirstServerAsync(path, (first, _) => EditRecordAsync(first, _ => "{ not a record"));

            using var second = new ServerFactory(databasePath: path);
            await using var alice = await GameClient.ConnectAsync(second, "alice");

            await second.WithDbAsync(async db => Assert.Equal(MatchStatus.Abandoned, (await db.Matches.SingleAsync()).Status));
            Assert.Equal(0, second.Services.GetRequiredService<MatchRegistry>().Count);
            Assert.Equal(matchId, (await alice.GetMatchAsync()).Ended!.MatchId);
        }
        finally
        {
            ServerFactory.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task A_match_whose_seat_user_no_longer_exists_is_abandoned_and_the_server_still_starts()
    {
        var path = ServerFactory.NewDatabasePath();
        try
        {
            var matchId = await FirstServerAsync(path, (first, _) => first.WithDbAsync(async db =>
            {
                // The owner may delete users by hand with foreign keys off, so do the same here.
                await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF; UPDATE Matches SET Seat1UserId = 9999;");
                Assert.Equal(9999, (await db.Matches.AsNoTracking().SingleAsync()).Seat1UserId);
            }));

            using var second = new ServerFactory(databasePath: path);
            await using var alice = await GameClient.ConnectAsync(second, "alice");
            var told = await alice.GetMatchAsync();

            Assert.Equal(0, second.Services.GetRequiredService<MatchRegistry>().Count);
            await second.WithDbAsync(async db => Assert.Equal(MatchStatus.Abandoned, (await db.Matches.SingleAsync()).Status));
            Assert.Equal((matchId, MatchEndReason.Abandoned), (told.Ended!.MatchId, told.Ended.Reason));
            Assert.Equal(MatchReply.None, await alice.GetMatchAsync());
        }
        finally
        {
            ServerFactory.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task A_finished_match_stays_finished_and_keeps_its_record()
    {
        var path = ServerFactory.NewDatabasePath();
        try
        {
            await FirstServerAsync(path, async (_, players) =>
                Assert.True((await players.First.SubmitAsync(players.MatchId, new Concede())).Accepted));

            using var second = new ServerFactory(databasePath: path);
            await using var alice = await GameClient.ConnectAsync(second, "alice");

            Assert.Equal(0, second.Services.GetRequiredService<MatchRegistry>().Count);
            Assert.Equal(MatchReply.None, await alice.GetMatchAsync());
            await second.WithDbAsync(async db =>
            {
                var row = await db.Matches.SingleAsync();
                Assert.Equal(MatchStatus.Finished, row.Status);
                Assert.Contains(MatchStore.Read(row.RecordJson).Log, logged => logged.Action is Concede);
            });
        }
        finally
        {
            ServerFactory.DeleteDatabase(path);
        }
    }
}
