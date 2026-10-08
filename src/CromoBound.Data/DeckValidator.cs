using CromoBound.Models.Cards;

namespace CromoBound.Data;

public enum DeckIssueSeverity
{
    /// <summary>Not finished yet (e.g. 38/40 cards). Normal while building.</summary>
    Incomplete,

    /// <summary>Wrong regardless of what is added (e.g. 4 copies).</summary>
    Illegal,
}

public enum DeckIssueCode
{
    UnknownPrinting, InvalidCount, WrongCardType, MainDeckSize, RuneDeckSize, BattlefieldCount, DuplicateBattlefield, SideboardSize,
    TooManyCopies, UniqueCopies, TooManySignatures, SignatureTag, ChampionMismatch, OutsideIdentity,
}

/// <summary>One deck-construction problem. <see cref="CardIds"/> lets a deck builder highlight the cards involved.</summary>
public sealed record DeckIssue(
    DeckIssueCode Code, DeckIssueSeverity Severity, IReadOnlyList<string> CardIds, string Message, int? Actual = null, int? Expected = null);

public sealed record DeckReport(IReadOnlyList<DeckIssue> Issues)
{
    public bool IsLegal => Issues.Count == 0;
}

/// <summary>Deck construction rules (CR 103, TR 403 and 601). Used live by the deck builder and by the engine at match start.</summary>
public static class DeckValidator
{
    public const int MainDeckSize = 40;
    public const int RuneDeckSize = 12;
    public const int BattlefieldCount = 3;
    public const int MaxSideboardSize = 10;
    public const int MaxCopies = 3;
    public const int MaxSignatureCards = 3;

    public static DeckReport Validate(Deck deck, CardDatabase db)
    {
        var issues = new List<DeckIssue>();
        var legend = Resolve(deck.Legend, db, issues);
        var champion = Resolve(deck.Champion, db, issues);
        var mainEntries = ValidEntries(deck.Main, "main deck", issues);
        var runeEntries = ValidEntries(deck.Runes, "rune deck", issues);
        var sideboardEntries = ValidEntries(deck.Sideboard, "sideboard", issues);
        var main = ResolveEntries(mainEntries, db, issues);
        var runes = ResolveEntries(runeEntries, db, issues);
        var sideboard = ResolveEntries(sideboardEntries, db, issues);
        var battlefields = deck.Battlefields.Select(p => Resolve(p, db, issues)).OfType<Card>().ToList();

        CheckTypes(legend, runes, battlefields, main, sideboard, issues);
        CheckSizes(deck, mainEntries, runeEntries, sideboardEntries, issues);
        CheckBattlefieldNames(battlefields, issues);
        CheckCopies(champion, main, sideboard, issues);
        if (legend is { Type: CardType.Legend })
        {
            CheckChampion(legend, champion, issues);
            CheckSignatures(legend, main, sideboard, issues);
            CheckIdentity(legend, champion, main, runes, sideboard, issues);
        }
        return new DeckReport(issues);
    }

    private static Card? Resolve(string printingId, CardDatabase db, List<DeckIssue> issues)
    {
        if (db.Printings.TryGetValue(printingId, out var printing) && db.Cards.TryGetValue(printing.CardId, out var card))
            return card;
        issues.Add(new DeckIssue(DeckIssueCode.UnknownPrinting, DeckIssueSeverity.Illegal, [printingId], $"Printing '{printingId}' does not exist."));
        return null;
    }

    /// <summary>Entries with a count of at least 1. Others are reported as <see cref="DeckIssueCode.InvalidCount"/> and ignored by every other check.</summary>
    private static List<DeckEntry> ValidEntries(IEnumerable<DeckEntry> entries, string section, List<DeckIssue> issues)
    {
        var valid = new List<DeckEntry>();
        foreach (var entry in entries)
        {
            if (entry.Count >= 1)
                valid.Add(entry);
            else
                issues.Add(new DeckIssue(DeckIssueCode.InvalidCount, DeckIssueSeverity.Illegal, [entry.Printing],
                    $"Printing '{entry.Printing}' in the {section} has a count of {entry.Count}; it must be at least 1.", entry.Count));
        }
        return valid;
    }

    private static int Clamp(long value) => (int)Math.Min(value, int.MaxValue);

    private static List<(Card Card, int Count)> ResolveEntries(List<DeckEntry> entries, CardDatabase db, List<DeckIssue> issues)
    {
        var result = new List<(Card Card, int Count)>();
        foreach (var entry in entries)
            if (Resolve(entry.Printing, db, issues) is { } card)
                result.Add((card, entry.Count));
        return result;
    }

    private static void CheckTypes(
        Card? legend, List<(Card Card, int Count)> runes, List<Card> battlefields,
        List<(Card Card, int Count)> main, List<(Card Card, int Count)> sideboard, List<DeckIssue> issues)
    {
        if (legend is not null && legend.Type != CardType.Legend)
            issues.Add(WrongType(legend, "legend", "a Legend"));
        foreach (var (card, _) in runes.Where(r => r.Card.Type != CardType.Rune))
            issues.Add(WrongType(card, "rune deck", "a Rune"));
        foreach (var card in battlefields.Where(b => b.Type != CardType.Battlefield))
            issues.Add(WrongType(card, "battlefields", "a Battlefield"));
        foreach (var card in main.Concat(sideboard).Select(e => e.Card).Where(c => !IsMainDeckCard(c)).DistinctBy(c => c.Id))
            issues.Add(WrongType(card, "main deck or sideboard", "a unit, spell or gear"));
    }

    private static bool IsMainDeckCard(Card card) =>
        (card.Type is CardType.Unit or CardType.Spell or CardType.Gear) && card.Supertype != Supertype.Token;

