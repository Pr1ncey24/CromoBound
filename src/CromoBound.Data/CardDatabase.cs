using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Data;

public sealed record LoadedEffects(string FilePath, EffectsFile File);

/// <summary>Merged view of cards.json + tokens.json + printings.json + sets.json + effects/*.json.</summary>
public sealed class CardDatabase
{
    public required IReadOnlyDictionary<string, Card> Cards { get; init; }
    public required IReadOnlyDictionary<string, Printing> Printings { get; init; }
    public required IReadOnlyDictionary<string, CardSet> Sets { get; init; }
    public required IReadOnlyDictionary<string, LoadedEffects> Effects { get; init; }

    public MappingStatus StatusOf(string cardId) =>
        Effects.TryGetValue(cardId, out var effects) ? effects.File.Status : MappingStatus.Unmapped;
}
