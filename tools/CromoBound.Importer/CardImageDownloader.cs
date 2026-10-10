using System.Collections.Concurrent;

namespace CromoBound.Importer;

/// <summary>One printing's image and where it comes from.</summary>
public sealed record ImageSource(string PrintingId, string Url);

/// <summary>What a run did: images saved, files already there, and the printings whose download failed (sorted).</summary>
public sealed record ImageDownloadReport(int Downloaded, int Skipped, IReadOnlyList<string> Failed);

public enum ImageStep { Started, Saved, Skipped, Failed }

/// <summary>One step of one image: <see cref="Finished"/> counts the images done so far (it doesn't move on Started), and
/// <see cref="Detail"/> says what happened (size and time when saved, the reason when it failed).</summary>
public sealed record ImageProgress(ImageStep Step, string PrintingId, int Finished, int Total, string Detail);

/// <summary>Downloads printing images into one folder as <c>{printingId}.png</c> (spec §3.1). A file already there is skipped, so a
/// run resumes and only fetches what is new. At most <see cref="MaxAtOnce"/> downloads run at once. Only a successful, non-empty PNG
/// answer is saved, and it is written to a temporary file first, so a failed download never leaves a file behind. The client's timeout
/// covers the whole answer, body included, so a stalled download fails instead of hanging the run.</summary>
public sealed class CardImageDownloader(HttpClient http)
{
    public const int MaxAtOnce = 4;

    public async Task<ImageDownloadReport> DownloadAsync(IReadOnlyList<ImageSource> images, string folder, CancellationToken cancel = default,
        IProgress<ImageProgress>? progress = null)
    {
        Directory.CreateDirectory(folder);
        var downloaded = 0;
        var skipped = 0;
        var finished = 0;
        var failed = new ConcurrentBag<string>();
        using var gate = new SemaphoreSlim(MaxAtOnce);
        await Task.WhenAll(images.Select(async image =>
        {
            var target = Path.Combine(folder, image.PrintingId + ".png");
            if (File.Exists(target))
            {
                Interlocked.Increment(ref skipped);
                progress?.Report(new(ImageStep.Skipped, image.PrintingId, Interlocked.Increment(ref finished), images.Count, "already there"));
                return;
            }
            await gate.WaitAsync(cancel);
            try
            {
                progress?.Report(new(ImageStep.Started, image.PrintingId, Volatile.Read(ref finished), images.Count, image.Url));
                var (saved, detail) = await TryDownloadAsync(image.Url, target, cancel);
                if (saved) Interlocked.Increment(ref downloaded);
                else failed.Add(image.PrintingId);
                progress?.Report(new(saved ? ImageStep.Saved : ImageStep.Failed, image.PrintingId, Interlocked.Increment(ref finished), images.Count, detail));
            }
            finally
            {
                gate.Release();
            }
        }));
        return new ImageDownloadReport(downloaded, skipped, [.. failed.Order(StringComparer.Ordinal)]);
    }

    /// <summary>Saves one image; the detail is its size and time, or why it wasn't saved.</summary>
    private async Task<(bool Saved, string Detail)> TryDownloadAsync(string url, string target, CancellationToken cancel)
    {
        var part = target + ".part";
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var response = await http.GetAsync(url, cancel);
            if (!response.IsSuccessStatusCode) return (false, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            var type = response.Content.Headers.ContentType?.MediaType;
            if (type != "image/png") return (false, $"not a PNG ({type ?? "no content type"})");
            await using (var file = File.Create(part)) await response.Content.CopyToAsync(file, cancel);
            var length = new FileInfo(part).Length;
            if (length == 0) return (false, "empty answer");
            File.Move(part, target);
            return (true, FormattableString.Invariant($"{length / 1024} KB in {clock.Elapsed.TotalSeconds:0.0} s"));
        }
        catch (TaskCanceledException) when (!cancel.IsCancellationRequested)
        {
            return (false, $"timed out after {clock.Elapsed.TotalSeconds:0} s");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"connection failed: {ex.Message}");
        }
        catch (IOException ex)
        {
            return (false, $"disk error: {ex.Message}");
        }
        finally
        {
            if (File.Exists(part)) File.Delete(part);
        }
    }
}
