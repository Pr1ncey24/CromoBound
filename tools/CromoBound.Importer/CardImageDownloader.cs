using System.Collections.Concurrent;

namespace CromoBound.Importer;

/// <summary>One printing's image and where it comes from.</summary>
public sealed record ImageSource(string PrintingId, string Url);

/// <summary>What a run did: images saved, files already there, and the printings whose download failed (sorted).</summary>
public sealed record ImageDownloadReport(int Downloaded, int Skipped, IReadOnlyList<string> Failed);

/// <summary>Downloads printing images into one folder as <c>{printingId}.png</c> (spec §3.1). A file already there is skipped, so a
/// run resumes and only fetches what is new. At most <see cref="MaxAtOnce"/> downloads run at once. Only a successful, non-empty PNG
/// answer is saved, and it is written to a temporary file first, so a failed download never leaves a file behind.</summary>
public sealed class CardImageDownloader(HttpClient http)
{
    public const int MaxAtOnce = 4;

    public async Task<ImageDownloadReport> DownloadAsync(IReadOnlyList<ImageSource> images, string folder, CancellationToken cancel = default)
    {
        Directory.CreateDirectory(folder);
        var downloaded = 0;
        var skipped = 0;
        var failed = new ConcurrentBag<string>();
        using var gate = new SemaphoreSlim(MaxAtOnce);
        await Task.WhenAll(images.Select(async image =>
        {
            var target = Path.Combine(folder, image.PrintingId + ".png");
            if (File.Exists(target))
            {
                Interlocked.Increment(ref skipped);
                return;
            }
            await gate.WaitAsync(cancel);
            try
            {
                if (await TryDownloadAsync(image.Url, target, cancel)) Interlocked.Increment(ref downloaded);
                else failed.Add(image.PrintingId);
            }
            finally
            {
                gate.Release();
            }
        }));
        return new ImageDownloadReport(downloaded, skipped, [.. failed.Order(StringComparer.Ordinal)]);
    }

    private async Task<bool> TryDownloadAsync(string url, string target, CancellationToken cancel)
    {
        var part = target + ".part";
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "image/png") return false;
            await using (var file = File.Create(part)) await response.Content.CopyToAsync(file, cancel);
            if (new FileInfo(part).Length == 0) return false;
            File.Move(part, target);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException || (ex is TaskCanceledException && !cancel.IsCancellationRequested))
        {
            return false;
        }
        finally
        {
            if (File.Exists(part)) File.Delete(part);
        }
    }
}
