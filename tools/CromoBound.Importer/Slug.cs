using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CromoBound.Importer;

public static partial class Slug
{
    /// <summary>"Kai'Sa, Survivor" → "kaisa-survivor". Accents stripped, apostrophes dropped, other non-alphanumerics become single dashes.</summary>
    public static string From(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c is '\'' or '’') continue;
            builder.Append(char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        }
        return Dashes().Replace(builder.ToString(), "-").Trim('-');
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex Dashes();
}
