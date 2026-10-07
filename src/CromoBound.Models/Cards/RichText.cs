using System.Text.RegularExpressions;

namespace CromoBound.Models.Cards;

public static partial class RichText
{
    private static readonly string[] Separators = ["<br />", "<br/>", "<br>", "\n"];

    /// <summary>Card text lines, split on &lt;br /&gt; and paragraphs. Line numbers in effects files are 1-based indexes into this list.</summary>
    public static IReadOnlyList<string> Lines(string? rich)
    {
        if (string.IsNullOrWhiteSpace(rich)) return [];
        var body = rich.Replace("<p>", "", StringComparison.Ordinal).Replace("</p>", "\n", StringComparison.Ordinal);
        return body.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Removes reminder text such as "(When you play me, ...)".</summary>
    public static string StripReminders(string text) => ReminderRegex().Replace(text, "").Trim();

    [GeneratedRegex(@"\s*\([^)]*\)")]
    private static partial Regex ReminderRegex();
}
