using System.Net;
using System.Net.Http.Headers;
using CromoBound.Importer;

namespace CromoBound.Models.Tests;

public class CardImageTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static HttpResponseMessage Image(byte[] bytes, string mediaType = "image/png")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static CardImageDownloader Downloader(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://img.example.test/") });

    private static ImageSource Source(string id) => new(id, $"https://img.example.test/{id}.png?tag=x");

    [Fact]
    public async Task Png_answers_are_saved_under_the_printing_id()
    {
        using var dir = new TempDir();
        var handler = new FakeHandler(_ => Image(Png));

        var report = await Downloader(handler).DownloadAsync([Source("a1"), Source("b2")], dir.Root);

        Assert.Equal((2, 0), (report.Downloaded, report.Skipped));
        Assert.Empty(report.Failed);
        Assert.Equal(Png, await File.ReadAllBytesAsync(Path.Combine(dir.Root, "a1.png")));
        Assert.Equal(Png, await File.ReadAllBytesAsync(Path.Combine(dir.Root, "b2.png")));
    }

    [Fact]
    public async Task Files_already_there_are_skipped()
    {
        using var dir = new TempDir();
        dir.Write("a1.png", "kept");
        var handler = new FakeHandler(_ => Image(Png));

        var report = await Downloader(handler).DownloadAsync([Source("a1"), Source("b2")], dir.Root);

        Assert.Equal((1, 1), (report.Downloaded, report.Skipped));
        Assert.Equal(new[] { "/b2.png?tag=x" }, handler.Requests);
        Assert.Equal("kept", await File.ReadAllTextAsync(Path.Combine(dir.Root, "a1.png")));
    }

    [Fact]
    public async Task Failed_downloads_leave_no_file_and_are_reported()
    {
        using var dir = new TempDir();
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/html.png" => Image("<html></html>"u8.ToArray(), "text/html"),
            "/missing.png" => new HttpResponseMessage(HttpStatusCode.NotFound),
            "/empty.png" => Image([]),
            "/dropped.png" => throw new HttpRequestException("The connection was reset."),
            _ => Image(Png),
        });

        var report = await Downloader(handler).DownloadAsync(
            [Source("html"), Source("missing"), Source("empty"), Source("dropped"), Source("good")], dir.Root);

        Assert.Equal((1, 0), (report.Downloaded, report.Skipped));
        Assert.Equal(new[] { "dropped", "empty", "html", "missing" }, report.Failed);
        Assert.Equal(new[] { "good.png" }, Directory.GetFiles(dir.Root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task At_most_four_downloads_run_at_once()
    {
        using var dir = new TempDir();
        var handler = new SlowHandler(Png);

        var report = await Downloader(handler).DownloadAsync([.. Enumerable.Range(0, 12).Select(i => Source($"p{i}"))], dir.Root);

        Assert.Equal(12, report.Downloaded);
        Assert.InRange(handler.MostAtOnce, 2, CardImageDownloader.MaxAtOnce);
    }

    [Fact]
    public async Task Progress_counts_every_finished_image_up_to_the_total()
    {
        using var dir = new TempDir();
        dir.Write("kept.png", "kept");
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath == "/bad.png"
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : Image(Png));
        var seen = new SyncProgress();

        await Downloader(handler).DownloadAsync([Source("kept"), Source("bad"), Source("a1"), Source("b2")], dir.Root, progress: seen);

        Assert.Equal(new[] { 1, 2, 3, 4 }, seen.Values.Where(p => p.Step != ImageStep.Started).Select(p => p.Finished).Order());
        Assert.All(seen.Values, p => Assert.Equal(4, p.Total));
        Assert.Equal(new[] { "a1", "b2", "bad" }, seen.Values.Where(p => p.Step == ImageStep.Started).Select(p => p.PrintingId).Order());
        Assert.Equal(ImageStep.Skipped, seen.Values.Single(p => p.PrintingId == "kept").Step);
    }

    [Fact]
    public async Task Every_finished_download_says_why_it_failed_or_how_big_it_was()
    {
        using var dir = new TempDir();
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/html.png" => Image("<html></html>"u8.ToArray(), "text/html"),
            "/missing.png" => new HttpResponseMessage(HttpStatusCode.NotFound),
            "/empty.png" => Image([]),
            "/dropped.png" => throw new HttpRequestException("The connection was reset."),
            "/slow.png" => throw new TaskCanceledException("The request timed out."),
            _ => Image(Png),
        });
        var seen = new SyncProgress();

        await Downloader(handler).DownloadAsync(
            [Source("html"), Source("missing"), Source("empty"), Source("dropped"), Source("slow"), Source("good")], dir.Root, progress: seen);

        string Detail(string id) => seen.Values.Single(p => p.PrintingId == id && p.Step != ImageStep.Started).Detail;
        Assert.Equal("not a PNG (text/html)", Detail("html"));
        Assert.Equal("HTTP 404 Not Found", Detail("missing"));
        Assert.Equal("empty answer", Detail("empty"));
        Assert.Equal("connection failed: The connection was reset.", Detail("dropped"));
        Assert.StartsWith("timed out after ", Detail("slow"));
        Assert.Matches(@"^0 KB in \d+\.\d s$", Detail("good"));
        Assert.Equal(ImageStep.Saved, seen.Values.Single(p => p.PrintingId == "good" && p.Step != ImageStep.Started).Step);
    }

    private sealed class SyncProgress : IProgress<ImageProgress>
    {
        public List<ImageProgress> Values { get; } = [];

        public void Report(ImageProgress value)
        {
            lock (Values) Values.Add(value);
        }
    }

    /// <summary>Answers after a short delay and records how many requests were open at the same time.</summary>
    private sealed class SlowHandler(byte[] bytes) : HttpMessageHandler
    {
        private int _open;

        public int MostAtOnce { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var open = Interlocked.Increment(ref _open);
            lock (this) MostAtOnce = Math.Max(MostAtOnce, open);
            await Task.Delay(30, cancellationToken);
            Interlocked.Decrement(ref _open);
            return Image(bytes);
        }
    }
}
