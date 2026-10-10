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
    /// script notices. Running it again fetches only what is missing. Every download prints a line when it starts and when it ends,
    /// with the reason when it fails, so a slow or stuck run is visible.</summary>
    private static async Task<int> ImagesAsync(string dataDir, string folder)
    {
        var printings = CardRepository.Load(dataDir).Printings.Values;
        var sources = printings.Where(p => !string.IsNullOrEmpty(p.ImageUrl)).Select(p => new ImageSource(p.Id, p.ImageUrl!)).ToList();
        var target = Path.GetFullPath(folder);
        var there = sources.Count(s => File.Exists(Path.Combine(target, s.PrintingId + ".png")));
        var urls = sources.ToDictionary(s => s.PrintingId, s => s.Url);
        var timeout = TimeSpan.FromSeconds(60);
        Console.WriteLine($"Images: {sources.Count} printings have an image link ({printings.Count() - sources.Count} have none).");
        Console.WriteLine($"Images: saving to {target}");
        Console.WriteLine($"Images: {there} already there, {sources.Count - there} to fetch, "
            + $"{CardImageDownloader.MaxAtOnce} at a time, {timeout.TotalSeconds:0} s timeout each.");

        using var http = new HttpClient { Timeout = timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CromoBound-Importer/1.0");
        var width = sources.Count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        var progress = new SyncProgress(p =>
        {
            var count = $"[{p.Finished.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(width)}/{p.Total}]";
            var line = p.Step switch
            {
                ImageStep.Started => $"start  {count} {p.PrintingId}",
                ImageStep.Saved => $"saved  {count} {p.PrintingId} ({p.Detail})",
                ImageStep.Failed => $"FAILED {count} {p.PrintingId}: {p.Detail} - {urls[p.PrintingId]}",
                _ => null,
            };
            if (line is not null) Console.WriteLine($"{DateTime.Now:HH:mm:ss} {line}");
        });
        var report = await new CardImageDownloader(http).DownloadAsync(sources, target, progress: progress);
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

    /// <summary>Reports on the calling thread at once, unlike <see cref="Progress{T}"/>, which posts to the thread pool.</summary>
    private sealed class SyncProgress(Action<ImageProgress> report) : IProgress<ImageProgress>
    {
        public void Report(ImageProgress value) => report(value);
    }
}
