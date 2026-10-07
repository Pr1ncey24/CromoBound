using System.Text.Json;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Data;

public static class CardRepository
{
    public static CardDatabase Load(string dataDir)
    {
        var cards = ReadList<Card>(Path.Combine(dataDir, "cards.json"));
        var tokensPath = Path.Combine(dataDir, "tokens.json");
        var tokens = File.Exists(tokensPath) ? ReadList<Card>(tokensPath) : [];
        var printings = ReadList<Printing>(Path.Combine(dataDir, "printings.json"));
        var sets = ReadList<CardSet>(Path.Combine(dataDir, "sets.json"));

        var effects = new Dictionary<string, LoadedEffects>(StringComparer.Ordinal);
        var effectsDir = Path.Combine(dataDir, "effects");
        if (Directory.Exists(effectsDir))
        {
            foreach (var path in Directory.EnumerateFiles(effectsDir, "*.json").Order(StringComparer.Ordinal))
            {
                var file = Read<EffectsFile>(path);
                if (!effects.TryAdd(file.CardId, new LoadedEffects(path, file)))
                    throw new InvalidDataException($"{path}: cardId '{file.CardId}' is already defined in {effects[file.CardId].FilePath}.");
            }
        }

        return new CardDatabase
        {
            Cards = Index(cards.Concat(tokens), c => c.Id, "cards.json/tokens.json"),
            Printings = Index(printings, p => p.Id, "printings.json"),
            Sets = Index(sets, s => s.Id, "sets.json"),
            Effects = effects,
        };
    }

    private static IReadOnlyList<T> ReadList<T>(string path) => Read<List<T>>(path);

    private static T Read<T>(string path)
    {
        try
        {
            return CromoJson.Deserialize<T>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidDataException($"{path}: {ex.Message}", ex);
        }
    }

    private static Dictionary<string, T> Index<T>(IEnumerable<T> items, Func<T, string> key, string source)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
            if (!result.TryAdd(key(item), item))
                throw new InvalidDataException($"{source}: id '{key(item)}' is defined more than once.");
        return result;
    }
}
