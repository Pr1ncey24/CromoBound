using System.Text.Json.Serialization;
using CromoBound.Models.Cards;

namespace CromoBound.Models.Effects;

/// <summary>Hand-authored (or scaffolded) effects for one card: data/effects/&lt;cardId&gt;.json.</summary>
public sealed record EffectsFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    public required string CardId { get; init; }
    public required MappingStatus Status { get; init; }
    public EffectsOverrides? Overrides { get; init; }
    public IReadOnlyList<AdditionalCost> AdditionalCosts { get; init; } = [];
    public IReadOnlyList<Step> AsYouPlay { get; init; } = [];
    public IReadOnlyList<KeywordEntry> Keywords { get; init; } = [];
    public IReadOnlyList<Ability> Abilities { get; init; } = [];
}

public sealed record EffectsOverrides
{
    public CardCost? Cost { get; init; }
}
