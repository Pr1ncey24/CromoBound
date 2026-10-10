using System.Text.RegularExpressions;

namespace CromoBound.Importer;

public static partial class NameNormalizer
{
    /// <summary>Names the API gets wrong, mapped to the printed name (before the suffix is split off).</summary>
    private static readonly Dictionary<string, string> Corrections = new(StringComparer.Ordinal)
    {
        // The API puts the "Yordle" tag in front of the legend's name.
        ["Yordle, Kennen - Heart of the Tempest"] = "Kennen - Heart of the Tempest",
    };

    /// <summary>Strips "// Buff" and a trailing "(...)" suffix, and unifies "Name - Subtitle" to "Name, Subtitle".</summary>
    public static (string Name, string? Suffix) Normalize(string rawName)
    {
        var name = BuffBack().Replace(rawName.Trim(), "");
        string? suffix = null;
        var match = TrailingParens().Match(name);
        if (match.Success)
        {
            suffix = match.Groups[1].Value.Trim();
            name = name[..match.Index];
        }
        name = Corrections.GetValueOrDefault(name.Trim(), name);
        return (name.Replace(" - ", ", ", StringComparison.Ordinal).Trim(), suffix);
    }

    [GeneratedRegex(@"\s*//\s*Buff\s*$")]
    private static partial Regex BuffBack();

    [GeneratedRegex(@"\s*\(([^)]*)\)\s*$")]
    private static partial Regex TrailingParens();
}
