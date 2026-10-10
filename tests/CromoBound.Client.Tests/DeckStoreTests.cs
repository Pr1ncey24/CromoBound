using CromoBound.Client.Services;
using CromoBound.Client.Tests.Fakes;
using CromoBound.Contracts;

namespace CromoBound.Client.Tests;

public class DeckStoreTests
{
    private static (DeckStore Store, MemoryStorage Storage, SessionState Session) Store(string user = "marco")
    {
        var session = new SessionState();
        session.SignedIn(new MeResponse(user, false));
        var storage = new MemoryStorage();
        return (new DeckStore(storage, session), storage, session);
    }

    [Fact]
    public async Task An_added_deck_is_listed_with_its_name_and_counts()
    {
        var (store, _, _) = Store();

        var added = await store.AddAsync(SampleDecks.Json("Jinx aggro", sideboard: 6));
        var decks = await store.ListAsync();

        Assert.True(added.Ok);
        var deck = Assert.Single(decks);
        Assert.Equal(("Jinx aggro", "legend-1"), (deck.Deck.Name, deck.Deck.Legend));
        Assert.Equal("40 main · 12 runes · 3 battlefields · sideboard 6", DeckStore.Counts(deck.Deck));
        Assert.Equal("40 main · 12 runes · 3 battlefields", DeckStore.Counts((await store.AddAsync(SampleDecks.Json("Vi"))).Value!.Deck));
    }

    [Theory]
    [InlineData("", DeckStore.Empty)]
    [InlineData("   ", DeckStore.Empty)]
    [InlineData("{ \"name\": \"Jinx", DeckStore.NotJson)]
    [InlineData("not json at all", DeckStore.NotJson)]
    [InlineData("{ \"name\": \"Jinx\" }", DeckStore.NotADeck)]
    [InlineData("[1, 2, 3]", DeckStore.NotADeck)]
    [InlineData("{ \"name\": \"Jinx\", \"legend\": \"l\", \"champion\": \"c\", \"colour\": \"red\" }", DeckStore.NotADeck)]
    [InlineData("{ \"name\": \" \", \"legend\": \"l\", \"champion\": \"c\" }", DeckStore.NoName)]
    public async Task Text_that_isnt_a_deck_is_refused_and_nothing_is_saved(string json, string error)
    {
        var (store, storage, _) = Store();

        var added = await store.AddAsync(json);

        Assert.Equal(error, added.Error);
        Assert.Empty(storage.Items);
    }

    [Fact]
    public async Task Text_over_a_megabyte_is_refused()
    {
        var (store, storage, _) = Store();

        var added = await store.AddAsync(SampleDecks.Json(new string('x', DeckStore.MaxBytes)));

        Assert.Equal(DeckStore.TooBig, added.Error);
        Assert.Empty(storage.Items);
    }

    [Fact]
    public async Task Decks_are_kept_per_user()
    {
        var (marco, storage, _) = Store("marco");
        await marco.AddAsync(SampleDecks.Json("Marco's deck"));
        var giuliaSession = new SessionState();
        giuliaSession.SignedIn(new MeResponse("giulia", false));
        var giulia = new DeckStore(storage, giuliaSession);
        var marcoAgain = new SessionState();
        marcoAgain.SignedIn(new MeResponse("Marco", false));

        Assert.Empty(await giulia.ListAsync());
        Assert.Equal("Marco's deck", Assert.Single(await new DeckStore(storage, marcoAgain).ListAsync()).Deck.Name);
        Assert.Equal(new[] { DeckStore.KeyFor("marco") }, storage.Items.Keys);
    }

    [Fact]
    public async Task A_deck_can_be_renamed_and_deleted()
    {
        var (store, _, _) = Store();
        var first = (await store.AddAsync(SampleDecks.Json("First"))).Value!;
        var second = (await store.AddAsync(SampleDecks.Json("Second"))).Value!;

        Assert.Equal(DeckStore.NameNeeded, (await store.RenameAsync(first.Id, "  ")).Error);
        Assert.True((await store.RenameAsync(first.Id, " Jinx aggro ")).Ok);
        Assert.True((await store.DeleteAsync(second.Id)).Ok);

        Assert.Equal("Jinx aggro", Assert.Single(await store.ListAsync()).Deck.Name);
    }

    [Fact]
    public async Task Unreadable_storage_reads_as_no_decks()
    {
        var (store, storage, _) = Store();
        storage.Items[DeckStore.KeyFor("marco")] = "garbage";

        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task A_full_browser_storage_is_a_plain_error()
    {
        var (store, storage, _) = Store();
        storage.Full = true;

        Assert.Equal(DeckStore.NoRoom, (await store.AddAsync(SampleDecks.Json())).Error);
    }
}
