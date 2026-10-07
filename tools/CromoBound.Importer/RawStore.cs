using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CromoBound.Importer;

public sealed record RawSnapshot(JsonArray Cards, JsonArray Sets, IReadOnlyDictionary<string, JsonNode> Indexes);

public sealed record RawData(IReadOnlyList<RawCard> Cards, IReadOnlyList<RawSet> Sets, IReadOnlyList<string> KeywordIndex);

/// <summary>data/raw: cards.json (all page items), sets.json (items), index/&lt;name&gt;.json (full responses).</summary>
public static class RawStore
{
    private static readonly JsonSerializerOptions Output = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Write(RawSnapshot snapshot, string rawDir)
    {
        Directory.CreateDirectory(Path.Combine(rawDir, "index"));
        File.WriteAllText(Path.Combine(rawDir, "cards.json"), snapshot.Cards.ToJsonString(Output) + "\n");
        File.WriteAllText(Path.Combine(rawDir, "sets.json"), snapshot.Sets.ToJsonString(Output) + "\n");
        foreach (var (name, node) in snapshot.Indexes)
            File.WriteAllText(Path.Combine(rawDir, "index", name + ".json"), node.ToJsonString(Output) + "\n");
    }

    public static RawData Read(string rawDir)
    {
        var cards = Deserialize<List<RawCard>>(Path.Combine(rawDir, "cards.json"));
        var sets = Deserialize<List<RawSet>>(Path.Combine(rawDir, "sets.json"));
        var keywordsPath = Path.Combine(rawDir, "index", "keywords.json");
        var keywords = File.Exists(keywordsPath)
            ? JsonNode.Parse(File.ReadAllText(keywordsPath))?["values"]?.AsArray().Select(v => v!.ToJsonString().Trim('"')).ToList() ?? []
            : [];
        return new RawData(cards, sets, keywords);
    }

    private static T Deserialize<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), RawJson.Options) ?? throw new InvalidDataException($"{path} is empty.");
}
