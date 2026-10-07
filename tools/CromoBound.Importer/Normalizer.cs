using System.Globalization;
using System.Text.RegularExpressions;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Importer;

public sealed record NormalizeResult(IReadOnlyList<Card> Cards, IReadOnlyList<Printing> Printings, IReadOnlyList<CardSet> Sets);

public static partial class Normalizer
{
    private sealed record Parsed(
        RawCard Raw, string Name, string? Suffix, CardType Type, Supertype? Supertype, Rarity? Rarity,
        IReadOnlyList<Domain> Domains, PrintingVariant Variant);

    public static NormalizeResult Normalize(
        IReadOnlyList<RawCard> rawCards, IReadOnlyList<RawSet> rawSets, IReadOnlyCollection<Card> tokens, ImportReport report)
    {
        var sets = rawSets.Select(ToSet).OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
        var published = sets.ToDictionary(s => s.Id, s => s.PublishedOn ?? DateOnly.MinValue, StringComparer.Ordinal);
        var tokenIds = tokens.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        var parsed = ResolveLegendTitles(rawCards.Select(Parse).ToList());

        var cards = new List<Card>();
        var printings = new List<Printing>();
        var namesById = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var group in parsed.Where(p => p.Supertype != Supertype.Token).GroupBy(p => p.Name, StringComparer.Ordinal))
        {
            var id = Slug.From(group.Key);
            if (!namesById.TryAdd(id, group.Key))
                throw new ImportException($"Names '{namesById[id]}' and '{group.Key}' both produce card id '{id}'.");

            var ordered = group
                .OrderByDescending(p => published.GetValueOrDefault(p.Raw.Set.SetId, DateOnly.MinValue))
                .ThenBy(p => p.Variant == PrintingVariant.Standard ? 0 : 1)
                .ThenBy(p => p.Raw.CollectorNumber ?? int.MaxValue)
                .ThenBy(p => p.Raw.Id, StringComparer.Ordinal)
                .ToList();

            cards.Add(BuildCard(id, ordered, report));
            printings.AddRange(ordered.Select(p => ToPrinting(p, id)));
            RecordTextConflict(id, ordered, report);
        }

        foreach (var token in parsed.Where(p => p.Supertype == Supertype.Token))
        {
            var id = "token-" + Slug.From(token.Name);
            if (!tokenIds.Contains(id)) report.MissingTokens.Add(id);
            printings.Add(ToPrinting(token, id));
        }

        foreach (var card in cards)
            report.TypeCounts[card.Type] = report.TypeCounts.GetValueOrDefault(card.Type) + 1;

