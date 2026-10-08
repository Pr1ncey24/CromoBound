using System.Text.RegularExpressions;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Rules;

/// <summary>
/// The keywords a card itself has (spec §7.10): bracketed keywords that start a line of its text. Keywords it grants to others
/// ("give a unit [Tank]"), has only conditionally ("while buffed, I have [Ganking]"), or that time one of its abilities
/// ("[Reaction][>] …", "cost: [Reaction] - …") don't count. The importer's keyword list holds every bracketed term and is for search only.
/// </summary>
public static partial class CardKeywords
{
    public static IReadOnlySet<DisplayKeyword> Own(Card card)
    {
        var result = new HashSet<DisplayKeyword>();
        foreach (var line in RichText.Lines(card.Text.Rich))
        {
            var leading = LeadingKeywords().Match(line);
            if (!leading.Success) continue;
            var rest = line[leading.Length..];
            if (rest.StartsWith("[&gt;]", StringComparison.Ordinal) || rest.StartsWith("[>]", StringComparison.Ordinal)) continue;
            foreach (Capture name in leading.Groups["name"].Captures)
                if (Enum.TryParse<DisplayKeyword>(name.Value.Replace("-", ""), out var keyword) && Enum.IsDefined(keyword))
                    result.Add(keyword);
        }
        return result;
    }

    /// <summary>True when the line is only the card's own keywords and their reminder text, e.g. "[Tank] (I must be assigned combat damage first.)".</summary>
    public static bool IsKeywordLine(string line)
    {
        var leading = LeadingKeywords().Match(line);
        return leading.Success && RichText.StripReminders(line[leading.Length..]).Length == 0;
    }

    /// <summary>One or more bracketed terms at the start of a line, each optionally with a number ("[Shield 2]").</summary>
    [GeneratedRegex(@"^(?:\[(?<name>[A-Za-z-]+)(?:\s+\d+)?\]\s*)+")]
    private static partial Regex LeadingKeywords();
}