    private static DeckIssue WrongType(Card card, string section, string expected) =>
        new(DeckIssueCode.WrongCardType, DeckIssueSeverity.Illegal, [card.Id], $"'{card.Name}' in the {section} is not {expected}.");

    private static void CheckSizes(
        Deck deck, List<DeckEntry> main, List<DeckEntry> runes, List<DeckEntry> sideboardEntries, List<DeckIssue> issues)
    {
        CheckExact(DeckIssueCode.MainDeckSize, "Main deck (with the champion)", main.Sum(e => (long)e.Count) + 1, MainDeckSize, issues);
        CheckExact(DeckIssueCode.RuneDeckSize, "Rune deck", runes.Sum(e => (long)e.Count), RuneDeckSize, issues);
        CheckExact(DeckIssueCode.BattlefieldCount, "Battlefields", deck.Battlefields.Count, BattlefieldCount, issues);
        var sideboard = Clamp(sideboardEntries.Sum(e => (long)e.Count));
        if (sideboard > MaxSideboardSize)
            issues.Add(new DeckIssue(DeckIssueCode.SideboardSize, DeckIssueSeverity.Illegal, [],
                $"Sideboard has {sideboard} cards; the maximum is {MaxSideboardSize}.", sideboard, MaxSideboardSize));
    }

    private static void CheckExact(DeckIssueCode code, string what, long total, int expected, List<DeckIssue> issues)
    {
        if (total == expected) return;
        var actual = Clamp(total);
        var severity = total < expected ? DeckIssueSeverity.Incomplete : DeckIssueSeverity.Illegal;
        issues.Add(new DeckIssue(code, severity, [], $"{what} has {actual} cards; it needs exactly {expected}.", actual, expected));
    }

    private static void CheckBattlefieldNames(List<Card> battlefields, List<DeckIssue> issues)
    {
        foreach (var group in battlefields.GroupBy(b => b.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
            issues.Add(new DeckIssue(DeckIssueCode.DuplicateBattlefield, DeckIssueSeverity.Illegal, [group.Key],
                $"Battlefield '{group.First().Name}' is included {group.Count()} times; battlefields must have different names."));
    }

    private static void CheckCopies(Card? champion, List<(Card Card, int Count)> main, List<(Card Card, int Count)> sideboard, List<DeckIssue> issues)
    {
        var all = main.Concat(sideboard).ToList();
        if (champion is not null) all.Add((champion, 1));
        foreach (var group in all.GroupBy(e => e.Card.Id, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var card = group.First().Card;
            var copies = Clamp(group.Sum(e => (long)e.Count));
            var unique = card.Keywords.Contains(DisplayKeyword.Unique);
            var limit = unique ? 1 : MaxCopies;
            if (copies <= limit) continue;
            issues.Add(new DeckIssue(unique ? DeckIssueCode.UniqueCopies : DeckIssueCode.TooManyCopies, DeckIssueSeverity.Illegal, [card.Id],
                $"'{card.Name}' has {copies} copies across main deck and sideboard; the limit is {limit}.", copies, limit));
        }
    }

    private static void CheckChampion(Card legend, Card? champion, List<DeckIssue> issues)
    {
        if (champion is null) return;
        if (champion.Type == CardType.Unit && champion.Supertype == Supertype.Champion && SharesTag(champion, legend)) return;
        issues.Add(new DeckIssue(DeckIssueCode.ChampionMismatch, DeckIssueSeverity.Illegal, [champion.Id],
            $"'{champion.Name}' is not a champion unit matching the legend '{legend.Name}'."));
    }

    private static void CheckSignatures(Card legend, List<(Card Card, int Count)> main, List<(Card Card, int Count)> sideboard, List<DeckIssue> issues)
    {
        var mainSignatures = main.Where(e => e.Card.Supertype == Supertype.Signature).ToList();
        var count = Clamp(mainSignatures.Sum(e => (long)e.Count));
        if (count > MaxSignatureCards)
            issues.Add(new DeckIssue(DeckIssueCode.TooManySignatures, DeckIssueSeverity.Illegal,
                [.. mainSignatures.Select(e => e.Card.Id).Distinct().Order(StringComparer.Ordinal)],
                $"The main deck has {count} Signature cards; the limit is {MaxSignatureCards}.", count, MaxSignatureCards));
        var wrongTag = main.Concat(sideboard).Select(e => e.Card)
            .Where(c => c.Supertype == Supertype.Signature && !SharesTag(c, legend))
            .DistinctBy(c => c.Id);
        foreach (var card in wrongTag)
            issues.Add(new DeckIssue(DeckIssueCode.SignatureTag, DeckIssueSeverity.Illegal, [card.Id],
                $"Signature card '{card.Name}' does not carry the legend's champion tag."));
    }

    private static void CheckIdentity(
        Card legend, Card? champion, List<(Card Card, int Count)> main, List<(Card Card, int Count)> runes,
        List<(Card Card, int Count)> sideboard, List<DeckIssue> issues)
    {
        var identity = legend.Domains.ToHashSet();
        var cards = main.Concat(runes).Concat(sideboard).Select(e => e.Card);
        if (champion is not null) cards = cards.Append(champion);
        foreach (var card in cards.DistinctBy(c => c.Id).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var outside = card.Domains.Where(d => d != Domain.Colorless && !identity.Contains(d)).ToList();
            if (outside.Count > 0)
                issues.Add(new DeckIssue(DeckIssueCode.OutsideIdentity, DeckIssueSeverity.Illegal, [card.Id],
                    $"'{card.Name}' has {string.Join(", ", outside)}, outside the legend's domains ({string.Join(", ", legend.Domains)})."));
        }
    }

    private static bool SharesTag(Card card, Card legend) => card.Tags.Intersect(legend.Tags, StringComparer.Ordinal).Any();
}
