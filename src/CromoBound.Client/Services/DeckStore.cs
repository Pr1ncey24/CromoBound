using System.Text;
using System.Text.Json;
using CromoBound.Models.Cards;
using CromoBound.Models.Json;

namespace CromoBound.Client.Services;

/// <summary>A deck the player keeps in this browser, with the id the app gave it.</summary>
public sealed record StoredDeck(string Id, Deck Deck);

/// <summary>The player's decks in the browser's local storage, under a key with their username, so two accounts on one computer
/// don't mix (spec §7). A deck is pasted as JSON or as a deck list (<see cref="DeckText"/>), and only has to be readable; legality is
/// the server's, at challenge and accept time.</summary>
public sealed class DeckStore(IBrowserStorage storage, SessionState session, CatalogClient catalog)
{
    public const int MaxBytes = 1_000_000;

    public const string Empty = "Paste a deck or upload a file first.";
    public const string NotJson = "That isn't a deck the app can read: it isn't valid JSON.";
    public const string NotADeck = "That isn't a deck the app can read: it doesn't have a deck's fields.";
    public const string NoName = "That isn't a deck the app can read: the deck has no name.";
    public const string TooBig = "That file is too big to be a deck.";
    public const string NameNeeded = "A deck needs a name.";
    public const string NoRoom = "The browser has no room for more decks.";
    public const string Missing = "That deck isn't here any more.";
    public const string NoCatalog = "The card list couldn't be loaded, so a deck list can't be read right now. Try again in a moment.";

    /// <summary>Usernames are unique in any letter case, so the key uses the lower-case name.</summary>
    public static string KeyFor(string userName) => "cromobound.decks." + userName.ToLowerInvariant();

    public static string Counts(Deck deck)
    {
        var counts = $"{deck.Main.Sum(e => e.Count)} main · {deck.Runes.Sum(e => e.Count)} runes · {deck.Battlefields.Count} battlefields";
        var sideboard = deck.Sideboard.Sum(e => e.Count);
        return sideboard > 0 ? $"{counts} · sideboard {sideboard}" : counts;
    }

    /// <summary>Unreadable storage (edited by hand, or from an older app) reads as no decks.</summary>
    public async Task<IReadOnlyList<StoredDeck>> ListAsync()
    {
        var json = await storage.GetAsync(Key);
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            return CromoJson.Deserialize<List<StoredDeck>>(json);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Text that starts with a brace or a bracket is read as JSON; anything else as a deck list, which needs the card catalog
    /// to turn names into printings.</summary>
    public async Task<ApiResult<StoredDeck>> AddAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Fail(Empty);
        if (Encoding.UTF8.GetByteCount(input) > MaxBytes) return Fail(TooBig);
        var read = DeckText.LooksLikeList(input) ? await ReadListAsync(input) : ReadJson(input);
        if (read.Value is not { } deck) return Fail(read.Error!);
        if (string.IsNullOrWhiteSpace(deck.Name)) return Fail(NoName);
        var stored = new StoredDeck(Guid.NewGuid().ToString("N"), deck with { Name = deck.Name.Trim() });
        var error = await SaveAsync([.. await ListAsync(), stored]);
        return error is null ? new ApiResult<StoredDeck>(stored, null) : Fail(error);
    }

    private async Task<ApiResult<Deck>> ReadListAsync(string text)
    {
        if ((await catalog.GetAsync()).Value is not { } cards) return new ApiResult<Deck>(null, NoCatalog);
        var (deck, error) = DeckText.Parse(text, cards);
        return new ApiResult<Deck>(deck, error);
    }

    private static ApiResult<Deck> ReadJson(string json)
    {
        try
        {
            JsonDocument.Parse(json).Dispose();
        }
        catch (JsonException)
        {
            return new ApiResult<Deck>(null, NotJson);
        }
        try
        {
            return new ApiResult<Deck>(CromoJson.Deserialize<Deck>(json), null);
        }
        catch (JsonException)
        {
            return new ApiResult<Deck>(null, NotADeck);
        }
    }

    public async Task<ApiResult> RenameAsync(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return new ApiResult(NameNeeded);
        var decks = (await ListAsync()).ToList();
        var index = decks.FindIndex(d => d.Id == id);
        if (index < 0) return new ApiResult(Missing);
        decks[index] = decks[index] with { Deck = decks[index].Deck with { Name = name.Trim() } };
        return new ApiResult(await SaveAsync(decks));
    }

    public async Task<ApiResult> DeleteAsync(string id) =>
        new(await SaveAsync([.. (await ListAsync()).Where(d => d.Id != id)]));

    private string Key => KeyFor(session.Me?.UserName ?? "");

    /// <summary>The browser refuses a write when its storage is full; any failure there reads the same to the player.</summary>
    private async Task<string?> SaveAsync(List<StoredDeck> decks)
    {
        try
        {
            await storage.SetAsync(Key, CromoJson.Serialize(decks));
            return null;
        }
        catch (Exception)
        {
            return NoRoom;
        }
    }

    private static ApiResult<StoredDeck> Fail(string error) => new(null, error);
}
