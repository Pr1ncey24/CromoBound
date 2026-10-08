using CromoBound.Engine.Actions;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Matches;

/// <summary>Applies sideboard swaps to a deck (TR 403). Only the main deck, champion and sideboard change.</summary>
internal static class Sideboarding
{
    /// <summary>The new deck, or null with the reason when a swap names a card that isn't there.</summary>
    public static Deck? Apply(Deck deck, SubmitSideboard submit, out string? error)
    {
        var main = Count(deck.Main);
        var side = Count(deck.Sideboard);
        foreach (var swap in submit.Swaps)
        {
            if (main.GetValueOrDefault(swap.Out) < 1) return Fail($"'{swap.Out}' is not in your main deck.", out error);
            if (side.GetValueOrDefault(swap.In) < 1) return Fail($"'{swap.In}' is not in your sideboard.", out error);
            main[swap.Out]--;
            side[swap.Out] = side.GetValueOrDefault(swap.Out) + 1;
            side[swap.In]--;
            main[swap.In] = main.GetValueOrDefault(swap.In) + 1;
        }

        var champion = deck.Champion;
        if (submit.Champion is { } chosen && chosen != deck.Champion)
        {
            var source = main.GetValueOrDefault(chosen) > 0 ? main : side.GetValueOrDefault(chosen) > 0 ? side : null;
            if (source is null) return Fail($"'{chosen}' is not in your main deck or sideboard.", out error);
            source[chosen]--;
            source[deck.Champion] = source.GetValueOrDefault(deck.Champion) + 1;
            champion = chosen;
        }

        error = null;
        return deck with { Champion = champion, Main = Entries(main), Sideboard = Entries(side) };
    }

    private static Dictionary<string, int> Count(IEnumerable<DeckEntry> entries)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in entries) counts[entry.Printing] = counts.GetValueOrDefault(entry.Printing) + entry.Count;
        return counts;
    }

    private static IReadOnlyList<DeckEntry> Entries(Dictionary<string, int> counts) =>
        [.. counts.Where(c => c.Value > 0).OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => new DeckEntry { Printing = c.Key, Count = c.Value })];

    private static Deck? Fail(string message, out string? error)
    {
        error = message;
        return null;
    }
}
