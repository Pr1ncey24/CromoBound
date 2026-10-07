namespace CromoBound.Models.Cards;

/// <summary>One physical version of a card (set, art, rarity).</summary>
public sealed record Printing
{
    public required string Id { get; init; }
    public required string CardId { get; init; }
    public string? RiftboundId { get; init; }
    public string? TcgplayerId { get; init; }
    public required string Set { get; init; }
    public int? CollectorNumber { get; init; }
    public Rarity? Rarity { get; init; }
    public PrintingVariant Variant { get; init; }
    public string? ImageUrl { get; init; }
    public Orientation Orientation { get; init; }
    public string? Artist { get; init; }
    public string? Flavour { get; init; }
}
