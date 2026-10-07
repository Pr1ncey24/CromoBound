using System.Text.RegularExpressions;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Importer;

public static partial class KeywordText
{
    private static readonly HashSet<string> IgnoredBracketTerms = new(StringComparer.Ordinal) { "Add" };
    private static readonly HashSet<string> IndexNoise = new(StringComparer.Ordinal) { "ADD", "11", "TEXT" };

    // Keywords that need no cost or steps, so a line made only of them can be scaffolded.
    private static readonly HashSet<MechanicalKeyword> Scaffoldable =
    [
        MechanicalKeyword.Accelerate, MechanicalKeyword.Action, MechanicalKeyword.Reaction, MechanicalKeyword.Assault,
        MechanicalKeyword.Shield, MechanicalKeyword.Deflect, MechanicalKeyword.Ganking, MechanicalKeyword.Hidden,
        MechanicalKeyword.Tank, MechanicalKeyword.Backline, MechanicalKeyword.Temporary, MechanicalKeyword.Vision,
        MechanicalKeyword.QuickDraw, MechanicalKeyword.Weaponmaster, MechanicalKeyword.Ambush, MechanicalKeyword.Hunt,
        MechanicalKeyword.Unique,
    ];

    private static readonly HashSet<MechanicalKeyword> Valued =
        [MechanicalKeyword.Assault, MechanicalKeyword.Shield, MechanicalKeyword.Deflect, MechanicalKeyword.Hunt];

    public static IReadOnlyList<DisplayKeyword> ExtractDisplay(string rich, ICollection<string> unknown)
    {
        var result = new List<DisplayKeyword>();
        foreach (Match match in Bracket().Matches(rich))
        {
            var name = match.Groups["name"].Value;
            if (IgnoredBracketTerms.Contains(name)) continue;
            if (TryParseName<DisplayKeyword>(name, out var keyword))
            {
                if (!result.Contains(keyword)) result.Add(keyword);
            }
            else if (!unknown.Contains(name))
            {
                unknown.Add(name);
            }
        }
        return result;
    }

    /// <summary>Keyword entries when every text line is only parameter-free mechanical keywords (reminder text ignored); otherwise null.</summary>
    public static IReadOnlyList<KeywordEntry>? TryParseKeywordOnly(string rich)
    {
        var lines = RichText.Lines(rich).Select(RichText.StripReminders).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) return null;

        var entries = new List<KeywordEntry>();
        foreach (var line in lines)
        {
            var matches = Bracket().Matches(line);
            if (matches.Count == 0) return null;
            var covered = string.Concat(matches.Select(m => m.Value));
            if (Whitespace().Replace(line, "") != Whitespace().Replace(covered, "")) return null;

            foreach (Match match in matches)
            {
                if (!TryParseName<MechanicalKeyword>(match.Groups["name"].Value, out var keyword) || !Scaffoldable.Contains(keyword))
                    return null;
                var value = match.Groups["value"];
                if (value.Success && !Valued.Contains(keyword)) return null;
                entries.Add(new KeywordEntry { Keyword = keyword, Value = value.Success ? int.Parse(value.Value) : null });
            }
        }
        return entries;
    }

    public static bool IsKnownIndexKeyword(string value) =>
        IndexNoise.Contains(value) || TryParseName<DisplayKeyword>(value, out _);

    private static bool TryParseName<T>(string text, out T result) where T : struct, Enum
    {
        var name = text.Replace("-", "", StringComparison.Ordinal);
        if (Enum.GetNames<T>().Contains(name, StringComparer.Ordinal))
        {
            result = Enum.Parse<T>(name);
            return true;
        }
        result = default;
        return false;
    }

    [GeneratedRegex(@"\[(?<name>[A-Za-z][A-Za-z\-]*)(?: (?<value>\d+))?\]")]
    private static partial Regex Bracket();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
