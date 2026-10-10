using CromoBound.Data;

namespace CromoBound.Importer;

public static class ImporterApp
{
    private const string Usage = "Usage: dotnet run --project tools/CromoBound.Importer -- <fetch|normalize|schema|all|images <folder>>";

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
            case "images" when args.Length == 2:
                return await ImagesAsync(dataDir, args[1]);
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

    /// <summary>Downloads every printing's image into the folder (spec §3.1); fails when any image couldn't be fetched, so a deploy
    /// script notices. Running it again fetches only what is missing.</summary>
    private static async Task<int> ImagesAsync(string dataDir, string folder)
    {
        var printings = CardRepository.Load(dataDir).Printings.Values;
        var sources = printings.Where(p => !string.IsNullOrEmpty(p.ImageUrl)).Select(p => new ImageSource(p.Id, p.ImageUrl!)).ToList();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CromoBound-Importer/1.0");
        var report = await new CardImageDownloader(http).DownloadAsync(sources, Path.GetFullPath(folder));
        Console.WriteLine($"Images: {report.Downloaded} downloaded, {report.Skipped} already there, {report.Failed.Count} failed, "
            + $"{printings.Count() - sources.Count} printings without an image link.");
        foreach (var id in report.Failed) Console.Error.WriteLine($"Failed: {id}");
        return report.Failed.Count == 0 ? 0 : 1;
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
