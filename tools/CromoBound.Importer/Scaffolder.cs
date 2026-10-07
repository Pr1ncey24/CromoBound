using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Importer;

/// <summary>Creates effects files for vanilla and keyword-only cards. Never touches an existing file.</summary>
public static class Scaffolder
{
    public const string SchemaRef = "../../schema/effects.schema.json";

    public static EffectsFile? Classify(Card card)
    {
        // Unknown power domains need a hand-written override first, so such cards stay Unmapped.
        if (card.Cost is { Power: null }) return null;
        if (RichText.Lines(card.Text.Rich).Count == 0)
            return new EffectsFile { Schema = SchemaRef, CardId = card.Id, Status = MappingStatus.Full };
        var keywords = KeywordText.TryParseKeywordOnly(card.Text.Rich);
        return keywords is null
            ? null
            : new EffectsFile { Schema = SchemaRef, CardId = card.Id, Status = MappingStatus.Full, Keywords = keywords };
    }

    public static void Run(IEnumerable<Card> cards, string effectsDir, ImportReport report)
    {
        Directory.CreateDirectory(effectsDir);
        foreach (var card in cards)
        {
            var path = Path.Combine(effectsDir, card.Id + ".json");
            MappingStatus status;
            if (File.Exists(path))
            {
                status = ReadStatus(path);
            }
            else if (Classify(card) is { } file)
            {
                File.WriteAllText(path, CromoJson.Serialize(file) + "\n");
                status = file.Status;
            }
            else
            {
                status = MappingStatus.Unmapped;
            }
            report.StatusCounts[status] = report.StatusCounts.GetValueOrDefault(status) + 1;
        }
    }

    private static MappingStatus ReadStatus(string path)
    {
        try
        {
            return CromoJson.Deserialize<EffectsFile>(File.ReadAllText(path)).Status;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            throw new InvalidDataException($"{path}: {ex.Message}", ex);
        }
    }
}
