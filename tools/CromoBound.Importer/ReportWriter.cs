using System.Text;

namespace CromoBound.Importer;

public static class ReportWriter
{
    public static string ToMarkdown(ImportReport report)
    {
        var md = new StringBuilder();
        md.AppendLine("# Import report").AppendLine();

        md.AppendLine("## Cards per type").AppendLine();
        md.AppendLine("| Type | Cards |").AppendLine("|---|---|");
        foreach (var (type, count) in report.TypeCounts) md.AppendLine($"| {type} | {count} |");

        md.AppendLine().AppendLine("## Mapping status").AppendLine();
        md.AppendLine("| Status | Cards |").AppendLine("|---|---|");
        foreach (var (status, count) in report.StatusCounts) md.AppendLine($"| {status} | {count} |");

        md.AppendLine().AppendLine($"## Multi-domain cards missing power domains ({report.MissingPowerDomains.Count})").AppendLine();
        foreach (var id in report.MissingPowerDomains) md.AppendLine($"- `{id}`");

        md.AppendLine().AppendLine($"## Token prints without a token card ({report.MissingTokens.Count})").AppendLine();
        foreach (var id in report.MissingTokens) md.AppendLine($"- `{id}`");

        md.AppendLine().AppendLine($"## Unknown keywords ({report.UnknownKeywords.Count})").AppendLine();
        foreach (var (keyword, cardId) in report.UnknownKeywords) md.AppendLine($"- `{keyword}` (first seen on `{cardId}`)");

        md.AppendLine().AppendLine($"## Text conflicts between printings ({report.TextConflicts.Count})").AppendLine();
        foreach (var conflict in report.TextConflicts.OrderBy(c => c.CardId, StringComparer.Ordinal))
        {
            md.AppendLine($"### `{conflict.CardId}` ({conflict.Kind})").AppendLine();
            foreach (var text in conflict.Texts) md.AppendLine($"- {text}");
            md.AppendLine();
        }

        return md.ToString().ReplaceLineEndings("\n");
    }
}
