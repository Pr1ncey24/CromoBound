using CromoBound.Models.Cards;
using CromoBound.Models.Json;

namespace CromoBound.Importer;

public static class ImportCommands
{
    /// <summary>Downloads everything first; writes data/raw only if the whole fetch succeeded.</summary>
    public static async Task FetchAsync(RiftcodexClient client, string rawDir, CancellationToken ct = default)
    {
        var snapshot = await client.FetchAllAsync(ct);
        RawStore.Write(snapshot, rawDir);
    }

    /// <summary>raw → cards/printings/sets, scaffold trivial effects files, write import-report.md.</summary>
    public static ImportReport Normalize(string dataDir)
    {
        var raw = RawStore.Read(Path.Combine(dataDir, "raw"));
        var tokens = CromoJson.Deserialize<List<Card>>(File.ReadAllText(Path.Combine(dataDir, "tokens.json")));
        var report = new ImportReport();
        foreach (var keyword in raw.KeywordIndex)
            if (!KeywordText.IsKnownIndexKeyword(keyword))
                report.UnknownKeywords.TryAdd(keyword, "(keywords index)");

        var result = Normalizer.Normalize(raw.Cards, raw.Sets, tokens, report);
        WriteJson(Path.Combine(dataDir, "cards.json"), result.Cards);
        WriteJson(Path.Combine(dataDir, "printings.json"), result.Printings);
        WriteJson(Path.Combine(dataDir, "sets.json"), result.Sets);
        Scaffolder.Run(result.Cards.Concat(tokens), Path.Combine(dataDir, "effects"), report);
        File.WriteAllText(Path.Combine(dataDir, "import-report.md"), ReportWriter.ToMarkdown(report));
        return report;
    }

    public static void WriteSchemas(string schemaDir)
    {
        Directory.CreateDirectory(schemaDir);
        foreach (var (name, content) in SchemaGenerator.GenerateAll())
            File.WriteAllText(Path.Combine(schemaDir, name), content);
    }

    private static void WriteJson<T>(string path, T value) => File.WriteAllText(path, CromoJson.Serialize(value) + "\n");
}