        return new NormalizeResult(
            cards.OrderBy(c => c.Id, StringComparer.Ordinal).ToList(),
            printings.OrderBy(p => p.Id, StringComparer.Ordinal).ToList(),
            sets);
    }

    private static Parsed Parse(RawCard raw)
    {
        var (name, suffix) = NameNormalizer.Normalize(raw.Name);
        return new Parsed(
            raw, name, suffix,
            ParseEnum<CardType>(raw.Classification.Type, "type", raw),
            raw.Classification.Supertype is null ? null : ParseEnum<Supertype>(raw.Classification.Supertype, "supertype", raw),
            raw.Classification.Rarity is null ? null : ParseEnum<Rarity>(raw.Classification.Rarity, "rarity", raw),
            raw.Classification.Domain.Select(d => ParseEnum<Domain>(d, "domain", raw)).ToList(),
            VariantOf(suffix, raw.Metadata));
    }

    /// <summary>Some legend prints carry only the title ("Matriarch of War"); they get the full name ("Ambessa, Matriarch of War").</summary>
    private static List<Parsed> ResolveLegendTitles(List<Parsed> parsed)
    {
        var fullNames = parsed
            .Where(p => p.Type == CardType.Legend && p.Name.Contains(", ", StringComparison.Ordinal))
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return parsed.Select(p =>
        {
            if (p.Type != CardType.Legend || p.Name.Contains(", ", StringComparison.Ordinal)) return p;
            var matches = fullNames.Where(n => n.EndsWith(", " + p.Name, StringComparison.Ordinal)).ToList();
            return matches.Count switch
            {
                0 => p,
                1 => p with { Name = matches[0] },
                _ => throw new ImportException($"Legend '{p.Raw.Name}' ({p.Raw.Id}) matches several full names: {string.Join(" | ", matches)}."),
            };
        }).ToList();
    }

    private static T ParseEnum<T>(string? value, string field, RawCard raw) where T : struct, Enum =>
        value is not null && Enum.GetNames<T>().Contains(value, StringComparer.Ordinal)
            ? Enum.Parse<T>(value)
            : throw new ImportException($"Card '{raw.Name}' ({raw.Id}) has unknown {field} '{value}'.");

    private static PrintingVariant VariantOf(string? suffix, RawMetadata metadata)
    {
        if (suffix is null || suffix.All(char.IsDigit))
            return metadata.AlternateArt ? PrintingVariant.AlternateArt
                : metadata.Overnumbered ? PrintingVariant.Overnumbered
                : metadata.Signature ? PrintingVariant.Signature
                : PrintingVariant.Standard;
        return suffix switch
        {
            "Alternate Art" => PrintingVariant.AlternateArt,
            "Overnumbered" => PrintingVariant.Overnumbered,
            "Signature" => PrintingVariant.Signature,
            "Metal" => PrintingVariant.Metal,
            "Starter" => PrintingVariant.Starter,
            "Ultimate" => PrintingVariant.Ultimate,
            "Launch Exclusive" => PrintingVariant.LaunchExclusive,
            _ => PrintingVariant.Other,
        };
    }

    private static Card BuildCard(string id, IReadOnlyList<Parsed> ordered, ImportReport report)
    {
        var canonical = ordered[0];
        var raw = canonical.Raw;
        var rich = raw.Text.Rich ?? "";
        var unknown = new List<string>();
        var keywords = KeywordText.ExtractDisplay(rich, unknown);
        foreach (var name in unknown) report.UnknownKeywords.TryAdd(name, id);

        return new Card
        {
            Id = id,
            Name = canonical.Name,
            Type = canonical.Type,
            Supertype = canonical.Supertype,
            Domains = canonical.Domains,
            Cost = BuildCost(canonical),
            Might = raw.Attributes.Might,
            Tags = raw.Tags,
            Keywords = keywords,
            Text = new CardText { Rich = rich, Plain = raw.Text.Plain ?? "" },
            DefaultPrintingId = (ordered.FirstOrDefault(p => p.Variant == PrintingVariant.Standard) ?? canonical).Raw.Id,
        };
    }

    private static CardCost? BuildCost(Parsed card)
    {
        var energy = card.Raw.Attributes.Energy;
        var powerCount = card.Raw.Attributes.Power ?? 0;
        if (energy is null && powerCount == 0) return null;
        if (powerCount == 0) return new CardCost { Energy = energy };

        // A multi-domain card's power symbols can be paid with any of its domains.
        var colored = card.Domains.Where(d => d != Domain.Colorless).ToList();
        var symbol = colored.Count switch
        {
            0 => throw new ImportException($"Card '{card.Raw.Name}' ({card.Raw.Id}) has a power cost but no domain."),
            1 => Enum.Parse<PowerSymbol>(colored[0].ToString()),
            _ => PowerSymbol.Self,
        };
        return new CardCost { Energy = energy, Power = Enumerable.Repeat(symbol, powerCount).ToList() };
    }

    private static void RecordTextConflict(string id, IReadOnlyList<Parsed> ordered, ImportReport report)
    {
        var texts = ordered.Select(p => (p.Raw.Text.Rich ?? "").Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (texts.Count < 2) return;
        var withoutReminders = texts.Select(t => Whitespace().Replace(RichText.StripReminders(t), " ").Trim()).Distinct(StringComparer.Ordinal).Count();
        report.TextConflicts.Add(new TextConflict(id, withoutReminders == 1 ? ConflictKind.ReminderOnly : ConflictKind.Wording, texts));
    }

    private static Printing ToPrinting(Parsed p, string cardId) => new()
    {
        Id = p.Raw.Id,
        CardId = cardId,
        RiftboundId = p.Raw.RiftboundId,
        TcgplayerId = p.Raw.TcgplayerId,
        Set = p.Raw.Set.SetId,
        CollectorNumber = p.Raw.CollectorNumber,
        Rarity = p.Rarity,
        Variant = p.Variant,
        ImageUrl = p.Raw.Media.ImageUrl,
        Orientation = string.Equals(p.Raw.Orientation, "landscape", StringComparison.OrdinalIgnoreCase) ? Orientation.Landscape : Orientation.Portrait,
        Artist = p.Raw.Media.Artist,
        Flavour = p.Raw.Text.Flavour,
    };

    private static CardSet ToSet(RawSet set) => new()
    {
        Id = set.SetId,
        Name = set.Name,
        CardCount = set.CardCount,
        PublishedOn = set.PublishedOn is { Length: >= 10 } date
            ? DateOnly.ParseExact(date[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null,
    };

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
