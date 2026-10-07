using CromoBound.Data;

namespace CromoBound.Importer;

public static class ImporterApp
{
    private const string Usage = "Usage: dotnet run --project tools/CromoBound.Importer -- <fetch|normalize|schema|all>";

    public static async Task<int> RunAsync(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "";
        var root = RepoLocator.FindRoot(Directory.GetCurrentDirectory());
        var dataDir = Path.Combine(root, "data");
        var schemaDir = Path.Combine(root, "schema");
        switch (command)
        {
            case "fetch":
                await FetchAsync(dataDir);
                return 0;
            case "normalize":
                return NormalizeAndValidate(dataDir);
            case "schema":
                ImportCommands.WriteSchemas(schemaDir);
                Console.WriteLine("Wrote schema/*.json.");
                return 0;
            case "all":
                await FetchAsync(dataDir);
                ImportCommands.WriteSchemas(schemaDir);
                return NormalizeAndValidate(dataDir);
            default:
                Console.Error.WriteLine(Usage);
                return 1;
        }
    }

    private static async Task FetchAsync(string dataDir)
    {
        using var http = new HttpClient { BaseAddress = new Uri("https://api.riftcodex.com/"), Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CromoBound-Importer/1.0");
        await ImportCommands.FetchAsync(new RiftcodexClient(http, TimeSpan.FromSeconds(1)), Path.Combine(dataDir, "raw"));
        Console.WriteLine("Fetched Riftcodex data into data/raw.");
    }

    private static int NormalizeAndValidate(string dataDir)
    {
        var report = ImportCommands.Normalize(dataDir);
        Console.WriteLine($"Normalized {report.TypeCounts.Values.Sum()} cards. Report: data/import-report.md");
        var issues = EffectsValidator.Validate(CardRepository.Load(dataDir));
        foreach (var issue in issues) Console.Error.WriteLine($"{issue.Location}: {issue.Message}");
        Console.WriteLine(issues.Count == 0 ? "Validation passed." : $"Validation found {issues.Count} issue(s).");
        return issues.Count == 0 ? 0 : 1;
    }
}
