using System.Text.RegularExpressions;
using CromoBound.Contracts;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Services;

/// <summary>Reads a deck list as deck sites export it: section headers (<c>Legend:</c>, <c>Champion:</c>, <c>MainDeck:</c>,
/// <c>Battlefields:</c>, <c>Rune Pool:</c>, <c>Sideboard:</c>) and card lines (<c>3 Name</c> or <c>3x Name</c>). Names match in any
/// case, with curly apostrophes and extra spaces ignored; each card gets its default printing, or its first one. Like a pasted JSON deck,
/// the list only has to be readable: whether it is legal is the server's to say.</summary>
public static partial class DeckText
{
    public const string NoLegend = "That deck list has no Legend section.";
    public const string NoChampion = "That deck list has no Champion section.";

    private enum Section { Legend, Champion, Main, Battlefields, Runes, Sideboard }

    private static readonly Dictionary<string, Section> Headers = new(StringComparer.Ordinal)
    {
        ["legend"] = Section.Legend,
        ["champion"] = Section.Champion,
        ["maindeck"] = Section.Main,
        ["main"] = Section.Main,
        ["battlefields"] = Section.Battlefields,
        ["battlefield"] = Section.Battlefields,
        ["runepool"] = Section.Runes,
        ["runes"] = Section.Runes,
        ["sideboard"] = Section.Sideboard,
    };

    /// <summary>JSON starts with a brace or a bracket; anything else is read as a deck list.</summary>
    public static bool LooksLikeList(string input)
    {
        var start = input.TrimStart().TrimStart('\uFEFF').TrimStart();
        return !(start.StartsWith('{') || start.StartsWith('['));
    }

    public static (Deck? Deck, string? Error) Parse(string text, CardCatalog catalog)
    {
        var lines = text.TrimStart('\uFEFF').Split('\n');
        var sections = new Dictionary<Section, List<(int Count, string Name)>>();
        Section? current = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0) continue;
            if (line.EndsWith(':'))
            {
                if (!Headers.TryGetValue(HeaderKey(line), out var section)) return (null, $"Line {i + 1} isn't a section this app knows: {line}");
                current = section;
                sections.TryAdd(section, []);
                continue;
            }
            if (current is not { } at) return (null, $"Line {i + 1} comes before any section, like Legend: or MainDeck:.");
            var card = CardLine().Match(line);
            if (!card.Success || !int.TryParse(card.Groups[1].Value, out var count) || count < 1)
                return (null, $"Line {i + 1} isn't a card line (a count, then the card's name): {line}");
            sections[at].Add((count, card.Groups[2].Value.Trim()));
        }

        var printings = PrintingsByName(catalog);
        var unknown = sections.Values.SelectMany(s => s).Select(c => c.Name).Where(n => !printings.ContainsKey(Normalize(n)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (unknown.Count > 0) return (null, $"These cards aren't known: {string.Join(", ", unknown)}.");
        if (!sections.TryGetValue(Section.Legend, out var legend) || legend.Count == 0) return (null, NoLegend);
        if (!sections.TryGetValue(Section.Champion, out var champion) || champion.Count == 0) return (null, NoChampion);
        if (legend.Count > 1) return (null, "The Legend section has more than one card.");
        if (champion.Count > 1) return (null, "The Champion section has more than one card.");

        var deck = new Deck
        {
            Name = catalog.Cards.First(c => c.Id == printings[Normalize(legend[0].Name)].CardId).Name,
            Legend = printings[Normalize(legend[0].Name)].Printing,
            Champion = printings[Normalize(champion[0].Name)].Printing,
            Main = Entries(sections, Section.Main, printings),
            Runes = Entries(sections, Section.Runes, printings),
            Battlefields = [.. sections.GetValueOrDefault(Section.Battlefields, [])
                .SelectMany(c => Enumerable.Repeat(printings[Normalize(c.Name)].Printing, c.Count))],
            Sideboard = Entries(sections, Section.Sideboard, printings),
        };
        return (deck, null);
    }

    /// <summary>One entry per card, in the list's order; the same card twice in a section is added up.</summary>
    private static List<DeckEntry> Entries(
        Dictionary<Section, List<(int Count, string Name)>> sections, Section section, Dictionary<string, (string CardId, string Printing)> printings) =>
        [.. sections.GetValueOrDefault(section, [])
            .GroupBy(c => printings[Normalize(c.Name)].Printing, StringComparer.Ordinal)
            .Select(g => new DeckEntry { Printing = g.Key, Count = g.Sum(c => c.Count) })];

    /// <summary>A deck's cards by normalized name, with the printing a list entry stands for. Tokens and cards without a printing
    /// can't be in a deck.</summary>
    private static Dictionary<string, (string CardId, string Printing)> PrintingsByName(CardCatalog catalog)
    {
        var firstPrinting = catalog.Printings.GroupBy(p => p.CardId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Id).Min(StringComparer.Ordinal)!, StringComparer.Ordinal);
        var byName = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach (var card in catalog.Cards.Where(c => c.Supertype != Supertype.Token).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var printing = card.DefaultPrintingId ?? firstPrinting.GetValueOrDefault(card.Id);
            if (printing is not null) byName.TryAdd(Normalize(card.Name), (card.Id, printing));
        }
        return byName;
    }

    private static string Normalize(string name) => Spaces().Replace(name.Replace('\u2019', '\'').Replace('\u2018', '\'').Trim(), " ").ToLowerInvariant();

    private static string HeaderKey(string header) => Spaces().Replace(header.TrimEnd(':'), "").ToLowerInvariant();

    [GeneratedRegex(@"^(\d+)\s*[xX]?\s+(.+)$")]
    private static partial Regex CardLine();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
