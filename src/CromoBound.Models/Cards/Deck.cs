namespace CromoBound.Models.Cards;

/// <summary>A deck references printings so each player picks the art/rarity they use. Copy limits count per cardId.</summary>
public sealed record Deck
{
    public required string Name { get; init; }
    public required string Legend { get; init; }
    public required string Champion { get; init; }
    public IReadOnlyList<DeckEntry> Main { get; init; } = [];
    public IReadOnlyList<DeckEntry> Runes { get; init; } = [];
    public IReadOnlyList<string> Battlefields { get; init; } = [];
}

public sealed record DeckEntry
{
    public required string Printing { get; init; }
    public required int Count { get; init; }
}
