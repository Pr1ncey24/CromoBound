using System.Net;
using CromoBound.Importer;

namespace CromoBound.Models.Tests;

public class FetchTests
{
    private static HttpResponseMessage Respond(HttpRequestMessage request, int total, HttpStatusCode page2Status = HttpStatusCode.OK)
    {
        var path = request.RequestUri!.PathAndQuery;
        if (path.StartsWith("/cards?page=1", StringComparison.Ordinal))
            return FakeHandler.Json($$"""{ "items": [ { "id": "a" }, { "id": "b" } ], "total": {{total}}, "page": 1, "size": 100, "pages": 2 }""");
        if (path.StartsWith("/cards?page=2", StringComparison.Ordinal))
            return page2Status == HttpStatusCode.OK
                ? FakeHandler.Json($$"""{ "items": [ { "id": "c" } ], "total": {{total}}, "page": 2, "size": 100, "pages": 2 }""")
                : new HttpResponseMessage(page2Status);
        if (path.StartsWith("/sets", StringComparison.Ordinal))
            return FakeHandler.Json("""{ "items": [ { "set_id": "OGN", "name": "Origins" } ], "total": 1, "page": 1, "size": 100, "pages": 1 }""");
        return FakeHandler.Json("""{ "total": 1, "type": "x", "values": ["Shield"] }""");
    }

    private static (RiftcodexClient Client, FakeHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") };
        return (new RiftcodexClient(http, TimeSpan.Zero), handler);
    }

    [Fact]
    public async Task Fetches_all_pages_sets_and_indexes()
    {
        var (client, handler) = Create(r => Respond(r, total: 3));

        var snapshot = await client.FetchAllAsync();

        Assert.Equal(3, snapshot.Cards.Count);
        Assert.Single(snapshot.Sets);
        Assert.Equal(RiftcodexClient.IndexNames.Length, snapshot.Indexes.Count);
        Assert.Contains("/cards?page=1&size=100", handler.Requests);
        Assert.Contains("/cards?page=2&size=100", handler.Requests);
        Assert.Contains("/sets?size=100", handler.Requests);
        Assert.Contains("/index/keywords", handler.Requests);
        Assert.Equal(3 + RiftcodexClient.IndexNames.Length, handler.Requests.Count);
    }

    [Fact]
    public async Task Total_mismatch_throws()
    {
        var (client, _) = Create(r => Respond(r, total: 5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.FetchAllAsync());
    }

    [Fact]
    public async Task Failed_fetch_writes_no_files()
    {
        var (client, _) = Create(r => Respond(r, total: 3, page2Status: HttpStatusCode.InternalServerError));
        using var temp = new TempDir();
        var rawDir = Path.Combine(temp.Root, "raw");

        await Assert.ThrowsAsync<HttpRequestException>(() => ImportCommands.FetchAsync(client, rawDir));

        Assert.False(Directory.Exists(rawDir));
    }

    [Fact]
    public async Task Written_snapshot_reads_back_as_raw_data()
    {
        var (client, _) = Create(r => Respond(r, total: 3));
        using var temp = new TempDir();
        var rawDir = Path.Combine(temp.Root, "raw");

        await ImportCommands.FetchAsync(client, rawDir);
        var data = RawStore.Read(rawDir);

        Assert.Equal(new[] { "a", "b", "c" }, data.Cards.Select(c => c.Id));
        Assert.Equal("OGN", Assert.Single(data.Sets).SetId);
        Assert.Equal(new[] { "Shield" }, data.KeywordIndex);
        Assert.True(File.Exists(Path.Combine(rawDir, "index", "tags.json")));
    }
}
