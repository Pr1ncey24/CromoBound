using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Tests;
using CromoBound.Models.Json;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

public class MatchStoreTests
{
    private static Match NewMatch(ulong seed = 7) => Match.Create(TestDecks.Setup(MatchFormat.Bo1, seed), ServerFactory.TestCards).Match!;

    private static async Task<(IMatchStore Store, int Alice, int Bob)> StoreAsync(ServerFactory factory)
    {
        var alice = await factory.AddUserAsync("alice");
        var bob = await factory.AddUserAsync("bob");
        return (factory.Services.GetRequiredService<IMatchStore>(), alice.Id, bob.Id);
    }

    [Fact]
    public async Task A_new_match_is_running_and_a_save_replaces_its_record()
    {
        using var factory = new ServerFactory();
        var (store, alice, bob) = await StoreAsync(factory);
        var id = Guid.NewGuid();

        await store.CreateAsync(id, alice, bob, NewMatch(7).ToRecord());
        var created = Assert.Single(await store.RunningAsync());
        await store.SaveAsync(id, NewMatch(8).ToRecord(), MatchStatus.Running);
        var saved = Assert.Single(await store.RunningAsync());

        Assert.Equal((id, alice, bob, MatchStatus.Running), (created.Id, created.Seat0UserId, created.Seat1UserId, created.Status));
        Assert.Equal(7UL, MatchStore.Read(created.RecordJson).Setup.Seed);
        Assert.Equal(8UL, MatchStore.Read(saved.RecordJson).Setup.Seed);
        Assert.True(saved.UpdatedAt >= created.UpdatedAt);
        Assert.Equal(created.CreatedAt, saved.CreatedAt);
    }

    [Theory]
    [InlineData("Finished")]
    [InlineData("Abandoned")]
    public async Task Finished_and_abandoned_matches_arent_running_and_keep_their_record(string status)
    {
        using var factory = new ServerFactory();
        var (store, alice, bob) = await StoreAsync(factory);
        var id = Guid.NewGuid();
        await store.CreateAsync(id, alice, bob, NewMatch(7).ToRecord());

        await store.SetStatusAsync(id, Enum.Parse<MatchStatus>(status));

        Assert.Empty(await store.RunningAsync());
        await factory.WithDbAsync(async db =>
        {
            var row = await db.Matches.SingleAsync();
            Assert.Equal(Enum.Parse<MatchStatus>(status), row.Status);
            Assert.Equal(7UL, MatchStore.Read(row.RecordJson).Setup.Seed);
        });
    }

    [Fact]
    public async Task Saving_a_match_that_isnt_there_fails()
    {
        using var factory = new ServerFactory();
        var (store, _, _) = await StoreAsync(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(Guid.NewGuid(), NewMatch().ToRecord(), MatchStatus.Running));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetStatusAsync(Guid.NewGuid(), MatchStatus.Abandoned));
    }

    [Fact]
    public void A_saved_record_reads_back_and_replays_to_the_same_match()
    {
        var match = NewMatch();
        var first = match.Pending!.Players[0];
        Assert.True(match.Submit(first, new ChoosePlayOrder(true)).Accepted);

        var loaded = Match.Load(MatchStore.Read(MatchStore.Json(match.ToRecord())), ServerFactory.TestCards);

        foreach (var seat in new[] { new PlayerId(0), new PlayerId(1) })
            Assert.Equal(CromoJson.Serialize(match.ViewFor(seat)), CromoJson.Serialize(loaded.ViewFor(seat)));
    }
}
