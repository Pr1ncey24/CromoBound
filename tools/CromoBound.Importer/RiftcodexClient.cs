using System.Text.Json.Nodes;

namespace CromoBound.Importer;

/// <summary>Downloads everything from Riftcodex once. Waits <c>delay</c> between requests to be polite.</summary>
public sealed class RiftcodexClient(HttpClient http, TimeSpan delay)
{
    public const int PageSize = 100;

    public static readonly string[] IndexNames =
        ["keywords", "card-names", "card-types", "card-supertypes", "domains", "rarities", "artists", "energy", "might", "power", "tags"];

    public async Task<RawSnapshot> FetchAllAsync(CancellationToken ct = default)
    {
        var cards = new JsonArray();
        var first = await GetAsync($"cards?page=1&size={PageSize}", ct);
        var total = first["total"]!.GetValue<int>();
        var pages = first["pages"]!.GetValue<int>();
        AppendItems(first, cards);
        for (var page = 2; page <= pages; page++)
        {
            await Task.Delay(delay, ct);
            AppendItems(await GetAsync($"cards?page={page}&size={PageSize}", ct), cards);
        }
        if (cards.Count != total)
            throw new InvalidOperationException($"Riftcodex reported {total} cards but {cards.Count} were downloaded.");

        await Task.Delay(delay, ct);
        var sets = new JsonArray();
        AppendItems(await GetAsync($"sets?size={PageSize}", ct), sets);

        var indexes = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var name in IndexNames)
        {
            await Task.Delay(delay, ct);
            indexes[name] = await GetAsync($"index/{name}", ct);
        }
        return new RawSnapshot(cards, sets, indexes);
    }

    private async Task<JsonNode> GetAsync(string relativeUrl, CancellationToken ct)
    {
        using var response = await http.GetAsync(relativeUrl, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"GET {relativeUrl} returned {(int)response.StatusCode}.");
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))
            ?? throw new InvalidDataException($"GET {relativeUrl} returned no JSON.");
    }

    private static void AppendItems(JsonNode page, JsonArray target)
    {
        foreach (var item in page["items"]!.AsArray())
            target.Add(item!.DeepClone());
    }
}
