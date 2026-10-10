using System.Text.RegularExpressions;

namespace CromoBound.Client.Tests;

public partial class StylesheetTests
{
    /// <summary>Both stylesheets load on every page, so a board rule that starts with a class the app's pages use restyles those pages.</summary>
    [Fact]
    public void The_board_stylesheet_starts_no_rule_with_a_class_the_app_stylesheet_styles()
    {
        var app = LeadingClasses(Read("app.css"));
        var board = LeadingClasses(Read("board.css"));

        Assert.Empty(board.Intersect(app).Order());
    }

    private static string Read(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "CromoBound.slnx")))
                return File.ReadAllText(Path.Combine(dir.FullName, "src", "CromoBound.Client", "wwwroot", "css", name));
        throw new InvalidOperationException($"CromoBound.slnx not found above {AppContext.BaseDirectory}.");
    }

    /// <summary>The first class of every selector, outside @-rule headers and declaration blocks.</summary>
    private static HashSet<string> LeadingClasses(string css)
    {
        var classes = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match rule in Rule().Matches(Comment().Replace(css, "")))
            foreach (var selector in rule.Groups[1].Value.Split(','))
                if (LeadingClass().Match(selector.Trim()) is { Success: true } first)
                    classes.Add(first.Groups[1].Value);
        return classes;
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"(?:^|[{}])\s*([^{}@]+?)\s*\{")]
    private static partial Regex Rule();

    [GeneratedRegex(@"^\.([A-Za-z0-9_-]+)")]
    private static partial Regex LeadingClass();
}
