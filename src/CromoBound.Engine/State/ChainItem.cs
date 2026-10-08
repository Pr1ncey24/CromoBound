using CromoBound.Engine.Effects;

namespace CromoBound.Engine.State;

public enum ChainItemKind { Card, Ability }

public enum ChainItemStatus { Pending, Finalized }

public enum AbilityKind { Triggered, Activated }

/// <summary>An item on the chain: a card being played, or an ability. Plan B adds the play choices.</summary>
public sealed class ChainItem
{
    public required int Id { get; init; }
    public required ChainItemKind Kind { get; init; }
    public required PlayerId Controller { get; init; }
    public ChainItemStatus Status { get; set; } = ChainItemStatus.Pending;

    /// <summary>The card on the chain (card items).</summary>
    public ObjectId? Card { get; set; }

    /// <summary>The card the ability comes from, its text line, and its kind (ability items).</summary>
    public ObjectId? Source { get; init; }
    public int? TextLine { get; init; }
    public AbilityKind? AbilityKind { get; init; }

    /// <summary>Where a permanent will enter, and whether Accelerate was paid (choices made while playing).</summary>
    public Place? Location { get; set; }
    public bool Accelerate { get; set; }

    /// <summary>Ability items: the source card's id and the ability text, kept even if the source leaves play.</summary>
    public string? SourceCardId { get; init; }
    public string? Text { get; init; }

    /// <summary>The resolution context of a card the engine runs: its targets, chosen while playing (spec §4.2).</summary>
    internal EffectContext? Effect { get; set; }
}
